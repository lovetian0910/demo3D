using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isMoving;
    private bool isJumping;
    private PlayerHealth playerHealth;

    // 供其他脚本查询状态
    public bool IsRolling => false; // 保留接口兼容，不再翻滚
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
        // 只有在地面上才能跳
        if (Input.GetKeyDown(KeyCode.Space) && controller.isGrounded)
        {
            // 物理公式: v = sqrt(2 * g * h)
            // 要跳到 jumpHeight 高度，需要的初速度
            velocity.y = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(gravity));
            isJumping = true;
        }

        // 落地后重置跳跃状态
        if (isJumping && controller.isGrounded && velocity.y <= 0f)
        {
            isJumping = false;
        }
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f; // 保持小的向下力确保 isGrounded 判定
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
