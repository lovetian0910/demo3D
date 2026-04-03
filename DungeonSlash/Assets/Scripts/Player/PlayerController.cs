using UnityEngine;

/// <summary>
/// 🎓 Coyote Time + Input Buffer + 状态互斥
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Tooltip("离开地面后仍可跳跃的宽容时间（秒）")]
    [SerializeField] private float coyoteTime = 0.15f;

    [Tooltip("按下跳跃后的输入缓冲时间（秒）")]
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -20f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isMoving;
    private bool isJumping;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private PlayerHealth playerHealth;
    private PlayerAnimator playerAnimator;
    private PlayerState playerState;

    public bool InputEnabled = true;

    public bool IsRolling => false;
    public bool IsMoving => isMoving;
    public bool IsJumping => isJumping;
    public bool IsGrounded => controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
        playerAnimator = GetComponent<PlayerAnimator>();
        playerState = GetComponent<PlayerState>();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        if (transform.position.y < fallDeathY)
        {
            if (playerHealth != null && !playerHealth.IsDead)
                playerHealth.TakeDamage(9999f, Vector3.zero);
            return;
        }

        if (!InputEnabled)
        {
            ApplyGravity();
            return;
        }

        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // 🎓 受击/攻击/死亡时不能移动
        if (!playerState.CanMove)
        {
            isMoving = false;
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Camera cam = Camera.main;
        Vector3 camForward = cam.transform.forward;
        Vector3 camRight = cam.transform.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        isMoving = moveDir.magnitude > 0.1f;

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    private void HandleJump()
    {
        // Coyote Time
        if (controller.isGrounded && !isJumping)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // Input Buffer
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        // 🎓 执行跳跃：额外检查 PlayerState.CanJump
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isJumping && playerState.CanJump)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(gravity));
            isJumping = true;
            playerState.EnterJumping();
            playerAnimator.PlayJump();
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
        }

        // 落地重置
        if (isJumping && controller.isGrounded && velocity.y <= 0f)
        {
            isJumping = false;
            playerState.ExitJumping();
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
