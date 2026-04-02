using UnityEngine;

/// <summary>
/// 玩家战斗系统。
///
/// 🎓 双手持武：
/// 左手武器对应轻攻击（鼠标左键），右手武器对应重攻击（鼠标右键）。
/// 每次攻击只激活对应手的碰撞体，另一只手保持关闭。
/// 这样即使双手都有武器模型，也只有正在出击的那只手能造成伤害。
///
/// 🎓 攻击判定时机（Active Frames）：
///   1. Wind-up（蓄力）：碰撞体关闭
///   2. Strike（打击）：碰撞体激活，造成伤害
///   3. Recovery（收招）：碰撞体关闭
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    // 当前武器数据（由 WeaponManager 通过 EquipWeapon 设置）
    private WeaponData currentWeaponData;
    private Collider leftWeaponCollider;   // 左手武器碰撞体（轻攻击用）
    private Collider rightWeaponCollider;  // 右手武器碰撞体（重攻击用）
    private Collider activeCollider;       // 当前攻击正在使用的碰撞体

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private float currentHitDuration;
    private bool isAttacking;
    private float hitTimer;
    private bool hitActive;

    public bool IsAttacking => isAttacking;

    public bool HasWeapon => currentWeaponData != null
        && leftWeaponCollider != null
        && rightWeaponCollider != null;

    private void Awake()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playerController = GetComponent<PlayerController>();
    }

    /// <summary>
    /// 由 WeaponManager 调用，装备新武器时更新双手碰撞体引用。
    ///
    /// 🎓 为什么分左右手？
    /// 轻攻击动画挥的是左手，重攻击动画挥的是右手。
    /// 如果两只手共用一个碰撞体，另一只手挥动时也会误判命中。
    /// 分开管理后，每次攻击只激活出击手的碰撞体，精确对应动画。
    /// </summary>
    public void EquipWeapon(WeaponData data, Collider leftCollider, Collider rightCollider)
    {
        currentWeaponData = data;
        leftWeaponCollider = leftCollider;
        rightWeaponCollider = rightCollider;

        // 确保碰撞体初始状态关闭
        if (leftWeaponCollider != null) leftWeaponCollider.enabled = false;
        if (rightWeaponCollider != null) rightWeaponCollider.enabled = false;

        Debug.Log($">>> 装备武器: {data.weaponName} | 轻攻(左手) {data.lightDamage} | 重攻(右手) {data.heavyDamage}");
    }

    private void Update()
    {
        attackCooldownTimer -= Time.deltaTime;

        if (isAttacking)
        {
            hitTimer -= Time.deltaTime;

            if (hitTimer <= 0f && !hitActive)
            {
                OnAttackHitStart();
                hitActive = true;
                hitTimer = currentHitDuration;
            }
            else if (hitActive && hitTimer <= 0f)
            {
                OnAttackHitEnd();
                hitActive = false;
            }

            return;
        }

        if (playerController.IsRolling) return;
        if (!HasWeapon) return;

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
        hitTimer = currentWeaponData.lightHitDelay;
        currentHitDuration = currentWeaponData.lightHitDuration;
        hitActive = false;
        // 🎓 轻攻击 → 激活左手武器碰撞体
        activeCollider = leftWeaponCollider;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = currentWeaponData.heavyDamage;
        attackCooldownTimer = currentWeaponData.heavyAttackCooldown;
        hitTimer = currentWeaponData.heavyHitDelay;
        currentHitDuration = currentWeaponData.heavyHitDuration;
        hitActive = false;
        // 🎓 重攻击 → 激活右手武器碰撞体
        activeCollider = rightWeaponCollider;
        playerAnimator.PlayHeavyAttack();
    }

    public void OnAttackHitStart()
    {
        if (activeCollider != null)
        {
            activeCollider.enabled = true;
        }
    }

    public void OnAttackHitEnd()
    {
        if (activeCollider != null)
        {
            activeCollider.enabled = false;
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
