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
    [SerializeField] private GameObject primaryBulletPrefab;  // 普攻彈 Prefab (PrimaryBullet)
    [SerializeField] private GameObject utilityBulletPrefab;  // 功能彈 Prefab (UtilityBullet)
    [SerializeField] private Transform firePoint;            // 開火點 Transform
    [SerializeField] private float primaryFireRate = 0.15f;    // 普攻彈連射間隔時間 (秒)
    [SerializeField] private float utilityFireRate = 2.0f;     // 右鍵功能彈冷卻時間 (秒)
    [SerializeField] private GameObject formationPrefab;     // 陣式 Prefab
    private FormationArea activeFormation;                    // 當前場上的陣式實體
    private bool isPreparingFormation = false;                // 是否處於 R 鍵陣式預預瞄準狀態
    private float nextPrimaryFireTime = 0f;                  // 普攻彈下次可射擊時間點
    private float nextUtilityFireTime = 0f;                  // 功能彈下次可射擊時間點

    [Header("機體資源")]
    [SerializeField] private int maxHealCharges = 3;       // Q 鍵回血最大次數
    [SerializeField] private float healAmount = 30f;       // 每一次 Q 鍵恢復的血量（可自訂）
    private int currentHealCharges;

    [Header("主角血量設定")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    // 提供給 UI 讀取的公用屬性 (Property)
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

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
        currentHealCharges = maxHealCharges;
        currentHealth = maxHealth; // 初始化主角血量
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
                // 切換為收回狀態時重置陣式預備
                isPreparingFormation = false;
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
        if (partnerController != null && partnerController.IsDisabled)
        {
            // 夥伴停機時的限制邏輯
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
                PerformUpwardAttack();
            }
            else if (!isGrounded && Input.GetKey(KeyCode.S))
            {
                PerformDownwardAttack();
            }
            else
            {
                PerformNormalMeleeAttack();
            }
        }
    }

    private void PerformUpwardAttack()
    {
        float facingDir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(facingDir * 8f, jumpForce * 0.95f);
        attackMoveTimer = 0.15f;

        Debug.Log("<color=red>[Recalled] 觸發：W + 左鍵 上跳攻擊！（空中可再接二段跳）</color>");
        ExecuteHitDetection(1.2f, "上跳攻擊");
    }

    private void PerformDownwardAttack()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce * 1.4f);
        attackMoveTimer = 0.2f;

        Debug.Log("<color=red>[Recalled] 觸發：空中 S + 左鍵 下壓攻擊！</color>");
        ExecuteHitDetection(1.5f, "下壓攻擊");
    }

    private void PerformNormalMeleeAttack()
    {
        lastComboTime = Time.time;
        comboStep++;

        if (comboStep > 4) comboStep = 1;

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
        }
    }

    #endregion

    #region 【放出狀態】遠程彈藥與資源

    private void HandleDeployedRangedInputs()
    {
        // 1. 左鍵處理：部署陣式或普攻彈連射
        if (isPreparingFormation)
        {
            if (Input.GetMouseButtonDown(0))
            {
                DeployFormationToMousePosition();
            }
        }
        else
        {
            if (Input.GetMouseButton(0))
            {
                if (Time.time >= nextPrimaryFireTime)
                {
                    ShootPrimaryBullet();
                    nextPrimaryFireTime = Time.time + primaryFireRate;
                }
            }
        }

        // 2. 右鍵功能彈（自動鎖定夥伴方向發射，帶冷卻判斷）
        if (Input.GetMouseButtonDown(1))
        {
            if (Time.time >= nextUtilityFireTime)
            {
                ShootUtilityBulletToPartner();
                nextUtilityFireTime = Time.time + utilityFireRate;
            }
            else
            {
                Debug.Log($"<color=yellow>[Deployed] 功能彈冷卻中... 剩餘 {(nextUtilityFireTime - Time.time):F1} 秒</color>");
            }
        }

        // 3. R 鍵陣式彈
        if (Input.GetKeyDown(KeyCode.R))
        {
            HandleFormationKey();
        }
    }

    private void ShootPrimaryBullet()
    {
        if (primaryBulletPrefab == null || firePoint == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 fireDirection = (mousePos - firePoint.position).normalized;

        // 生成普攻彈 Prefab
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

        Debug.Log("<color=green>[Deployed] 左鍵：普攻彈發射！</color>");
    }

    private void ShootUtilityBulletToPartner()
    {
        //【修改點】：將檢查條件改為 utilityBulletPrefab
        if (utilityBulletPrefab == null || firePoint == null || partnerTransform == null) return;

        Vector2 directionToPartner = (partnerTransform.position - firePoint.position).normalized;

        //【修改點】：直接生成 utilityBulletPrefab
        GameObject bulletObj = Instantiate(utilityBulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            //【修改點】：調用基礎初始化即可（Prefab 上已設定好 Is Utility Bullet = true）
            bullet.Initialize(directionToPartner, GetComponent<Collider2D>());
        }

        // 觸發 Partner 的協同追擊
        if (partnerController != null)
        {
            partnerController.TriggerCoopAttack();
        }

        Debug.Log($"<color=cyan>[Deployed] 右鍵：功能彈！自動鎖定夥伴方向發射</color>");
    }

    private void HandleFormationKey()
    {
        if (activeFormation != null)
        {
            activeFormation.RecallFormation();
            activeFormation = null;
            isPreparingFormation = false;
            Debug.Log("<color=purple>[Deployed] R 鍵：手動收回場上陣式！</color>");
            return;
        }

        if (isPreparingFormation)
        {
            isPreparingFormation = false;
            Debug.Log("<color=yellow>[Deployed] R 鍵：取消陣式預備狀態</color>");
        }
        else
        {
            if (formationPrefab == null)
            {
                Debug.LogError("<color=red>[Deployed] 失敗：PlayerController 上的 Formation Prefab 未綁定！</color>");
                return;
            }
            isPreparingFormation = true;
            Debug.Log("<color=purple>[Deployed] R 鍵：拿出陣式彈！請點擊左鍵指定部署位置</color>");
        }
    }

    private void DeployFormationToMousePosition()
    {
        if (formationPrefab == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        GameObject obj = Instantiate(formationPrefab, mouseWorldPos, Quaternion.identity);
        activeFormation = obj.GetComponent<FormationArea>();

        isPreparingFormation = false;
        Debug.Log($"<color=purple>[Deployed] 左鍵：成功部署陣式於位置 {mouseWorldPos}！</color>");
    }

    private void UseHeal()
    {
        if (currentHealCharges > 0)
        {
            if (currentHealth >= maxHealth)
            {
                Debug.Log("<color=yellow>[Resource] 主角血量已滿，無法使用回血！</color>");
                return;
            }

            currentHealCharges--;
            currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);

            Debug.Log($"<color=green>[Resource] 主角回血 {healAmount} 點！當前血量：{currentHealth}/{maxHealth}，剩餘回血次數：{currentHealCharges}/{maxHealCharges}</color>");
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
        if (switchManager != null && switchManager.currentState == GameControlState.Recalled)
        {
            if (partnerController != null && !partnerController.IsDisabled)
            {
                partnerController.TakeDamage(damage);
                return;
            }
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"<color=red>[Player] 主角受到傷害：{damage}，剩餘血量：{currentHealth}/{maxHealth}</color>");
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