using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;

    [Header("Fall Death")]
    [SerializeField] private float fallDeathY = -20f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isMoving;
    private bool isJumping;
    private PlayerHealth playerHealth;

    // 供其他脚本查询状态
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
        HandleJumpInput();
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

    private void HandleJumpInput()
    {
        // 🎓 跳跃条件：在地面上 + 不在跳跃中
        // 去掉了 CD，只要落地了（isJumping == false）就能立刻再跳。
        // 用 isJumping 标志而不是 CD 计时器来防止连跳，
        // 因为跳跃的限制应该是"还没落地"，而不是"冷却没好"。
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded && !isJumping)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(gravity));
            isJumping = true;
        }

        // 🎓 落地检测：velocity.y <= 0 确保是在下落阶段才判定落地
        // 避免刚按跳跃时（还在地面但 velocity.y > 0）就立刻判定为落地
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
