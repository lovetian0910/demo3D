using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

public abstract class EnemyBase : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    [SerializeField] protected float maxHealth = 30f;
    [SerializeField] protected float moveSpeed = 3.5f;
    [SerializeField] protected float detectionRange = 10f;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackCooldown = 1.5f;

    [Header("Hit Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    [Header("Death")]
    [SerializeField] private GameObject deathEffectPrefab;

    protected float currentHealth;
    protected EnemyState currentState = EnemyState.Idle;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected float attackTimer;
    private Renderer[] renderers;
    private Color[] originalColors;
    private bool isDead;

    public bool IsDead => isDead;
    public float HealthPercent => currentHealth / maxHealth;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        currentHealth = maxHealth;

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            else if (renderers[i].material.HasProperty("_Color"))
                originalColors[i] = renderers[i].material.color;
        }
    }

    protected virtual void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    protected virtual void Update()
    {
        if (isDead || playerTransform == null) return;

        attackTimer -= Time.deltaTime;
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                if (distToPlayer <= detectionRange)
                {
                    currentState = EnemyState.Chase;
                }
                break;

            case EnemyState.Chase:
                ChasePlayer(distToPlayer);
                break;

            case EnemyState.Attack:
                AttackUpdate(distToPlayer);
                break;
        }
    }

    private void ChasePlayer(float distance)
    {
        agent.SetDestination(playerTransform.position);

        if (distance <= attackRange)
        {
            agent.ResetPath();
            currentState = EnemyState.Attack;
        }
        else if (distance > detectionRange * 1.5f)
        {
            agent.ResetPath();
            currentState = EnemyState.Idle;
        }
    }

    private void AttackUpdate(float distance)
    {
        Vector3 lookDir = (playerTransform.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        if (distance > attackRange * 1.3f)
        {
            currentState = EnemyState.Chase;
            return;
        }

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    protected abstract void PerformAttack();

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        foreach (var r in renderers)
        {
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", flashColor);
            else if (r.material.HasProperty("_Color"))
                r.material.color = flashColor;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
                renderers[i].material.SetColor("_BaseColor", originalColors[i]);
            else if (renderers[i].material.HasProperty("_Color"))
                renderers[i].material.color = originalColors[i];
        }
    }

    protected virtual void Die()
    {
        isDead = true;
        currentState = EnemyState.Dead;
        agent.enabled = false;

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }

        Destroy(gameObject, 1f);
    }
}
