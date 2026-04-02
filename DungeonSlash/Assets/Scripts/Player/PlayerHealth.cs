using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit Feedback")]
    [SerializeField] private float knockbackForce = 3f;
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.white;

    private float currentHealth;
    private bool isDead;
    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private CharacterController characterController;
    private Renderer[] renderers;
    private Vector3 knockbackVelocity;

    // ============================================================
    // 🎓 材质实例化策略
    //
    //   MaterialPropertyBlock 在 URP + SRP Batcher 下不生效：
    //     SRP Batcher 把 CBUFFER(UnityPerMaterial) 里的属性缓存到
    //     GPU Constant Buffer 中，渲染时直接读 Constant Buffer，
    //     跳过 PropertyBlock → 颜色覆盖被忽略
    //
    //   所以闪白效果必须用 renderer.material（实例化材质）
    //
    //   优化策略：
    //     1. Awake 里主动访问 .material 触发实例化
    //        → 材质实例在初始化时就创建好，不会在战斗中意外分配
    //     2. 缓存 Material[] 引用，避免每次 .material 的查找开销
    //     3. 缓存原始颜色，恢复时直接赋值
    //
    //   🎓 为什么不用 sharedMaterial？
    //     sharedMaterial 修改会影响所有使用这个材质的物体
    //     → 一个敌人被打，所有同材质的敌人都会闪白！
    //     .material 返回的实例只属于这个物体，互不影响
    //
    //   🎓 面试说法：
    //     "URP 的 SRP Batcher 和 MaterialPropertyBlock 不兼容，
    //      因为 SRP Batcher 直接从 Constant Buffer 读材质属性，
    //      绕过了 PropertyBlock 的覆盖层。闪白效果只能通过材质
    //      实例化来实现，我们在 Awake 里主动实例化并缓存引用，
    //      把内存分配控制在初始化阶段。"
    // ============================================================
    private Material[] cachedMaterials;
    private PlayerState playerState;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        playerState = GetComponent<PlayerState>();

        // 🎓 缓存所有渲染器、材质实例和原始颜色
        //   访问 renderer.material 会自动创建材质实例
        //   这里主动在 Awake 触发，把分配集中在初始化阶段
        //   之后闪白/恢复只操作已缓存的引用，0 额外分配
        renderers = GetComponentsInChildren<Renderer>();
        cachedMaterials = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            cachedMaterials[i] = renderers[i].material;

            // 🎓 统一开启 _EMISSION keyword（同 EnemyBase）
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].EnableKeyword("_EMISSION");
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void Update()
    {
        // 应用击退
        if (knockbackVelocity.magnitude > 0.1f)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 10f * Time.deltaTime);
        }
    }

    public void TakeDamage(float damage, Vector3 knockbackDirection)
    {
        if (isDead) return;

        currentHealth -= damage;

        // 击退
        knockbackVelocity = knockbackDirection * knockbackForce;

        // 闪白反馈
        StartCoroutine(FlashCoroutine());

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
        else
        {
            playerAnimator.PlayHit();
            playerState.EnterHit(); // 🎓 通知状态管理器进入受击硬直
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        // 闪白：Emission 颜色设为强白色
        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", flashColor * 3f);
            }
        }

        yield return new WaitForSeconds(flashDuration);

        // 恢复：Emission 设回黑色
        for (int i = 0; i < cachedMaterials.Length; i++)
        {
            if (cachedMaterials[i].HasProperty("_EmissionColor"))
            {
                cachedMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }

    private void Die()
    {
        isDead = true;
        playerState.EnterDead(); // 🎓 永久锁定所有行动
        playerAnimator.PlayDeath();
        playerController.enabled = false;
        GetComponent<PlayerCombat>().enabled = false;

        // 支持两种 GameManager
        if (SimpleGameManager.Instance != null)
        {
            SimpleGameManager.Instance.OnPlayerDeath();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDeath();
        }
    }
}
