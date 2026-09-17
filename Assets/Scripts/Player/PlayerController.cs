using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private SwitchManager switchManager;

    [Header("移動與跳躍參數")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;

    [Header("滑步 (Dash) 參數")]
    [SerializeField] private float dashSpeed = 16f;        // 滑步速度
    [SerializeField] private float dashDuration = 0.2f;     // 滑步持續時間 (秒)
    [SerializeField] private float dashCooldown = 0.8f;     // 滑步冷卻時間 (秒)

    [Header("地面檢測")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private bool canDoubleJump;                             // 是否可以使用二段跳

    // 滑步內部狀態
    private bool isDashing;
    private float dashTimer;
    private float nextDashTime;
    private float dashDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // 如果正在滑步中，更新計時器並暫停其他移動/跳躍判定
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
            return;
        }

        // 1. 滑鼠游標自動決定面向
        HandleFacing();

        // 2. 地面碰撞檢查
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
        }

        // 3. 著地時重置二段跳狀態
        if (isGrounded)
        {
            canDoubleJump = true;
        }

        // 4. 跳躍邏輯判斷 (支援 Recalled 狀態下的二段跳)
        if (Input.GetButtonDown("Jump"))
        {
            if (isGrounded)
            {
                // 地面上的一般起跳
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
            else if (canDoubleJump && switchManager != null && switchManager.currentState == GameControlState.Recalled)
            {
                // 只有在「收回狀態 (Recalled)」且空中仍有二段跳次數時觸發
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                canDoubleJump = false; // 消耗二段跳
                Debug.Log("<color=cyan>[Player] 觸發夥伴收回限定：二段跳！</color>");
            }
        }

        // 5. 觸發滑步 ( Shift + 按住 A 或 D )
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) && Time.time >= nextDashTime)
        {
            float moveInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                StartDash(moveInput);
            }
        }
    }

    private void FixedUpdate()
    {
        // 如果正在滑步，強制給予水平衝刺速度
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            return;
        }

        // 普通左右移動 (A / D)
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void StartDash(float direction)
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashDirection = Mathf.Sign(direction); // 取得方向 (+1 表示右, -1 表示左)
        nextDashTime = Time.time + dashCooldown;
    }

    private void HandleFacing()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (mousePosition.x > transform.position.x && !isFacingRight)
        {
            Flip();
        }
        else if (mousePosition.x < transform.position.x && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        }
    }
}