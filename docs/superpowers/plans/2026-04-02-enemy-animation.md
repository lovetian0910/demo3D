# MeleeEnemy 动画与状态完善 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完善 MeleeEnemy 的动画驱动（Idle/Run/Attack/Hit/Die）、受击硬直、攻击帧同步伤害、死亡动画等待。

**Architecture:** 改造 EnemyBase 的状态机新增 Hit 状态，添加 Animator 参数驱动。攻击使用代码计时器延迟伤害判定（帧同步）。受击打断当前状态并硬直。死亡等待动画播完再销毁。

**Tech Stack:** Unity 6 / C# / NavMeshAgent / Animator / 状态机

**Spec:** `docs/superpowers/specs/2026-04-02-enemy-animation-design.md`

---

## File Structure

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `Assets/Scripts/Enemy/EnemyBase.cs` | 新增 Hit 状态；Animator 参数驱动；受击硬直计时；攻击帧同步计时；死亡动画等待 |
| 修改 | `Assets/Scripts/Enemy/MeleeEnemy.cs` | PerformAttack 不变，但攻击流程由 EnemyBase 统一管理时机 |
| Editor | 创建 `Assets/Animations/MeleeEnemyAnimator.controller` | 敌人专用 Animator Controller |

---

### Task 1: 改造 EnemyBase 状态机和 Animator 驱动

🎓 **学习目标**:
- 敌人 AI 状态机如何和 Animator 状态机配合
- NavMeshAgent 在不同状态下的启停控制
- 攻击帧同步（代码计时器延迟伤害）
- 受击硬直（状态打断 + 计时器恢复）

**Files:**
- Modify: `DungeonSlash/Assets/Scripts/Enemy/EnemyBase.cs`

- [ ] **Step 1: 替换 EnemyBase.cs 完整内容**

```csharp
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Hit,   // 🎓 新增：受击硬直状态
    Dead
}

/// <summary>
/// 敌人基类——状态机 + Animator 驱动 + 受击硬直 + 攻击帧同步。
///
/// 🎓 AI 状态机 vs Animator 状态机：
/// 这里有两个"状态机"在同时工作：
/// 1. AI 状态机（代码中的 EnemyState enum）：控制敌人"该做什么"（追击？攻击？）
/// 2. Animator 状态机（Unity Animator Controller）：控制"播什么动画"
///
/// 两者通过 Animator 参数（Speed/Attack/Hit/Die）同步：
/// AI 切换状态 → 设置对应的 Animator 参数 → Animator 播对应动画。
/// AI 决定行为逻辑，Animator 决定视觉表现。
///
/// 🎓 攻击帧同步：
/// 不是进入 Attack 状态就立刻判定伤害，而是等 attackHitDelay 秒
/// （对应动画中"挥刀"那一帧），然后才执行 PerformAttack()。
/// 这样玩家看到的"被砍到"和实际扣血是同步的。
///
/// 🎓 受击硬直：
/// 被打时切换到 Hit 状态，硬直 hitStunDuration 秒内无法行动。
/// Hit 优先级高于 Attack，所以玩家快速连击可以压制敌人（打断攻击）。
/// 硬直期间再次被打会刷新计时器——这就是"连击"手感的来源。
/// </summary>
public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 30f;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("Attack Timing")]
    [Tooltip("攻击动画开始后多久执行伤害判定（对应挥刀帧）")]
    [SerializeField] protected float attackHitDelay = 0.4f;

    [Tooltip("攻击动画总时长（伤害判定后还要等动画播完才能下一次行动）")]
    [SerializeField] protected float attackAnimDuration = 0.8f;

    [Header("Hit Stun")]
    [Tooltip("受击硬直时长（秒），期间无法行动")]
    [SerializeField] private float hitStunDuration = 0.4f;

    [Header("Hit Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    [Header("Death")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Tooltip("死亡动画播放时长，播完后才销毁")]
    [SerializeField] private float deathAnimDuration = 1.5f;

    protected float currentHealth;
    protected EnemyState currentState = EnemyState.Idle;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected Animator animator;

    // 计时器
    protected float attackTimer;        // 攻击冷却
    private float attackPhaseTimer;     // 攻击阶段计时（delay → hit → 动画结束）
    private bool attackHitDone;         // 本次攻击是否已执行伤害判定
    private float hitStunTimer;         // 受击硬直倒计时

    // 材质闪白
    private Renderer[] renderers;
    private Material[] cachedMaterials;
    private bool isDead;

    // Animator 参数 hash 缓存
    // 🎓 和 PlayerAnimator 一样，用 StringToHash 避免每帧字符串比较
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public bool IsDead => isDead;
    public float HealthPercent => currentHealth / maxHealth;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        currentHealth = maxHealth;
        animator = GetComponentInChildren<Animator>();

        // 材质实例化 + Emission 初始化（同之前）
        renderers = GetComponentsInChildren<Renderer>();
        cachedMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            cachedMaterials[i] = renderers[i].material;
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].EnableKeyword("_EMISSION");
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }

    protected virtual void Start()
    {
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
                UpdateIdle(distToPlayer);
                break;

            case EnemyState.Chase:
                UpdateChase(distToPlayer);
                break;

            case EnemyState.Attack:
                UpdateAttack(distToPlayer);
                break;

            case EnemyState.Hit:
                UpdateHit();
                break;
        }

        // 🎓 每帧更新 Animator 的 Speed 参数
        // NavMeshAgent.velocity.magnitude 反映实际移动速度
        // Animator 用这个值来混合 Idle ↔ Run 动画
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        }
    }

    // ======================== 各状态 Update ========================

    private void UpdateIdle(float distToPlayer)
    {
        if (distToPlayer <= detectionRange)
        {
            currentState = EnemyState.Chase;
        }
    }

    private void UpdateChase(float distToPlayer)
    {
        agent.SetDestination(playerTransform.position);

        if (distToPlayer <= attackRange && attackTimer <= 0f)
        {
            // 进入攻击
            agent.ResetPath();
            StartAttack();
        }
        else if (distToPlayer > detectionRange * 1.5f)
        {
            agent.ResetPath();
            currentState = EnemyState.Idle;
        }
    }

    /// <summary>
    /// 🎓 攻击阶段管理——帧同步的核心逻辑。
    ///
    /// 时间线：
    /// [0s]              → 播放攻击动画
    /// [attackHitDelay]  → 执行 PerformAttack()（挥刀帧，判定伤害）
    /// [attackAnimDuration] → 动画播完，回到 Chase/Idle
    ///
    /// 这和 PlayerCombat 的 hitActiveDelay + hitActiveDuration 思路相同，
    /// 但敌人更简单——只有一个"判定瞬间"而不是一段持续时间。
    /// </summary>
    private void UpdateAttack(float distToPlayer)
    {
        // 面向玩家
        Vector3 lookDir = (playerTransform.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        attackPhaseTimer -= Time.deltaTime;

        // 阶段1：等待挥刀帧
        if (!attackHitDone && attackPhaseTimer <= 0f)
        {
            PerformAttack();
            attackHitDone = true;
            attackPhaseTimer = attackAnimDuration - attackHitDelay; // 剩余动画时间
        }
        // 阶段2：等待动画播完
        else if (attackHitDone && attackPhaseTimer <= 0f)
        {
            attackTimer = attackCooldown;

            // 根据距离决定下一个状态
            if (distToPlayer > detectionRange * 1.5f)
            {
                currentState = EnemyState.Idle;
            }
            else
            {
                currentState = EnemyState.Chase;
            }
        }
    }

    private void UpdateHit()
    {
        hitStunTimer -= Time.deltaTime;

        if (hitStunTimer <= 0f)
        {
            // 硬直结束，根据距离决定恢复到什么状态
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= detectionRange)
            {
                currentState = EnemyState.Chase;
                agent.isStopped = false;
            }
            else
            {
                currentState = EnemyState.Idle;
                agent.isStopped = false;
            }
        }
    }

    // ======================== 状态进入 ========================

    private void StartAttack()
    {
        currentState = EnemyState.Attack;
        attackPhaseTimer = attackHitDelay; // 先等挥刀帧
        attackHitDone = false;

        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    /// <summary>
    /// 进入受击硬直。
    /// 🎓 可以打断攻击（Hit 优先级 > Attack），并且重复被打会刷新计时器。
    /// </summary>
    private void EnterHitState()
    {
        currentState = EnemyState.Hit;
        hitStunTimer = hitStunDuration;

        // 停止移动
        agent.ResetPath();
        agent.isStopped = true;

        if (animator != null)
        {
            animator.SetTrigger(HitHash);
        }
    }

    // ======================== 伤害接口 ========================

    protected abstract void PerformAttack();

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($">>> 敌人受伤! 伤害: {damage}, 剩余血量: {currentHealth}/{maxHealth}");
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
        else
        {
            // 🎓 受击打断当前状态，进入硬直
            EnterHitState();
        }
    }

    // ======================== 死亡 ========================

    protected virtual void Die()
    {
        isDead = true;
        currentState = EnemyState.Dead;

        // 禁用寻路和碰撞
        agent.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 播放死亡动画
        if (animator != null)
        {
            animator.SetTrigger(DieHash);
        }

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }

        // 🎓 等待死亡动画播完再销毁（之前是 1s 固定值，现在可配置）
        Destroy(gameObject, deathAnimDuration);
    }

    // ======================== 闪白 ========================

    private System.Collections.IEnumerator FlashCoroutine()
    {
        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", flashColor * 3f);
            }
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
```

- [ ] **Step 2: 保存后回到 Unity，确认编译通过**

- [ ] **Step 3: Commit**

```bash
git add DungeonSlash/Assets/Scripts/Enemy/EnemyBase.cs
git commit -m "feat: overhaul EnemyBase with Hit state, Animator driving, attack frame sync"
```

---

### Task 2: 更新 MeleeEnemy（微调）

🎓 **学习目标**: MeleeEnemy 的 PerformAttack 逻辑本身不变，但需要确认它和新的攻击流程兼容。

**Files:**
- Modify: `DungeonSlash/Assets/Scripts/Enemy/MeleeEnemy.cs`

- [ ] **Step 1: 确认 MeleeEnemy.cs 无需改动**

当前的 `PerformAttack()` 方法已经是纯粹的伤害判定逻辑（OverlapSphere），不涉及动画或计时。攻击时机完全由 EnemyBase.UpdateAttack() 控制。

MeleeEnemy.cs 保持现有内容不变：

```csharp
using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeHitRadius = 1.5f;

    protected override void PerformAttack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeHitRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                DamageSystem.DealDamage(hit, meleeDamage, transform.position);
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeHitRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
```

- [ ] **Step 2: Commit（如果无改动则跳过）**

---

### Task 3: Editor 操作——创建 MeleeEnemy Animator Controller

🎓 **学习目标**: 手动搭建 Animator Controller，理解状态、过渡、参数之间的关系

**Files:**
- 操作: Unity Editor（创建 .controller 文件、配置状态和过渡）

> **注意：此任务需要用户在 Unity Editor 中手动操作。**

- [ ] **Step 1: 创建 Animator Controller**

1. 在 Project 窗口：右键 `Assets/Animations/` → Create → **Animator Controller**
2. 重命名为 `MeleeEnemyAnimator`

- [ ] **Step 2: 添加参数**

双击 `MeleeEnemyAnimator` 打开 Animator 窗口。在左侧 **Parameters** 面板中添加：
- 点 **+** → **Float** → 命名 `Speed`
- 点 **+** → **Trigger** → 命名 `Attack`
- 点 **+** → **Trigger** → 命名 `Hit`
- 点 **+** → **Trigger** → 命名 `Die`

🎓 为什么 Speed 是 Float 而其他是 Trigger？
> Speed 是持续变化的值（0=站立，>0=跑步），需要每帧更新。
> Attack/Hit/Die 是瞬发动作，触发一次就行，用 Trigger 自动重置。

- [ ] **Step 3: 创建状态**

在 Animator 窗口的网格区域：
1. 右键 → **Create State → Empty** → 在 Inspector 中重命名为 `Idle`
2. 右键 → **Create State → Empty** → 命名 `Run`
3. 右键 → **Create State → Empty** → 命名 `Attack`
4. 右键 → **Create State → Empty** → 命名 `Hit`
5. 右键 → **Create State → Empty** → 命名 `Die`

- [ ] **Step 4: 分配动画 Clip 到每个状态**

逐个点击每个状态，在 Inspector 的 **Motion** 字段中拖入对应的动画：
- **Idle** → `Assets/ThirdParty/Little Heroes Mega Pack/Animations/Base@Idle.FBX`（展开 FBX，拖入里面的 clip）
- **Run** → `Base@Run.FBX` 的 clip
- **Attack** → `Base@Melee Right Attack 01.FBX` 的 clip
- **Hit** → `Base@Take Damage.FBX` 的 clip
- **Die** → `Base@Die.FBX` 的 clip

🎓 **注意**: FBX 文件旁边有个 ▶ 展开箭头，点开后里面才是实际的 AnimationClip，要拖里面的 clip 而不是 FBX 本身。

- [ ] **Step 5: 设置默认状态**

右键 **Idle** 状态 → **Set as Layer Default State**（变成橙色）。

- [ ] **Step 6: 创建过渡——Idle ↔ Run**

1. 右键 **Idle** → **Make Transition** → 连到 **Run**
   - 点击过渡箭头 → Inspector 中：
   - **Has Exit Time**: ❌ 取消勾选
   - **Transition Duration**: `0.1`
   - **Conditions**: 添加 `Speed` → Greater → `0.1`

2. 右键 **Run** → **Make Transition** → 连到 **Idle**
   - **Has Exit Time**: ❌ 取消勾选
   - **Transition Duration**: `0.1`
   - **Conditions**: 添加 `Speed` → Less → `0.1`

🎓 **为什么 Has Exit Time 关闭？** 移动状态切换要求即时响应。如果开启 Exit Time，跑步停下来时动画还要播完当前循环才切回 Idle，手感会很迟钝。

- [ ] **Step 7: 创建过渡——Any State → Attack**

1. 右键 **Any State**（灰绿色方块）→ **Make Transition** → 连到 **Attack**
   - **Has Exit Time**: ❌ 取消勾选
   - **Transition Duration**: `0.05`
   - **Conditions**: `Attack`（Trigger，无需设值）

2. 右键 **Attack** → **Make Transition** → 连到 **Idle**
   - **Has Exit Time**: ✅ 勾选
   - **Exit Time**: `0.9`
   - **Transition Duration**: `0.1`
   - **Conditions**: 无（靠 Exit Time 自然回 Idle）

- [ ] **Step 8: 创建过渡——Any State → Hit**

1. 右键 **Any State** → **Make Transition** → 连到 **Hit**
   - **Has Exit Time**: ❌ 取消勾选
   - **Transition Duration**: `0.05`
   - **Conditions**: `Hit`

2. 右键 **Hit** → **Make Transition** → 连到 **Idle**
   - **Has Exit Time**: ✅ 勾选
   - **Exit Time**: `0.9`
   - **Transition Duration**: `0.1`
   - **Conditions**: 无

- [ ] **Step 9: 创建过渡——Any State → Die**

1. 右键 **Any State** → **Make Transition** → 连到 **Die**
   - **Has Exit Time**: ❌ 取消勾选
   - **Transition Duration**: `0.05`
   - **Conditions**: `Die`

2. **Die 状态不需要出口过渡**——死亡是终态。

- [ ] **Step 10: 更新 MeleeEnemy Prefab**

1. 双击 `Assets/Prefabs/MeleeEnemy.prefab` 进入 Prefab 编辑模式
2. 找到带 **Animator** 组件的子对象（角色模型）
3. 将 Animator 的 **Controller** 字段从旧的 Controller 替换为 `Assets/Animations/MeleeEnemyAnimator`
4. 保存 Prefab

- [ ] **Step 11: 配置 EnemyBase 的新参数**

选中 MeleeEnemy Prefab 的根对象，在 Inspector 中配置 EnemyBase 的新字段：
- **Attack Hit Delay**: `0.4`（攻击动画挥刀帧时间）
- **Attack Anim Duration**: `0.8`（攻击动画总时长）
- **Hit Stun Duration**: `0.4`（受击硬直时长）
- **Death Anim Duration**: `1.5`（死亡动画时长）
- 保存 Prefab

---

### Task 4: 运行测试

🎓 **学习目标**: 验证 AI 状态机和 Animator 的配合，调试攻击帧同步

**Files:**
- 无代码修改，纯测试

- [ ] **Step 1: 验证 Idle 和 Chase 动画**

运行游戏。观察 MeleeEnemy：
- 静止时应播放 Idle 动画
- 玩家走入检测范围后 → 敌人开始跑步追踪（Run 动画）
- 玩家跑出范围 → 敌人回到 Idle

- [ ] **Step 2: 验证攻击动画和帧同步**

让敌人靠近玩家进入攻击范围：
- 敌人应播放攻击动画
- 伤害应该在动画播放约 0.4s 后才判定（不是一开始就扣血）
- 攻击动画播完后恢复追击

如果时机不对：调整 MeleeEnemy Prefab 上的 **Attack Hit Delay** 和 **Attack Anim Duration**。

- [ ] **Step 3: 验证受击硬直**

攻击敌人：
- 敌人应播放受击动画（Take Damage）
- 硬直期间敌人不动、不攻击
- 连续攻击 → 敌人持续被压制（硬直计时器刷新）
- 停止攻击 → 0.4s 后敌人恢复追击

- [ ] **Step 4: 验证死亡动画**

打死敌人：
- 敌人播放死亡动画（不是立刻消失）
- 死亡后 NavMeshAgent 和 Collider 已禁用（不挡路）
- 等待 deathAnimDuration 秒后 GameObject 被销毁

- [ ] **Step 5: Commit 最终状态**

```bash
git add -A
git commit -m "feat: complete MeleeEnemy animation system with hit stun and frame sync"
```
