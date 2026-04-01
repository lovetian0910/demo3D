using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    [Header("Dodge Roll")]
    [SerializeField] private float rollSpeed = 12f;
    [SerializeField] private float rollDuration = 0.4f;
    [SerializeField] private float rollCooldown = 0.8f;

    private CharacterController controller;
    private Vector3 velocity;
    private float rollTimer;
    private float rollCooldownTimer;
    private Vector3 rollDirection;
    private bool isRolling;
    private bool isMoving;
    private PlayerHealth playerHealth;

    // 供其他脚本查询状态
    public bool IsRolling => isRolling;
    public bool IsMoving => isMoving;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead) return;

        if (isRolling)
        {
            UpdateRoll();
            return; // 翻滚时不接受其他输入
        }

        HandleMovement();
        HandleDodgeInput();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        // 获取输入（WASD 或方向键）
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 基于相机方向计算移动方向
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
            // 角色朝移动方向旋转
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // 移动
            controller.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    private void HandleDodgeInput()
    {
        rollCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space) && rollCooldownTimer <= 0f)
        {
            StartRoll();
        }
    }

    private void StartRoll()
    {
        isRolling = true;
        rollTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            Camera cam = Camera.main;
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
            rollDirection = (camForward * v + camRight * h).normalized;
        }
        else
        {
            rollDirection = transform.forward;
        }
    }

    private void UpdateRoll()
    {
        rollTimer -= Time.deltaTime;

        if (rollTimer <= 0f)
        {
            isRolling = false;
            return;
        }

        controller.Move(rollDirection * rollSpeed * Time.deltaTime);
        ApplyGravity();
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
