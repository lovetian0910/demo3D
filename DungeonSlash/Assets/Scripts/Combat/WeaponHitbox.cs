using UnityEngine;

/// <summary>
/// 挂在武器碰撞体上，把碰撞事件转发给 PlayerCombat。
///
/// 🎓 为什么需要 Init 方法？
/// 武器是运行时 Instantiate 创建的。Unity 的执行顺序是：
///   Instantiate → Awake() → 你的代码继续执行 → SetParent()
/// 也就是说 Awake 执行时，武器还没有被挂到 Player 骨骼下，
/// GetComponentInParent<PlayerCombat>() 会找不到目标（返回 null）。
///
/// 解决方案：提供 Init() 方法，让 WeaponManager 在挂载完成后手动注入引用。
/// 同时保留 Awake 中的查找作为兜底（适用于在 Editor 中直接放置的情况）。
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    private PlayerCombat playerCombat;

    /// <summary>
    /// 由 WeaponManager 在实例化并挂载武器后调用，手动注入 PlayerCombat 引用。
    /// </summary>
    public void Init(PlayerCombat combat)
    {
        playerCombat = combat;
    }

    private void Awake()
    {
        // 兜底查找：如果武器是在 Editor 中直接放在骨骼下的，Awake 时能找到
        if (playerCombat == null)
        {
            playerCombat = GetComponentInParent<PlayerCombat>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            playerCombat?.OnWeaponHit(other);
        }
    }
}
