using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private PartnerController partnerController;
    [SerializeField] private Transform partnerTransform; // 用於右鍵功能彈鎖定夥伴方向

    [Header("基礎移動與跳躍")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float doubleJumpForce = 10f;  // 收回狀態夥伴噴射二段跳高度
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash 與 背身滑步")]
    [SerializeField] private float dashSpeed = 16f;        // 前衝 Dash 速度
    [SerializeField] private float chargeDashSpeed = 24f;  // 長蓄力衝刺速度
    [SerializeField] private float backstepSpeed = 10f;    // 背身滑步速度
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float chargeDashDuration = 0.35f; // 長蓄力衝刺持續時間
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private float chargeThreshold = 0.4f; // 按住 Shift 超過此時間轉換為長蓄力衝刺

    [Header("近戰棺槨連招系統")]
    [SerializeField] private Transform attackPoint;        // 攻擊判定產生點 (AttackPoint)
    [SerializeField] private float attackRange = 0.8f;      // 攻擊範圍半徑
    [SerializeField] private LayerMask enemyLayer;          // 敵人的 Layer
    [SerializeField] private float comboForwardImpulse = 2.5f; // 普通棺槨擊打時的小幅前衝力

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

    // Dash / 滑步 / 蓄力內部狀態
    private bool isDashing;
    private float dashTimer;
    private float nextDashTime;
    private float dashDirection;
    private float currentActiveDashSpeed;
    private float shiftHoldTimer;
    private bool isChargingDash;

    // 攻擊物理與連招內部狀態
    private float attackMoveTimer;                         // 上跳/下壓攻擊位移保護計時器
    private int comboStep = 0;                              // 當前連擊段數 (0~4)
    private float lastComboTime;                            // 上次攻擊時間
    private float comboResetDelay = 1.0f;                   // 連招重置等待時間

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealCharges = maxHealCharges; // 初始補滿回血次數
    }

    private void Update()
    {
        // 滑步/Dash 進行中暫停其他動作輸入
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
            rb.linearVelocity = new Vector2(dashDirection * currentActiveDashSpeed, 0f);
            return;
        }

        // 如果正在上跳/下壓攻擊位移中，暫停玩家按鍵對 X 軸速度的強制覆蓋
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
                // 收回狀態專屬：二段跳（夥伴噴射跳），需確認 Partner 未停機
                if (partnerController != null && !partnerController.IsDisabled)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
                    canDoubleJump = false;
                    Debug.Log("<color=cyan>[Recalled] 夥伴噴射二段跳發動！</color>");
                }
                else
                {
                    Debug.Log("<color=red>[Recalled] 夥伴已停機，無法使用噴射二段跳！</color>");
                }
            }
        }
    }

    private void TriggerDash(float moveInput, bool isCharged)
    {
        if (Time.time < nextDashTime) return;

        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        float facingDir = isFacingRight ? 1f : -1f;
        float inputDir = Mathf.Sign(moveInput);

        if (Mathf.Approximately(inputDir, facingDir))
        {
            // 同方向：前衝 Dash / 長蓄力 Dash
            dashDirection = facingDir;
            currentActiveDashSpeed = isCharged ? chargeDashSpeed : dashSpeed;
            dashTimer = isCharged ? chargeDashDuration : dashDuration;

            if (isCharged)
                Debug.Log("<color=red>[Dash] 長蓄力強烈前衝 Dash！</color>");
            else
                Debug.Log("<color=orange>[Dash] 前衝 Dash！</color>");
        }
        else
        {
            // 反方向：背身滑步 (較短位移)
            dashDirection = inputDir;
            currentActiveDashSpeed = backstepSpeed;
            dashTimer = dashDuration;
            Debug.Log("<color=yellow>[Dash] 背身滑步！</color>");
        }
    }

    #endregion

    #region 【收回狀態】近戰體術招式組

    private void HandleRecalledCombatInputs()
    {
        // 若夥伴停機，體術招式依然可進行本體攻擊，但輸出/位移效果受限
        if (partnerController != null && partnerController.IsDisabled)
        {
            // 可在此選擇性降低傷害或停用特殊噴射技巧
        }

        // 連招超時未按，自動重置回第一擊
        if (Time.time - lastComboTime > comboResetDelay)
        {
            comboStep = 0;
        }

        // 1. Dash / 長蓄力 Dash / 背身滑步 (A/D + Shift)
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            shiftHoldTimer += Time.deltaTime;
            if (shiftHoldTimer >= chargeThreshold && !isChargingDash)
            {
                isChargingDash = true;
                Debug.Log("<color=yellow>[Dash] 進入蓄力狀態...</color>");
            }
        }

        if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
        {
            float moveInput = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(moveInput) > 0.1f)
            {
                TriggerDash(moveInput, isChargingDash);
            }
            shiftHoldTimer = 0f;
            isChargingDash = false;
        }

        // 2. 招式組合判斷 (W/S/鍵盤 + 左鍵)
        if (Input.GetMouseButtonDown(0))
        {
            if (Input.GetKey(KeyCode.W))
            {
                // W + 左鍵：上跳攻擊（斜上前方，含位移，可銜接二段跳）
                PerformUpwardAttack();
            }
            else if (!isGrounded && Input.GetKey(KeyCode.S))
            {
                // 空中 S + 左鍵：下壓攻擊
                PerformDownwardAttack();
            }
            else
            {
                // 橫向普攻（棺槨四段擊，帶微幅前衝 DashA/D）
                PerformNormalMeleeAttack();
            }
        }
    }

    private void PerformUpwardAttack()
    {
        float facingDir = isFacingRight ? 1f : -1f;

        // 設定斜上方衝量
        rb.linearVelocity = new Vector2(facingDir * 8f, jumpForce * 0.95f);

        // 給予 0.15 秒物理保護展現斜上躍擊
        attackMoveTimer = 0.15f;

        Debug.Log("<color=red>[Recalled] 觸發：W + 左鍵 上跳攻擊！（空中可再接二段跳）</color>");
        ExecuteHitDetection(1.2f, "上跳攻擊");
    }

    private void PerformDownwardAttack()
    {
        // 給予向下快速壓制強烈位移
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce * 1.4f);
        attackMoveTimer = 0.2f;

        Debug.Log("<color=red>[Recalled] 觸發：空中 S + 左鍵 下壓攻擊！</color>");
        ExecuteHitDetection(1.5f, "下壓攻擊");
    }

    private void PerformNormalMeleeAttack()
    {
        lastComboTime = Time.time;
        comboStep++;

        // 限制最高為第 4 段，打完第 4 段後重置
        if (comboStep > 4) comboStep = 1;

        // 棺槨四段擊附帶微幅前沖（Dash A/D 概念）
        float facingDir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(facingDir * comboForwardImpulse * comboStep, rb.linearVelocity.y);

        Debug.Log($"<color=red>[Recalled] 棺槨四段擊：第 {comboStep} 擊！</color>");
        ExecuteHitDetection(1.0f + (comboStep * 0.2f), $"棺槨第 {comboStep} 擊");
    }

    private void ExecuteHitDetection(float damageMultiplier, string attackName)
    {
        if (attackPoint == null) return;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            Debug.Log($"<color=yellow>[Hit!] {attackName} 命中目標：{enemy.name} (倍率: {damageMultiplier})</color>");
            // 未來在此呼叫敵人的 TakeDamage()
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
            bullet.Initialize(fireDirection, GetComponent<Collider2D>());
        }

        // 觸發 Partner 的協同追擊
        if (partnerController != null)
        {
            partnerController.TriggerCoopAttack();
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
            bullet.Initialize(directionToPartner, GetComponent<Collider2D>());
        }

        // 觸發 Partner 的協同追擊
        if (partnerController != null)
        {
            partnerController.TriggerCoopAttack();
        }

        Debug.Log($"<color=green>[Deployed] 右鍵：功能彈！自動鎖定夥伴方向發射</color>");
    }

    private void ToggleFormationSkill()
    {
        if (activeFormation == null)
        {
            if (formationPrefab == null) return;

            GameObject obj = Instantiate(formationPrefab, transform.position, Quaternion.identity);
            activeFormation = obj.GetComponent<FormationArea>();
            Debug.Log("<color=purple>[Deployed] R 鍵：部署陣式！</color>");
        }
        else
        {
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

    #region 通用面向、承傷與 Gizmos

    public void TakeDamageFromEnemy(float damage)
    {
        // 當處於【收回狀態】且夥伴尚未停機時，由 Partner 代替承傷
        if (switchManager != null && switchManager.currentState == GameControlState.Recalled)
        {
            if (partnerController != null && !partnerController.IsDisabled)
            {
                partnerController.TakeDamage(damage);
                return;
            }
        }

        // 若 Partner 已停機，或在【放出狀態】，由主角本體承傷
        Debug.Log($"<color=red>[Player] 主角本體受到傷害：{damage}</color>");
    }

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
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, 0.2f);
        }

        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }

    #endregion
}