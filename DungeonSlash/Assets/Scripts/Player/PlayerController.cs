using UnityEngine;

/// <summary>
/// 🎓 CharacterController.isGrounded 的坑：
/// isGrounded 只在上一帧 Move() 有向下接触地面时才为 true。
/// 跑步经过不平地面时，某些帧会短暂"悬空"导致 isGrounded = false，
/// 这一帧按空格就跳不起来——玩家觉得"吞输入"了。
///
/// 解决方案：Coyote Time（土狼时间）+ Input Buffer（输入缓冲）
/// - Coyote Time：离地后 0.15 秒内仍可跳跃
/// - Input Buffer：按空格后 0.15 秒内如果落地，自动执行跳跃
/// 两者配合可以大幅提升操控手感。大部分动作/平台游戏都有这两个机制。
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
    private float coyoteTimer;      // 离地后的宽容计时
    private float jumpBufferTimer;  // 按键后的缓冲计时
    private PlayerHealth playerHealth;

    public bool IsRolling => false;
    public bool IsMoving => isMoving;
    public bool IsJumping => isJumping;
    public bool IsGrounded => controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        if (transform.position.y < fallDeathY)
        {
            if (playerHealth != null && !playerHealth.IsDead)
            {
                playerHealth.TakeDamage(9999f, Vector3.zero);
            }
            return;
        }

        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void HandleMovement()
    {
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
        // ---- Coyote Time 计时 ----
        // 🎓 在地面上时持续重置计时器；离地后计时器开始倒计。
        // 只要计时器 > 0 就视为"可以跳"。
        if (controller.isGrounded && !isJumping)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }

        // ---- Input Buffer 计时 ----
        // 🎓 按下空格时记录，之后每帧递减。
        // 如果在缓冲时间内落地，自动触发跳跃——不"吞"玩家的输入。
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        // ---- 执行跳跃 ----
        // 条件：有缓冲的跳跃输入 + 在土狼时间内（视为在地面）+ 不在跳跃中
        if (jumpBufferTimer > 0f && coyoteTimer > 0f && !isJumping)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(gravity));
            isJumping = true;
            coyoteTimer = 0f;      // 用掉 coyote time，防止空中二段跳
            jumpBufferTimer = 0f;   // 消耗掉缓冲输入
        }

        // ---- 落地重置 ----
        if (isJumping && controller.isGrounded && velocity.y <= 0f)
        {
            isJumping = false;
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
