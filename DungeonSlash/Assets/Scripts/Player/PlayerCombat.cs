using UnityEngine;

/// <summary>
/// 玩家战斗系统。
///
/// 🎓 状态互斥：攻击前先查询 PlayerState.CanAttack，
/// 受击/死亡状态下不能发动攻击。攻击开始时通知 PlayerState 进入 Attacking，
/// 攻击结束时通知退出。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    private WeaponData currentWeaponData;
    private Collider leftWeaponCollider;
    private Collider rightWeaponCollider;
    private Collider activeCollider;

    private PlayerAnimator playerAnimator;
    private PlayerState playerState;
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
        playerState = GetComponent<PlayerState>();
    }

    public void EquipWeapon(WeaponData data, Collider leftCollider, Collider rightCollider)
    {
        currentWeaponData = data;
        leftWeaponCollider = leftCollider;
        rightWeaponCollider = rightCollider;

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

            // 🎓 受击打断攻击：如果在攻击过程中被打了，立刻取消攻击
            if (playerState.CurrentState == PlayerState.State.Hit ||
                playerState.CurrentState == PlayerState.State.Dead)
            {
                CancelAttack();
                return;
            }

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

        if (!HasWeapon) return;

        // 🎓 通过 PlayerState 统一检查是否可以攻击
        if (!playerState.CanAttack) return;

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
        activeCollider = leftWeaponCollider;
        playerState.EnterAttacking();
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
        activeCollider = rightWeaponCollider;
        playerState.EnterAttacking();
        playerAnimator.PlayHeavyAttack();
    }

    /// <summary>
    /// 被打断时取消当前攻击，关闭碰撞体。
    /// </summary>
    private void CancelAttack()
    {
        if (activeCollider != null)
        {
            activeCollider.enabled = false;
        }
        isAttacking = false;
        hitActive = false;
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
        playerState.ExitAttacking();
    }

    public void OnWeaponHit(Collider other)
    {
        DamageSystem.DealDamage(other, currentAttackDamage, transform.position);
    }
}
