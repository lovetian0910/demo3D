using UnityEngine;

/// <summary>
/// 武器管理器——负责在运行时将武器模型挂载到角色双手骨骼上，并支持切换。
///
/// 🎓 核心原理：骨骼挂载（Bone Attachment）
/// 3D 角色的骨骼是 Transform 层级树。武器通过 SetParent() 成为手骨的子物体后，
/// 播放攻击动画时手骨移动 → 武器自动跟随，不需要每帧手动更新位置。
///
/// 🎓 双手持武：
/// 左手武器用于轻攻击（鼠标左键），右手武器用于重攻击（鼠标右键）。
/// 两只手挂载的是同一种武器（同一个 WeaponData），但各自有独立的碰撞体实例。
/// 切换武器时两只手同时更换。
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("武器列表")]
    [Tooltip("按 1/2/3 键切换的武器，需要在 Inspector 中拖入 WeaponData 资产")]
    [SerializeField] private WeaponData[] weapons;

    [Header("骨骼设置")]
    [Tooltip("右手骨骼名称")]
    [SerializeField] private string rightHandBoneName = "RigRArmPalm";

    [Tooltip("左手骨骼名称")]
    [SerializeField] private string leftHandBoneName = "RigLArmPalm";

    [Header("调试")]
    [Tooltip("开启后，Play 模式下会持续读取 WeaponData 的偏移值并实时应用到武器上。调好后记得关掉")]
    [SerializeField] private bool debugLivePreview = true;

    // 运行时状态
    private Transform rightHandBone;
    private Transform leftHandBone;
    private GameObject rightWeaponInstance;
    private GameObject leftWeaponInstance;
    private BoxCollider rightWeaponCollider;
    private BoxCollider leftWeaponCollider;
    private int currentWeaponIndex = -1;
    private PlayerCombat playerCombat;

    /// <summary>当前装备的武器数据，供外部读取</summary>
    public WeaponData CurrentWeaponData =>
        (currentWeaponIndex >= 0 && currentWeaponIndex < weapons.Length)
            ? weapons[currentWeaponIndex]
            : null;

    private void Awake()
    {
        playerCombat = GetComponent<PlayerCombat>();

        // 找到双手骨骼
        rightHandBone = FindBoneRecursive(transform, rightHandBoneName);
        leftHandBone = FindBoneRecursive(transform, leftHandBoneName);

        if (rightHandBone == null)
            Debug.LogError($">>> WeaponManager: 找不到右手骨骼 '{rightHandBoneName}'！");
        if (leftHandBone == null)
            Debug.LogError($">>> WeaponManager: 找不到左手骨骼 '{leftHandBoneName}'！");
    }

    private void Start()
    {
        // 清除 Editor 中预先放置的旧武器（双手都清）
        ClearOldWeapons(rightHandBone);
        ClearOldWeapons(leftHandBone);

        // 装备默认武器（第一把）
        if (weapons != null && weapons.Length > 0)
        {
            SwitchWeapon(0);
        }
        else
        {
            Debug.LogWarning(">>> WeaponManager: 武器列表为空！请在 Inspector 中配置 WeaponData。");
        }
    }

    private void Update()
    {
        // 监听数字键 1/2/3 切换武器
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);

        // 调试模式：实时预览偏移值和碰撞体
        if (debugLivePreview && CurrentWeaponData != null)
        {
            if (rightWeaponInstance != null)
            {
                rightWeaponInstance.transform.localPosition = CurrentWeaponData.localPositionOffset;
                rightWeaponInstance.transform.localRotation = Quaternion.Euler(CurrentWeaponData.localRotationOffset);
                UpdateColliderPreview(rightWeaponCollider, CurrentWeaponData);
            }
            if (leftWeaponInstance != null)
            {
                leftWeaponInstance.transform.localPosition = CurrentWeaponData.leftPositionOffset;
                leftWeaponInstance.transform.localRotation = Quaternion.Euler(CurrentWeaponData.leftRotationOffset);
                UpdateColliderPreview(leftWeaponCollider, CurrentWeaponData);
            }
        }
    }

    /// <summary>
    /// 切换到指定索引的武器，双手同时更换。
    /// </summary>
    public void SwitchWeapon(int index)
    {
        if (weapons == null || index < 0 || index >= weapons.Length)
        {
            Debug.LogWarning($">>> WeaponManager: 武器索引 {index} 无效");
            return;
        }

        if (index == currentWeaponIndex) return;
        if (playerCombat.IsAttacking) return;

        WeaponData weaponData = weapons[index];
        if (weaponData == null || weaponData.prefab == null)
        {
            Debug.LogWarning($">>> WeaponManager: 武器数据或预制体为空 (index={index})");
            return;
        }

        // ---- 销毁旧武器（双手） ----
        if (rightWeaponInstance != null) Destroy(rightWeaponInstance);
        if (leftWeaponInstance != null) Destroy(leftWeaponInstance);

        // ---- 右手武器（重攻击用） ----
        rightWeaponInstance = SpawnWeapon(weaponData, rightHandBone,
            weaponData.localPositionOffset, weaponData.localRotationOffset);
        rightWeaponCollider = SetupWeaponCollider(rightWeaponInstance, weaponData);
        SetupWeaponHitbox(rightWeaponInstance);

        // ---- 左手武器（轻攻击用） ----
        // 🎓 左右手骨骼是镜像的，本地坐标轴方向相反，所以需要单独的偏移值
        leftWeaponInstance = SpawnWeapon(weaponData, leftHandBone,
            weaponData.leftPositionOffset, weaponData.leftRotationOffset);
        leftWeaponCollider = SetupWeaponCollider(leftWeaponInstance, weaponData);
        SetupWeaponHitbox(leftWeaponInstance);

        // ---- 通知 PlayerCombat（传入双手碰撞体） ----
        currentWeaponIndex = index;
        playerCombat.EquipWeapon(weaponData, leftWeaponCollider, rightWeaponCollider);

        Debug.Log($">>> WeaponManager: 双手切换到武器 [{index + 1}] {weaponData.weaponName}");
    }

    /// <summary>
    /// 实例化武器并挂载到指定手骨。
    ///
    /// 🎓 同一个 prefab 可以 Instantiate 多次，每次得到独立的副本。
    /// 所以左右手各 Instantiate 一次，得到两个独立的武器 GameObject，
    /// 各自有自己的碰撞体，互不干扰。
    /// </summary>
    private GameObject SpawnWeapon(WeaponData weaponData, Transform handBone,
        Vector3 posOffset, Vector3 rotOffset)
    {
        GameObject instance = Instantiate(weaponData.prefab);
        instance.name = $"{weaponData.weaponName}_{handBone.name}";
        instance.transform.SetParent(handBone, worldPositionStays: false);
        instance.transform.localPosition = posOffset;
        instance.transform.localRotation = Quaternion.Euler(rotOffset);
        return instance;
    }

    /// <summary>
    /// 在武器上配置 BoxCollider 作为攻击判定区域。
    /// </summary>
    private BoxCollider SetupWeaponCollider(GameObject weaponInstance, WeaponData weaponData)
    {
        BoxCollider collider = weaponInstance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = weaponInstance.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        collider.center = weaponData.colliderCenter;
        collider.size = weaponData.colliderSize;
        collider.enabled = false;

        return collider;
    }

    /// <summary>
    /// 在武器上配置 WeaponHitbox 桥接脚本和 Rigidbody。
    /// </summary>
    private void SetupWeaponHitbox(GameObject weaponInstance)
    {
        WeaponHitbox hitbox = weaponInstance.GetComponent<WeaponHitbox>();
        if (hitbox == null)
        {
            hitbox = weaponInstance.AddComponent<WeaponHitbox>();
        }
        hitbox.Init(playerCombat);

        Rigidbody rb = weaponInstance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = weaponInstance.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// 调试模式下实时更新碰撞体参数。
    /// </summary>
    private void UpdateColliderPreview(BoxCollider collider, WeaponData weaponData)
    {
        if (collider != null)
        {
            collider.center = weaponData.colliderCenter;
            collider.size = weaponData.colliderSize;
            // 🎓 不能在这里设 enabled = true！
            // 碰撞体的开关必须由 PlayerCombat 在攻击时控制。
            // 如果这里强制开启，走路碰到敌人就会触发伤害。
            // 要看碰撞体线框：在 Scene 视图中选中武器物体即可看到（不需要 enabled）
        }
    }

    /// <summary>
    /// 清除手骨上预先放置的旧武器模型。
    /// </summary>
    private void ClearOldWeapons(Transform handBone)
    {
        if (handBone == null) return;
        for (int i = handBone.childCount - 1; i >= 0; i--)
        {
            Transform child = handBone.GetChild(i);
            if (child.GetComponent<MeshRenderer>() != null)
            {
                Debug.Log($">>> WeaponManager: 清除旧武器 '{child.name}'");
                Destroy(child.gameObject);
            }
        }
    }

    /// <summary>
    /// 递归查找子骨骼。
    /// </summary>
    private Transform FindBoneRecursive(Transform parent, string boneName)
    {
        if (parent.name == boneName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindBoneRecursive(parent.GetChild(i), boneName);
            if (result != null) return result;
        }

        return null;
    }
}
