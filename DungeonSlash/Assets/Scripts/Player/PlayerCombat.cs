using UnityEngine;

/// <summary>
/// 玩家战斗系统。
///
/// 🎓 重构要点：伤害、冷却、碰撞体激活时机全部从 WeaponData（ScriptableObject）读取。
/// 这就是「数据驱动设计」——改数值只需要改 ScriptableObject 资产，不用改代码。
///
/// 🎓 攻击判定时机（Active Frames）：
/// 动作游戏的攻击动画分为三个阶段：
///   1. Wind-up（蓄力）：抬手/蓄力，碰撞体关闭，玩家可被打断
///   2. Strike（打击）：下挥/出击，碰撞体激活，这时才能造成伤害
///   3. Recovery（收招）：收刀/恢复，碰撞体关闭，有短暂硬直
/// hitDelay 控制 Wind-up 的时长，hitDuration 控制 Strike 的时长。
/// 如果 hitDelay 太短，抬手就判定命中，体验会很差。
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    // 当前武器数据（由 WeaponManager 通过 EquipWeapon 设置）
    private WeaponData currentWeaponData;
    private Collider weaponCollider;

    private PlayerAnimator playerAnimator;
    private PlayerController playerController;
    private float attackCooldownTimer;
    private float currentAttackDamage;
    private float currentHitDuration; // 当前攻击的碰撞持续时间
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
                // Wind-up 阶段结束 → 进入 Strike 阶段，激活碰撞体
                OnAttackHitStart();
                hitActive = true;
                hitTimer = currentHitDuration;
            }
            else if (hitActive && hitTimer <= 0f)
            {
                // Strike 阶段结束 → 进入 Recovery 阶段，关闭碰撞体
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
        // 🎓 轻攻击用轻攻击的判定时机——蓄力短，出手快
        hitTimer = currentWeaponData.lightHitDelay;
        currentHitDuration = currentWeaponData.lightHitDuration;
        hitActive = false;
        playerAnimator.PlayLightAttack();
    }

    private void StartHeavyAttack()
    {
        isAttacking = true;
        currentAttackDamage = currentWeaponData.heavyDamage;
        attackCooldownTimer = currentWeaponData.heavyAttackCooldown;
        // 🎓 重攻击蓄力更久，但打击窗口也稍长——大开大合的感觉
        hitTimer = currentWeaponData.heavyHitDelay;
        currentHitDuration = currentWeaponData.heavyHitDuration;
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
