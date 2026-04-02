# 武器挂载系统设计

**日期**: 2026-04-02
**状态**: 待实施
**范围**: 给 Player 添加可切换的可视化武器，运行时通过骨骼挂载显示，按数字键切换

---

## 1. 目标

- 角色手中显示可见的武器模型，攻击时武器跟随手骨动画自然挥舞
- 支持 3 种预设武器（剑/斧/匕首），按 1/2/3 键切换
- 不同武器有不同的伤害、冷却等属性
- 学习核心 3D 概念：骨骼挂载、ScriptableObject 数据驱动、运行时 Prefab 实例化

## 2. 架构

```
WeaponData (ScriptableObject)     ← 数据层：武器属性资产文件
    ↓
WeaponManager.cs (MonoBehaviour)  ← 逻辑层：挂载/切换武器
    ↓
PlayerCombat.cs (已有，需修改)     ← 战斗层：从 WeaponManager 读取当前武器数据
```

### 数据流

```
按下数字键 1/2/3
  → WeaponManager.SwitchWeapon(index)
    → 销毁当前武器 GameObject
    → Instantiate(weaponData.prefab)
    → SetParent(rightHandBone, worldPositionStays: false)
    → 设置 localPosition = Vector3.zero, localRotation = 预设值
    → 给新武器添加 WeaponHitbox 组件（如果没有）
    → 更新 PlayerCombat 的 weaponCollider 引用和武器数据
```

## 3. 新增文件

### 3.1 `WeaponData.cs` — ScriptableObject

位置: `Assets/Scripts/Combat/WeaponData.cs`

```csharp
[CreateAssetMenu(fileName = "NewWeapon", menuName = "DungeonSlash/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("基本信息")]
    public string weaponName;
    public GameObject prefab;          // 武器预制体（来自素材包）

    [Header("战斗属性")]
    public float lightDamage = 15f;
    public float heavyDamage = 30f;
    public float lightAttackCooldown = 0.5f;
    public float heavyAttackCooldown = 1.2f;

    [Header("碰撞体设置")]
    public Vector3 colliderCenter = Vector3.zero;
    public Vector3 colliderSize = new Vector3(0.3f, 0.3f, 1f);
}
```

为什么用 ScriptableObject:
- 武器数据是**共享的、不变的**，不需要挂在 GameObject 上
- 在 Inspector 中编辑，所见即所得
- 多个实例可引用同一份数据，内存只有一份
- 面试高频问题：MonoBehaviour vs ScriptableObject 区别

### 3.2 `WeaponManager.cs` — 武器管理器

位置: `Assets/Scripts/Player/WeaponManager.cs`

职责:
- 持有 `WeaponData[]` 武器列表
- `Awake` 时找到右手骨骼 `RigRArmPalm`
- `Start` 时装备默认武器（index 0）
- 监听数字键输入，调用 `SwitchWeapon(int index)`
- 切换时：销毁旧武器 → 实例化新武器 → 挂载到手骨 → 配置碰撞体 → 通知 PlayerCombat

找骨骼的方式:
```csharp
// 方式1: 递归查找（通用）
Transform FindBone(Transform root, string boneName);

// 方式2: Animator.GetBoneTransform（需要 Humanoid Avatar）
animator.GetBoneTransform(HumanBodyBones.RightHand);
```
优先使用方式 1 直接找 "RigRArmPalm"，因为素材包的骨骼命名已知且固定。

### 3.3 修改 `PlayerCombat.cs`

改动点:
- 移除硬编码的 `lightDamage`、`heavyDamage`、`lightAttackCooldown`、`heavyAttackCooldown`
- 新增 `public void EquipWeapon(WeaponData data, Collider collider)` 方法
- 攻击时从 `currentWeaponData` 读取伤害和冷却值
- `weaponCollider` 改为运行时由 WeaponManager 设置

### 3.4 修改 `WeaponHitbox.cs`

改动点:
- 无需大改，`GetComponentInParent<PlayerCombat>()` 在 Awake 中查找
- 但武器是运行时实例化的，需确保实例化后能正确找到 PlayerCombat
- 解决方案：WeaponManager 实例化武器后手动调用 `weaponHitbox.Init(playerCombat)`，或改为 Start 中查找（延迟一帧）

## 4. 预设武器数据

| 武器 | 预制体来源 | 轻攻击伤害 | 重攻击伤害 | 轻攻冷却 | 重攻冷却 | 特点 |
|------|-----------|-----------|-----------|---------|---------|------|
| Sword | `04 Attach Weapons/01 R Arm/Sword` 系列 | 15 | 30 | 0.5s | 1.2s | 均衡型 |
| Axe | `04 Attach Weapons/01 R Arm/Axe` 系列 | 10 | 45 | 0.6s | 1.5s | 重击型 |
| Dagger | `04 Attach Weapons/01 R Arm/Dagger` 系列 | 8 | 18 | 0.3s | 0.7s | 速攻型 |

## 5. Editor 操作步骤

用户需要在 Unity Editor 中完成:
1. 创建 3 个 WeaponData 资产（右键 → Create → DungeonSlash → Weapon Data）
2. 为每个 WeaponData 配置预制体引用和数值
3. 在 Player prefab 上添加 WeaponManager 组件
4. 将 3 个 WeaponData 拖入 WeaponManager 的武器列表
5. 移除 Player prefab 上当前手动挂载的 Knuckles 武器模型（改为运行时动态生成）

## 6. 不做的事情

- ❌ 不做武器掉落/拾取系统
- ❌ 不做武器随机属性/品质
- ❌ 不做武器 UI 显示（可后续扩展）
- ❌ 不做武器升级/强化
- ❌ 不改变动画（轻重攻击动画对所有近战武器复用）

## 7. 学习目标

完成后能在面试中讲清楚:
- ScriptableObject 是什么，和 MonoBehaviour 有什么区别，什么场景用它
- 3D 骨骼挂载的原理（Transform 父子层级 + 动画驱动骨骼）
- 运行时 Instantiate + SetParent 的工作方式
- 为什么 worldPositionStays 参数很重要
- 数据驱动设计的好处（改数值不用改代码）
