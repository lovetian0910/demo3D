# 动画分层系统设计 (Animation Layers)

**日期:** 2026-04-08
**状态:** 已批准
**关联项目:** Dungeon Slash — 玩家角色动画系统

---

## 目标

让玩家角色在攻击时可以自由移动——上半身播放攻击动画，下半身继续播放跑步/待机动画，两者同时进行、互不干扰。

---

## 背景

当前的动画系统是单层状态机：进入 `Attacking` 状态后，`PlayerState.CanMove` 返回 false，`PlayerController` 停止移动输入，角色站定攻击。这与目标的"边跑边砍"体验不符。

---

## 🎓 核心知识点

### Animator Layers（动画层）

Unity Animator 支持多个 Layer 同时运行，每个 Layer 有独立的状态机。最终姿势由所有 Layer 按权重混合而成。

| 属性 | 说明 |
|------|------|
| Weight | 0~1，该层对最终姿势的影响程度 |
| Blending: Override | 上层完全覆盖下层对应骨骼的姿势 |
| Blending: Additive | 上层姿势叠加到下层上（适合抖动、呼吸等） |
| Avatar Mask | 限定该层只影响哪些骨骼 |

### Avatar Mask（骨骼遮罩）

Avatar Mask 是一个资产文件，记录哪些骨骼"受该 Layer 控制"。
- 🟢 绿色 = 该骨骼由此 Layer 驱动
- 🔴 红色 = 该骨骼忽略此 Layer，由更低层的 Layer 决定

Humanoid 视图按身体区域点选，Transform 列表可精确到单根骨骼。

---

## 设计方案

### Animator Controller 结构

```
PlayerAnimator.controller
├── Layer 0: Base Layer  (Weight=1, 无 Mask)
│   └── Idle ←──Speed──→ Run          ← 驱动全身
│
└── Layer 1: Upper Body  (Weight=1, Override, Mask=UpperBodyMask)
    ├── Empty             ← 默认状态（空动画，让 Layer 0 完全穿透）
    ├── LightAttack       ← LightAttack Trigger 触发
    └── HeavyAttack       ← HeavyAttack Trigger 触发
        两者播完后自动回 Empty（Has Exit Time，无需额外 Trigger）
```

**混合效果：**
- 站立时：Layer 1 为 Empty，Layer 0 全身 Idle 穿透 → 正常待机
- 跑步时：Layer 1 为 Empty，Layer 0 全身 Run 穿透 → 正常跑步
- 跑步+攻击：Layer 0 下半身 Run + Layer 1 上半身 LightAttack → 边跑边砍
- 站立+攻击：Layer 0 全身 Idle（下半身穿透）+ Layer 1 上半身 LightAttack → 站定挥剑

---

### 新建资产：UpperBodyMask

**路径：** `Assets/Animations/UpperBodyMask.mask`

骨骼勾选规则（Humanoid 视图）：

```
     🟢 头 (Head)
     🟢 颈 (Neck)
  🟢 左肩  右肩 🟢
  🟢 左臂  右臂 🟢
  🟢 左手  右手 🟢
     🟢 脊椎 (Spine/Chest)
─────────────────────  ← 分界线
     🔴 臀部 (Hips/Root)
  🔴 左腿  右腿 🔴
  🔴 左膝  右膝 🔴
  🔴 左脚  右脚 🔴
```

> **注意：** Little Heroes Mega Pack 骨骼命名可能不完全符合 Humanoid 标准，需在 Transform 列表确认 Spine 及以上骨骼均被正确包含。

---

### PlayerState.cs 改动

**目标：** 攻击状态下允许移动和跳跃（解除锁定）。

| 属性 | 改动前 | 改动后 |
|------|--------|--------|
| `CanMove` | `State <= Jumping` | `State <= Attacking` |
| `CanJump` | `State == Normal` | `State <= Attacking` |
| `CanAttack` | `State == Normal` | `State <= Attacking` |

> `Hit` 和 `Dead` 状态仍然锁定所有行为，优先级不变。

---

### PlayerController.cs 改动

- 移除攻击状态下阻断旋转的逻辑（允许攻击中自由转向）
- 攻击中 `CanMove` 为 true，移动输入照常处理，无需额外改动

---

### PlayerAnimator.cs 改动

- **无需改动参数。** `LightAttack` / `HeavyAttack` Trigger 已存在，Layer 1 直接复用
- Layer 0 的 `Speed` 参数继续每帧更新，不受攻击状态影响

---

### PlayerCombat.cs 改动

- 移除攻击开始时停止移动的相关调用（如有）
- 攻击的 hitDelay / hitDuration 计时逻辑保持不变，伤害判定不受影响

---

## 不在本次范围内

| 功能 | 原因 |
|------|------|
| 受击时继续跑步 | 当前 Hit 状态优先级高，是合理的击退/硬直设计，不在本次调整 |
| 攻击时限制转向速度 | 当前先实现基础功能，转向手感调优可后续迭代 |
| 连击窗口/Combo 计数 | 独立功能，本次不涉及 |
| 敌人动画分层 | 敌人当前无移动+攻击同时需求，暂不处理 |

---

## 成功标准

1. 玩家按住移动键同时点击攻击键，角色**边跑边挥剑**，脚步动画不中断
2. 攻击动画播完后，上半身自动回到 Idle/Run 混合状态，**无跳帧或闪烁**
3. 攻击时的**伤害判定时机不变**，武器碰撞体在正确帧激活
4. 站定攻击时，下半身播放 Idle，视觉上与改动前一致

---

## 文件清单

| 文件 | 操作 |
|------|------|
| `Assets/Animations/UpperBodyMask.mask` | 新建 |
| `Assets/Animations/PlayerAnimator.controller` | 修改（新增 Layer 1） |
| `Assets/Scripts/Player/PlayerState.cs` | 修改（CanMove/CanJump/CanAttack） |
| `Assets/Scripts/Player/PlayerController.cs` | 修改（移除攻击旋转锁定） |
| `Assets/Scripts/Player/PlayerCombat.cs` | 检查并清理攻击时停止移动的调用 |
