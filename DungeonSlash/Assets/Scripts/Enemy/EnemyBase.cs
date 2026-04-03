using UnityEngine;
using UnityEngine.AI;

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Hit,
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

    [Header("Attack Timing")]
    [Tooltip("攻击动画开始后多久执行伤害判定（对应挥刀帧）")]
    [SerializeField] protected float attackHitDelay = 0.4f;

    [Tooltip("攻击动画总时长（伤害判定后还要等动画播完才能下一次行动）")]
    [SerializeField] protected float attackAnimDuration = 0.8f;

    [Header("Hit Stun")]
    [Tooltip("受击硬直时长（秒），期间无法行动")]
    [SerializeField] private float hitStunDuration = 0.4f;

    [Header("Hit Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    [Header("Death")]
    [SerializeField] private GameObject deathEffectPrefab;

    [Tooltip("死亡动画播放时长，播完后才销毁")]
    [SerializeField] private float deathAnimDuration = 1.5f;

    protected float currentHealth;
    protected EnemyState currentState = EnemyState.Idle;
    protected NavMeshAgent agent;
    protected Transform playerTransform;
    protected Animator animator;

    protected float attackTimer;
    private float attackPhaseTimer;
    private bool attackHitDone;
    private float hitStunTimer;

    private Renderer[] renderers;
    private Material[] cachedMaterials;
    private bool isDead;
    public bool AIEnabled = true;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public bool IsDead => isDead;
    public float HealthPercent => currentHealth / maxHealth;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        currentHealth = maxHealth;
        animator = GetComponentInChildren<Animator>();

        renderers = GetComponentsInChildren<Renderer>();
        cachedMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            cachedMaterials[i] = renderers[i].material;
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].EnableKeyword("_EMISSION");
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
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
        if (!AIEnabled)
        {
            agent.isStopped = true;
            return;
        }

        attackTimer -= Time.deltaTime;
        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        switch (currentState)
        {
            case EnemyState.Idle:   UpdateIdle(distToPlayer);   break;
            case EnemyState.Chase:  UpdateChase(distToPlayer);  break;
            case EnemyState.Attack: UpdateAttack(distToPlayer); break;
            case EnemyState.Hit:    UpdateHit();                break;
        }

        if (animator != null)
            animator.SetFloat(SpeedHash, agent.velocity.magnitude);
    }

    private void UpdateIdle(float distToPlayer)
    {
        if (distToPlayer <= detectionRange)
        {
            currentState = EnemyState.Chase;
        }
    }

    private void UpdateChase(float distToPlayer)
    {
        // 🎓 stoppingDistance 让 NavMeshAgent 在到达攻击范围前就减速停下，
        // 而不是一直冲到玩家身上把玩家挤开。
        agent.isStopped = false;
        agent.stoppingDistance = attackRange * 0.8f;
        agent.SetDestination(playerTransform.position);

        if (distToPlayer <= attackRange && attackTimer <= 0f)
        {
            agent.ResetPath();
            StartAttack();
        }
        else if (distToPlayer > detectionRange * 1.5f)
        {
            agent.ResetPath();
            currentState = EnemyState.Idle;
            agent.isStopped = true;
        }
    }

    private void UpdateAttack(float distToPlayer)
    {
        Vector3 lookDir = (playerTransform.position - transform.position).normalized;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDir);
        }

        attackPhaseTimer -= Time.deltaTime;

        if (!attackHitDone && attackPhaseTimer <= 0f)
        {
            PerformAttack();
            attackHitDone = true;
            attackPhaseTimer = attackAnimDuration - attackHitDelay;
        }
        else if (attackHitDone && attackPhaseTimer <= 0f)
        {
            attackTimer = attackCooldown;
            agent.isStopped = false; // 恢复移动

            if (distToPlayer > detectionRange * 1.5f)
            {
                currentState = EnemyState.Idle;
                agent.isStopped = true;
            }
            else
            {
                currentState = EnemyState.Chase;
            }
        }
    }

    private void UpdateHit()
    {
        hitStunTimer -= Time.deltaTime;

        if (hitStunTimer <= 0f)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= detectionRange)
            {
                currentState = EnemyState.Chase;
                agent.isStopped = false;
            }
            else
            {
                currentState = EnemyState.Idle;
                agent.isStopped = false;
            }
        }
    }

    private void StartAttack()
    {
        currentState = EnemyState.Attack;
        attackPhaseTimer = attackHitDelay;
        attackHitDone = false;

        // 🎓 攻击时必须停下来！否则 NavMeshAgent 还会继续移动
        agent.ResetPath();
        agent.isStopped = true;

        if (animator != null)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    private void EnterHitState()
    {
        currentState = EnemyState.Hit;
        hitStunTimer = hitStunDuration;

        agent.ResetPath();
        agent.isStopped = true;

        if (animator != null)
        {
            animator.SetTrigger(HitHash);
        }
    }

    protected abstract void PerformAttack();

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($">>> 敌人受伤! 伤害: {damage}, 剩余血量: {currentHealth}/{maxHealth}");
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
        else
        {
            EnterHitState();
        }
    }

    protected virtual void Die()
    {
        isDead = true;
        currentState = EnemyState.Dead;

        agent.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (animator != null)
        {
            animator.SetTrigger(DieHash);
        }

        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
        }

        Destroy(gameObject, deathAnimDuration);
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", flashColor * 3f);
            }
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }
}
