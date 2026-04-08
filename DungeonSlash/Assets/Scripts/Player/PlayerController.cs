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

    [Header("Ground Check")]
    [Tooltip("挂在角色脚底的空对象，用于地面球形检测")]
    [SerializeField] private Transform groundCheck;
    [Tooltip("球形检测半径，需略大于角色底部到地面的间距")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("地面所在的 Layer，防止检测到自身或敌人")]
    [SerializeField] private LayerMask groundLayer;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isMoving;
    private bool isJumping;
    private bool isGrounded;   // 🎓 用 CheckSphere 替代 controller.isGrounded，避免贴墙时误判
    private float coyoteTimer;
    private float jumpBufferTimer;
    private PlayerHealth playerHealth;
    private PlayerAnimator playerAnimator;
    private PlayerState playerState;

    private bool _inputEnabled = true;
    public bool InputEnabled
    {
        get => _inputEnabled;
        set => _inputEnabled = value;
    }

    public bool IsRolling => false;
    public bool IsMoving => isMoving;
    public bool IsJumping => isJumping;
    public bool IsGrounded => isGrounded;

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

        if (!_inputEnabled)
        {
            ApplyGravity();
            return;
        }

        UpdateGrounded();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void UpdateGrounded()
    {
        // 🎓 Physics.CheckSphere 在脚底打一个球形检测，只要球碰到 groundLayer 就算落地。
        // 比 controller.isGrounded 更可靠：后者依赖上一帧 Move() 的碰撞结果，
        // 贴墙移动时碰撞解算会把角色微微上推，导致 isGrounded 误报 false。
        isGrounded = groundCheck != null &&
                     Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void HandleMovement()
    {
        // 🎓 受击/死亡时不能移动；攻击时仍可移动（Animator Layer 分层，上半身播攻击、下半身继续跑）
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
        if (isGrounded && !isJumping)
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
        if (isJumping && isGrounded && velocity.y <= 0f)
        {
            isJumping = false;
            playerState.ExitJumping();
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
