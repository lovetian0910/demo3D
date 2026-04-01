using UnityEngine;

public static class DamageSystem
{
    /// <summary>
    /// 对碰撞体造成伤害。返回 true 表示成功命中了一个 IDamageable 对象。
    /// </summary>
    public static bool DealDamage(Collider target, float damage, Vector3 attackerPosition)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead)
        {
            return false;
        }

        Vector3 knockbackDir = (target.transform.position - attackerPosition).normalized;
        damageable.TakeDamage(damage, knockbackDir);
        return true;
    }
}
