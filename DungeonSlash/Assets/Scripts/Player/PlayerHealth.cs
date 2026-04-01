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
    private Color[] originalColors;
    private Vector3 knockbackVelocity;

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

        // 缓存所有渲染器和原始颜色用于闪白效果
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
            {
                originalColors[i] = renderers[i].material.GetColor("_BaseColor");
            }
            else if (renderers[i].material.HasProperty("_Color"))
            {
                originalColors[i] = renderers[i].material.color;
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
        }
    }

    private System.Collections.IEnumerator FlashCoroutine()
    {
        // 设置闪白
        foreach (var renderer in renderers)
        {
            if (renderer.material.HasProperty("_BaseColor"))
            {
                renderer.material.SetColor("_BaseColor", flashColor);
            }
            else if (renderer.material.HasProperty("_Color"))
            {
                renderer.material.color = flashColor;
            }
        }

        yield return new WaitForSeconds(flashDuration);

        // 恢复原色
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].material.HasProperty("_BaseColor"))
            {
                renderers[i].material.SetColor("_BaseColor", originalColors[i]);
            }
            else if (renderers[i].material.HasProperty("_Color"))
            {
                renderers[i].material.color = originalColors[i];
            }
        }
    }

    private void Die()
    {
        isDead = true;
        playerAnimator.PlayDeath();
        playerController.enabled = false;
        GetComponent<PlayerCombat>().enabled = false;
    }
}
