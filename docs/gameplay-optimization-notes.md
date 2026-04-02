# 游戏体验优化实践记录

> 项目：Dungeon Slash（Unity 6 URP 俯视角 3D Roguelike）
> 更新日期：2026-04-02

本文档记录武器系统、战斗手感、跳跃操控、敌人 AI 等游戏体验层面的优化。渲染/性能优化见 `rendering-optimization-notes.md`。

---

## 1. 武器挂载系统

### 1.1 ScriptableObject 数据驱动

**文件**：`Assets/Scripts/Combat/WeaponData.cs`

- 武器属性（伤害、冷却、碰撞体、挂载偏移）全部存储在 ScriptableObject 资产文件中
- 修改数值不需要改代码，直接在 Inspector 中编辑 `.asset` 文件
- 多个敌人/场景可引用同一份数据，内存只有一份

🎓 **面试要点**：ScriptableObject 和 MonoBehaviour 的区别——SO 不挂在 GameObject 上，是项目级资产；Play 模式下修改 SO 不会回滚（MonoBehaviour 会）。

### 1.2 运行时骨骼挂载

**文件**：`Assets/Scripts/Player/WeaponManager.cs`

- 武器模型通过 `Instantiate()` + `SetParent(handBone, worldPositionStays: false)` 挂载到角色手骨
- 左右手各挂一把武器，轻攻击用左手碰撞体，重攻击用右手碰撞体
- `FindBoneRecursive()` 递归遍历 Transform 树查找骨骼（`Transform.Find()` 只搜直接子级）

🎓 **面试要点**：`worldPositionStays` 参数——`false` 保持 localPosition 不变（武器"粘"在骨骼上），`true` 保持世界坐标不变（常用于拖拽物品到容器）。

### 1.3 左右手镜像偏移

- 人体左右手骨骼本地坐标轴是镜像的（X 轴方向相反）
- 同一个旋转偏移用在左手会导致武器方向相反
- 解决方案：WeaponData 中分别配置 `localRotationOffset`（右手）和 `leftRotationOffset`（左手）

### 1.4 Debug Live Preview

- WeaponManager 上有 `debugLivePreview` 开关
- 开启后 Play 模式下每帧从 WeaponData 读取偏移值并实时应用
- 调参时不用停止游戏，直接改 SO 资产就能看到效果
- 碰撞体的 center/size 也实时更新（但 **不能** 强制 `enabled = true`，否则走路就触发伤害）

---

## 2. 攻击判定优化

### 2.1 Active Frames（攻击活跃帧）

**文件**：`Assets/Scripts/Player/PlayerCombat.cs`、`Assets/Scripts/Combat/WeaponData.cs`

攻击动画分为三个阶段：

```
[0s]              Wind-up（蓄力）   碰撞体 OFF
[hitDelay]        Strike（打击）    碰撞体 ON   ← 只有这里能造成伤害
[hitDelay+dur]    Recovery（收招）  碰撞体 OFF
```

- 轻攻击和重攻击各有独立的 `hitDelay` 和 `hitDuration`，配置在 WeaponData 中
- 重攻击蓄力更久（hitDelay 更大），打击窗口也稍长

🎓 **面试要点**：如果 hitDelay 太短，抬手阶段就判定命中，体验很差。帧同步有两种方案：Animation Event（精确但耦合动画文件）vs 代码计时器（灵活但需手动调参）。

### 2.2 攻击辅助瞄准（Auto-Aim）

**文件**：`Assets/Scripts/Player/PlayerCombat.cs`

- 攻击瞬间用 `Physics.OverlapSphere(position, autoAimRadius)` 搜索附近敌人
- 找到最近的活敌人后，`Quaternion.LookRotation()` 瞬间面向它
- 默认搜索半径 5m，可在 Inspector 中调整，设为 0 关闭

🎓 **面试要点**：俯视角游戏玩家操控的是移动方向不是精确朝向，没有辅助瞄准会频繁砍空。暗黑破坏神、原神、塞尔达都有此机制。

### 2.3 碰撞体设计原则

- 碰撞体应比武器模型大 30~50%，给玩家"打到了"的宽容感
- 太精确 → 玩家觉得"明明砍到了却没命中"
- 业内称为"coyote hitbox"——和 Coyote Time 同理，都是对玩家有利的宽容设计

---

## 3. 跳跃操控优化

### 3.1 Coyote Time（土狼时间）

**文件**：`Assets/Scripts/Player/PlayerController.cs`

```
地面 ──────┐
           │ 离地
           ├── 0.15s 内按空格 → 仍可跳 ✅
           └── 超过 0.15s    → 不能跳 ❌
```

- `CharacterController.isGrounded` 在不平地面上会瞬间闪烁为 false
- 离地后给 0.15s 宽容窗口，避免跑步时"吞输入"

### 3.2 Input Buffer（输入缓冲）

```
空中 ── 按空格 ── 0.15s 内落地 → 自动跳 ✅
                  超过 0.15s   → 输入失效 ❌
```

- 玩家在落地前一瞬间按空格，系统"记住"输入，落地后自动执行

🎓 **面试要点**：Coyote Time + Input Buffer 是几乎所有动作/平台游戏的标配（塞尔达、空洞骑士、Celeste）。

### 3.3 跳跃动画 Trigger 替代 Bool

- 之前用 `SetBool("IsJumping", true)` 每帧设置，Any State 过渡条件持续满足 → 动画反复重启
- 改为 `SetTrigger("Jump")` 只在起跳瞬间触发一次，动画播完自然过渡回 Idle

🎓 **面试要点**：Bool 适合持续状态（跑步），Trigger 适合瞬发动作（跳跃、攻击）。用错类型是 Animator 最常见的坑。

---

## 4. 玩家状态互斥

### 4.1 PlayerState 集中管理

**文件**：`Assets/Scripts/Player/PlayerState.cs`

```
优先级（高 → 低）：
Dead     → 永久锁定
Hit      → 打断攻击，0.4s 硬直
Attacking → 不能跳、不能移动
Jumping  → 不能攻击
Normal   → 可以做任何事
```

- 所有脚本行动前查询 `PlayerState.CanAttack` / `CanJump` / `CanMove`
- 高优先级状态可打断低优先级（受击打断攻击），反之不行
- 受击时 `PlayerCombat.CancelAttack()` 关闭碰撞体、重置状态

🎓 **面试要点**：集中式状态机 vs 分散检查——集中管理后不需要每个脚本互相引用和检查，逻辑清晰，不容易出 bug。

---

## 5. 敌人 AI 动画与状态

### 5.1 状态机 + Animator 双驱动

**文件**：`Assets/Scripts/Enemy/EnemyBase.cs`

两个"状态机"同时工作：
- **AI 状态机**（代码 EnemyState enum）：决定"该做什么"
- **Animator 状态机**（Unity Animator Controller）：决定"播什么动画"

通过 Animator 参数同步：
| AI 状态 | Animator 参数 | 动画 |
|---------|-------------|------|
| Idle | Speed = 0 | Base@Idle |
| Chase | Speed > 0 | Base@Run |
| Attack | SetTrigger("Attack") | Base@Melee Right Attack 01 |
| Hit | SetTrigger("Hit") | Base@Take Damage |
| Dead | SetTrigger("Die") | Base@Die |

### 5.2 敌人攻击帧同步

```
进入 Attack 状态
  → 播放攻击动画
  → 等待 attackHitDelay（蓄力阶段）
  → PerformAttack()（伤害判定）
  → 等待 attackAnimDuration 剩余时间
  → 恢复 Chase/Idle
```

### 5.3 敌人受击硬直

- 被打时切换到 Hit 状态，打断攻击
- NavMeshAgent `isStopped = true`，停止移动
- 硬直期间再次被打 → 刷新计时器（连击压制）
- 计时器归零 → 恢复 Chase

### 5.4 NavMeshAgent 不再挤玩家

- `agent.stoppingDistance = attackRange * 0.8f` — 到攻击距离前减速停下
- 攻击/受击/Idle 时 `agent.isStopped = true` — 彻底停止移动
- 状态恢复时 `agent.isStopped = false` — 重新启用

🎓 **面试要点**：NavMeshAgent 默认 stoppingDistance = 0，会一直冲到目标点。设置 stoppingDistance 是让 AI 保持攻击距离的标准做法。

### 5.5 Animator 过渡的 Fixed Duration

- Transition Duration 默认是**归一化时间**（源动画的百分比），不是秒
- 从不同长度的动画过渡到同一状态 → 实际过渡时间不同 → 动画起始帧偏移
- 勾选 **Fixed Duration** 后改为固定秒数，保证过渡一致

### 5.6 死亡动画等待

- 死亡后禁用 NavMeshAgent + Collider（不挡路）
- `Destroy(gameObject, deathAnimDuration)` 等动画播完再销毁
- deathAnimDuration 可在 Inspector 中配置

---

## 6. 通用经验总结

| 问题 | 根因 | 解决方案 | 适用范围 |
|------|------|---------|---------|
| 抬手就触发伤害 | hitDelay 太短 | Active Frames 帧同步 | 所有攻击动作 |
| 砍空率高 | 俯视角朝向不精确 | Auto-Aim 辅助瞄准 | 动作游戏通用 |
| 跑步跳不起来 | isGrounded 闪烁 | Coyote Time + Input Buffer | 平台/动作游戏 |
| 动画反复重播 | Bool + Any State 每帧触发 | 改用 Trigger | 瞬发动作 |
| 被打中还能攻击 | 各脚本独立无互斥 | 集中状态管理 PlayerState | 复杂角色系统 |
| 敌人挤玩家 | stoppingDistance = 0 | 设为 attackRange 的 80% | NavMesh AI |
| 不同源动画过渡时间不一致 | Transition Duration 是百分比 | 勾选 Fixed Duration | Animator 过渡 |
| 调试碰撞体强制开启导致误伤 | Live Preview 设 enabled=true | 只更新 center/size | 调试工具设计 |
