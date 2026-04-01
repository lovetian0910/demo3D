using UnityEngine;

public class RangedEnemy : EnemyBase
{
    [Header("Ranged Attack")]
    [SerializeField] private float rangedDamage = 10f;
    [SerializeField] private float preferredDistance = 6f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;

    protected override void Awake()
    {
        base.Awake();
        attackRange = preferredDistance + 1f;
    }

    protected override void Update()
    {
        base.Update();

        if (currentState == EnemyState.Chase && playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist < preferredDistance * 0.7f)
            {
                Vector3 retreatDir = (transform.position - playerTransform.position).normalized;
                agent.SetDestination(transform.position + retreatDir * 2f);
            }
        }
    }

    protected override void PerformAttack()
    {
        if (projectilePrefab == null || firePoint == null) return;

        Vector3 dir = (playerTransform.position - firePoint.position).normalized;
        dir.y = 0;

        GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Projectile projectile = proj.GetComponent<Projectile>();
        projectile.Initialize(dir, rangedDamage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
