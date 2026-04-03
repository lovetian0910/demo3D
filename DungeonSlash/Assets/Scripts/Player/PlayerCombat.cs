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
    [Header("Attack Assist")]
    [Tooltip("攻击时自动面向范围内最近敌人的搜索半径。设为 0 关闭此功能")]
    [SerializeField] private float autoAimRadius = 5f;

    private WeaponData currentWeaponData;
    private Collider leftWeaponCollider;
    private Collider rightWeaponCollider;
    private Collider activeCollider;

    private PlayerAnimator playerAnimator;
    private PlayerState playerState;
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
        playerState = GetComponent<PlayerState>();
        playerController = GetComponent<PlayerController>();
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

        // 剧情演出时禁止攻击输入
        if (playerController != null && !playerController.InputEnabled) return;

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
        AutoAimToNearestEnemy();
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
        AutoAimToNearestEnemy();
        playerState.EnterAttacking();
        playerAnimator.PlayHeavyAttack();
    }

    /// <summary>
    /// 攻击瞬间自动面向最近的敌人。
    ///
    /// 🎓 攻击辅助瞄准（Attack Assist / Auto-Aim）：
    /// 俯视角动作游戏中，玩家按攻击时角色朝向可能不对准敌人，
    /// 导致砍空。自动面向最近敌人能大幅提升手感。
    /// 几乎所有动作游戏都有这个机制（暗黑破坏神、原神、塞尔达）。
    ///
    /// 用 Physics.OverlapSphere 在范围内搜索所有 Enemy tag 物体，
    /// 找到最近的那个，立刻转向它。
    /// </summary>
    private void AutoAimToNearestEnemy()
    {
        if (autoAimRadius <= 0f) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, autoAimRadius);
        float closestDist = float.MaxValue;
        Transform closestEnemy = null;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                // 跳过已死亡的敌人
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsDead) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestEnemy = hit.transform;
                }
            }
        }

        if (closestEnemy != null)
        {
            Vector3 dir = (closestEnemy.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }
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
