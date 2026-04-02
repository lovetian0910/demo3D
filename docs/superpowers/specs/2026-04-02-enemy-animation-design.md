# MeleeEnemy 动画与状态完善设计

**日期**: 2026-04-02
**状态**: 待实施
**范围**: 完善 MeleeEnemy 的动画驱动和状态管理，包括受击硬直和攻击帧同步伤害

---

## 1. 目标

- 敌人在每个状态下播放对应动画（Idle/Run/Attack/Hit/Die）
- 攻击伤害和动画帧同步（挥刀帧才判定伤害）
- 受击时有硬直（短暂无法行动），可以被玩家连击
- 死亡播放完整死亡动画后再销毁
- 学习：Animator 状态机驱动、Animation Event 帧同步、敌人 AI 状态管理

## 2. 状态机改造

### 现有状态
```
enum EnemyState { Idle, Chase, Attack, Dead }
```

### 改造后
```
enum EnemyState { Idle, Chase, Attack, Hit, Dead }
```

新增 `Hit` 状态，表示受击硬直中。

### 状态互斥规则（优先级从高到低）
```
Dead    → 永久锁定，什么都不做
Hit     → 打断 Attack/Chase/Idle，硬直 hitStunDuration 秒后恢复
Attack  → 播攻击动画，期间不移动不转向
Chase   → NavMesh 追踪玩家
Idle    → 待机，检测玩家进入范围
```

受击（Hit）可以打断攻击（Attack），所以玩家快速连击可以压制敌人。

## 3. Animator 参数设计

| 参数名 | 类型 | 驱动方式 | 用途 |
|--------|------|---------|------|
| Speed | Float | 每帧设置（NavMeshAgent.velocity.magnitude） | 控制 Idle ↔ Run 过渡 |
| Attack | Trigger | 进入 Attack 状态时触发一次 | 播放攻击动画 |
| Hit | Trigger | TakeDamage 时触发一次 | 播放受击动画 |
| Die | Trigger | 死亡时触发一次 | 播放死亡动画 |

### 动画对应
| 状态 | 动画 FBX | 说明 |
|------|---------|------|
| Idle | Base@Idle.FBX | 站立待机 |
| Chase(Run) | Base@Run.FBX | 追踪跑步 |
| Attack | Base@Melee Right Attack 01.FBX | 右手近战攻击 |
| Hit | Base@Take Damage.FBX | 受击反应 |
| Die | Base@Die.FBX | 死亡倒地 |

## 4. 攻击帧同步

当前问题：进入 Attack 状态后 PerformAttack() 立即执行 OverlapSphere 判定伤害，动画还没挥刀就已经扣血了。

改造方案：和 Player 的 hitDelay 类似，使用**代码计时器**延迟伤害判定。

```
进入 Attack 状态
  → 播放攻击动画（SetTrigger("Attack")）
  → 等待 attackHitDelay 秒（蓄力/抬手阶段）
  → 执行 PerformAttack()（伤害判定）
  → 等待动画播完 + 冷却
  → 回到 Chase/Idle
```

attackHitDelay 配置在 EnemyBase 上（SerializeField），默认 0.4s，可在 Inspector 中调整匹配动画节奏。

## 5. 受击硬直

```
被打中（TakeDamage）
  → 状态切换到 Hit（打断当前状态，包括 Attack）
  → 播放受击动画（SetTrigger("Hit")）
  → NavMeshAgent 停止移动
  → 硬直 hitStunDuration 秒（默认 0.4s）
  → 恢复到 Chase（如果玩家还在范围内）或 Idle
```

硬直期间可以被再次触发 Hit（刷新计时器），实现连击压制。

## 6. 死亡动画

当前问题：Die() 直接 Destroy，没有播放死亡动画。

改造：
```
血量归零
  → 状态切换到 Dead
  → 播放死亡动画（SetTrigger("Die")）
  → 禁用 NavMeshAgent、Collider
  → 等待死亡动画时长（deathAnimDuration，默认 1.5s）
  → Destroy
```

## 7. 修改文件清单

| 操作 | 文件 | 改动 |
|------|------|------|
| 修改 | `EnemyBase.cs` | 新增 Hit 状态；Animator 参数驱动；受击硬直；死亡动画等待；攻击帧同步计时 |
| 修改 | `MeleeEnemy.cs` | PerformAttack 配合新的帧同步流程 |
| Editor | 创建 MeleeEnemy Animator Controller | 包含 Idle/Run/Attack/Hit/Die 状态和过渡 |

## 8. Editor 操作

用户需要在 Unity Editor 中：
1. 创建新的 Animator Controller（MeleeEnemyAnimator.controller）
2. 设置 5 个状态（Idle/Run/Attack/Hit/Die）和过渡条件
3. 配置参数（Speed Float、Attack Trigger、Hit Trigger、Die Trigger）
4. 将动画 clip 拖入对应状态
5. 更新 MeleeEnemy prefab 使用新 Controller

## 9. 不做的事

- ❌ 不做 RangedEnemy 动画（后续单独做）
- ❌ 不做敌人武器挂载
- ❌ 不做新敌人类型
- ❌ 不做 Animation Event（用代码计时器代替，更灵活且不依赖动画文件）

## 10. 学习目标

完成后能在面试中讲清楚：
- 敌人 AI 状态机如何和 Animator 状态机配合
- 攻击帧同步的两种方案（Animation Event vs 代码计时器）及各自优缺点
- 受击硬直的实现原理（状态打断 + 计时器恢复）
- NavMeshAgent 在不同状态下的启停控制
