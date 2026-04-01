using UnityEngine;

public class MeleeEnemy : EnemyBase
{
    [Header("Melee Attack")]
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeHitRadius = 1.5f;

    protected override void PerformAttack()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, meleeHitRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                DamageSystem.DealDamage(hit, meleeDamage, transform.position);
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeHitRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
