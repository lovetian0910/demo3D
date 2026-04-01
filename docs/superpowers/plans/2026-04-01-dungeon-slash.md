# Dungeon Slash 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个俯视角 3D 动作 Roguelike Demo，通过逐步实现掌握 Unity 3D 开发核心知识。

**Architecture:** Unity 6 URP 项目。玩家使用 CharacterController 移动，Cinemachine 俯视角相机跟随。敌人用 NavMeshAgent 寻路 + 手写状态机。战斗通过 IDamageable 接口统一处理伤害。房间在同一场景内通过门触发器串联。

**Tech Stack:** Unity 6, C#, URP, Cinemachine, NavMesh, Animator, Particle System

---

## 重要说明

本项目是 Unity 编辑器项目，代码由 Claude Code 生成，但**场景搭建、Prefab 配置、动画导入**等操作需要用户在 Unity Editor 中手动完成。每个 Task 会明确标注哪些是代码步骤（Claude 生成），哪些是编辑器步骤（用户操作）。

每个 Task 完成后，在 Unity 中进入 Play Mode 验证功能，然后提交。

---

## Task 0: 项目初始化与素材准备

**说明：此任务全部在 Unity Editor 和浏览器中完成，无代码。**

- [ ] **Step 1: 创建 Unity 项目**

打开 Unity Hub → New Project → 选择 **3D (URP)** 模板 → 项目名 `DungeonSlash` → 创建位置选 `/Users/kuangjianwei/AI_Discover/learn-3d/`

> **🎓 知识点：URP（Universal Render Pipeline）**
> Unity 有三种渲染管线：Built-in（旧）、URP（通用，性能平衡）、HDRP（高清，吃性能）。URP 是目前中小型项目标准选择。它用 Scriptable Render Pipeline 架构，允许你自定义渲染流程。面试时知道这三者区别即可。

- [ ] **Step 2: 创建目录结构**

在 Unity Project 窗口的 Assets 下创建以下文件夹：
```
Assets/
├── Scripts/Core/
├── Scripts/Player/
├── Scripts/Enemy/
├── Scripts/Combat/
├── Scripts/UI/
├── Prefabs/
├── Animations/
├── Materials/
├── Scenes/
└── ThirdParty/
```

- [ ] **Step 3: 安装 Cinemachine**

Window → Package Manager → Unity Registry → 搜索 **Cinemachine** → Install

> **🎓 知识点：Package Manager**
> Unity 的依赖管理系统，类似 npm/pip。Cinemachine 是 Unity 官方的相机解决方案，比手写相机跟随脚本强大得多（支持阻尼、边界、震屏等）。

- [ ] **Step 4: 下载 Mixamo 角色和动画**

访问 https://www.mixamo.com （需要 Adobe 账号，免费）：

1. **角色模型**：选一个你喜欢的角色（推荐搜索 "Knight" 或 "Warrior"），点 Download → Format: **FBX for Unity (.fbx)** → Download
2. **动画**（对每个动画重复操作）：
   - 搜索 "Idle" → 选一个 → **勾选 "In Place"**（原地播放）→ Download FBX
   - 搜索 "Running" → 选一个 → **勾选 "In Place"** → Download FBX
   - 搜索 "Slash" → 选两个（一快一慢，分别作为轻击/重击）→ Download FBX
   - 搜索 "Roll" 或 "Dodge" → 选一个 → Download FBX（这个不勾选 In Place，需要位移）
   - 搜索 "Hit Reaction" → 选一个 → Download FBX
   - 搜索 "Death" → 选一个 → Download FBX
3. 将所有 FBX 文件拖入 Unity 的 `Assets/ThirdParty/Mixamo/` 文件夹

> **🎓 知识点：FBX 与 "In Place"**
> FBX 是 3D 模型/动画的通用交换格式。"In Place" 意思是动画播放时角色不产生位移（位移由代码控制），这是游戏开发标准做法——否则动画和代码会争夺角色位置控制权。

- [ ] **Step 5: 下载地牢素材**

访问 https://quaternius.com → Free Assets → 下载 **Ultimate Modular Dungeon** 或类似的地牢包。解压后将模型文件拖入 `Assets/ThirdParty/Dungeon/`。

如果 Quaternius 不可用，在 Unity Asset Store 搜索 "dungeon low poly free" 选一个免费包。

- [ ] **Step 6: 配置 Mixamo 模型导入设置**

在 Unity 中选择角色模型 FBX：
1. **Model** 标签 → 默认即可
2. **Rig** 标签 → Animation Type 选 **Humanoid** → Apply
3. **Animation** 标签 → 对每个动画 FBX 同样操作，确认 Loop Time 勾选（Idle 和 Running 需要循环）

> **🎓 知识点：Humanoid Rig**
> Unity 的 Humanoid 系统把不同来源的骨骼映射到统一骨架上，这样 A 角色的模型可以播放 B 角色的动画。这是 Mixamo 动画能通用的核心原因。面试高频考点。

- [ ] **Step 7: 验证**

双击任意动画 FBX，在 Inspector 底部的预览窗口应该能看到角色播放动画。确认动画播放正常。

- [ ] **Step 8: 提交**

```bash
cd /Users/kuangjianwei/AI_Discover/learn-3d/DungeonSlash
git init
git add -A
git commit -m "feat: initialize Unity 6 URP project with Mixamo and dungeon assets"
```

---

## Task 1: 玩家移动与相机

**Files:**
- Create: `Assets/Scripts/Player/PlayerController.cs`

- [ ] **Step 1: 搭建测试场景**（编辑器操作）

1. 打开 `Scenes/SampleScene`（或新建场景保存为 `Scenes/Main`）
2. 在 Hierarchy 中创建一个 **3D Object → Plane**，Scale 设为 (5, 1, 5)，作为地面
3. 将 Mixamo 角色模型拖入场景，重命名为 `Player`
4. 给 Player 添加组件：**Character Controller**
   - Center: (0, 0.9, 0)（大约角色腰部高度，根据模型调整）
   - Radius: 0.3
   - Height: 1.8

> **🎓 知识点：CharacterController vs Rigidbody**
> Rigidbody 是物理驱动的——受重力、碰撞力影响，适合物理模拟（赛车、布娃娃）。CharacterController 是代码驱动的——你完全控制移动逻辑，它只负责碰撞检测。动作游戏几乎都用 CharacterController，因为你需要精确的手感控制。

- [ ] **Step 2: 编写 PlayerController.cs**

```csharp
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Dodge Roll")]
    [SerializeField] private float rollSpeed = 12f;
    [SerializeField] private float rollDuration = 0.4f;
    [SerializeField] private float rollCooldown = 0.8f;

    private CharacterController controller;
    private Vector3 velocity;
    private float rollTimer;
    private float rollCooldownTimer;
    private Vector3 rollDirection;
    private bool isRolling;

    // 供其他脚本查询状态
    public bool IsRolling => isRolling;
    public bool IsMoving => controller.velocity.magnitude > 0.1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (isRolling)
        {
            UpdateRoll();
            return; // 翻滚时不接受其他输入
        }

        HandleMovement();
        HandleDodgeInput();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // 获取输入（WASD 或方向键）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 基于相机方向计算移动方向
        // 俯视角下 camera.forward 朝下看，我们需要水平分量
        Camera cam = Camera.main;
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            // 角色朝移动方向旋转
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // 移动
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    private void HandleDodgeInput()
    {
        rollCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space) && rollCooldownTimer <= 0f)
        {
            StartRoll();
        }
    }

    private void StartRoll()
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        // 翻滚方向：如果有输入就用输入方向，否则用角色朝向
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            Camera cam = Camera.main;
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
            rollDirection = (camForward * v + camRight * h).normalized;
        }
        else
        {
            rollDirection = transform.forward;
        }
    }

    private void UpdateRoll()
    {
        rollTimer -= Time.deltaTime;

        if (rollTimer <= 0f)
        {
            isRolling = false;
            return;
        }

        controller.Move(rollDirection * rollSpeed * Time.deltaTime);
        ApplyGravity();
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // 保持一个小的向下力确保 isGrounded 判定
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
```

> **🎓 知识点：3D 坐标系与相机相对移动**
> Unity 用左手坐标系：X 右，Y 上，Z 前。在俯视角游戏中，按 W 应该让角色向"屏幕上方"移动——但这不是世界空间的 Z 轴，而是相机的前方投影。所以代码中用 `cam.transform.forward` 的水平分量来计算移动方向。这是 2D→3D 最核心的思维转变之一。

> **🎓 知识点：Quaternion.LookRotation**
> 3D 中旋转用四元数（Quaternion）而非欧拉角，因为欧拉角有万向锁（Gimbal Lock）问题。`LookRotation(dir)` 把方向向量转为旋转，`RotateTowards` 做平滑插值。面试常问。

- [ ] **Step 3: 挂载脚本**（编辑器操作）

选中 Player 对象 → Add Component → 搜索 `PlayerController` → 添加

- [ ] **Step 4: 设置 Cinemachine 俯视角相机**（编辑器操作）

1. 菜单 → GameObject → Cinemachine → **Virtual Camera**
2. 选中创建的 CinemachineCamera 对象，在 Inspector 中：
   - Follow: 拖入 Player
   - 在 Body 区域（Procedural 部分），Position Control 选 **Third Person Follow**
   - Shoulder Offset: (0, 0, 0)
   - Camera Distance: **12**
   - 将 Virtual Camera 的 Transform Rotation 设为 (60, 0, 0) — 俯视角度

> **🎓 知识点：Cinemachine 架构**
> Cinemachine 用"虚拟相机"概念——你可以有多个虚拟相机，每个有不同的跟随目标和参数。Cinemachine Brain（自动挂在 Main Camera 上）会根据优先级选择当前活跃的虚拟相机并平滑过渡。这种设计让相机切换（如对话镜头、Boss 登场）变得简单。

- [ ] **Step 5: 验证**

进入 Play Mode：
- WASD 移动角色，角色应朝移动方向旋转
- 相机以 60° 俯视角跟随玩家
- 空格键翻滚，翻滚期间不接受移动输入
- 角色不会穿过地面（重力正常）

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Player/PlayerController.cs
git commit -m "feat: player movement with CharacterController and Cinemachine top-down camera"
```

---

## Task 2: 动画系统

**Files:**
- Create: `Assets/Scripts/Player/PlayerAnimator.cs`

- [ ] **Step 1: 创建 Animator Controller**（编辑器操作）

1. 在 `Assets/Animations/` 右键 → Create → **Animator Controller**，命名 `PlayerAnimator`
2. 双击打开 Animator 窗口
3. 创建以下参数（左侧 Parameters 面板）：
   - `Speed` (Float) — 移动速度
   - `IsRolling` (Bool) — 是否在翻滚
   - `LightAttack` (Trigger) — 轻击触发
   - `HeavyAttack` (Trigger) — 重击触发
   - `Hit` (Trigger) — 受击触发
   - `Die` (Trigger) — 死亡触发
4. 创建状态和过渡：
   - 右键 → Create State → Empty，命名 `Idle`，设为默认状态（右键 → Set as Layer Default State）
   - 再创建：`Run`、`Roll`、`LightAttack`、`HeavyAttack`、`Hit`、`Death`
   - 将对应的 Mixamo 动画片段拖入各状态的 Motion 字段
5. 创建过渡（右键状态 → Make Transition）：
   - `Idle` → `Run`：条件 Speed > 0.1，**取消勾选 Has Exit Time**
   - `Run` → `Idle`：条件 Speed < 0.1，取消勾选 Has Exit Time
   - `Any State` → `Roll`：条件 IsRolling = true，取消勾选 Has Exit Time
   - `Roll` → `Idle`：**勾选 Has Exit Time**（播完自动回 Idle）
   - `Any State` → `LightAttack`：条件 LightAttack trigger，取消勾选 Has Exit Time
   - `Any State` → `HeavyAttack`：条件 HeavyAttack trigger，取消勾选 Has Exit Time
   - `LightAttack` → `Idle`：勾选 Has Exit Time
   - `HeavyAttack` → `Idle`：勾选 Has Exit Time
   - `Any State` → `Hit`：条件 Hit trigger
   - `Hit` → `Idle`：勾选 Has Exit Time
   - `Any State` → `Death`：条件 Die trigger

> **🎓 知识点：Animator 状态机**
> 这是 Unity 动画的核心。每个状态对应一个动画片段，过渡由参数驱动。**Has Exit Time** 是关键概念：勾选表示动画播完才过渡（适合攻击、死亡），不勾选表示条件满足立即过渡（适合移动、闪避这种需要即时响应的动作）。面试必考。

- [ ] **Step 2: 编写 PlayerAnimator.cs**

```csharp
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;

    // Animator 参数名缓存为 hash，避免字符串比较开销
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsRollingHash = Animator.StringToHash("IsRolling");
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        // 持续更新移动速度和翻滚状态
        float speed = playerController.IsMoving ? 1f : 0f;
        animator.SetFloat(SpeedHash, speed);
        animator.SetBool(IsRollingHash, playerController.IsRolling);
    }

    // 供 PlayerCombat 调用
    public void PlayLightAttack()
    {
        animator.SetTrigger(LightAttackHash);
    }

    public void PlayHeavyAttack()
    {
        animator.SetTrigger(HeavyAttackHash);
    }

    // 供 PlayerHealth 调用
    public void PlayHit()
    {
        animator.SetTrigger(HitHash);
    }

    public void PlayDeath()
    {
        animator.SetTrigger(DieHash);
    }
}
```

> **🎓 知识点：Animator.StringToHash**
> Animator 内部用整数 ID 匹配参数，每帧用字符串查找很浪费。`StringToHash` 预计算 hash 值是 Unity 性能优化的基本功。面试能提到这个会加分。

- [ ] **Step 3: 挂载并配置**（编辑器操作）

1. 选中 Player 对象 → Add Component → `PlayerAnimator`
2. 确保 Player 的子物体（Mixamo 模型）上有 **Animator** 组件
3. 将 Animator 的 Controller 字段拖入刚创建的 `PlayerAnimator` controller

- [ ] **Step 4: 验证**

进入 Play Mode：
- 站着不动播放 Idle 动画
- WASD 移动时切换到 Run 动画
- 空格键翻滚播放 Roll 动画
- 动画过渡应平滑，无跳帧

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Player/PlayerAnimator.cs Assets/Animations/
git commit -m "feat: player animation system with Animator state machine"
```

---

## Task 3: 战斗系统 — 伤害接口与玩家攻击

**Files:**
- Create: `Assets/Scripts/Combat/IDamageable.cs`
- Create: `Assets/Scripts/Combat/DamageSystem.cs`
- Create: `Assets/Scripts/Player/PlayerCombat.cs`

- [ ] **Step 1: 编写 IDamageable 接口**

```csharp
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage, Vector3 knockbackDirection);
    bool IsDead { get; }
}
```

> **🎓 知识点：接口（Interface）在游戏开发中的用途**
> 玩家和敌人都能受伤，但受伤逻辑不同。用接口定义"可受伤"的契约，攻击方不需要知道对方是玩家还是敌人——只要实现了 IDamageable 就能对它造成伤害。这是"面向接口编程"在游戏中的经典应用。

- [ ] **Step 2: 编写 DamageSystem.cs**

```csharp
using UnityEngine;

public static class DamageSystem
{
    /// <summary>
    /// 对碰撞体造成伤害。返回 true 表示成功命中了一个 IDamageable 对象。
    /// </summary>
    public static bool DealDamage(Collider target, float damage, Vector3 attackerPosition)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead)
        {
            return false;
        }

        Vector3 knockbackDir = (target.transform.position - attackerPosition).normalized;
        damageable.TakeDamage(damage, knockbackDir);
        return true;
    }
}
```

- [ ] **Step 3: 编写 PlayerCombat.cs**

```csharp
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Light Attack")]
    [SerializeField] private float lightDamage = 10f;
    [SerializeField] private float lightAttackCooldown = 0.5f;

    [Header("Heavy Attack")]
    [SerializeField] private float heavyDamage = 25f;
    [SerializeField] private float heavyAttackCooldown = 1.2f;

    [Header("Weapon Collider")]
    [SerializeField] private Collider weaponCollider; // 在编辑器中拖入武器的碰撞体

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();

        // 武器碰撞体默认关闭，攻击时才激活
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    private void Update()
    {
        attackCooldownTimer -= Time.deltaTime;

        if (playerController.IsRolling || isAttacking)
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
        currentAttackDamage = lightDamage;
        attackCooldownTimer = lightAttackCooldown;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = heavyDamage;
        attackCooldownTimer = heavyAttackCooldown;
        playerAnimator.PlayHeavyAttack();
    }

    // —— 以下两个方法由 Animation Event 调用 ——

    /// <summary>
    /// 在攻击动画的挥砍帧调用，激活武器碰撞体
    /// </summary>
    public void OnAttackHitStart()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
    }

    /// <summary>
    /// 在攻击动画结束时调用，关闭武器碰撞体
    /// </summary>
    public void OnAttackHitEnd()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
        isAttacking = false;
    }

    /// <summary>
    /// 挂在武器碰撞体的子物体上，当武器碰到敌人时触发
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            DamageSystem.DealDamage(other, currentAttackDamage, transform.position);
        }
    }
}
```

> **🎓 知识点：Animation Event（动画事件）**
> 动画事件让你在动画的特定帧调用脚本方法。比如挥剑动画的第 10 帧（刀划过去的时刻）激活伤害碰撞体，第 20 帧关闭。这比用计时器精确得多，而且动画师可以直接调节命中时机。行业标准做法。

- [ ] **Step 4: 编辑器配置**（编辑器操作）

1. 给 Player 添加 `PlayerCombat` 组件
2. 创建武器碰撞体：
   - 在 Player 模型的手部骨骼下创建一个空 GameObject，命名 `WeaponHitbox`
   - 添加 **Box Collider** 组件 → 勾选 **Is Trigger** → 调整大小覆盖武器挥动范围
   - 添加 **Rigidbody** 组件 → 勾选 **Is Kinematic**（触发器检测需要至少一方有 Rigidbody）
3. 将 `WeaponHitbox` 拖入 PlayerCombat 的 `weaponCollider` 字段
4. 设置 Animation Event（对每个攻击动画）：
   - 选中攻击动画 FBX → Animation 标签 → 在时间轴上添加事件
   - 在挥砍起始帧添加事件 → Function: `OnAttackHitStart`
   - 在挥砍结束帧添加事件 → Function: `OnAttackHitEnd`

> **🎓 知识点：Trigger Collider + Kinematic Rigidbody**
> Unity 的碰撞检测规则：OnTriggerEnter 至少需要一方有 Rigidbody。把武器设为 Kinematic Rigidbody + Trigger Collider 意味着它跟着骨骼动画移动，不受物理力影响，但能检测到碰撞。这是武器碰撞的标准设置。

- [ ] **Step 5: 验证**

进入 Play Mode：
- 创建一个 Cube，Tag 设为 "Enemy"，添加 Rigidbody（用于接收碰撞）
- 左键轻击、右键重击，观察动画播放
- 攻击时武器碰撞体应在正确时机激活（可在 Scene 视图中看到 Collider 闪烁）
- 翻滚中不能攻击
- 验证后删除测试 Cube

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Combat/ Assets/Scripts/Player/PlayerCombat.cs
git commit -m "feat: combat system with IDamageable interface and player melee attacks"
```

---

## Task 4: 玩家血量与受击反馈

**Files:**
- Create: `Assets/Scripts/Player/PlayerHealth.cs`

- [ ] **Step 1: 编写 PlayerHealth.cs**

```csharp
using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForce = 3f;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    private float currentHealth;
    private bool isDead;
    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private CharacterController characterController;
    private Renderer[] renderers;
    private Color[] originalColors;
    private Vector3 knockbackVelocity;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    // 供 UI 使用的归一化血量值（0~1）
    public float HealthPercent => currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();

        // 缓存所有渲染器和原始颜色用于闪白效果
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
            {
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            }
            else if (renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.color;
            }
        }
    }

    private void Update()
    {
        // 应用击退
        if (knockbackVelocity.magnitude > 0.1f)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 10f * Time.deltaTime);
        }
    }

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;

        // 击退
        knockbackVelocity = knockbackDirection * knockbackForce;

        // 闪白反馈
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
        else
        {
            playerAnimator.PlayHit();
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        // 设置闪白
        foreach (var renderer in renderers)
        {
            if (renderer.material.HasProperty("_BaseColor"))
            {
                renderer.material.SetColor("_BaseColor", flashColor);
            }
            else if (renderer.material.HasProperty("_Color"))
            {
                renderer.material.color = flashColor;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        // 恢复原色
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
            {
                renderers[i].material.SetColor("_BaseColor", originalColors[i]);
            }
            else if (renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = originalColors[i];
            }
        }
    }

    private void Die()
    {
        isDead = true;
        playerAnimator.PlayDeath();
        playerController.enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
        // TODO: GameManager 触发 Game Over（在 Task 8 实现）
    }
}
```

> **🎓 知识点：URP 材质属性名**
> Built-in 管线用 `_Color`，URP 用 `_BaseColor`。代码中两个都检查是为了兼容。这种细节在实际项目中经常踩坑。闪白效果直接修改材质颜色是最简单的方式；更高级的做法是用 Shader 的 emission。

> **🎓 知识点：Coroutine（协程）**
> Unity 的协程让你可以把"等一会再做"的逻辑写成线性代码，而不是用计时器和状态变量。`yield return new WaitForSeconds(0.1f)` 暂停执行 0.1 秒后继续。闪白效果正好适合：变白→等→恢复。

- [ ] **Step 2: 挂载**（编辑器操作）

选中 Player → Add Component → `PlayerHealth`

- [ ] **Step 3: 更新 PlayerController 以阻止受击/死亡时移动**

在 PlayerController.cs 中：先在 Awake 中缓存 PlayerHealth 引用，然后在 Update 方法开头检查死亡状态：

```csharp
private PlayerHealth playerHealth;

private void Awake()
{
    controller = GetComponent<CharacterController>();
    playerHealth = GetComponent<PlayerHealth>();
}

private void Update()
{
    // 如果死亡则不处理任何输入
    if (playerHealth != null && playerHealth.IsDead) return;

    if (isRolling)
    {
        UpdateRoll();
        return;
    }

    HandleMovement();
    HandleDodgeInput();
    ApplyGravity();
}
```

- [ ] **Step 4: 验证**

进入 Play Mode → 在 Inspector 中手动调用 `TakeDamage(10, Vector3.back)` 或者写一个临时测试脚本对玩家造成伤害，确认：
- 角色闪白
- 被击退
- 血量减少
- 血量到 0 播放死亡动画，不再能移动

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Player/PlayerHealth.cs Assets/Scripts/Player/PlayerController.cs
git commit -m "feat: player health system with hit flash and knockback feedback"
```

---

## Task 5: 敌人基类与近战敌人

**Files:**
- Create: `Assets/Scripts/Enemy/EnemyBase.cs`
- Create: `Assets/Scripts/Enemy/MeleeEnemy.cs`

- [ ] **Step 1: 编写 EnemyBase.cs**

```csharp
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 30f;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("Hit Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    [Header("Death")]
    [SerializeField] private GameObject deathEffectPrefab; // 死亡粒子特效预制件

    protected float currentHealth;
    protected EnemyState currentState = EnemyState.Idle;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected float attackTimer;
    private Renderer[] renderers;
    private Color[] originalColors;
    private bool isDead;

    public bool IsDead => isDead;
    public float HealthPercent => currentHealth / maxHealth;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        currentHealth = maxHealth;

        // 缓存渲染器和颜色
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            else if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
        }
    }

    protected virtual void Start()
    {
        // 找到场景中的玩家
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    protected virtual void Update()
    {
        if (isDead || playerTransform == null) return;

        attackTimer -= Time.deltaTime;
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                if (distToPlayer <= detectionRange)
                {
                    currentState = EnemyState.Chase;
                }
                break;

            case EnemyState.Chase:
                ChasePlayer(distToPlayer);
                break;

            case EnemyState.Attack:
                AttackUpdate(distToPlayer);
                break;
        }
    }

    private void ChasePlayer(float distance)
    {
        agent.SetDestination(playerTransform.position);

        if (distance <= attackRange)
        {
            agent.ResetPath(); // 停止移动
            currentState = EnemyState.Attack;
        }
        else if (distance > detectionRange * 1.5f)
        {
            agent.ResetPath();
            currentState = EnemyState.Idle;
        }
    }

    private void AttackUpdate(float distance)
    {
        // 面向玩家
        Vector3 lookDir = (playerTransform.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        if (distance > attackRange * 1.3f)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    /// <summary>
    /// 子类实现具体的攻击行为
    /// </summary>
    protected abstract void PerformAttack();

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        foreach (var r in renderers)
        {
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", flashColor);
            else if (r.material.HasProperty("_Color"))
                r.material.color = flashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                renderers[i].material.SetColor("_BaseColor", originalColors[i]);
            else if (renderers[i].material.HasProperty("_Color"))
                renderers[i].material.color = originalColors[i];
        }
    }

    protected virtual void Die()
    {
        isDead = true;
        currentState = EnemyState.Dead;
        agent.enabled = false;

        // 生成死亡特效
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }

        // 1 秒后销毁自身
        Destroy(gameObject, 1f);
    }
}
```

> **🎓 知识点：NavMeshAgent**
> NavMesh（导航网格）是 Unity 的内置寻路系统。你在场景中"烘焙"一张可行走区域的网格，NavMeshAgent 自动沿最短路径移动并避障。不用自己写 A* 算法。烘焙操作在 Task 7 场景搭建时做。

> **🎓 知识点：抽象类 vs 接口**
> EnemyBase 是抽象类（有共享实现），IDamageable 是接口（只有契约）。敌人之间共享 95% 的逻辑（状态机、血量、受击），只有攻击行为不同，所以用抽象类 + 抽象方法 `PerformAttack()` 是最合适的设计。

- [ ] **Step 2: 编写 MeleeEnemy.cs**

```csharp
using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeHitRadius = 1.5f;

    protected override void PerformAttack()
    {
        // 用 OverlapSphere 检测攻击范围内的玩家
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeHitRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                DamageSystem.DealDamage(hit, meleeDamage, transform.position);
                break; // 只命中一次
            }
        }
    }

    // 在 Scene 视图中显示攻击范围（仅编辑器调试用）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeHitRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
```

> **🎓 知识点：Physics.OverlapSphere**
> 在 3D 空间中以球形区域检测碰撞体，不需要创建实际的碰撞体对象。适合"瞬间"的范围判定（爆炸、近战挥击），比触发器更简洁。另一个常用的是 `Physics.Raycast`（射线检测，用于射击、视线判定）。

> **🎓 知识点：OnDrawGizmosSelected**
> 在 Scene 视图中画辅助线框，只在选中物体时显示。开发期间用来可视化攻击范围、检测距离等。不影响游戏运行，但极大提高调试效率。养成给每个"范围"加 Gizmo 的习惯。

- [ ] **Step 3: 编辑器配置**（编辑器操作）

1. 用 Mixamo 模型或简单 Capsule 创建一个敌人对象
2. Tag 设为 **Enemy**（如果没有这个 Tag，在 Tag 下拉菜单点 Add Tag 新建）
3. 同时给 Player 对象设置 Tag 为 **Player**
4. 给敌人添加组件：
   - **Nav Mesh Agent**
   - **Capsule Collider**（调整到包裹模型）
   - **MeleeEnemy** 脚本
5. 将敌人拖到 `Assets/Prefabs/` 做成 Prefab，命名 `MeleeEnemy`

- [ ] **Step 4: 验证**

进入 Play Mode：
- 敌人应在检测到玩家后开始追击
- 追到近距离后停下并攻击
- 玩家受到伤害（闪白、血量减少）
- 攻击敌人后敌人闪白，死亡后销毁

（注意：NavMesh 需要先烘焙才能工作，见 Step 5）

- [ ] **Step 5: 烘焙 NavMesh**（编辑器操作）

1. 选中地面 Plane → Inspector → Static 下拉 → 勾选 **Navigation Static**
2. Window → AI → **Navigation** → Bake 标签 → 点 **Bake**
3. Scene 视图中应看到蓝色的可行走区域覆盖

> **🎓 知识点：NavMesh 烘焙**
> 烘焙过程会分析场景中所有标记为 Navigation Static 的物体，计算出哪些区域可行走。Agent 的半径和高度决定了哪些缝隙能通过。如果场景改了（加了墙、移了家具），需要重新烘焙。动态障碍物用 NavMeshObstacle 组件。

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Enemy/
git commit -m "feat: enemy base class with state machine AI and melee enemy type"
```

---

## Task 6: 远程敌人与弹体

**Files:**
- Create: `Assets/Scripts/Enemy/RangedEnemy.cs`
- Create: `Assets/Scripts/Combat/Projectile.cs`

- [ ] **Step 1: 编写 Projectile.cs**

```csharp
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float damage = 10f;

    private Vector3 direction;
    private bool initialized;

    /// <summary>
    /// 由发射者调用，设置弹体方向和伤害
    /// </summary>
    public void Initialize(Vector3 shootDirection, float projectileDamage)
    {
        direction = shootDirection.normalized;
        damage = projectileDamage;
        initialized = true;

        // 面向移动方向
        transform.rotation = Quaternion.LookRotation(direction);

        // 超时自动销毁
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!initialized) return;
        transform.position += direction * speed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 不打敌人自己
        if (other.CompareTag("Enemy")) return;

        if (other.CompareTag("Player"))
        {
            DamageSystem.DealDamage(other, damage, transform.position);
        }

        // 碰到任何东西都销毁（包括墙壁）
        Destroy(gameObject);
    }
}
```

- [ ] **Step 2: 编写 RangedEnemy.cs**

```csharp
using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged Attack")]
    [SerializeField] private float rangedDamage = 10f;
    [SerializeField] private float preferredDistance = 6f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint; // 发射位置（在模型上创建空物体标记）

    protected override void Awake()
    {
        base.Awake();
        // 远程敌人攻击距离更远
        attackRange = preferredDistance + 1f;
    }

    protected override void Update()
    {
        base.Update();

        // 远程敌人在追击状态时保持距离
        if (currentState == EnemyState.Chase && playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist < preferredDistance * 0.7f)
            {
                // 太近了，后退
                Vector3 retreatDir = (transform.position - playerTransform.position).normalized;
                agent.SetDestination(transform.position + retreatDir * 2f);
            }
        }
    }

    protected override void PerformAttack()
    {
        if (projectilePrefab == null || firePoint == null) return;

        // 计算朝向玩家的方向
        Vector3 dir = (playerTransform.position - firePoint.position).normalized;
        dir.y = 0; // 保持水平射击

        // 实例化弹体
        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = proj.GetComponent<Projectile>();
        projectile.Initialize(dir, rangedDamage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
```

> **🎓 知识点：Instantiate（实例化）**
> `Instantiate` 从 Prefab 创建游戏对象实例——相当于运行时"复制"一个预设物体到场景中。弹体、特效、敌人刷新都用这个。性能敏感场景（大量弹幕）应该用对象池（Object Pool）替代，但我们的 demo 用 Instantiate 足够。

- [ ] **Step 3: 创建弹体 Prefab**（编辑器操作）

1. 创建 3D Object → **Sphere**，Scale 设为 (0.2, 0.2, 0.2)
2. 添加组件：
   - **Sphere Collider** → 勾选 **Is Trigger**
   - **Rigidbody** → 勾选 **Is Kinematic**（运动由代码控制）
   - **Projectile** 脚本
3. 创建一个新 Material → 设为红色/橙色，拖给弹体
4. 拖到 `Prefabs/` 做成 Prefab `EnemyProjectile`，然后从场景中删除

- [ ] **Step 4: 创建远程敌人 Prefab**（编辑器操作）

1. 和近战敌人类似，创建一个角色对象，Tag 设为 **Enemy**
2. 添加：NavMeshAgent、Capsule Collider、**RangedEnemy** 脚本
3. 在模型上创建空 GameObject `FirePoint`，放在胸口/手部位置
4. 将 `FirePoint` 拖入 RangedEnemy 的 `firePoint` 字段
5. 将 `EnemyProjectile` Prefab 拖入 `projectilePrefab` 字段
6. 拖到 `Prefabs/` 做成 Prefab `RangedEnemy`

- [ ] **Step 5: 验证**

进入 Play Mode，在场景中放一个近战敌人和一个远程敌人：
- 近战敌人追击玩家、近距离攻击
- 远程敌人保持距离、发射弹体
- 弹体击中玩家造成伤害
- 弹体击中墙壁消失
- 两种敌人都能被玩家攻击并死亡

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Enemy/RangedEnemy.cs Assets/Scripts/Combat/Projectile.cs
git commit -m "feat: ranged enemy with projectile system"
```

---

## Task 7: 血量 UI

**Files:**
- Create: `Assets/Scripts/UI/HealthBarUI.cs`
- Create: `Assets/Scripts/UI/EnemyHealthBar.cs`

- [ ] **Step 1: 编写 HealthBarUI.cs（玩家血条）**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;  // 血条填充图
    [SerializeField] private Image bgImage;    // 血条背景
    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = GameObject.FindGameObjectWithTag("Player")
            ?.GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (playerHealth == null) return;
        fillImage.fillAmount = playerHealth.HealthPercent;

        // 血量低时变红
        fillImage.color = playerHealth.HealthPercent > 0.3f
            ? Color.green
            : Color.red;
    }
}
```

- [ ] **Step 2: 编写 EnemyHealthBar.cs（敌人头顶血条）**

```csharp
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private Vector3 offset = new Vector3(0, 2.2f, 0);
    private EnemyBase enemy;
    private Camera mainCamera;
    private Canvas canvas;

    private void Awake()
    {
        enemy = GetComponentInParent<EnemyBase>();
        mainCamera = Camera.main;
        canvas = GetComponent<Canvas>();

        // 世界空间 Canvas 需要设置相机
        if (canvas != null)
        {
            canvas.worldCamera = mainCamera;
        }
    }

    private void LateUpdate()
    {
        if (enemy == null) return;

        // 跟随敌人位置
        transform.position = enemy.transform.position + offset;

        // 始终朝向相机（Billboard 效果）
        transform.forward = mainCamera.transform.forward;

        // 更新血条
        fillImage.fillAmount = enemy.HealthPercent;

        // 满血时隐藏
        canvas.enabled = enemy.HealthPercent < 1f;
    }
}
```

> **🎓 知识点：World Space Canvas**
> Unity UI 有三种 Canvas 模式：Screen Space-Overlay（覆盖屏幕，用于 HUD）、Screen Space-Camera（投射到相机，有透视感）、**World Space**（放在 3D 场景中，用于头顶血条、3D 交互面板）。敌人头顶血条用 World Space，让它像 3D 物体一样存在于场景中，然后用 Billboard 技术让它始终面向相机。

- [ ] **Step 3: 创建玩家血条 UI**（编辑器操作）

1. Hierarchy → 右键 → UI → **Canvas**（自动创建 Screen Space-Overlay Canvas）
2. 在 Canvas 下右键 → UI → **Image**，命名 `HealthBarBG`
   - Rect Transform：Anchor 设为左上角，Pos: (120, -40), Width: 200, Height: 20
   - Color: 深灰色 (50,50,50)
3. 在 HealthBarBG 下右键 → UI → **Image**，命名 `HealthBarFill`
   - Rect Transform：stretch 填满父级（四个 anchor 都是 0 到 1）
   - **Image Type: Filled** → Fill Method: Horizontal → Fill Origin: Left
   - Color: 绿色
4. 在 Canvas 上添加 `HealthBarUI` 脚本 → 将 Fill 和 BG Image 拖入对应字段

- [ ] **Step 4: 创建敌人头顶血条 Prefab**（编辑器操作）

1. 在一个敌人 Prefab 的子物体下，右键 → UI → **Canvas**
   - Canvas 组件 → Render Mode: **World Space**
   - Rect Transform → Width: 1, Height: 0.1（世界空间单位）
2. 在 Canvas 下添加背景 Image（灰色）和 Fill Image（红色，Filled 类型）
3. 给 Canvas 添加 `EnemyHealthBar` 脚本 → 拖入 Fill Image
4. 在两个敌人 Prefab 上都加上这个血条结构

- [ ] **Step 5: 验证**

进入 Play Mode：
- 左上角显示玩家血条
- 受伤后血条减少，低血量时变红
- 敌人满血时不显示血条
- 敌人受击后头顶出现血条并减少
- 敌人血条始终面向相机

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/UI/
git commit -m "feat: player HUD health bar and enemy world-space health bars"
```

---

## Task 8: 房间管理与游戏流程

**Files:**
- Create: `Assets/Scripts/Core/GameManager.cs`
- Create: `Assets/Scripts/Core/RoomManager.cs`
- Create: `Assets/Scripts/Core/RoomTrigger.cs`

- [ ] **Step 1: 编写 GameManager.cs**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameObject gameOverUI;  // Game Over 画面
    [SerializeField] private GameObject victoryUI;   // 胜利画面

    private bool isGameOver;

    private void Awake()
    {
        // 单例模式
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void OnPlayerDeath()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(true);
        }

        // 2 秒后可以重新开始
        Invoke(nameof(EnableRestart), 2f);
    }

    public void OnAllRoomsCleared()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }

        Invoke(nameof(EnableRestart), 2f);
    }

    private void EnableRestart()
    {
        // 按任意键重新开始
        StartCoroutine(WaitForRestart());
    }

    private System.Collections.IEnumerator WaitForRestart()
    {
        while (!Input.anyKeyDown)
        {
            yield return null;
        }
        isGameOver = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
```

> **🎓 知识点：单例模式（Singleton）在 Unity 中**
> GameManager 全局只需要一个实例，用单例模式通过 `GameManager.Instance` 访问。这是 Unity 中最常见的管理器模式。注意：单例容易被滥用导致耦合过度，只对真正的全局管理器使用（GameManager、AudioManager 等）。

- [ ] **Step 2: 编写 RoomManager.cs**

```csharp
using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public string roomName;
        public GameObject roomObject;       // 房间根物体（包含地形、墙壁）
        public Transform playerSpawnPoint;  // 玩家出生点
        public Transform[] enemySpawnPoints;// 敌人刷新点
        public GameObject[] enemyPrefabs;   // 该房间使用的敌人 Prefab
        public GameObject exitDoor;         // 出口门对象
    }

    [SerializeField] private Room[] rooms;
    private int currentRoomIndex = -1;
    private List<EnemyBase> aliveEnemies = new List<EnemyBase>();

    private void Start()
    {
        // 关闭所有房间，只开启第一个
        foreach (var room in rooms)
        {
            room.roomObject.SetActive(false);
            if (room.exitDoor != null)
            {
                room.exitDoor.SetActive(false);
            }
        }

        ActivateRoom(0);
    }

    public void ActivateRoom(int index)
    {
        if (index >= rooms.Length)
        {
            // 所有房间通关
            GameManager.Instance.OnAllRoomsCleared();
            return;
        }

        currentRoomIndex = index;
        Room room = rooms[currentRoomIndex];

        // 激活房间
        room.roomObject.SetActive(true);

        // 移动玩家到出生点
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false; // CharacterController 会阻止直接设置位置
        player.transform.position = room.playerSpawnPoint.position;
        cc.enabled = true;

        // 刷新敌人
        SpawnEnemies(room);
    }

    private void SpawnEnemies(Room room)
    {
        aliveEnemies.Clear();

        for (int i = 0; i < room.enemySpawnPoints.Length; i++)
        {
            // 循环使用 enemyPrefabs（如果刷新点多于 Prefab 种类）
            GameObject prefab = room.enemyPrefabs[i % room.enemyPrefabs.Length];
            Transform spawnPoint = room.enemySpawnPoints[i];

            GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
            aliveEnemies.Add(enemyBase);
        }
    }

    private void Update()
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Length) return;

        // 检查是否所有敌人都死了
        aliveEnemies.RemoveAll(e => e == null || e.IsDead);

        if (aliveEnemies.Count == 0 && rooms[currentRoomIndex].exitDoor != null)
        {
            // 开门
            rooms[currentRoomIndex].exitDoor.SetActive(true);
        }
    }

    public void OnPlayerEnterNextRoom()
    {
        // 关闭当前房间
        if (currentRoomIndex >= 0 && currentRoomIndex < rooms.Length)
        {
            rooms[currentRoomIndex].roomObject.SetActive(false);
            rooms[currentRoomIndex].exitDoor.SetActive(false);
        }

        ActivateRoom(currentRoomIndex + 1);
    }
}
```

- [ ] **Step 3: 编写 RoomTrigger.cs**

```csharp
using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    private RoomManager roomManager;

    private void Start()
    {
        roomManager = FindObjectOfType<RoomManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            roomManager.OnPlayerEnterNextRoom();
        }
    }
}
```

- [ ] **Step 4: 更新 PlayerHealth 的死亡逻辑**

在 `PlayerHealth.cs` 的 `Die()` 方法末尾添加：

```csharp
private void Die()
{
    isDead = true;
    playerAnimator.PlayDeath();
    playerController.enabled = false;
    GetComponent<PlayerCombat>().enabled = false;

    // 通知 GameManager
    if (GameManager.Instance != null)
    {
        GameManager.Instance.OnPlayerDeath();
    }
}
```

- [ ] **Step 5: 场景搭建**（编辑器操作 — 这是最耗时的步骤）

1. **创建空 GameObject** `GameManager` → 挂载 `GameManager` 脚本
2. **创建空 GameObject** `RoomManager` → 挂载 `RoomManager` 脚本
3. **搭建 3-5 个房间**（使用地牢素材包的墙壁、地板模型）：
   - 每个房间用一个空父物体包裹（如 `Room_01`、`Room_02`...）
   - 在每个房间内放置空物体标记 `PlayerSpawn` 和若干 `EnemySpawn` 点
   - 在每个房间出口放一个可见物体（如发光的门框）命名为 `ExitDoor`
   - 在 ExitDoor 上添加 Box Collider (Is Trigger) + `RoomTrigger` 脚本
4. **配置 RoomManager**：在 Inspector 中展开 Rooms 数组，拖入每个房间的引用
5. **设置光照**：
   - 场景中应已有 Directional Light（太阳光），调整角度
   - 在每个房间内放 1-2 个 **Point Light**（暖色调，模拟火把）
   - 在 Lighting 设置中确保 Realtime Shadow 开启
6. **重新烘焙 NavMesh**（加了墙壁后可行走区域变了）
7. **创建简单的 Game Over / Victory UI**：
   - Canvas 下创建两个 Panel，各带一个 Text 显示 "GAME OVER" / "VICTORY"
   - 默认都隐藏（取消勾选 active）
   - 拖入 GameManager 的对应字段

> **🎓 知识点：光照 — Directional vs Point vs Spot**
> Directional Light 模拟太阳，平行光照射整个场景。Point Light 从一点向四周发光（火把、灯泡）。Spot Light 是锥形光（手电筒、聚光灯）。地牢场景用昏暗的 Directional Light + 暖色 Point Light 就能营造氛围。阴影由光源的 Shadow Type 设置控制。

- [ ] **Step 6: 验证**

进入 Play Mode：
- 游戏从第一个房间开始
- 清空敌人后出口门出现
- 走到门上触发进入下一房间
- 通关所有房间显示 Victory
- 死亡显示 Game Over
- 按任意键重新开始

- [ ] **Step 7: 提交**

```bash
git add Assets/Scripts/Core/ Assets/Scripts/Player/PlayerHealth.cs Assets/Scenes/
git commit -m "feat: room management system with game flow (spawn, clear, next room, win/lose)"
```

---

## Task 9: 粒子特效

**Files:**
- Create: `Assets/Scripts/Combat/HitEffect.cs`

- [ ] **Step 1: 创建死亡爆炸特效**（编辑器操作）

1. Hierarchy → 右键 → Effects → **Particle System**
2. 在 Inspector 中调整 Particle System 参数：
   - **Duration**: 0.5
   - **Start Lifetime**: 0.3 ~ 0.5
   - **Start Speed**: 3 ~ 6
   - **Start Size**: 0.1 ~ 0.3
   - **Start Color**: 红色到橙色渐变
   - **Simulation Space**: World
   - **Max Particles**: 20
   - **Emission** → Rate over Time: 0 → Bursts: 添加一个 burst, Count: 20（一次性全部喷出）
   - **Shape**: Sphere, Radius: 0.3
   - **Color over Lifetime**: 从有色 → 透明（Alpha 从 1 到 0）
   - **Size over Lifetime**: 曲线从 1 到 0（缩小消失）
   - **勾选 Stop Action**: Destroy（播完自动销毁）
3. 取消 **Looping**（只播一次）
4. 拖到 `Prefabs/` 保存为 `DeathEffect`，从场景删除
5. 将 `DeathEffect` Prefab 拖入两种敌人 Prefab 的 `deathEffectPrefab` 字段

> **🎓 知识点：Particle System**
> 粒子系统是 3D 游戏视觉反馈的核心工具。关键参数：**Emission**（多少/何时发射）、**Shape**（从什么形状发射）、**Color/Size over Lifetime**（生命周期内的变化）。理解这几个就能做出 80% 的常见特效。Unity 的 Shuriken 粒子系统是行业标准之一。

- [ ] **Step 2: 编写 HitEffect.cs（受击火花）**

```csharp
using UnityEngine;

public class HitEffect : MonoBehaviour
{
    [SerializeField] private GameObject hitEffectPrefab;

    /// <summary>
    /// 在受击位置生成粒子特效
    /// </summary>
    public static void Spawn(GameObject prefab, Vector3 position)
    {
        if (prefab == null) return;
        GameObject effect = Instantiate(prefab, position, Quaternion.identity);
        // 粒子系统设置了 Stop Action: Destroy，会自动清理
    }
}
```

- [ ] **Step 3: 在 EnemyBase.TakeDamage 中添加受击特效**

在 `EnemyBase.cs` 的 `TakeDamage` 方法中添加：

```csharp
[Header("Hit Effect")]
[SerializeField] private GameObject hitEffectPrefab;

public void TakeDamage(float damage, Vector3 knockbackDirection)
{
    if (isDead) return;

    currentHealth -= damage;
    StartCoroutine(FlashCoroutine());

    // 生成受击特效
    HitEffect.Spawn(hitEffectPrefab, transform.position + Vector3.up);

    if (currentHealth <= 0f)
    {
        currentHealth = 0f;
        Die();
    }
}
```

- [ ] **Step 4: 创建受击火花特效**（编辑器操作）

类似死亡特效但更小更快：
- Duration: 0.2, Start Lifetime: 0.1~0.2, Start Speed: 5~10
- Burst Count: 8, Start Size: 0.05~0.1
- 颜色：白色到黄色
- 保存为 Prefab `HitSpark`
- 拖入两种敌人 Prefab 的 `hitEffectPrefab` 字段

- [ ] **Step 5: 验证**

进入 Play Mode：
- 攻击敌人时出现火花特效
- 敌人死亡时出现爆炸特效
- 特效播完自动消失（无残留物体）

- [ ] **Step 6: 提交**

```bash
git add Assets/Scripts/Combat/HitEffect.cs Assets/Scripts/Enemy/EnemyBase.cs Assets/Prefabs/
git commit -m "feat: particle effects for hit sparks and enemy death explosion"
```

---

## Task 10: 最终打磨与面试准备

- [ ] **Step 1: 游戏手感调优**

在 Unity 中反复试玩，调整 Inspector 中的数值：
- `PlayerController`: moveSpeed (建议 5-7), rollSpeed (建议 10-14), rotationSpeed (建议 600-800)
- `PlayerCombat`: lightDamage, heavyDamage, cooldown 时间
- `MeleeEnemy`: moveSpeed, attackRange, attackCooldown, meleeDamage
- `RangedEnemy`: preferredDistance, attackCooldown, rangedDamage
- `PlayerHealth`: maxHealth, knockbackForce

> **🎓 知识点：SerializeField 的力量**
> 所有标记了 `[SerializeField]` 的变量都能在 Inspector 中实时调节，Play Mode 中也能改（但退出 Play Mode 会重置）。这是 Unity 快速调参的核心工作流——改参数 → 测试 → 再改，不需要改代码重新编译。面试时知道这个工作流很重要。

- [ ] **Step 2: 检查常见问题**

逐项确认：
- 玩家不能穿墙（所有墙壁都有 Collider）
- 敌人不会卡在墙里（NavMesh 正确烘焙）
- 弹体碰墙消失（墙壁有 Collider）
- 死亡后不能操作
- 通关/死亡后能重新开始

- [ ] **Step 3: 最终提交**

```bash
git add -A
git commit -m "feat: final polish - gameplay tuning and bug fixes"
```

- [ ] **Step 4: 面试知识点复习清单**

通过本项目你应该能回答以下面试题：

| 3D 概念 | 你做了什么 | 面试可能怎么问 |
|---------|-----------|---------------|
| 坐标系 | 相机相对移动 | "Unity 用什么坐标系？左手还是右手？" |
| CharacterController vs Rigidbody | 选了 CC 做动作游戏 | "什么时候用 CC，什么时候用 RB？" |
| Quaternion | LookRotation 转向 | "什么是万向锁？四元数怎么解决？" |
| Animator 状态机 | 完整的状态过渡 | "Has Exit Time 是什么？Trigger 和 Bool 的区别？" |
| NavMesh | 敌人寻路 | "NavMesh 怎么工作？动态障碍物怎么处理？" |
| 碰撞检测 | Trigger + OverlapSphere | "OnTriggerEnter 和 OnCollisionEnter 的区别？" |
| URP | 项目用 URP 管线 | "Built-in、URP、HDRP 的区别？什么时候用哪个？" |
| Particle System | 死亡/受击特效 | "Emission 和 Shape 模块是做什么的？" |
| 单例模式 | GameManager | "Unity 中单例怎么实现？有什么缺点？" |
| 接口 | IDamageable | "为什么用接口而不是基类？" |
