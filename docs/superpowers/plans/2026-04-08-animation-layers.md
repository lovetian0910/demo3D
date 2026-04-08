# 动画分层系统 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通过 Animator Layers + Avatar Mask 实现上半身攻击与下半身跑步同时播放，让玩家可以边跑边攻击。

**Architecture:** 在 `PlayerAnimator.controller` 新增 Upper Body Layer，配合 `UpperBodyMask`（只覆盖 Spine 及以上骨骼）覆盖上半身攻击动画，Layer 0 继续驱动全身移动。同时放开 `PlayerState` 中 Attacking 状态对移动的锁定。

**Tech Stack:** Unity 6, Animator Layers, Avatar Mask (Humanoid), C#

---

## 文件清单

| 文件 | 操作 |
|------|------|
| `DungeonSlash/Assets/Animations/UpperBodyMask.mask` | 新建（Editor） |
| `DungeonSlash/Assets/Animations/PlayerAnimator.controller` | 修改（Editor，新增 Layer 1） |
| `DungeonSlash/Assets/Scripts/Player/PlayerState.cs` | 修改（CanMove / CanJump / CanAttack） |

> `PlayerController.cs`、`PlayerCombat.cs`、`PlayerAnimator.cs` 无需修改——`CanMove` 改完后会自动生效，攻击 Trigger 天然被两个 Layer 共享。

---

## Task 1：修改 PlayerState.cs — 解除攻击状态的移动锁定

**文件：**
- Modify: `DungeonSlash/Assets/Scripts/Player/PlayerState.cs:46-52`

**背景：**
当前三个属性的限制：
```csharp
public bool CanAttack => currentState == State.Normal;          // 只有 Normal 能攻击
public bool CanJump   => currentState == State.Normal;          // 只有 Normal 能跳
public bool CanMove   => currentState <= State.Jumping;         // Normal + Jumping 可以移动
```
改完后：
```csharp
public bool CanAttack => currentState <= State.Attacking;       // Normal + Jumping + Attacking
public bool CanJump   => currentState <= State.Attacking;       // 同上
public bool CanMove   => currentState <= State.Attacking;       // 同上
```
`Hit`（3）和 `Dead`（4）依然大于 `Attacking`（2），仍然锁定所有行为。

- [ ] **Step 1: 打开 PlayerState.cs，修改三个属性**

将 `DungeonSlash/Assets/Scripts/Player/PlayerState.cs` 第 46-52 行替换为：

```csharp
/// <summary>是否可以攻击：Normal / Jumping / Attacking 状态均可</summary>
public bool CanAttack => currentState <= State.Attacking;

/// <summary>是否可以跳跃：Normal / Jumping / Attacking 状态均可</summary>
public bool CanJump => currentState <= State.Attacking;

/// <summary>是否可以移动：Normal / Jumping / Attacking 状态均可</summary>
public bool CanMove => currentState <= State.Attacking;
```

- [ ] **Step 2: 同步更新注释枚举，消除误导性描述**

将第 30 行的注释从：
```csharp
Attacking = 2,  // 攻击中，不能跳跃、不能移动转向
```
改为：
```csharp
Attacking = 2,  // 攻击中，可以移动（上下半身分层）
```

- [ ] **Step 3: 提交代码**

```bash
cd /Users/kuangjianwei/AI_Discover/learn-3d
git add DungeonSlash/Assets/Scripts/Player/PlayerState.cs
git commit -m "feat: allow movement during attack for animation layer system"
```

---

## Task 2：在 Unity Editor 创建 UpperBodyMask

**文件：**
- Create: `DungeonSlash/Assets/Animations/UpperBodyMask.mask`（Editor 操作，不是手写文件）

**背景：**
Avatar Mask 是 Unity 的资产文件，只能在 Editor 中通过 GUI 创建。它告诉 Layer 1："你只负责这些骨骼，其余骨骼交给 Layer 0"。

- [ ] **Step 1: 在 Project 窗口创建 Avatar Mask**

在 Unity Editor 中：
1. Project 窗口 → 展开到 `Assets/Animations/` 文件夹
2. 右键 → **Create → Avatar Mask**
3. 命名为 `UpperBodyMask`

创建后双击打开，Inspector 显示人形骨骼图。

- [ ] **Step 2: 配置 Humanoid 骨骼勾选**

在 Inspector 的人形图上点击各部位切换颜色（🟢绿=勾选，🔴红=不勾选）：

**需要 🟢 绿色（勾选）的部位：**
- Head（头部）
- Neck（颈部）
- Left Arm / Right Arm（双臂，含大臂、小臂）
- Left Hand / Right Hand（双手）
- Chest / Spine（脊椎/胸口）—— **这是上下半身分界线**

**需要 🔴 红色（不勾选）的部位：**
- Hips / Root（臀部、根骨骼）
- Left Leg / Right Leg（双腿）
- Left Foot / Right Foot（双脚）

> 🎓 小提示：点击一个区域通常会批量切换整个肢体。如果某个部位无法点击（灰色），说明角色的 Avatar 配置不完全符合 Humanoid 标准，此时转用下方 Transform 列表。

- [ ] **Step 3: 检查 Transform 列表（备选验证）**

在 Inspector 下方展开 **"Transform"** 折叠区：
- 如果 Humanoid 视图不够准确，这里可以精确勾选骨骼名
- 对于 Little Heroes Mega Pack，确认 `Spine`、`Chest`（或 `UpperChest`）及其子骨骼（手臂、头部）均已勾选

- [ ] **Step 4: 保存资产**

按 `Cmd+S` 保存，`UpperBodyMask.mask` 出现在 `Assets/Animations/` 目录下。

- [ ] **Step 5: 提交**

```bash
cd /Users/kuangjianwei/AI_Discover/learn-3d
git add "DungeonSlash/Assets/Animations/UpperBodyMask.mask"
git add "DungeonSlash/Assets/Animations/UpperBodyMask.mask.meta"
git commit -m "feat: add UpperBodyMask avatar mask for animation layer"
```

---

## Task 3：在 PlayerAnimator.controller 添加 Upper Body Layer

**文件：**
- Modify: `DungeonSlash/Assets/Animations/PlayerAnimator.controller`（Editor 操作）

**背景：**
Layer 1 需要：
1. 设置 Avatar Mask = UpperBodyMask（只影响上半身）
2. 有一个 `Empty` 默认状态（空动画，让 Layer 0 完全穿透）
3. 有 `LightAttack` 和 `HeavyAttack` 两个状态，由同名 Trigger 触发
4. 攻击动画播完后有 Exit Time 自动回 `Empty`

- [ ] **Step 1: 打开 Animator Controller**

双击 `Assets/Animations/PlayerAnimator.controller` 打开 Animator 窗口。

- [ ] **Step 2: 新增 Upper Body Layer**

在 Animator 窗口左侧 **Layers** 面板：
1. 点击右上角的 **"+"** 按钮
2. 将新 Layer 重命名为 `Upper Body`
3. 点击该 Layer 旁边的齿轮图标 ⚙️，配置：
   - **Weight:** `1`
   - **Blending:** `Override`
   - **Mask:** 点击右侧圆形选择器，选择刚创建的 `UpperBodyMask`

- [ ] **Step 3: 创建 Empty 默认状态**

确认当前在 `Upper Body` Layer（点击 Layers 面板中的 "Upper Body"）：
1. 在 Animator 图中空白处右键 → **Create State → Empty**
2. 将新状态重命名为 `Empty`
3. 右键该状态 → **Set as Layer Default State**（它会变成橙色）

> 🎓 Empty 状态的作用：Layer 1 默认处于 Empty（无动画输出），Layer 0 的全身动画完全穿透。只有当攻击 Trigger 触发时，Layer 1 才"接管"上半身。

- [ ] **Step 4: 添加 LightAttack 状态**

1. 在图中空白处右键 → **Create State → Empty**，命名为 `LightAttack`
2. 选中 `LightAttack` 状态，在 Inspector 中：
   - **Motion:** 点击右侧圆形，选择与 Layer 0 的 LightAttack 相同的动画 clip（例如 `Base@Melee Right Attack 01`）
3. 右键 `Empty` 状态 → **Make Transition** → 点击 `LightAttack`
4. 选中这条 Transition，在 Inspector 中：
   - 取消勾选 **Has Exit Time**
   - **Conditions:** 点击 "+" → 选择 `LightAttack`（Trigger）

- [ ] **Step 5: 添加 LightAttack → Empty 的返回 Transition**

1. 右键 `LightAttack` 状态 → **Make Transition** → 点击 `Empty`
2. 选中这条 Transition，在 Inspector 中：
   - 勾选 **Has Exit Time**，Exit Time 设为 `0.9`（动画播到 90% 时开始过渡）
   - **Transition Duration:** `0.1`（短暂混合回 Empty）
   - **Conditions:** 留空（依靠 Exit Time 自动返回）

- [ ] **Step 6: 添加 HeavyAttack 状态（步骤同 Step 4-5）**

1. 右键空白处 → Create State → Empty，命名为 `HeavyAttack`
2. Motion 设置为重攻击动画 clip（例如 `Base@Melee Right Attack 02` 或对应的重攻击动画）
3. 从 `Empty` → `HeavyAttack`：
   - Has Exit Time: ❌
   - Condition: `HeavyAttack`（Trigger）
4. 从 `HeavyAttack` → `Empty`：
   - Has Exit Time: ✅，Exit Time: `0.9`
   - Transition Duration: `0.1`
   - Conditions: 留空

- [ ] **Step 7: 确认 Layer 0 的攻击状态无需改动**

切回 `Base Layer`，确认 Layer 0 中 `LightAttack`、`HeavyAttack` 状态仍然存在、逻辑不变。Layer 1 复用相同的 Trigger 参数，两个 Layer 共享同一套 Trigger，互不冲突。

- [ ] **Step 8: 提交**

```bash
cd /Users/kuangjianwei/AI_Discover/learn-3d
git add "DungeonSlash/Assets/Animations/PlayerAnimator.controller"
git commit -m "feat: add upper body animation layer to PlayerAnimator controller"
```

---

## Task 4：Play Mode 验证

这一步在 Unity Editor 中进入 Play Mode，逐项核对成功标准。

- [ ] **Check 1：边跑边攻击**

操作：按住 WASD 移动，同时点击鼠标左键攻击
预期：角色持续跑步，同时上半身播放挥剑动画，脚步动画不中断、不跳帧

- [ ] **Check 2：站定攻击**

操作：松开移动键，点击鼠标左键攻击
预期：下半身播放 Idle，上半身播放攻击动画，与改动前视觉一致

- [ ] **Check 3：攻击结束后自动恢复**

操作：攻击一次，不按移动键等待
预期：攻击动画播完后，上半身平滑过渡回 Idle 状态，无闪烁或姿势突变

- [ ] **Check 4：伤害判定时机**

操作：边跑边攻击，打中敌人
预期：敌人收到伤害，伤害时机与动画挥剑帧一致（同改动前），无伤害失效

- [ ] **Check 5：受击状态仍然锁定**

操作：让玩家被敌人击中
预期：受击硬直期间角色停止移动（Hit 状态 > Attacking，依然锁定），状态机行为正确

- [ ] **Step：如有问题，常见排查**

| 症状 | 原因 | 解决 |
|------|------|------|
| 上半身不动，只有下半身 | Layer 1 Weight 不是 1 | 检查 Upper Body Layer 的 Weight 设为 1 |
| 攻击时全身都停了 | CanMove 改动未生效（编译错误？） | 检查 PlayerState.cs 编译无报错 |
| 上半身姿势奇怪/扭曲 | Avatar Mask 包含了 Root 骨骼 | 确认 Hips/Root 为红色（不勾选） |
| 动画一直循环不结束 | 返回 Empty 的 Transition 没有 Exit Time | 检查 LightAttack→Empty 勾选了 Has Exit Time |
| 攻击动画在 Layer 1 不播放 | Motion 没有设置 | 选中 LightAttack 状态，Inspector 里设置 Motion clip |
