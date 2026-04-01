# Dungeon Slash - 设计文档

## 概述

**游戏类型**：俯视角 3D 动作 Roguelike Demo
**引擎**：Unity 6
**目标**：通过制作一个可玩的 demo 掌握 3D 游戏开发核心知识，为面试做准备
**时间**：1 周

**一句话描述**：玩家在俯视角 3D 地牢中战斗，击败房间内所有敌人后进入下一个房间。

## 核心玩法循环

```
进入房间 → 敌人刷新 → 战斗（攻击/闪避）→ 清空房间 → 进入下一房间
```

## 功能范围

### 包含

- 俯视角相机跟随（Cinemachine）
- 角色移动 + 翻滚闪避（CharacterController）
- 近战攻击（轻击：快速低伤 / 重击：慢速高伤，武器碰撞触发器）
- 2 种敌人：近战追击型（MeleeEnemy）、远程射击型（RangedEnemy）
- 3-5 个房间的线性关卡流程（预设房间，同一场景内通过门触发器激活/关闭区域，不做场景切换）
- 血量 UI（玩家 + 敌人血条）
- 受击反馈（闪白 + 击退）
- 敌人死亡粒子特效

### 不包含（面试时口述设计思路即可）

- 背包 / 装备系统
- 技能树
- 存档 / 读档
- 音效 / 音乐
- 随机地牢生成
- 多种武器

## 3D 核心知识点覆盖

通过本项目将学到的 3D 开发知识：

| # | 知识点 | 对应实现 |
|---|--------|----------|
| 1 | 3D 坐标系与变换 | 角色/敌人/相机的 Position/Rotation/Scale |
| 2 | 相机系统 | Cinemachine 俯视角跟随相机 |
| 3 | 角色控制器 | CharacterController 移动 + 重力处理 |
| 4 | 动画系统 | Animator 状态机，Mixamo 动画导入与混合 |
| 5 | 3D 物理与碰撞 | Collider 类型、触发器、受击检测 |
| 6 | 光照基础 | Directional Light、Point Light、阴影 |
| 7 | 导航与 AI | NavMeshAgent 寻路 + 手写状态机（Idle/Chase/Attack） |
| 8 | 粒子系统 | 攻击拖尾、受击火花、死亡爆炸 |
| 9 | Prefab 与场景管理 | 预制件工作流、房间切换逻辑 |

## 技术架构

### 项目结构

```
Assets/
├── Scripts/
│   ├── Core/                  # 游戏管理
│   │   ├── GameManager.cs     # 全局游戏状态（单例）
│   │   └── RoomManager.cs     # 房间流程控制、敌人刷新
│   ├── Player/                # 玩家系统
│   │   ├── PlayerController.cs    # 移动 + 翻滚（CharacterController）
│   │   ├── PlayerCombat.cs        # 攻击输入 + 武器碰撞管理
│   │   └── PlayerHealth.cs        # 血量 + 受击反馈
│   ├── Enemy/                 # 敌人系统
│   │   ├── EnemyBase.cs           # 敌人基类（血量、受击、死亡）
│   │   ├── MeleeEnemy.cs          # 近战型：NavMesh 追击 → 近距离攻击
│   │   └── RangedEnemy.cs         # 远程型：保持距离 → 发射弹体
│   ├── Combat/                # 战斗系统
│   │   ├── DamageSystem.cs        # 统一伤害接口 IDamageable
│   │   └── Projectile.cs          # 远程弹体（移动 + 碰撞销毁）
│   └── UI/                    # 界面
│       └── HealthBarUI.cs         # 玩家血条 + 敌人头顶血条
├── Prefabs/                   # 预制件（玩家、敌人、弹体、特效）
├── Animations/                # Animator Controller + Animation Clips
├── Materials/                 # 材质和着色器
├── Scenes/                    # Room_01 ~ Room_05 场景文件
└── ThirdParty/                # Mixamo 模型、Quaternius 地牢素材
```

### 关键技术决策

| 决策点 | 选择 | 理由 |
|--------|------|------|
| 角色移动 | CharacterController | 比 Rigidbody 更可控，动作游戏标准方案 |
| 相机 | Cinemachine Virtual Camera | Unity 官方方案，配置俯视角跟随只需调参数 |
| 敌人寻路 | NavMeshAgent | Unity 内置，烘焙 NavMesh 后开箱即用 |
| 敌人 AI | 手写状态机（enum + switch） | 简单直接，2 种敌人不需要行为树 |
| 伤害系统 | IDamageable 接口 | 玩家和敌人共用，解耦战斗逻辑 |
| 动画 | Animator 状态机 + Mixamo | 零成本获取专业动画，学习 Animator 工作流 |
| 关卡流程 | 预设房间 + 触发器 | 不做程序化生成，手动搭建 3-5 个房间够用 |

### 核心数据流

```
[Input] → PlayerController → (移动/翻滚)
                           → PlayerCombat → (激活武器 Collider)
                                          → DamageSystem.DealDamage()
                                          → EnemyBase.TakeDamage()
                                          → (血量扣减/死亡/特效)

[NavMeshAgent] → EnemyBase → (状态机: Idle→Chase→Attack)
                            → MeleeEnemy: 近身后激活攻击 Collider
                            → RangedEnemy: 远距离实例化 Projectile

[RoomManager] → 监听房间内敌人数量
              → 全部死亡 → 开启出口门 → 加载下一房间
```

### 素材来源（全部免费）

| 素材类型 | 来源 | 说明 |
|----------|------|------|
| 角色模型 + 动画 | Mixamo (mixamo.com) | Adobe 免费服务，导出 FBX 格式 |
| 地牢场景模型 | Quaternius (quaternius.com) 或 Unity Asset Store 免费包 | 低模风格，体积小 |
| 粒子特效 | Unity 内置粒子系统 | 手动创建，学习粒子系统 |
| 材质 | Unity URP 默认材质 | 简单调色即可 |

### 渲染管线

使用 **URP（Universal Render Pipeline）**：
- Unity 6 默认推荐管线
- 性能好，适合中小型项目
- 面试时能体现你了解 Unity 渲染管线的概念
