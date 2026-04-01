using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    private Animator animator;
    private PlayerController playerController;

    // Animator 参数名缓存为 hash，避免字符串比较开销
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
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
        animator.SetBool(IsJumpingHash, playerController.IsJumping);
    }

    // 供 PlayerCombat 调用
    public void PlayLightAttack()
    {
        animator.SetTrigger(LightAttackHash);
    }

    public void PlayHeavyAttack()
    {
        animator.SetTrigger(HeavyAttackHash);
    }

    // 供 PlayerHealth 调用
    public void PlayHit()
    {
        animator.SetTrigger(HitHash);
    }

    public void PlayDeath()
    {
        animator.SetTrigger(DieHash);
    }
}
