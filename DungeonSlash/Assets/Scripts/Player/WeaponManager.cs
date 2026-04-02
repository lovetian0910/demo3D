using UnityEngine;

/// <summary>
/// 武器管理器——负责在运行时将武器模型挂载到角色手骨上，并支持切换。
///
/// 🎓 核心原理：骨骼挂载（Bone Attachment）
/// 3D 角色的骨骼是 Transform 层级树。武器通过 SetParent() 成为手骨的子物体后，
/// 播放攻击动画时手骨移动 → 武器自动跟随，不需要每帧手动更新位置。
/// 这和 2D 中需要手动 Lerp 武器位置完全不同——3D 引擎通过层级关系自动处理。
///
/// 🎓 worldPositionStays 参数：
/// - true: 子物体保持世界坐标不变，Unity 会反算 localPosition（常用于拖拽物品到容器）
/// - false: 子物体保持 localPosition 不变，相当于"粘"在父节点的本地原点上（武器挂载用这个）
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("武器列表")]
    [Tooltip("按 1/2/3 键切换的武器，需要在 Inspector 中拖入 WeaponData 资产")]
    [SerializeField] private WeaponData[] weapons;

    [Header("骨骼设置")]
    [Tooltip("右手骨骼名称，素材包中固定为 RigRArmPalm")]
    [SerializeField] private string rightHandBoneName = "RigRArmPalm";

    // 运行时状态
    private Transform rightHandBone;
    private GameObject currentWeaponInstance;
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

        // 找到右手骨骼
        rightHandBone = FindBoneRecursive(transform, rightHandBoneName);
        if (rightHandBone == null)
        {
            Debug.LogError($">>> WeaponManager: 找不到骨骼 '{rightHandBoneName}'！请检查角色模型的骨骼层级。");
        }
    }

    private void Start()
    {
        // 清除 Editor 中预先放置的旧武器（如 Knuckles）
        // 这些子物体有 MeshRenderer 但不是我们动态创建的
        if (rightHandBone != null)
        {
            for (int i = rightHandBone.childCount - 1; i >= 0; i--)
            {
                Transform child = rightHandBone.GetChild(i);
                if (child.GetComponent<MeshRenderer>() != null)
                {
                    Debug.Log($">>> WeaponManager: 清除旧武器 '{child.name}'");
                    Destroy(child.gameObject);
                }
            }
        }

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
        // 🎓 KeyCode.Alpha1 对应键盘顶部的数字键 1（不是小键盘的 Keypad1）
        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);
    }

    /// <summary>
    /// 切换到指定索引的武器。
    /// </summary>
    public void SwitchWeapon(int index)
    {
        // 边界检查
        if (weapons == null || index < 0 || index >= weapons.Length)
        {
            Debug.LogWarning($">>> WeaponManager: 武器索引 {index} 无效");
            return;
        }

        // 已经是当前武器，不重复切换
        if (index == currentWeaponIndex) return;

        // 攻击中不允许切换
        if (playerCombat.IsAttacking) return;

        WeaponData weaponData = weapons[index];
        if (weaponData == null || weaponData.prefab == null)
        {
            Debug.LogWarning($">>> WeaponManager: 武器数据或预制体为空 (index={index})");
            return;
        }

        // ---- 销毁旧武器 ----
        if (currentWeaponInstance != null)
        {
            Destroy(currentWeaponInstance);
        }

        // ---- 实例化新武器 ----
        // 🎓 Instantiate 会创建 prefab 的一个运行时副本（clone）
        currentWeaponInstance = Instantiate(weaponData.prefab);
        currentWeaponInstance.name = weaponData.weaponName; // 方便调试

        // ---- 挂载到手骨 ----
        // 🎓 SetParent(bone, worldPositionStays: false) 是关键：
        // false 表示保持 localPosition/localRotation 不变，武器会"粘"在骨骼的本地原点。
        // 素材包的武器 prefab 已经预设好了正确的 localPosition 和 localRotation，
        // 所以 SetParent 后位置和朝向就是对的。
        currentWeaponInstance.transform.SetParent(rightHandBone, worldPositionStays: false);

        // ---- 配置碰撞体 ----
        BoxCollider collider = SetupWeaponCollider(weaponData);

        // ---- 配置 WeaponHitbox 桥接脚本 ----
        SetupWeaponHitbox(collider);

        // ---- 通知 PlayerCombat ----
        currentWeaponIndex = index;
        playerCombat.EquipWeapon(weaponData, collider);

        Debug.Log($">>> WeaponManager: 切换到武器 [{index + 1}] {weaponData.weaponName}");
    }

    /// <summary>
    /// 在武器上配置 BoxCollider 作为攻击判定区域。
    ///
    /// 🎓 为什么用代码配置而不是在 prefab 上预设？
    /// 因为素材包的武器 prefab 没有碰撞体（它们只是视觉模型），
    /// 我们需要根据 WeaponData 中的数据动态添加和调整。
    /// </summary>
    private BoxCollider SetupWeaponCollider(WeaponData weaponData)
    {
        // 尝试复用已有的 BoxCollider，没有则添加
        BoxCollider collider = currentWeaponInstance.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = currentWeaponInstance.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true; // 🎓 Trigger 模式：不产生物理碰撞，只触发事件
        collider.center = weaponData.colliderCenter;
        collider.size = weaponData.colliderSize;
        collider.enabled = false; // 初始关闭，由 PlayerCombat 在攻击时激活

        return collider;
    }

    /// <summary>
    /// 在武器上配置 WeaponHitbox 桥接脚本。
    /// </summary>
    private void SetupWeaponHitbox(BoxCollider collider)
    {
        // 尝试复用，没有则添加
        WeaponHitbox hitbox = currentWeaponInstance.GetComponent<WeaponHitbox>();
        if (hitbox == null)
        {
            hitbox = currentWeaponInstance.AddComponent<WeaponHitbox>();
        }

        // 🎓 手动注入引用，因为 Awake 中 GetComponentInParent 可能还找不到
        hitbox.Init(playerCombat);

        // 武器需要 Rigidbody 才能触发 OnTriggerEnter
        // 🎓 isTrigger 的 Collider 需要至少一方有 Rigidbody 才能检测碰撞。
        // 设为 Kinematic 表示不受物理引擎驱动（不会自己飞走），但仍能参与碰撞检测。
        Rigidbody rb = currentWeaponInstance.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = currentWeaponInstance.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// 递归查找子骨骼。
    ///
    /// 🎓 为什么不用 Transform.Find()？
    /// Transform.Find("RigRArmPalm") 只搜索直接子级。
    /// 但骨骼层级是多层嵌套的（Player → RigPelvis → RigRibcage → RigRArm1 → RigRArm2 → RigRArmPalm），
    /// 所以需要递归遍历整棵 Transform 树。
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
