using UnityEngine;

/// <summary>
/// 玩家战斗系统。
///
/// 🎓 重构要点：伤害和冷却值不再硬编码在这里，
/// 而是通过 EquipWeapon() 从 WeaponData（ScriptableObject）读取。
/// 这就是「数据驱动设计」——改数值只需要改 ScriptableObject 资产，不用改代码。
///
/// weaponCollider 也改为运行时由 WeaponManager 设置，
/// 因为武器模型（包括碰撞体）是动态实例化的。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Weapon Hit Timing")]
    [SerializeField] private float hitActiveDelay = 0.15f;  // 攻击开始后多久激活碰撞
    [SerializeField] private float hitActiveDuration = 0.3f; // 碰撞持续多久

    // 当前武器数据（由 WeaponManager 通过 EquipWeapon 设置）
    private WeaponData currentWeaponData;
    private Collider weaponCollider;

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private bool isAttacking;
    private float hitTimer;
    private bool hitActive;

    public bool IsAttacking => isAttacking;

    /// <summary>
    /// 当前是否有武器装备。在没有武器时禁止攻击。
    /// </summary>
    public bool HasWeapon => currentWeaponData != null && weaponCollider != null;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// 由 WeaponManager 调用，装备新武器时更新战斗数据和碰撞体引用。
    ///
    /// 🎓 为什么 Collider 要从外部传入？
    /// 因为武器模型是运行时 Instantiate 的，碰撞体在武器 prefab 上，
    /// PlayerCombat 无法提前在 Inspector 中拖拽引用。
    /// </summary>
    public void EquipWeapon(WeaponData data, Collider collider)
    {
        currentWeaponData = data;
        weaponCollider = collider;

        // 确保碰撞体初始状态关闭
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }

        Debug.Log($">>> 装备武器: {data.weaponName} | 轻攻 {data.lightDamage} | 重攻 {data.heavyDamage}");
    }

    private void Update()
    {
        attackCooldownTimer -= Time.deltaTime;

        // 管理武器碰撞体的激活时机
        if (isAttacking)
        {
            hitTimer -= Time.deltaTime;

            if (hitTimer <= 0f && !hitActive)
            {
                OnAttackHitStart();
                hitActive = true;
                hitTimer = hitActiveDuration;
            }
            else if (hitActive && hitTimer <= 0f)
            {
                OnAttackHitEnd();
                hitActive = false;
            }

            return;
        }

        if (playerController.IsRolling)
        {
            return;
        }

        // 没有武器时不能攻击
        if (!HasWeapon)
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
        currentAttackDamage = currentWeaponData.lightDamage;
        attackCooldownTimer = currentWeaponData.lightAttackCooldown;
        hitTimer = hitActiveDelay;
        hitActive = false;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = currentWeaponData.heavyDamage;
        attackCooldownTimer = currentWeaponData.heavyAttackCooldown;
        hitTimer = hitActiveDelay;
        hitActive = false;
        playerAnimator.PlayHeavyAttack();
    }

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

    /// <summary>
    /// 由 WeaponHitbox 脚本调用，当武器碰到东西时
    /// </summary>
    public void OnWeaponHit(Collider other)
    {
        DamageSystem.DealDamage(other, currentAttackDamage, transform.position);
    }
}
