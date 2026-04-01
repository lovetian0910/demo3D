using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("Light Attack")]
    [SerializeField] private float lightDamage = 10f;
    [SerializeField] private float lightAttackCooldown = 0.5f;

    [Header("Heavy Attack")]
    [SerializeField] private float heavyDamage = 25f;
    [SerializeField] private float heavyAttackCooldown = 1.2f;

    [Header("Weapon Collider")]
    [SerializeField] private Collider weaponCollider;

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private bool isAttacking;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();

        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    private void Update()
    {
        attackCooldownTimer -= Time.deltaTime;

        if (playerController.IsRolling || isAttacking)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && attackCooldownTimer <= 0f)
        {
            StartLightAttack();
        }
        else if (Input.GetMouseButtonDown(1) && attackCooldownTimer <= 0f)
        {
            StartHeavyAttack();
        }
    }

    private void StartLightAttack()
    {
        isAttacking = true;
        currentAttackDamage = lightDamage;
        attackCooldownTimer = lightAttackCooldown;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = heavyDamage;
        attackCooldownTimer = heavyAttackCooldown;
        playerAnimator.PlayHeavyAttack();
    }

    // —— 以下两个方法由 Animation Event 调用 ——

    public void OnAttackHitStart()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }
    }

    public void OnAttackHitEnd()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
        isAttacking = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            DamageSystem.DealDamage(other, currentAttackDamage, transform.position);
        }
    }
}
