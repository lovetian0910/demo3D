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

    [Header("攻击判定时机")]
    [Tooltip("轻攻击：动画开始后多久激活碰撞体（抬手蓄力时间）")]
    public float lightHitDelay = 0.3f;

    [Tooltip("轻攻击：碰撞体保持激活多久（下挥打击时间）")]
    public float lightHitDuration = 0.2f;

    [Tooltip("重攻击：动画开始后多久激活碰撞体（蓄力更久）")]
    public float heavyHitDelay = 0.45f;

    [Tooltip("重攻击：碰撞体保持激活多久")]
    public float heavyHitDuration = 0.25f;

    [Header("挂载偏移 - 右手")]
    [Tooltip("右手武器旋转修正（欧拉角）")]
    public Vector3 localRotationOffset = new Vector3(0f, 0f, 180f);

    [Tooltip("右手武器位置修正")]
    public Vector3 localPositionOffset = new Vector3(-0.1f, 0.1f, 0f);

    [Header("挂载偏移 - 左手")]
    [Tooltip("左手武器旋转修正（欧拉角）。左右手骨骼是镜像的，通常需要不同的值")]
    public Vector3 leftRotationOffset = new Vector3(0f, 0f, 0f);

    [Tooltip("左手武器位置修正")]
    public Vector3 leftPositionOffset = new Vector3(0.1f, 0.1f, 0f);

    [Header("碰撞体设置")]
    [Tooltip("武器 BoxCollider 的中心偏移（相对于武器模型本地坐标）")]
    public Vector3 colliderCenter = new Vector3(0f, 0f, 0.5f);

    [Tooltip("武器 BoxCollider 的尺寸。在 Scene 视图中看绿色线框来调整")]
    public Vector3 colliderSize = new Vector3(0.3f, 0.3f, 1f);
}
