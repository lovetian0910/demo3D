using UnityEngine;

/// <summary>
/// 🎓 Bool vs Trigger 驱动动画：
/// - Bool（SetBool）：每帧持续设置，适合持续状态（跑步中 Speed > 0，落地中 IsGrounded = true）
/// - Trigger（SetTrigger）：只触发一次，自动重置，适合瞬发动作（攻击、跳跃）
///
/// 跳跃之前用 Bool 会出问题：如果 Animator 有 Any State → Jump 过渡，
/// Bool 为 true 期间每帧都会重新进入 Jump 状态，导致动画反复重播。
/// 改为 Trigger 后只触发一次，动画播完自然过渡回 Idle/Run。
///
/// 落地检测用 Bool（IsGrounded）而非 Trigger：落地是持续状态，需要每帧同步，
/// 确保落地瞬间立刻打断 Jump 动画，防止角色贴地却还在播跳跃动画导致滑行。
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;

    // Animator 参数名缓存为 hash，避免字符串比较开销
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int LightAttackHash = Animator.StringToHash("LightAttack");
    private static readonly int HeavyAttackHash = Animator.StringToHash("HeavyAttack");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DieHash = Animator.StringToHash("Die");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        float speed = playerController.IsMoving ? 1f : 0f;
        animator.SetFloat(SpeedHash, speed);

        // 🎓 每帧同步落地状态（Bool，持续状态）：
        // 落地瞬间 IsGrounded 变 true，Animator 立刻切出 Jump 状态，
        // 防止角色已落地但动画还在播跳跃导致的滑行感。
        animator.SetBool(IsGroundedHash, playerController.IsGrounded);
    }

    /// <summary>
    /// 供 PlayerController 在起跳瞬间调用一次。
    /// </summary>
    public void PlayJump()
    {
        animator.SetTrigger(JumpHash);
    }

    public void PlayLightAttack()
    {
        animator.SetTrigger(LightAttackHash);
    }

    public void PlayHeavyAttack()
    {
        animator.SetTrigger(HeavyAttackHash);
    }

    public void PlayHit()
    {
        animator.SetTrigger(HitHash);
    }

    public void PlayDeath()
    {
        animator.SetTrigger(DieHash);
    }

    /// <summary>
    /// 🎓 CrossFade vs SetTrigger:
    /// CrossFade can jump directly to any named State by string, regardless of
    /// Animator graph transitions. Ideal for data-driven cutscene animations where
    /// the state name comes from JSON. Transition duration 0.2f blends smoothly.
    /// </summary>
    public void PlayCutsceneAnim(string stateName)
    {
        if (string.IsNullOrEmpty(stateName))
        {
            Debug.LogWarning("[PlayerAnimator] PlayCutsceneAnim: stateName is null or empty.");
            return;
        }
        animator.CrossFade(stateName, 0.2f, 0); // layer 0
    }
}
