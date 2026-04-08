using UnityEngine;

/// <summary>
/// 玩家状态管理器——集中管理玩家当前处于什么状态，实现状态互斥。
///
/// 🎓 为什么需要集中管理？
/// 之前各脚本各管各的：PlayerCombat 只知道 isAttacking，PlayerHealth 只知道被打了，
/// PlayerController 只知道在跳。没人知道全局状态，导致：
/// - 被打中时还能攻击
/// - 攻击中还能跳
/// - 跳跃中还能攻击
///
/// 集中状态管理后，任何动作之前都先问 PlayerState："我现在能做这个吗？"
///
/// 🎓 面试说法：
/// "我们用了一个轻量的状态机来管理玩家动作互斥。每个状态有优先级——
/// 死亡 > 受击 > 攻击 > 跳跃 > 移动/待机。高优先级状态可以打断低优先级，
/// 但低优先级不能打断高优先级。这比在每个脚本里互相检查要清晰得多。"
/// </summary>
public class PlayerState : MonoBehaviour
{
    /// <summary>
    /// 玩家状态枚举，按优先级从低到高排列。
    /// 数值越大优先级越高，高优先级可以打断低优先级。
    /// </summary>
    public enum State
    {
        Normal = 0,     // 待机/移动，可以做任何事
        Jumping = 1,    // 跳跃中，不能攻击（可选）
        Attacking = 2,  // 攻击中，可以移动（上下半身分层）
        Hit = 3,        // 受击硬直中，什么都不能做
        Dead = 4        // 死亡，永久锁定
    }

    private State currentState = State.Normal;
    private float stateTimer; // 某些状态有持续时间（如受击硬直）

    [Header("状态时长")]
    [Tooltip("受击硬直时间（秒），Hit 动画播完前不能行动")]
    [SerializeField] private float hitStunDuration = 0.5f;

    /// <summary>当前状态，供所有脚本查询</summary>
    public State CurrentState => currentState;

    /// <summary>是否可以攻击：Normal / Jumping / Attacking 状态均可</summary>
    public bool CanAttack => currentState <= State.Attacking;

    /// <summary>是否可以跳跃：Normal / Jumping / Attacking 状态均可</summary>
    public bool CanJump => currentState <= State.Attacking;

    /// <summary>是否可以移动：Normal / Jumping / Attacking 状态均可</summary>
    public bool CanMove => currentState <= State.Attacking;

    private void Update()
    {
        // 有持续时间的状态自动倒计时恢复
        if (stateTimer > 0f)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                // 计时结束，回到 Normal（除非是 Dead）
                if (currentState != State.Dead)
                {
                    SetState(State.Normal);
                }
            }
        }
    }

    /// <summary>
    /// 进入攻击状态。由 PlayerCombat 调用。
    /// </summary>
    public void EnterAttacking()
    {
        if (currentState < State.Attacking)
        {
            SetState(State.Attacking);
            // 攻击状态由 PlayerCombat 手动结束（OnAttackHitEnd），不用计时器
        }
    }

    /// <summary>
    /// 攻击结束。由 PlayerCombat 调用。
    /// </summary>
    public void ExitAttacking()
    {
        if (currentState == State.Attacking)
        {
            SetState(State.Normal);
        }
    }

    /// <summary>
    /// 进入跳跃状态。由 PlayerController 调用。
    /// </summary>
    public void EnterJumping()
    {
        if (currentState < State.Jumping)
        {
            SetState(State.Jumping);
        }
    }

    /// <summary>
    /// 落地。由 PlayerController 调用。
    /// </summary>
    public void ExitJumping()
    {
        if (currentState == State.Jumping)
        {
            SetState(State.Normal);
        }
    }

    /// <summary>
    /// 进入受击硬直。由 PlayerHealth 调用。
    /// 🎓 受击可以打断攻击和跳跃（优先级更高），
    /// 并且有固定硬直时间，期间无法行动。
    /// </summary>
    public void EnterHit()
    {
        if (currentState < State.Dead)
        {
            SetState(State.Hit);
            stateTimer = hitStunDuration;
        }
    }

    /// <summary>
    /// 死亡。由 PlayerHealth 调用。永久锁定。
    /// </summary>
    public void EnterDead()
    {
        SetState(State.Dead);
    }

    private void SetState(State newState)
    {
        if (currentState != newState)
        {
            currentState = newState;
        }
    }
}
