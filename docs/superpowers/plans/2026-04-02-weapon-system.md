# 武器挂载系统 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给 Player 添加可切换的可视化近战武器，运行时通过骨骼挂载显示，按数字键 1/2/3 在剑/斧/匕首之间切换。

**Architecture:** ScriptableObject (`WeaponData`) 存储武器属性，`WeaponManager` 挂在 Player 上负责运行时实例化武器 prefab 并挂载到右手骨骼 `RigRArmPalm`。`PlayerCombat` 从 `WeaponManager` 读取当前武器的伤害和冷却数据。`WeaponHitbox` 改为显式注入 `PlayerCombat` 引用，因为武器是运行时动态创建的。

**Tech Stack:** Unity 6 / C# / ScriptableObject / Transform 骨骼挂载

**Spec:** `docs/superpowers/specs/2026-04-02-weapon-system-design.md`

---

## File Structure

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 新建 | `Assets/Scripts/Combat/WeaponData.cs` | ScriptableObject，定义武器属性（伤害、冷却、prefab 引用、碰撞体尺寸） |
| 新建 | `Assets/Scripts/Player/WeaponManager.cs` | 武器管理器，找骨骼、实例化武器、切换武器、通知 PlayerCombat |
| 修改 | `Assets/Scripts/Player/PlayerCombat.cs` | 移除硬编码数值，新增 `EquipWeapon()` 方法从 WeaponData 读取数据 |
| 修改 | `Assets/Scripts/Combat/WeaponHitbox.cs` | 新增 `Init(PlayerCombat)` 方法，支持运行时注入引用 |

---

### Task 1: 创建 WeaponData ScriptableObject

🎓 **学习目标**: ScriptableObject 是什么、`CreateAssetMenu` 特性的作用、和 MonoBehaviour 的区别

**Files:**
- Create: `DungeonSlash/Assets/Scripts/Combat/WeaponData.cs`

- [ ] **Step 1: 创建 WeaponData.cs**

```csharp
using UnityEngine;

/// <summary>
/// 武器数据资产（ScriptableObject）。
///
/// 🎓 ScriptableObject 是 Unity 中用于存储共享数据的资产类型。
/// 和 MonoBehaviour 不同，它不挂在 GameObject 上，而是作为项目文件（.asset）存在。
/// 好处：
/// 1. 数据和逻辑分离——改数值不用改代码
/// 2. 多个对象可引用同一份数据，内存只有一份
/// 3. 在 Inspector 中编辑，所见即所得
///
/// [CreateAssetMenu] 特性让你能在 Project 窗口右键 → Create → DungeonSlash → Weapon Data
/// 来创建这个资产文件，就像创建 Material 一样方便。
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "DungeonSlash/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("武器名称，用于调试和 UI 显示")]
    public string weaponName;

    [Tooltip("武器预制体，来自素材包的 04 Attach Weapons 目录")]
    public GameObject prefab;

    [Header("战斗属性")]
    [Tooltip("轻攻击（鼠标左键）伤害")]
    public float lightDamage = 15f;

    [Tooltip("重攻击（鼠标右键）伤害")]
    public float heavyDamage = 30f;

    [Tooltip("轻攻击冷却时间（秒）")]
    public float lightAttackCooldown = 0.5f;

    [Tooltip("重攻击冷却时间（秒）")]
    public float heavyAttackCooldown = 1.2f;

    [Header("碰撞体设置")]
    [Tooltip("武器 BoxCollider 的中心偏移")]
    public Vector3 colliderCenter = new Vector3(0f, 0f, 0.003f);

    [Tooltip("武器 BoxCollider 的尺寸")]
    public Vector3 colliderSize = new Vector3(0.003f, 0.001f, 0.005f);
}
```

- [ ] **Step 2: 保存后回到 Unity，等待编译完成**

Unity 会自动检测新脚本并编译。等 Console 没有报错后继续。

- [ ] **Step 3: 验证 CreateAssetMenu 生效**

在 Unity Editor 中：Project 窗口 → 右键 → Create，确认能看到 **DungeonSlash → Weapon Data** 菜单项。
（先不创建资产，后续 Task 5 统一创建）

- [ ] **Step 4: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Combat/WeaponData.cs
git commit -m "feat: add WeaponData ScriptableObject for weapon attributes"
```

---

### Task 2: 修改 WeaponHitbox 支持运行时注入

🎓 **学习目标**: 理解 Awake 生命周期的局限性——运行时 Instantiate 的对象，Awake 在 SetParent 之前就执行了，所以 `GetComponentInParent` 可能找不到目标

**Files:**
- Modify: `DungeonSlash/Assets/Scripts/Combat/WeaponHitbox.cs`

- [ ] **Step 1: 修改 WeaponHitbox.cs，添加 Init 方法**

将文件内容替换为：

```csharp
using UnityEngine;

/// <summary>
/// 挂在武器碰撞体上，把碰撞事件转发给 PlayerCombat。
///
/// 🎓 为什么需要 Init 方法？
/// 武器是运行时 Instantiate 创建的。Unity 的执行顺序是：
///   Instantiate → Awake() → 你的代码继续执行 → SetParent()
/// 也就是说 Awake 执行时，武器还没有被挂到 Player 骨骼下，
/// GetComponentInParent<PlayerCombat>() 会找不到目标（返回 null）。
///
/// 解决方案：提供 Init() 方法，让 WeaponManager 在挂载完成后手动注入引用。
/// 同时保留 Awake 中的查找作为兜底（适用于在 Editor 中直接放置的情况）。
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    private PlayerCombat playerCombat;

    /// <summary>
    /// 由 WeaponManager 在实例化并挂载武器后调用，手动注入 PlayerCombat 引用。
    /// </summary>
    public void Init(PlayerCombat combat)
    {
        playerCombat = combat;
    }

    private void Awake()
    {
        // 兜底查找：如果武器是在 Editor 中直接放在骨骼下的，Awake 时能找到
        if (playerCombat == null)
        {
            playerCombat = GetComponentInParent<PlayerCombat>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            playerCombat?.OnWeaponHit(other);
        }
    }
}
```

- [ ] **Step 2: 保存后回到 Unity，确认编译通过无报错**

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Combat/WeaponHitbox.cs
git commit -m "refactor: add Init method to WeaponHitbox for runtime injection"
```

---

### Task 3: 修改 PlayerCombat 支持动态武器数据

🎓 **学习目标**: 从硬编码到数据驱动的重构过程——代码不再"知道"具体伤害值，而是从外部数据读取

**Files:**
- Modify: `DungeonSlash/Assets/Scripts/Player/PlayerCombat.cs`

- [ ] **Step 1: 修改 PlayerCombat.cs，用 WeaponData 替代硬编码**

将文件内容替换为：

```csharp
using UnityEngine;

/// <summary>
/// 玩家战斗系统。
///
/// 🎓 重构要点：伤害和冷却值不再硬编码在这里，
/// 而是通过 EquipWeapon() 从 WeaponData（ScriptableObject）读取。
/// 这就是「数据驱动设计」——改数值只需要改 ScriptableObject 资产，不用改代码。
///
/// weaponCollider 也改为运行时由 WeaponManager 设置，
/// 因为武器模型（包括碰撞体）是动态实例化的。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon Hit Timing")]
    [SerializeField] private float hitActiveDelay = 0.15f;  // 攻击开始后多久激活碰撞
    [SerializeField] private float hitActiveDuration = 0.3f; // 碰撞持续多久

    // 当前武器数据（由 WeaponManager 通过 EquipWeapon 设置）
    private WeaponData currentWeaponData;
    private Collider weaponCollider;

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private bool isAttacking;
    private float hitTimer;
    private bool hitActive;

    public bool IsAttacking => isAttacking;

    /// <summary>
    /// 当前是否有武器装备。在没有武器时禁止攻击。
    /// </summary>
    public bool HasWeapon => currentWeaponData != null && weaponCollider != null;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// 由 WeaponManager 调用，装备新武器时更新战斗数据和碰撞体引用。
    ///
    /// 🎓 为什么 Collider 要从外部传入？
    /// 因为武器模型是运行时 Instantiate 的，碰撞体在武器 prefab 上，
    /// PlayerCombat 无法提前在 Inspector 中拖拽引用。
    /// </summary>
    public void EquipWeapon(WeaponData data, Collider collider)
    {
        currentWeaponData = data;
        weaponCollider = collider;

        // 确保碰撞体初始状态关闭
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        Debug.Log($">>> 装备武器: {data.weaponName} | 轻攻 {data.lightDamage} | 重攻 {data.heavyDamage}");
    }

    private void Update()
    {
        attackCooldownTimer -= Time.deltaTime;

        // 管理武器碰撞体的激活时机
        if (isAttacking)
        {
            hitTimer -= Time.deltaTime;

            if (hitTimer <= 0f && !hitActive)
            {
                OnAttackHitStart();
                hitActive = true;
                hitTimer = hitActiveDuration;
            }
            else if (hitActive && hitTimer <= 0f)
            {
                OnAttackHitEnd();
                hitActive = false;
            }

            return;
        }

        if (playerController.IsRolling)
        {
            return;
        }

        // 没有武器时不能攻击
        if (!HasWeapon)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && attackCooldownTimer <= 0f)
        {
            StartLightAttack();
        }
        else if (Input.GetMouseButtonDown(1) && attackCooldownTimer <= 0f)
        {
            StartHeavyAttack();
        }
    }

    private void StartLightAttack()
    {
        isAttacking = true;
        currentAttackDamage = currentWeaponData.lightDamage;
        attackCooldownTimer = currentWeaponData.lightAttackCooldown;
        hitTimer = hitActiveDelay;
        hitActive = false;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = currentWeaponData.heavyDamage;
        attackCooldownTimer = currentWeaponData.heavyAttackCooldown;
        hitTimer = hitActiveDelay;
        hitActive = false;
        playerAnimator.PlayHeavyAttack();
    }

    public void OnAttackHitStart()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
    }

    public void OnAttackHitEnd()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
        isAttacking = false;
    }

    /// <summary>
    /// 由 WeaponHitbox 脚本调用，当武器碰到东西时
    /// </summary>
    public void OnWeaponHit(Collider other)
    {
        DamageSystem.DealDamage(other, currentAttackDamage, transform.position);
    }
}
```

- [ ] **Step 2: 保存后回到 Unity，确认编译通过**

注意：Player prefab 的 Inspector 中 PlayerCombat 组件会丢失 `weaponCollider` 引用（因为字段被移除了），这是预期行为——后续由 WeaponManager 在运行时设置。

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Player/PlayerCombat.cs
git commit -m "refactor: PlayerCombat reads weapon data from WeaponData instead of hardcoded values"
```

---

### Task 4: 创建 WeaponManager

🎓 **学习目标**:
- 骨骼挂载原理：`Transform.SetParent()` + `worldPositionStays` 参数的含义
- 递归查找骨骼：`Transform.Find()` 只搜索直接子级，需要递归版本
- 运行时 Instantiate + 碰撞体配置

**Files:**
- Create: `DungeonSlash/Assets/Scripts/Player/WeaponManager.cs`

- [ ] **Step 1: 创建 WeaponManager.cs**

```csharp
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
```

- [ ] **Step 2: 保存后回到 Unity，确认编译通过无报错**

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Player/WeaponManager.cs
git commit -m "feat: add WeaponManager for runtime weapon mounting and switching"
```

---

### Task 5: Editor 配置——创建武器资产并连接组件

🎓 **学习目标**: 在 Unity Editor 中使用 ScriptableObject 工作流，理解 prefab 引用和 Inspector 配置

**Files:**
- 操作: Unity Editor（创建 .asset 文件、配置 Player prefab）

> **注意：此任务需要用户在 Unity Editor 中手动操作，无法用代码完成。**

- [ ] **Step 1: 创建武器数据资产文件夹**

在 Project 窗口中：右键 Assets → Create → Folder，命名为 `Data`，再在 Data 内创建子文件夹 `Weapons`。
最终路径：`Assets/Data/Weapons/`

🎓 **为什么单独建文件夹？** ScriptableObject 资产文件（.asset）和脚本（.cs）不同，它们是数据文件。好的项目习惯是把数据和代码分开存放。

- [ ] **Step 2: 创建 Sword 武器数据**

在 `Assets/Data/Weapons/` 下：右键 → Create → DungeonSlash → Weapon Data
- 重命名为 `Sword`
- 在 Inspector 中配置：
  - Weapon Name: `Sword`
  - Prefab: 从 Project 窗口拖入 `Assets/ThirdParty/Little Heroes Mega Pack/Prefabs/04 Attach Weapons/Swords (R Arm)/Sword 01 Blue.prefab`（或你喜欢的颜色）
  - Light Damage: `15`
  - Heavy Damage: `30`
  - Light Attack Cooldown: `0.5`
  - Heavy Attack Cooldown: `1.2`
  - Collider Center: `(0, 0, 0.003)`
  - Collider Size: `(0.003, 0.001, 0.005)`

🎓 **碰撞体尺寸说明**: 这些值是素材包模型的大致尺寸。运行后可以在 Scene 视图中看到绿色线框（Collider Gizmo），根据实际大小微调。

- [ ] **Step 3: 创建 Axe 武器数据**

同上操作，在 `Assets/Data/Weapons/` 下创建：
- 重命名为 `Axe`
- Inspector 配置：
  - Weapon Name: `Axe`
  - Prefab: 拖入 `Assets/ThirdParty/Little Heroes Mega Pack/Prefabs/04 Attach Weapons/Axes (R Arm)/Axe 01.prefab`
  - Light Damage: `10`
  - Heavy Damage: `45`
  - Light Attack Cooldown: `0.6`
  - Heavy Attack Cooldown: `1.5`
  - Collider Center: `(0, 0, 0.003)`
  - Collider Size: `(0.004, 0.002, 0.004)`

- [ ] **Step 4: 创建 Dagger 武器数据**

同上操作：
- 重命名为 `Dagger`
- Inspector 配置：
  - Weapon Name: `Dagger`
  - Prefab: 拖入 `Assets/ThirdParty/Little Heroes Mega Pack/Prefabs/04 Attach Weapons/Daggers (R Arm)/Dagger 01 Red.prefab`（或你喜欢的颜色）
  - Light Damage: `8`
  - Heavy Damage: `18`
  - Light Attack Cooldown: `0.3`
  - Heavy Attack Cooldown: `0.7`
  - Collider Center: `(0, 0, 0.002)`
  - Collider Size: `(0.002, 0.001, 0.003)`

- [ ] **Step 5: 在 Player prefab 上添加 WeaponManager 组件**

1. 在 Hierarchy 中选中 Player（或双击 `Assets/Prefabs/Player.prefab` 进入 Prefab 编辑模式）
2. 在 Inspector 中点 Add Component → 搜索 `WeaponManager` → 添加
3. 在 WeaponManager 组件中：
   - **Weapons** 数组大小设为 `3`
   - Element 0: 拖入 `Assets/Data/Weapons/Sword.asset`
   - Element 1: 拖入 `Assets/Data/Weapons/Axe.asset`
   - Element 2: 拖入 `Assets/Data/Weapons/Dagger.asset`
   - **Right Hand Bone Name**: 保持默认 `RigRArmPalm`

🎓 **为什么顺序是 Sword/Axe/Dagger？** 对应键盘 1/2/3 键。数组索引 0→按键1, 1→按键2, 2→按键3。

- [ ] **Step 6: 处理 PlayerCombat 的旧引用**

选中 Player，查看 PlayerCombat 组件：
- `Weapon Collider` 字段应该已经消失了（因为代码中移除了这个 SerializeField）
- 如果 Inspector 中出现 "Missing" 引用的黄色警告，这是正常的——运行时 WeaponManager 会设置正确的引用

- [ ] **Step 7: 删除旧的 Knuckles 武器（可选）**

在 Prefab 编辑模式下，展开 Player 的骨骼层级：
Player → ... → RigRArmPalm → 找到 `Knuckles 03 Brown` → 右键 → Delete

🎓 **为什么可选？** WeaponManager.Start() 中已经有清除旧武器的逻辑。但为了 prefab 整洁，手动删除更好。如果你不删，运行时也会自动清除。

- [ ] **Step 8: 保存 Prefab 并 Apply**

如果在 Prefab 编辑模式中：点击顶部 ← 箭头退出，弹窗选 "Save"。
如果在 Hierarchy 中修改的：选中 Player → Inspector 顶部 Overrides → Apply All。

---

### Task 6: 运行测试与调试

🎓 **学习目标**: 如何在 Scene 视图中调试碰撞体、运行时检查 Transform 层级

**Files:**
- 无代码修改，纯测试

- [ ] **Step 1: 运行游戏，验证默认武器加载**

按 Play。观察 Console 输出：
- 应该看到 `>>> WeaponManager: 切换到武器 [1] Sword`
- 应该看到 `>>> 装备武器: Sword | 轻攻 15 | 重攻 30`
- 角色手上应该拿着一把剑（替代了之前的 Knuckles）

如果武器位置/旋转不对：
- 在 Hierarchy 中选中武器实例 → 调整 localPosition 和 localRotation
- 记下正确的值，可以后续在 WeaponManager 中添加偏移配置

- [ ] **Step 2: 测试武器切换**

运行中按 1/2/3 键：
- 按 1: 切换到 Sword，Console 显示对应日志
- 按 2: 切换到 Axe，角色手中模型变化
- 按 3: 切换到 Dagger，角色手中模型变化
- 重复按同一个键不应重复切换

- [ ] **Step 3: 测试攻击命中**

找到敌人后：
- 鼠标左键轻攻击 → 应该造成当前武器的轻攻伤害
- 鼠标右键重攻击 → 应该造成当前武器的重攻伤害
- 切换到 Axe(2) 后重攻 → 伤害应为 45（比 Sword 的 30 高）
- 切换到 Dagger(3) 后连续轻攻 → 冷却更短，攻击更快

🎓 **调试碰撞体**: 如果攻击打不到敌人，在 Scene 视图中暂停游戏（⏸），选中武器，查看 BoxCollider 的绿色线框是否覆盖了武器模型。如果太小/偏移不对，回去调整 WeaponData 中的 colliderCenter 和 colliderSize。

- [ ] **Step 4: 攻击中尝试切换武器**

在攻击动画播放中按 1/2/3 → 不应切换（代码中有 `IsAttacking` 检查）。

- [ ] **Step 5: Commit 最终状态**

确认一切正常后：

```bash
git add -A
git commit -m "feat: complete weapon mounting system with 3 switchable weapons"
```
