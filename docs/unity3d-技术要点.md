# Dungeon Slash — Unity 3D 游戏开发技术要点

> 基于 Unity 6 + URP 的俯视角 3D 动作 Roguelike Demo 学习笔记
> 目标：掌握 3D 游戏开发基础，准备游戏开发岗位面试

---

## 目录

1. [项目总览](#1-项目总览)
2. [3D 角色移动](#2-3d-角色移动)
3. [Cinemachine v4 摄像机](#3-cinemachine-v4-摄像机)
4. [Animator 状态机](#4-animator-状态机)
5. [NavMesh 寻路 & 敌人 AI](#5-navmesh-寻路--敌人-ai)
6. [3D 物理检测](#6-3d-物理检测)
7. [状态机模式](#7-状态机模式)
8. [伤害系统](#8-伤害系统)
9. [URP 渲染管线](#9-urp-渲染管线)
10. [游戏手感](#10-游戏手感)
11. [ScriptableObject 数据驱动](#11-scriptableobject-数据驱动)
12. [面试常见问题 & 标准回答](#12-面试常见问题--标准回答)

---

## 1. 项目总览

### 项目简介

**Dungeon Slash** 是一个使用 Unity 6 + URP 开发的俯视角 3D 动作 Roguelike Demo。
核心游戏循环：

```
进入房间 → 敌人生成 → 战斗（攻击 / 闪避）→ 清空房间 → 进入下一间 → 胜利
```

### 技术栈

| 模块 | 技术选择 | 原因 |
|------|---------|------|
| 引擎 | Unity 6 (URP) | 行业主流，面试考查频率高 |
| 角色移动 | CharacterController | 比 Rigidbody 更可控，动作游戏行业标准 |
| 摄像机 | Cinemachine v4 | Unity 官方方案，自带 Follow / Damping / Offset |
| 敌人寻路 | NavMeshAgent | 内置方案，烘焙即用 |
| 动画 | Animator 状态机 | Unity 标准动画系统 |
| 敌人 AI | 手写枚举状态机 | 轻量，2 种敌人无需行为树 |
| 伤害系统 | IDamageable 接口 | 解耦玩家和敌人的血量逻辑 |

### 整体架构

```
Assets/Scripts/
├── Player/          # 玩家移动、战斗、状态、动画
├── Enemy/           # 敌人 AI（近战 / 远程）
├── Combat/          # 伤害系统、投射物、武器 Hitbox
├── Core/            # GameManager、RoomManager、房间触发器
├── UI/              # 血条、小地图、调试信息
└── Camera/          # 摄像机遮挡处理
```

### 脚本一览（26 个）

| 子系统 | 脚本 | 职责 |
|-------|------|------|
| 玩家 | PlayerController | 移动、重力、跳跃（含 Coyote Time） |
| 玩家 | PlayerCombat | 攻击输入、Auto-Aim、武器碰撞激活 |
| 玩家 | PlayerHealth | 受伤、死亡、闪烁反馈、击退 |
| 玩家 | PlayerAnimator | 动画状态机驱动 |
| 玩家 | PlayerState | 集中状态管理（互斥锁） |
| 玩家 | WeaponManager | 武器装备与切换 |
| 玩家 | WeaponData | ScriptableObject 武器数据 |
| 敌人 | EnemyBase | 抽象基类：NavMesh + 状态机 |
| 敌人 | MeleeEnemy | 近战攻击：OverlapSphere |
| 敌人 | RangedEnemy | 远程攻击：保持距离 + 发射投射物 |
| 战斗 | IDamageable | 接口：TakeDamage() + IsDead |
| 战斗 | DamageSystem | 静态工具：施加伤害 + 击退 |
| 战斗 | Projectile | 投射物：速度移动 + 碰撞消失 |
| 战斗 | WeaponHitbox | 碰撞体命中检测（按攻击时序激活） |
| 核心 | GameManager | 单例：游戏状态、胜负 UI |
| 核心 | RoomManager | 房间激活、敌人生成、关卡推进 |
| 核心 | RoomTrigger | 碰撞体触发房间切换 |
| UI | HealthBarUI | 玩家血条（fillAmount） |
| UI | EnemyHealthBar | 敌人悬浮血条（世界空间 Canvas） |
| UI | MinimapCamera | 小地图独立正交摄像机 |
| UI | DebugStatsUI | OnGUI 帧率计数器 |
| 摄像机 | CameraOcclusionHandler | 遮挡物透明化处理 |

---

## 2. 3D 角色移动

### CharacterController vs Rigidbody

🎓 这是 3D 游戏开发最常被问到的问题之一。

| 对比维度 | CharacterController | Rigidbody |
|---------|-------------------|-----------|
| 控制方式 | 代码直接控制位移 | 物理引擎驱动 |
| 重力/碰撞 | 需要手动写重力 | 自动处理 |
| 穿墙风险 | 内置防穿墙（stepOffset, skinWidth） | 高速时可能穿墙 |
| 适用场景 | 动作游戏角色 | 物理感强的对象（车辆、弹跳球） |
| 行业标准 | Unity/Unreal 动作游戏首选 | 平台跳跃、物理玩法 |

**结论**：动作游戏主角用 CharacterController，因为需要精确控制每帧位移，不希望物理引擎"插手"。

### 手动实现重力

CharacterController 没有内置重力，需要手动模拟：

```csharp
private Vector3 velocity;
private float gravity = -9.81f;

void Update() {
    // 落地时重置纵向速度，防止累积
    if (controller.isGrounded && velocity.y < 0) {
        velocity.y = -2f;  // 不用 0，确保 isGrounded 保持 true
    }

    // 每帧累加重力（模拟加速度）
    velocity.y += gravity * Time.deltaTime;

    // 应用纵向速度
    controller.Move(velocity * Time.deltaTime);
}
```

🎓 **为什么 `velocity.y = -2f` 而不是 `0`？**
`isGrounded` 通过向下发射射线检测，如果 y 速度为 0，角色会离地一帧导致 `isGrounded` 跳变。-2f 确保角色贴地。

### 摄像机相对移动

俯视角游戏的移动方向需要相对于摄像机朝向，而不是世界坐标轴：

```csharp
void Move() {
    float h = Input.GetAxisRaw("Horizontal");
    float v = Input.GetAxisRaw("Vertical");

    // 获取摄像机水平朝向（忽略 Y 轴仰俯）
    Vector3 camForward = mainCamera.transform.forward;
    Vector3 camRight   = mainCamera.transform.right;
    camForward.y = 0;
    camRight.y   = 0;
    camForward.Normalize();
    camRight.Normalize();

    // 输入方向 = 摄像机方向的加权合成
    Vector3 moveDir = (camForward * v + camRight * h).normalized;

    // 转向面朝移动方向（平滑旋转）
    if (moveDir != Vector3.zero) {
        transform.rotation = Quaternion.LookRotation(moveDir);
    }

    controller.Move(moveDir * speed * Time.deltaTime);
}
```

🎓 **与 2D 的对比**：Cocos/Godot 2D 直接用 `(x, y)` 轴输入即可。3D 中需要把"屏幕方向"映射到"世界方向"，因为摄像机可能旋转，玩家按"向上"应该往摄像机面对的方向走，不是世界 +Z。

### Quaternion.LookRotation

```csharp
// 让角色旋转朝向 moveDir 方向
transform.rotation = Quaternion.LookRotation(moveDir);

// 如果想平滑旋转（不要瞬间转身）
transform.rotation = Quaternion.Slerp(
    transform.rotation,
    Quaternion.LookRotation(moveDir),
    rotateSpeed * Time.deltaTime
);
```

🎓 **Quaternion 是什么？**
四元数，用来表示 3D 旋转，避免万向锁（Gimbal Lock）问题。面试不需要理解数学，只需要知道：
- `Quaternion.LookRotation(dir)` — 朝向某方向
- `Quaternion.Slerp(a, b, t)` — 球形插值，平滑旋转
- `Quaternion.Euler(x, y, z)` — 用欧拉角创建四元数

---

## 3. Cinemachine v4 摄像机

### 什么是 Cinemachine？

Cinemachine 是 Unity 官方的摄像机系统，核心思想是：
- **Main Camera**：最终输出画面的摄像机，始终只有一个
- **Cinemachine Camera**（Virtual Camera）：定义"我想从哪个角度看"，可以有多个
- Cinemachine 根据优先级、混合权重，把 Virtual Camera 的结果输送给 Main Camera

🎓 **与 2D 的对比**：Godot 的 Camera2D 直接挂在角色上跟随。Unity Cinemachine 则是"相机大脑 + 相机意图"分离，可以轻松切换多个视角（战斗视角、过场视角），无需移动 Main Camera。

### Unity 6 Cinemachine v4 变化

| 旧版（v2/v3） | 新版（v4，Unity 6） |
|-------------|------------------|
| Virtual Camera | Cinemachine Camera |
| Follow Distance | Follow Offset |
| 菜单：Component → Cinemachine | 菜单：GameObject → Cinemachine → Targeted Cameras → Follow Camera |

### 创建俯视角 Follow Camera

```
GameObject → Cinemachine → Targeted Cameras → Follow Camera
```

关键参数：
- **Follow**：拖入玩家 Transform，摄像机跟随目标
- **Look At**：拖入玩家 Transform，摄像机始终朝向目标
- **Follow Offset**：相对目标的偏移量，如 `(0, 10, -6)` 表示俯视角
- **Damping**：跟随阻尼，值越大摄像机跟上越慢（镜头感更平滑）

### 代码控制 Cinemachine（v4 API）

```csharp
using Unity.Cinemachine;

// 获取 Cinemachine Camera 组件
CinemachineCamera vcam = GetComponent<CinemachineCamera>();

// 切换跟随目标
vcam.Follow = newTarget;

// 修改 Follow Offset（v4 通过 CinemachineFollow 组件）
var follow = vcam.GetComponent<CinemachineFollow>();
follow.FollowOffset = new Vector3(0, 10, -6);
```

🎓 **v4 重要区别**：旧版直接在 `CinemachineVirtualCamera` 上操作 Body/Aim，新版把这些拆成独立组件（`CinemachineFollow`、`CinemachineRotationComposer` 等），更模块化。

### 摄像机遮挡处理（CameraOcclusionHandler）

当墙壁遮挡玩家时，将遮挡物变透明：

```csharp
void Update() {
    // 从摄像机向玩家做球形扫描（比 Raycast 更稳定）
    Vector3 dir = playerTransform.position - cam.transform.position;
    float dist = dir.magnitude;

    RaycastHit[] hits = Physics.SphereCastAll(
        cam.transform.position, sphereRadius, dir.normalized, dist, occlusionLayer
    );

    foreach (var hit in hits) {
        // 将命中的物体切换为透明材质
        SetTransparent(hit.collider.GetComponent<Renderer>());
    }

    // 恢复上一帧被透明化但这帧没命中的物体
    RestorePreviousOccluders(hits);
}

void SetTransparent(Renderer rend) {
    // URP 切换透明模式需要同时改多个属性
    Material mat = rend.material;  // 注意：用 .material 而非 .sharedMaterial（见第9章）
    mat.SetFloat("_Surface", 1);   // 0=Opaque, 1=Transparent
    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    mat.SetFloat("_ZWrite", 0);
    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
}
```

---

## 4. Animator 状态机

### 核心概念

Unity Animator 是一个**可视化状态机**：
- **State**（状态）：一段动画，如 Idle、Run、Attack
- **Transition**（过渡）：状态间的切换条件
- **Parameter**（参数）：C# 代码驱动过渡的变量

### 参数类型及使用场景

| 类型 | 特点 | 适用场景 |
|------|------|---------|
| **Float** | 持续浮点值 | 移动速度（连续变化） |
| **Int** | 持续整型 | 武器编号 |
| **Bool** | 持续布尔值 | 是否在地面、是否举盾 |
| **Trigger** | 一次性触发，自动复位 | 攻击、跳跃、受伤、死亡 |

### 🎓 最重要的坑：Trigger vs Bool

```csharp
// ❌ 错误：用 Bool 控制跳跃
animator.SetBool("Jump", true);
// 问题：Animator 有 "Any State → Jump" 过渡
// isJumping = true 时，每一帧都满足过渡条件
// → 跳跃动画每帧重新开始，永远播不完！

// ✅ 正确：用 Trigger 控制跳跃
animator.SetTrigger("Jump");
// Trigger 只激活一次，Animator 消费后自动复位
// → 跳跃动画正常播放一遍
```

**规则总结**：
- 持续性状态（"我正在移动"）→ Bool 或 Float
- 一次性事件（"我按下了攻击"）→ Trigger

### 代码控制 Animator

```csharp
Animator animator;

void Awake() {
    animator = GetComponent<Animator>();
}

void Update() {
    // Float：传入移动速度，Blend Tree 根据速度混合 Idle/Run 动画
    animator.SetFloat("Speed", controller.velocity.magnitude);

    // Bool：是否在地面
    animator.SetBool("IsGrounded", controller.isGrounded);

    // Trigger：播放一次性动画
    animator.SetTrigger("Attack");
    animator.SetTrigger("Hit");
    animator.SetTrigger("Die");
}
```

### Blend Tree（混合树）

用于根据一个 Float 参数平滑混合多个动画：

```
Speed = 0    → 播放 Idle
Speed = 0.5  → Idle 和 Run 各播 50%（过渡自然）
Speed = 1    → 播放 Run
```

设置方式：Animator 窗口 → 右键 → Create State → From New Blend Tree

### 动画事件（Animation Event）

在动画特定帧触发 C# 函数，常用于武器 Hitbox 激活：

```csharp
// 在 PlayerCombat.cs 中定义
public void OnAttackHitStart() {
    weaponHitbox.SetActive(true);   // 攻击判定帧开始
}

public void OnAttackHitEnd() {
    weaponHitbox.SetActive(false);  // 攻击判定帧结束
}
```

然后在 Animation 窗口的对应帧上添加 Event，调用这两个函数。

🎓 **与 2D 的对比**：Cocos 用 AnimationClip 的 frame event；Godot 用 AnimationPlayer 的 call_method track。Unity 的 Animation Event 是同一个概念，都是"在特定帧执行代码"。

### Animator.CrossFade（直接跳转）

不通过过渡条件，直接强制切换到某个状态：

```csharp
// 用于过场动画、强制播放特定动画
animator.CrossFade("OpeningIdle", 0.2f);
// 参数1：目标状态名
// 参数2：过渡时间（秒）
```

---

## 5. NavMesh 寻路 & 敌人 AI

### NavMesh 是什么？

NavMesh（导航网格）是 Unity 内置的寻路系统：
1. **烘焙（Bake）**：在编辑器中标记哪些地面可行走，生成网格数据（静态，运行前完成）
2. **NavMeshAgent**：挂在角色上的组件，自动在 NavMesh 上计算并沿路径移动

🎓 **类比**：NavMesh 像一张地图，NavMeshAgent 是看着地图走路的 NPC。

### 烘焙步骤

1. 选中场景中的地面、墙壁等静态物体 → Inspector → 勾选 **Static**
2. Window → AI → Navigation → 选 **Bake** 标签页
3. 设置 Agent Radius（角色半径）、Agent Height（角色高度）
4. 点击 **Bake** 按钮 → 场景中出现蓝色网格区域即为可行走区域

### NavMeshAgent 核心属性

```csharp
NavMeshAgent agent = GetComponent<NavMeshAgent>();

// 设置目标点，Agent 自动寻路
agent.SetDestination(playerTransform.position);

// 停止距离：距目标多近时停下（通常设为攻击范围的 80%）
agent.stoppingDistance = attackRange * 0.8f;

// 是否暂停移动（攻击时停步）
agent.isStopped = true;

// 当前速度（用于驱动动画）
float speed = agent.velocity.magnitude;
animator.SetFloat("Speed", speed);

// 是否已到达目标（remainingDistance 小于 stoppingDistance）
bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance;
```

🎓 **stoppingDistance 的设计意图**：
如果直接追到玩家坐标，Agent 会推着玩家走。设置 stoppingDistance 后，Agent 停在攻击范围内的合适位置，为攻击动画留出空间。

### 敌人 AI 状态机

```csharp
public enum EnemyState { Idle, Chase, Attack, Hit, Dead }

EnemyState currentState = EnemyState.Idle;

void Update() {
    switch (currentState) {
        case EnemyState.Idle:   UpdateIdle();   break;
        case EnemyState.Chase:  UpdateChase();  break;
        case EnemyState.Attack: UpdateAttack(); break;
        case EnemyState.Hit:    UpdateHit();    break;
    }
}

void UpdateIdle() {
    // 检测玩家是否进入感知范围
    float dist = Vector3.Distance(transform.position, player.position);
    if (dist < detectionRange) {
        currentState = EnemyState.Chase;
    }
}

void UpdateChase() {
    agent.SetDestination(player.position);
    float dist = Vector3.Distance(transform.position, player.position);

    if (dist <= attackRange) {
        agent.isStopped = true;
        currentState = EnemyState.Attack;
        StartCoroutine(DoAttack());
    }
}
```

### 攻击时序控制

```csharp
IEnumerator DoAttack() {
    animator.SetTrigger("Attack");

    // 等待动画进行到"出手帧"再造成伤害
    yield return new WaitForSeconds(attackHitDelay);   // 0.4s 后实际造伤
    DealDamage();

    // 等待整个攻击动画结束
    yield return new WaitForSeconds(attackAnimDuration - attackHitDelay);

    // 攻击结束，重新判断状态
    float dist = Vector3.Distance(transform.position, player.position);
    currentState = dist <= attackRange ? EnemyState.Attack : EnemyState.Chase;
    agent.isStopped = false;
}
```

🎓 **分离 `attackHitDelay` 和 `attackAnimDuration` 的意义**：
实际伤害时机和动画时长独立配置，可以调整"重击感"（延迟长=蓄力感强）或"快攻感"（延迟短=反应快）而不用改动画。

### 继承结构

```
EnemyBase（抽象类）
├── 通用逻辑：NavMesh 移动、状态机、受击、死亡
├── 抽象方法：abstract void DoAttack()
│
├── MeleeEnemy（继承 EnemyBase）
│   └── DoAttack()：Physics.OverlapSphere 近身命中检测
│
└── RangedEnemy（继承 EnemyBase）
    └── DoAttack()：Instantiate 投射物 + 保持距离逻辑
```

---

## 6. 3D 物理检测

### Collider 类型

| 类型 | 形状 | 常见用途 |
|------|------|---------|
| BoxCollider | 立方体 | 房间触发器、武器 Hitbox |
| SphereCollider | 球体 | 近战攻击范围、拾取物 |
| CapsuleCollider | 胶囊体 | 角色碰撞体（CharacterController 内置） |
| MeshCollider | 贴合模型 | 复杂地形（性能消耗高，慎用于动态物体） |

### 碰撞 vs 触发器

```csharp
// Collider 的 Is Trigger 勾选 → 变为触发器（不产生物理碰撞）

// 物理碰撞回调（Is Trigger 未勾选）
void OnCollisionEnter(Collision col) { }
void OnCollisionStay(Collision col)  { }
void OnCollisionExit(Collision col)  { }

// 触发器回调（Is Trigger 已勾选）
void OnTriggerEnter(Collider other) { }
void OnTriggerStay(Collider other)  { }
void OnTriggerExit(Collider other)  { }
```

🎓 **选哪个？**
- 玩家走进房间触发事件 → **Trigger**（不需要物理阻挡）
- 武器打到敌人 → **Trigger**（只需要判断"有没有接触"）
- 墙壁挡住玩家 → **Collider**（需要实际阻挡）

### Physics.OverlapSphere（近战攻击检测）

```csharp
// 以某点为圆心，radius 为半径，返回范围内所有 Collider
Collider[] hits = Physics.OverlapSphere(
    attackOrigin.position,   // 检测中心（通常是武器位置）
    attackRadius,            // 检测半径
    enemyLayer               // 只检测敌人层（优化性能）
);

foreach (var hit in hits) {
    IDamageable target = hit.GetComponentInParent<IDamageable>();
    if (target != null && !target.IsDead) {
        DamageSystem.DealDamage(hit, damage, transform.position);
    }
}
```

🎓 **为什么用 `GetComponentInParent` 而不是 `GetComponent`？**
角色的碰撞体通常挂在子物体上（如 HitboxChild），而 IDamageable 脚本挂在根物体上。`GetComponentInParent` 会沿父级链向上查找。

### Physics.SphereCast（摄像机遮挡检测）

```csharp
// SphereCast = 沿路径扫描一个球体（比 Raycast 更宽容，不会漏掉薄物体）
RaycastHit hit;
bool occluded = Physics.SphereCast(
    cam.transform.position,           // 起点
    0.3f,                             // 球半径
    (player.position - cam.transform.position).normalized,  // 方向
    out hit,
    Vector3.Distance(cam.transform.position, player.position), // 最大距离
    wallLayer
);

if (occluded) {
    // hit.collider 就是遮挡物
}
```

### Raycast（射线检测）

```csharp
// 从屏幕点击位置向场景发射射线
Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
RaycastHit hit;

if (Physics.Raycast(ray, out hit, 100f, groundLayer)) {
    // hit.point = 射线命中的世界坐标
    // hit.normal = 命中面的法线方向
    // hit.collider = 命中的 Collider
    agent.SetDestination(hit.point);  // 点击移动
}
```

### Gizmo 调试

在编辑器中可视化物理检测范围（不影响运行时性能）：

```csharp
void OnDrawGizmosSelected() {
    // 近战攻击范围（黄色）
    Gizmos.color = Color.yellow;
    Gizmos.DrawWireSphere(transform.position, attackRange);

    // 感知范围（红色）
    Gizmos.color = Color.red;
    Gizmos.DrawWireSphere(transform.position, detectionRange);
}
```

---

## 7. 状态机模式

### 为什么需要集中状态管理？

**没有状态管理时的问题**：

```csharp
// ❌ 各系统各自判断，容易冲突
void Update() {
    if (Input.GetKeyDown(KeyCode.Space)) Jump();    // 死亡时能跳
    if (Input.GetMouseButtonDown(0)) Attack();       // 受击时能攻击
}
```

**有集中状态管理后**：

```csharp
// ✅ 所有行为执行前先查询 PlayerState
if (!playerState.CanAttack) return;
if (!playerState.CanMove) return;
```

### PlayerState：优先级状态机

```csharp
public enum PlayerStateType {
    Normal    = 0,   // 可以做任何事
    Jumping   = 1,   // 可以移动，不能攻击
    Attacking = 2,   // 不能移动/跳跃
    Hit       = 3,   // 受击硬直，什么都不能做
    Dead      = 4    // 永久锁定
}

public class PlayerState : MonoBehaviour {
    public PlayerStateType CurrentState { get; private set; }

    // 便捷属性
    public bool CanMove    => CurrentState <= PlayerStateType.Jumping;
    public bool CanAttack  => CurrentState == PlayerStateType.Normal;
    public bool CanJump    => CurrentState <= PlayerStateType.Normal;
    public bool IsDead     => CurrentState == PlayerStateType.Dead;

    public bool TrySetState(PlayerStateType newState) {
        // 高优先级状态不能被低优先级打断
        // 例如：受击(3)时，攻击(2) < 3，无法打断受击
        if ((int)newState <= (int)CurrentState && newState != PlayerStateType.Normal) {
            return false;
        }
        CurrentState = newState;
        return true;
    }
}
```

🎓 **枚举值即优先级**：数值越大，优先级越高，越难被打断。`Dead = 4` 永远不会被覆盖（除非重置）。

### EnemyState：线性状态机

```csharp
// 敌人状态流转是线性的，不需要优先级
// Idle → Chase → Attack → Hit → Dead（单向）

void UpdateHit() {
    hitStunTimer -= Time.deltaTime;
    if (hitStunTimer <= 0) {
        float dist = Vector3.Distance(transform.position, player.position);
        // 硬直结束后，根据距离决定下一个状态
        currentState = dist <= detectionRange ? EnemyState.Chase : EnemyState.Idle;
        agent.isStopped = false;
    }
}
```

### 状态机 vs 其他 AI 方案

| 方案 | 适用场景 | 本项目选择 |
|------|---------|-----------|
| 枚举 + switch | ≤5 种状态，逻辑简单 | ✅ EnemyBase |
| 行为树（Behavior Tree） | 复杂 NPC，状态 10+ | 过重 |
| GOAP（目标导向） | 开放世界 AI | 过重 |
| 有限状态机类库 | 中等复杂度 | 可选 |

🎓 **面试回答技巧**：
> "我们选择手写枚举状态机，因为项目只有两种敌人、5 个状态以内，引入行为树会增加不必要的复杂度。如果扩展到 10 种敌人，我会考虑 Unity 的 Behavior package 或自定义行为树。"

---

## 8. 伤害系统

### 设计目标：解耦

伤害系统的核心问题：**谁打谁** 的逻辑不应该分散在每个攻击脚本里。

```
❌ 耦合设计：PlayerCombat 直接调用 EnemyHealth.TakeDamage()
   → PlayerCombat 需要知道 EnemyHealth 的存在
   → 无法用同一套代码伤害玩家（陷阱、反弹）

✅ 解耦设计：PlayerCombat → DamageSystem → IDamageable
   → 任何实现了 IDamageable 的对象都能被伤害
   → 玩家、敌人、可破坏物体共用同一套伤害流程
```

### IDamageable 接口

```csharp
public interface IDamageable {
    bool IsDead { get; }
    void TakeDamage(int damage, Vector3 knockbackDir);
}

// 玩家实现
public class PlayerHealth : MonoBehaviour, IDamageable {
    public bool IsDead => currentHP <= 0;

    public void TakeDamage(int damage, Vector3 knockbackDir) {
        if (IsDead) return;
        currentHP -= damage;
        StartCoroutine(FlashEffect());   // 闪烁反馈
        ApplyKnockback(knockbackDir);    // 击退
        if (IsDead) Die();
    }
}

// 敌人实现（EnemyBase 继承）
public class EnemyBase : MonoBehaviour, IDamageable {
    public bool IsDead => currentHP <= 0;

    public void TakeDamage(int damage, Vector3 knockbackDir) {
        if (IsDead) return;
        currentHP -= damage;
        UpdateHealthBar();
        EnterHitState();   // 进入硬直
        if (IsDead) Die();
    }
}
```

### DamageSystem（静态工具类）

```csharp
public static class DamageSystem {
    public static void DealDamage(Collider target, int damage, Vector3 attackerPos) {
        // 向上查找 IDamageable（碰撞体可能在子物体上）
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead) return;

        // 计算击退方向：从攻击者指向目标
        Vector3 knockbackDir = (target.transform.position - attackerPos).normalized;
        knockbackDir.y = 0;  // 不产生垂直击退

        damageable.TakeDamage(damage, knockbackDir);
    }
}
```

### 完整伤害流程

```
PlayerCombat.OnAttackHitStart()
  → 武器 Collider 激活
    → OnTriggerEnter(Collider other)
      → DamageSystem.DealDamage(other, damage, transform.position)
        → IDamageable.TakeDamage(damage, knockbackDir)
          → 扣血 + 闪烁 + 击退 + 状态切换 + 更新 UI
```

### 受击闪烁（Material 实例化）

```csharp
// ⚠️ 不能用 renderer.sharedMaterial（会影响所有同材质物体！）
// 必须用 renderer.material 获取该物体专属的实例

Material[] cachedMats;

void Awake() {
    // 缓存所有子物体的材质实例（Awake 中一次性创建）
    var renderers = GetComponentsInChildren<Renderer>();
    cachedMats = new Material[renderers.Length];
    for (int i = 0; i < renderers.Length; i++) {
        cachedMats[i] = renderers[i].material;  // 触发实例化
    }
}

IEnumerator FlashEffect() {
    foreach (var mat in cachedMats) {
        mat.SetColor("_EmissionColor", Color.white * 3f);
        mat.EnableKeyword("_EMISSION");
    }
    yield return new WaitForSeconds(0.1f);
    foreach (var mat in cachedMats) {
        mat.SetColor("_EmissionColor", Color.black);
    }
}
```

🎓 **URP 属性名**：
- `_BaseColor`（不是 Built-in 的 `_Color`）
- `_EmissionColor`（自发光颜色）
- 值乘以大于 1 的数 = HDR 高亮（`Color.white * 3f` = 很亮的白色）

---

## 9. URP 渲染管线

### Built-in vs URP vs HDRP

| 管线 | 特点 | 适用 |
|------|------|------|
| Built-in（Legacy） | 旧版，向后兼容 | 老项目维护 |
| **URP**（Universal） | 性能均衡，支持多平台 | 手游、独立游戏、主机 |
| HDRP（High Definition） | 极致画质，PC/主机专属 | AAA 写实风格 |

🎓 **面试考查点**：URP 是目前业内新项目的主流选择，理解 URP 的核心机制是加分项。

### SRP Batcher（合批优化）

**传统 Dynamic Batching**：引擎在 CPU 把多个 Mesh 合并成一个 Draw Call。

**SRP Batcher**：不合并 Mesh，而是把每个材质的属性（颜色、纹理偏移等）缓存在 **GPU 常量缓冲区**（Constant Buffer）中。相同 Shader 的物体可以共享 Constant Buffer，减少 CPU 向 GPU 传数据的次数。

```
传统：每帧 CPU → 设置材质属性 → GPU 渲染
SRP Batcher：CPU → 第一帧写入 GPU 缓冲 → 后续帧直接复用 GPU 缓冲
             → 大幅减少 CPU 开销
```

**关键限制**：SRP Batcher 要求**同一个 Shader** 才能合批。

### MaterialPropertyBlock 的陷阱

```csharp
// ❌ 常见错误：用 MaterialPropertyBlock 改颜色
MaterialPropertyBlock mpb = new MaterialPropertyBlock();
mpb.SetColor("_BaseColor", Color.red);
renderer.SetPropertyBlock(mpb);
// SRP Batcher 绕过 MaterialPropertyBlock！
// → 颜色改变了，但破坏了 SRP 合批

// ✅ 正确：用 renderer.material（材质实例）
renderer.material.SetColor("_BaseColor", Color.red);
// 每个物体有自己的实例，SRP Batcher 正常合批
```

🎓 **为什么 .material 而不是 .sharedMaterial？**
- `.sharedMaterial`：所有使用同一材质的物体共享，修改会影响所有物体
- `.material`：创建该物体专属的材质实例，修改只影响自己
- 代价：多一个 Material 对象（内存），但对于动态效果（闪烁、高亮）是必要的

### URP 材质切换透明度

URP 材质的透明/不透明不是单一属性，需要同时设置多个参数：

```csharp
void SetOpaque(Material mat) {
    mat.SetFloat("_Surface", 0);  // Opaque
    mat.SetFloat("_ZWrite", 1);
    mat.renderQueue = (int)RenderQueue.Geometry;
    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
}

void SetTransparent(Material mat, float alpha) {
    mat.SetFloat("_Surface", 1);  // Transparent
    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
    mat.SetFloat("_ZWrite", 0);
    mat.renderQueue = (int)RenderQueue.Transparent;
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    // 设置透明度
    Color c = mat.GetColor("_BaseColor");
    c.a = alpha;
    mat.SetColor("_BaseColor", c);
}
```

### 多平台渲染配置

| 平台 | 渲染模式 | 阴影 | 后处理 | 分辨率倍率 |
|------|---------|------|-------|-----------|
| PC | Deferred | 开，4 级联 | SSAO + Bloom | 1.0x |
| Mobile | Forward | 关 | 无 | 0.8x |
| WebGL | Forward | 关 | 最低 | 0.7x |

🎓 **Deferred vs Forward**：
- **Forward**：每个物体遍历所有光源，光源多时性能差
- **Deferred**：先渲染几何信息（G-Buffer），再统一计算光照，大量光源场景性能好

### 小地图优化案例

优化前：小地图摄像机和主摄像机相同设置 → 60 个批次

优化后：
1. 为小地图创建独立的 URP Renderer（无阴影、无后处理）
2. 小地图摄像机每 3 帧更新一次（`Camera.Render()` 手动调用）
3. 关闭小地图摄像机上所有 Point Light 阴影

结果：小地图从 60 批次降至 5 批次，整体帧率提升约 15%。

---

## 10. 游戏手感

游戏手感（Game Feel）是让游戏"操作爽"的细节集合，这些技术在所有顶级动作游戏中都有应用。

### Coyote Time（郊狼时间）

**问题**：玩家走到平台边缘后继续向前一步，脚下已经是空气，但玩家"感觉上"还在平台上，此时按跳跃没有反应 → 体验很差。

**解决**：离地后给一个短暂的宽限期（约 0.1~0.2s），期间仍然可以跳跃。

```csharp
float coyoteTimer = 0f;
const float coyoteTime = 0.15f;

void Update() {
    // 在地面时，持续刷新计时器
    if (controller.isGrounded) {
        coyoteTimer = coyoteTime;
    } else {
        // 离地后开始倒计时
        coyoteTimer -= Time.deltaTime;
    }

    // 判断能否跳跃：地面上，或宽限期内
    bool canJump = coyoteTimer > 0f;

    if (Input.GetKeyDown(KeyCode.Space) && canJump) {
        velocity.y = jumpForce;
        coyoteTimer = 0f;  // 防止重复跳
    }
}
```

### Input Buffer（输入缓冲）

**问题**：玩家在落地前 0.05s 按了跳跃，此时还在空中所以没响应，但玩家"认为"已经按了。

**解决**：记住最近的跳跃输入，在一个短时间窗口（约 0.15s）内落地就自动执行。

```csharp
float jumpBufferTimer = 0f;
const float jumpBufferTime = 0.15f;

void Update() {
    // 按下跳跃：写入缓冲
    if (Input.GetKeyDown(KeyCode.Space)) {
        jumpBufferTimer = jumpBufferTime;
    } else {
        jumpBufferTimer -= Time.deltaTime;
    }

    // 落地时：如果缓冲内有跳跃请求，立即执行
    if (controller.isGrounded && jumpBufferTimer > 0f) {
        velocity.y = jumpForce;
        jumpBufferTimer = 0f;
    }
}
```

🎓 **两者配合**：Coyote Time 解决"刚离地没反应"，Input Buffer 解决"快落地提前按没反应"，两者合用让跳跃极度流畅。

### Auto-Aim（攻击辅助瞄准）

**问题**：俯视角游戏中，玩家面朝左但敌人在右边，按攻击会打空。

**解决**：攻击按下瞬间，自动转向最近的敌人。

```csharp
void TryAttack() {
    // 搜索攻击范围内所有敌人
    Collider[] hits = Physics.OverlapSphere(transform.position, autoAimRadius, enemyLayer);

    Transform closestEnemy = null;
    float minDist = float.MaxValue;

    foreach (var hit in hits) {
        if (hit.GetComponentInParent<IDamageable>()?.IsDead == false) {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist) {
                minDist = dist;
                closestEnemy = hit.transform;
            }
        }
    }

    // 找到目标则瞬间转向
    if (closestEnemy != null) {
        Vector3 dir = (closestEnemy.position - transform.position).normalized;
        dir.y = 0;
        transform.rotation = Quaternion.LookRotation(dir);
    }

    // 无论有没有目标，都执行攻击动画
    StartAttackAnimation();
}
```

### 武器 Hitbox 时序（Attack Phases）

攻击动画分三个阶段，只有"出手帧"才造成伤害：

```
|——————|————————|——————————|
0s    0.3s    0.5s       0.8s
 蓄力   ↑        ↑          回收
      Hitbox  Hitbox
      激活    关闭
```

```csharp
// 通过 Animation Event 精确控制时机（不用计时器，跟动画帧完全同步）

// 在 PlayerCombat.cs 中定义（被 Animation Event 调用）
public void OnAttackHitStart() {
    weaponHitbox.EnableHitbox();  // 出手帧开始，激活碰撞体
}

public void OnAttackHitEnd() {
    weaponHitbox.DisableHitbox(); // 出手帧结束，关闭碰撞体
}
```

🎓 **为什么要分阶段？**
- **蓄力**（Wind-up）：玩家看到敌人的攻击预兆，有机会躲避
- **出手**（Active）：实际伤害窗口，时间短=精准感强
- **回收**（Recovery）：攻击结束后有硬直，是玩家反击的机会
- 这套设计是《黑魂》《鬼泣》《只狼》等动作游戏的核心战斗节奏基础

---

## 11. ScriptableObject 数据驱动

### 什么是 ScriptableObject？

ScriptableObject 是 Unity 中一种**数据容器资产**，可以存在于 Project 面板中（.asset 文件），不依附于任何场景中的 GameObject。

🎓 **与 MonoBehaviour 的区别**：
- `MonoBehaviour`：挂在 GameObject 上，随场景加载/卸载
- `ScriptableObject`：独立资产文件，多个 GameObject 可以引用同一份数据

### WeaponData 设计

```csharp
[CreateAssetMenu(menuName = "DungeonSlash/WeaponData", fileName = "NewWeapon")]
public class WeaponData : ScriptableObject {
    [Header("基础属性")]
    public string weaponName;
    public int damage;
    public float attackCooldown;
    public float attackRange;

    [Header("攻击时序")]
    public float lightHitDelay;     // 轻攻击：出手帧延迟
    public float lightHitDuration;  // 轻攻击：命中窗口长度
    public float heavyHitDelay;     // 重攻击
    public float heavyHitDuration;

    [Header("Auto-Aim")]
    public float autoAimRadius;

    [Header("视觉")]
    public GameObject weaponPrefab;
    public Sprite weaponIcon;
}
```

使用：在 Project 面板右键 → Create → DungeonSlash → WeaponData，创建剑、斧、弓等不同武器的数据文件。

### 优点

```csharp
// 武器切换：只需要替换 WeaponData，不需要改代码
public class WeaponManager : MonoBehaviour {
    public WeaponData currentWeapon;

    void Attack() {
        // 所有参数来自 ScriptableObject，策划可以直接在 Inspector 调参
        StartCoroutine(AttackRoutine(
            currentWeapon.lightHitDelay,
            currentWeapon.lightHitDuration,
            currentWeapon.damage
        ));
    }
}
```

**优点总结**：
1. **策划友好**：不懂代码也能在 Inspector 调整武器数值
2. **复用性高**：多个敌人、玩家可以引用同一份 WeaponData
3. **热更新**：游戏运行时修改 ScriptableObject 值立即生效（开发期）
4. **无内存浪费**：100 个同种敌人共享 1 个 EnemyData，不是 100 份

🎓 **适合做成 ScriptableObject 的数据**：
- 武器/道具属性
- 敌人配置（血量、速度、伤害）
- 关卡参数
- 对话文本
- 音效配置

---

## 12. 面试常见问题 & 标准回答

### Q1：CharacterController 和 Rigidbody 的区别，你在项目中如何选择？

> **回答**：CharacterController 是代码驱动位移，自带防穿墙和台阶处理，但需要手写重力；Rigidbody 是物理引擎驱动，自动处理重力碰撞，但在高速运动时可能穿墙，且难以精确控制每帧位移。
>
> 我们的动作 Roguelike 用了 CharacterController，原因是：动作游戏需要对角色每帧位移有精确控制，比如闪避时要精确位移一段固定距离，Rigidbody 的物理模拟会带来不可预期的速度变化。行业惯例也是如此——Unity 官方角色控制器示例、Unreal 的 Character Movement Component 都是同一思路。

---

### Q2：Animator 中 Bool 和 Trigger 什么时候用哪个？

> **回答**：Bool 用于持续性状态，比如"是否在奔跑"——玩家按住 W 整个期间 IsRunning=true；Trigger 用于一次性事件，比如"触发攻击"——Trigger 激活一次后 Animator 自动复位。
>
> 我们项目踩过坑：最初把跳跃做成 Bool，"Any State → Jump"的过渡在 isJumping=true 期间每帧都满足条件，导致跳跃动画每帧重新播放，永远播不完。改成 Trigger 后 Animator 只消费一次，动画正常播放。

---

### Q3：解释一下你的伤害系统设计

> **回答**：我们用了 IDamageable 接口 + 静态 DamageSystem 工具类的组合。IDamageable 定义了 TakeDamage() 和 IsDead 属性，玩家和敌人都实现这个接口。DamageSystem.DealDamage() 接收一个 Collider，通过 GetComponentInParent 找到 IDamageable，计算击退方向后调用 TakeDamage()。
>
> 这样设计的好处是完全解耦：PlayerCombat 不需要知道打到的是 EnemyBase 还是 PlayerHealth，只要对方实现了 IDamageable 就能受伤。未来加入陷阱、毒雾等环境伤害，也只需要调用同一个 DamageSystem，不用改现有代码。

---

### Q4：NavMesh 的工作原理是什么？

> **回答**：NavMesh 是预先烘焙的导航网格，标记场景中哪些区域可以行走。烘焙是在编辑器离线完成的，运行时 NavMeshAgent 在这个网格上用 A* 算法计算最短路径。
>
> 我们的敌人 AI 中，Chase 状态每帧调用 agent.SetDestination(playerPosition)，Agent 自动重新规划路径绕过障碍物。攻击时设 agent.isStopped=true 停止移动，硬直结束后再恢复。stoppingDistance 设为 attackRange 的 80%，让敌人在攻击范围内停下，而不是贴着玩家，为攻击动画留出空间。

---

### Q5：URP 的 SRP Batcher 是什么？

> **回答**：SRP Batcher 是 URP 的批渲染优化。传统 Dynamic Batching 把多个 Mesh 在 CPU 合并成一个 Draw Call；SRP Batcher 不合并 Mesh，而是把每个材质的属性数据缓存在 GPU 的常量缓冲区（Constant Buffer）里，相同 Shader 的物体共享这块缓冲，避免每帧重复上传数据到 GPU，大幅减少 CPU 端开销。
>
> 我们项目踩过坑：用 MaterialPropertyBlock 给角色做受击闪烁，发现颜色正确但 SRP Batcher 失效（Frame Debugger 里合批断开）。原因是 SRP Batcher 绕过了 MaterialPropertyBlock，需要改用 renderer.material 获取实例材质来修改属性。

---

### Q6：Coyote Time 和 Input Buffer 是什么？为什么要做这两个？

> **回答**：这两个都是游戏手感优化技术。
>
> Coyote Time：玩家走到平台边缘后，`isGrounded` 立刻变 false，但玩家"感知"上觉得自己还在平台上，此时按跳跃没反应体验很差。我们在离地后保留约 0.15 秒的"宽限期"，期间仍然允许跳跃，消除这种割裂感。
>
> Input Buffer：玩家在落地前几帧就按了跳跃，但因为还在空中所以没响应，落地后需要再按一次。我们记录最近的跳跃输入，落地后 0.15 秒内如果缓冲中有跳跃请求就自动执行。
>
> 两个技术加起来，跳跃操作的容错窗口大大扩大，这也是 Super Mario、Celeste 等平台游戏的标配手段。

---

### Q7：如何处理动画与游戏逻辑的同步？（武器 Hitbox 时序）

> **回答**：我们用 Animation Event 而非计时器来同步。在攻击动画的特定帧上添加 Animation Event，分别调用 OnAttackHitStart() 和 OnAttackHitEnd()，由这两个函数激活/关闭武器的 Collider。
>
> 这比 `yield return new WaitForSeconds(0.3f)` 更精确——计时器在帧率波动时可能偏移，而 Animation Event 永远精确对应动画帧。攻击分蓄力、出手、回收三个阶段，只有出手帧的 Collider 是激活状态，这也是动作游戏的标准设计：玩家能看到攻击前摇，有躲避时机，同时出手后有回收硬直是反击窗口。

---

### Q8：你的项目结构和代码架构是怎样的？

> **回答**：26 个脚本分 6 个子系统：玩家、敌人、战斗、核心、UI、摄像机。核心设计决策有三个：
>
> 第一，集中状态管理。PlayerState 用带优先级的枚举维护玩家当前状态，所有行为（移动、攻击、跳跃）执行前都检查 PlayerState，避免了"死亡时能攻击"这类状态冲突 bug。
>
> 第二，接口解耦伤害系统。IDamageable 接口让攻击逻辑不依赖具体的血量类，未来加入可破坏环境只需实现这个接口，无需改动任何攻击代码。
>
> 第三，数据驱动武器系统。WeaponData 是 ScriptableObject，武器的伤害、攻击时序、Auto-Aim 半径都在资产文件里配置，策划可以直接在 Inspector 调参不用改代码，也方便运行时热调整。
