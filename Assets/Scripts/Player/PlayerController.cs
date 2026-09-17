using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private Transform partnerTransform; // 用於右鍵功能彈鎖定夥伴方向

    [Header("基礎移動與跳躍")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash 與 背身滑步")]
    [SerializeField] private float dashSpeed = 16f;        // 前衝 Dash 速度
    [SerializeField] private float backstepSpeed = 10f;    // 背身滑步速度
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.8f;

    [Header("近戰棺槨連招系統")]
    [SerializeField] private Transform attackPoint;        // 攻擊判定產生點 (AttackPoint)
    [SerializeField] private float attackRange = 0.8f;      // 攻擊範圍半徑
    [SerializeField] private LayerMask enemyLayer;          // 敵人的 Layer

    [Header("遠程彈藥系統")]
    [SerializeField] private GameObject primaryBulletPrefab;  // 普攻彈 Prefab
    [SerializeField] private Transform firePoint;            // 開火點 Transform
    [SerializeField] private GameObject formationPrefab; // 陣式 Prefab
    private FormationArea activeFormation;              // 當前場上的陣式實體

    [Header("機體資源")]
    [SerializeField] private int maxHealCharges = 3;       // Q 鍵回血最大次數
    private int currentHealCharges;

    // 內部物理與狀態變數
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private bool canDoubleJump;

    // Dash 內部狀態
    private bool isDashing;
    private float dashTimer;
    private float nextDashTime;
    private float dashDirection;

    // 攻擊物理與連招內部狀態
    private float attackMoveTimer;                         // 上跳攻擊位移保護計時器
    private int comboStep = 0;                              // 當前連擊段數 (0~3)
    private float lastComboTime;                             // 上次攻擊時間
    private float comboResetDelay = 1.0f;                   // 連招重置等待時間

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealCharges = maxHealCharges; // 初始補滿回血次數
    }

    private void Update()
    {
        // 滑步進行中暫停其他動作
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
            return;
        }

        // 1. 通用：面向始終由滑鼠游標位置決定
        HandleFacing();

        // 2. 地面檢測
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
        }

        if (isGrounded) canDoubleJump = true;

        // 3. 通用：跳躍與二段跳判定
        HandleJumping();

        // 4. 通用：Q 鍵主角回血
        if (Input.GetKeyDown(KeyCode.Q))
        {
            UseHeal();
        }

        // 5. 核心：根據 SwitchManager 狀態進行戰鬥輸入分流
        if (switchManager != null)
        {
            if (switchManager.currentState == GameControlState.Recalled)
            {
                // 【收回狀態】：近戰體術招式組
                HandleRecalledCombatInputs();
            }
            else
            {
                // 【放出狀態】：遠程彈藥系統
                HandleDeployedRangedInputs();
            }
        }
    }

    private void FixedUpdate()
    {
        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0f);
            return;
        }

        // 如果正在上跳攻擊位移中，暫停玩家按鍵對 X 軸速度的強制覆蓋
        if (attackMoveTimer > 0f)
        {
            attackMoveTimer -= Time.fixedDeltaTime;
            return;
        }

        // 標準 A / D 左右移動
        float moveInput = Input.GetAxisRaw("Horizontal");
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    #region 跳躍與 Dash 邏輯

    private void HandleJumping()
    {
        if (Input.GetButtonDown("Jump"))
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            }
            else if (canDoubleJump && switchManager != null && switchManager.currentState == GameControlState.Recalled)
            {
                // 收回狀態專屬：二段跳（噴射跳）
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                canDoubleJump = false;
                Debug.Log("<color=cyan>[Player] 觸發收回狀態：二段跳！</color>");
            }
        }
    }

    private void TriggerDash(float moveInput)
    {
        if (Time.time < nextDashTime) return;

        isDashing = true;
        dashTimer = dashDuration;
        nextDashTime = Time.time + dashCooldown;

        // 判斷玩家輸入方向與當前「面向」是否相同
        // isFacingRight == true 時面向右(+1)，否則面向左(-1)
        float facingDir = isFacingRight ? 1f : -1f;
        float inputDir = Mathf.Sign(moveInput);

        if (Mathf.Approximately(inputDir, facingDir))
        {
            // 同方向：面向方向衝刺 (Dash)
            dashDirection = facingDir;
            Debug.Log("<color=orange>[Dash] 前衝 Dash！</color>");
        }
        else
        {
            // 反方向：背身滑步 (Backstep)
            dashDirection = inputDir;
            Debug.Log("<color=yellow>[Dash] 背身滑步！</color>");
        }
    }

    #endregion

    #region 【收回狀態】近戰體術招式組

    private void HandleRecalledCombatInputs()
    {
        // 連招超時未按，自動重置回第一擊
        if (Time.time - lastComboTime > comboResetDelay)
        {
            comboStep = 0;
        }

        // 1. Dash 與 背身滑步 (A/D + Shift)
        if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)))
        {
            float moveInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                TriggerDash(moveInput);
            }
        }

        // 2. 招式組合判斷 (W/S/鍵盤 + 左鍵)
        if (Input.GetMouseButtonDown(0))
        {
            if (Input.GetKey(KeyCode.W))
            {
                // W + 左鍵：上跳攻擊（斜上前方，含位移）
                PerformUpwardAttack();
            }
            else if (!isGrounded && Input.GetKey(KeyCode.S))
            {
                // 空中 S + 左鍵：下壓攻擊
                PerformDownwardAttack();
            }
            else
            {
                // 橫向普攻（棺槨四段擊）
                PerformNormalMeleeAttack();
            }
        }
    }

    private void PerformUpwardAttack()
    {
        float facingDir = isFacingRight ? 1f : -1f;

        // 設定斜上方衝量 (加大 X 軸比例)
        rb.linearVelocity = new Vector2(facingDir * 8f, jumpForce * 0.9f);

        // 給予 0.15 秒的物理保護，讓斜上飛行的位移展現出來
        attackMoveTimer = 0.15f;

        Debug.Log("<color=red>[Recalled] 觸發：W + 左鍵 上跳攻擊！</color>");
    }

    private void PerformDownwardAttack()
    {
        // 給予向下的快速壓制位移
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce * 1.2f);
        Debug.Log("<color=red>[Recalled] 觸發：空中 S + 左鍵 下壓攻擊！</color>");
    }

    private void PerformNormalMeleeAttack()
    {
        lastComboTime = Time.time;
        comboStep++;

        // 限制最高為第 4 段，打完第 4 段後重置
        if (comboStep > 4) comboStep = 1;

        Debug.Log($"<color=red>[Recalled] 棺槨四段擊：第 {comboStep} 擊！</color>");

        // 發動物理攻擊判定
        if (attackPoint != null)
        {
            // 偵測攻擊範圍內的敵人 Collider
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);

            foreach (Collider2D enemy in hitEnemies)
            {
                Debug.Log($"<color=yellow>[Hit!] 棺槨第 {comboStep} 擊命中目標：{enemy.name}</color>");
                // 未來在此處呼叫敵人的 TakeDamage() 介面
            }
        }
    }

    #endregion

    #region 【放出狀態】遠程彈藥與資源

    private void HandleDeployedRangedInputs()
    {
        // 1. 左鍵單點與 Hold 自動連射
        if (Input.GetMouseButtonDown(0))
        {
            ShootPrimaryBulletSingle();
        }
        else if (Input.GetMouseButton(0))
        {
            // 連射邏輯預留
        }

        // 2. 右鍵功能彈（自動鎖定夥伴方向發射）
        if (Input.GetMouseButtonDown(1))
        {
            ShootUtilityBulletToPartner();
        }

        // 3. R 鍵陣式彈（預備/部署/取消）
        if (Input.GetKeyDown(KeyCode.R))
        {
            ToggleFormationSkill();
        }
    }

    private void ShootPrimaryBulletSingle()
    {
        if (primaryBulletPrefab == null || firePoint == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 fireDirection = (mousePos - firePoint.position).normalized;

        GameObject bulletObj = Instantiate(primaryBulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            // 傳入玩家自己的 Collider2D 進行物理忽略
            bullet.Initialize(fireDirection, GetComponent<Collider2D>());
        }

        Debug.Log("<color=green>[Deployed] 左鍵：實體普攻彈發射！</color>");
    }

    private void ShootUtilityBulletToPartner()
    {
        if (primaryBulletPrefab == null || firePoint == null || partnerTransform == null) return;

        Vector2 directionToPartner = (partnerTransform.position - firePoint.position).normalized;

        GameObject bulletObj = Instantiate(primaryBulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            // 傳入玩家自己的 Collider2D 進行物理忽略
            bullet.Initialize(directionToPartner, GetComponent<Collider2D>());
        }

        Debug.Log($"<color=green>[Deployed] 右鍵：功能彈！自動鎖定夥伴方向發射</color>");
    }

    private void ToggleFormationSkill()
    {
        // 如果場上沒有陣式 ➔ 部署新陣式
        if (activeFormation == null)
        {
            if (formationPrefab == null) return;

            GameObject obj = Instantiate(formationPrefab, transform.position, Quaternion.identity);
            activeFormation = obj.GetComponent<FormationArea>();
            Debug.Log("<color=purple>[Deployed] R 鍵：部署陣式！</color>");
        }
        else
        {
            // 如果場上已有陣式 ➔ 主動收回/發動
            activeFormation.RecallFormation();
            activeFormation = null;
            Debug.Log("<color=purple>[Deployed] R 鍵：手動收回陣式！</color>");
        }
    }

    private void UseHeal()
    {
        if (currentHealCharges > 0)
        {
            currentHealCharges--;
            Debug.Log($"<color=green>[Resource] 主角回血！剩餘次數：{currentHealCharges}/{maxHealCharges}</color>");
        }
        else
        {
            Debug.Log("<color=yellow>[Resource] 回血次數已用盡，請至休息點補充！</color>");
        }
    }

    #endregion

    #region 通用面向與 Gizmos

    private void HandleFacing()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if (mousePosition.x > transform.position.x && !isFacingRight) Flip();
        else if (mousePosition.x < transform.position.x && isFacingRight) Flip();
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
        // 地面檢測環
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        }

        // 近戰攻擊範圍環
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }

    #endregion
}