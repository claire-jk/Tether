using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private PartnerController partnerController;
    [SerializeField] private Transform partnerTransform;

    [Header("基礎移動手感參數 (企劃第 1 項)")]
    [SerializeField] private MovementData playerGroundMove = new MovementData(10f, 60f, 80f, 120f); // 主角地面組
    [SerializeField] private MovementData playerAirMove = new MovementData(10f, 30f, 10f, 60f);   // 主角空中組

    [Header("跳躍手感參數 (土狼時間 & 緩衝)")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float doubleJumpForce = 10f;
    [SerializeField] private float coyoteTime = 0.1f;       // 土狼時間 (0.1s)
    [SerializeField] private float jumpBufferTime = 0.15f;   // 跳躍指令緩衝 (0.15s)
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Dash 與 背身滑步")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float chargeDashSpeed = 24f;
    [SerializeField] private float backstepSpeed = 10f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float chargeDashDuration = 0.35f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private float chargeThreshold = 0.4f;

    [Header("近戰棺槨連招系統")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRange = 0.8f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float comboForwardImpulse = 2.5f;

    [Header("遠程彈藥系統")]
    [SerializeField] private GameObject primaryBulletPrefab;
    [SerializeField] private GameObject utilityBulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float primaryFireRate = 0.15f;
    [SerializeField] private float utilityFireRate = 2.0f;
    [SerializeField] private GameObject formationPrefab;
    private FormationArea activeFormation;
    private bool isPreparingFormation = false;
    private float nextPrimaryFireTime = 0f;
    private float nextUtilityFireTime = 0f;

    [Header("機體資源與血量")]
    [SerializeField] private int maxHealCharges = 3;
    [SerializeField] private float maxHealth = 100f;

    [SerializeField] private float currentHealth;

    [Header("Parry & 投擲拋物線參數")]
    [SerializeField] private TrajectoryLine trajectoryLine; // 引用拋物線
    [SerializeField] private float throwForce = 18f;        // 投擲初始速度
    [SerializeField] private float partnerGravityScale = 1f; // 夥伴投擲時的重力係數
    [SerializeField] private KeyCode parryKey = KeyCode.F;   // Parry 按鍵定義 (預設 F 鍵)
    private bool isAimingThrow = false;                     // 記錄是否正在按住右鍵瞄準投擲

    // 血量與喝水狀態變數
    private int currentHealCharges;
    private bool isDead = false;
    private bool isHealing = false;                          // 是否正在喝水讀條中
    private Coroutine healCoroutine;                         // 喝水協程引用 (用於中斷)

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // 內部物理與狀態變數
    private Rigidbody2D rb;
    private bool isGrounded;
    private bool isFacingRight = true;
    private bool canDoubleJump;

    // 手感優化內部計時器
    private float coyoteTimeCounter;   // 土狼時間計時器
    private float jumpBufferCounter;   // 跳躍緩衝計時器

    // Dash 內部狀態
    private bool isDashing;
    private float dashTimer;
    private float nextDashTime;
    private float dashDirection;
    private float currentActiveDashSpeed;
    private float shiftHoldTimer;
    private bool isChargingDash;

    // 攻擊內部狀態
    private float attackMoveTimer;
    private int comboStep = 0;
    private float lastComboTime;
    private float comboResetDelay = 1.0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealCharges = maxHealCharges;
        currentHealth = maxHealth;
    }

    private void Update()
    {
        // 死亡時禁止所有操作
        if (isDead) return;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
            return;
        }

        // 1. 面向滑鼠
        HandleFacing();

        // 2. 地面檢測與「土狼時間」計時
        if (groundCheck != null)
        {
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);
        }

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime; // 站在地面時，刷新土狼時間
            canDoubleJump = true;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime; // 離地時開始倒數
        }

        // 3. 「跳躍指令緩衝」計時
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime; // 按下跳躍時，開啟緩衝窗口
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        // 4. 觸發跳躍邏輯
        HandleJumping();

        // 5. Q 鍵觸發喝水
        if (Input.GetKeyDown(KeyCode.Q))
        {
            TryStartHeal();
        }

        // 6. 喝水讀條期間的中斷檢測 (移動或攻擊中斷)
        if (isHealing)
        {
            if (Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Input.GetMouseButtonDown(0))
            {
                CancelHeal();
            }
        }

        // 7. 根據狀態分流戰鬥輸入
        if (switchManager != null)
        {
            if (switchManager.currentState == GameControlState.Recalled)
            {
                isPreparingFormation = false;
                HandleRecalledCombatInputs();
            }
            else
            {
                // 若切換為 Deployed，關閉投擲預覽線
                if (trajectoryLine != null && trajectoryLine.gameObject.activeSelf)
                {
                    trajectoryLine.HideLine();
                }
                isAimingThrow = false;
                HandleDeployedRangedInputs();
            }
        }

        // 如果在 Recalled 狀態且正在右鍵瞄準投擲，即時更新繪製拋物線
        if (isAimingThrow)
        {
            UpdateThrowTrajectory();
        }
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirection * currentActiveDashSpeed, 0f);
            return;
        }

        if (attackMoveTimer > 0f)
        {
            attackMoveTimer -= Time.fixedDeltaTime;
            return;
        }

        // 高級平滑移動物理 (Acceleration / Deceleration)
        HandleMovement();
    }

    #region 手感優化：平滑移動與跳躍處理

    private void HandleMovement()
    {
        float targetInput = Input.GetAxisRaw("Horizontal");

        // 根據在地面或空中，選擇對應的移動參數組
        MovementData currentMoveData = isGrounded ? playerGroundMove : playerAirMove;

        float targetSpeed = targetInput * currentMoveData.maxSpeed;
        float currentSpeedX = rb.linearVelocity.x;

        float accelRate;

        if (Mathf.Abs(targetInput) > 0.01f)
        {
            // 判斷是否正在反向轉向
            bool isTurning = (targetInput > 0 && currentSpeedX < 0) || (targetInput < 0 && currentSpeedX > 0);

            accelRate = isTurning ? currentMoveData.turnAcceleration : currentMoveData.acceleration;
        }
        else
        {
            // 放開按鍵時使用減速度
            accelRate = currentMoveData.deceleration;
        }

        float newSpeedX = Mathf.MoveTowards(currentSpeedX, targetSpeed, accelRate * Time.deltaTime);
        rb.linearVelocity = new Vector2(newSpeedX, rb.linearVelocity.y);
    }

    private void HandleJumping()
    {
        // 條件 1：跳躍緩衝內有輸入 (jumpBufferCounter > 0)
        // 條件 2：處於土狼時間許可範圍內 (coyoteTimeCounter > 0)
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;  // 消耗緩衝
            coyoteTimeCounter = 0f;  // 消耗土狼時間
        }
        // 二段跳（夥伴協助噴射跳）
        else if (jumpBufferCounter > 0f && canDoubleJump && switchManager != null && switchManager.currentState == GameControlState.Recalled)
        {
            if (partnerController != null && !partnerController.IsDisabled)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, doubleJumpForce);
                canDoubleJump = false;
                jumpBufferCounter = 0f;
                Debug.Log("<color=cyan>[Recalled] 夥伴噴射二段跳發動！</color>");
            }
        }
    }

    #endregion

    #region Dash 邏輯

    private void TriggerDash(float moveInput, bool isCharged)
    {
        if (Time.time < nextDashTime) return;

        isDashing = true;
        nextDashTime = Time.time + dashCooldown;

        float facingDir = isFacingRight ? 1f : -1f;
        float inputDir = Mathf.Sign(moveInput);

        if (Mathf.Approximately(inputDir, facingDir))
        {
            dashDirection = facingDir;
            currentActiveDashSpeed = isCharged ? chargeDashSpeed : dashSpeed;
            dashTimer = isCharged ? chargeDashDuration : dashDuration;
        }
        else
        {
            dashDirection = inputDir;
            currentActiveDashSpeed = backstepSpeed;
            dashTimer = dashDuration;
        }
    }

    #endregion

    #region 近戰招式組 (Recalled)

    private void HandleRecalledCombatInputs()
    {
        if (Time.time - lastComboTime > comboResetDelay)
        {
            comboStep = 0;
        }

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            shiftHoldTimer += Time.deltaTime;
            if (shiftHoldTimer >= chargeThreshold && !isChargingDash)
            {
                isChargingDash = true;
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

        // 近戰左鍵攻擊
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

        // Parry 按鍵觸發 (F 鍵)
        if (Input.GetKeyDown(parryKey))
        {
            PerformParry();
        }

        // 長按右鍵預覽投擲拋物線，放開時丟出夥伴
        if (Input.GetMouseButtonDown(1))
        {
            isAimingThrow = true;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            if (isAimingThrow)
            {
                ThrowPartner();
                isAimingThrow = false;
                if (trajectoryLine != null) trajectoryLine.HideLine();
            }
        }
    }

    private void PerformUpwardAttack()
    {
        float facingDir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(facingDir * 8f, jumpForce * 0.95f);
        attackMoveTimer = 0.15f;
        ExecuteHitDetection(1.2f, "上跳攻擊");
    }

    private void PerformDownwardAttack()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -jumpForce * 1.4f);
        attackMoveTimer = 0.2f;
        ExecuteHitDetection(1.5f, "下壓攻擊");
    }

    private void PerformNormalMeleeAttack()
    {
        lastComboTime = Time.time;
        comboStep++;

        if (comboStep > 4) comboStep = 1;

        float facingDir = isFacingRight ? 1f : -1f;
        rb.linearVelocity = new Vector2(facingDir * comboForwardImpulse * comboStep, rb.linearVelocity.y);
        ExecuteHitDetection(1.0f + (comboStep * 0.2f), $"棺槨第 {comboStep} 擊");
    }

    private void ExecuteHitDetection(float damageMultiplier, string attackName)
    {
        if (attackPoint == null) return;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, enemyLayer);
        foreach (Collider2D enemy in hitEnemies)
        {
            Debug.Log($"<color=yellow>[Hit!] {attackName} 命中目標：{enemy.name}</color>");
        }
    }

    private void PerformParry()
    {
        if (partnerController != null && !partnerController.IsDisabled)
        {
            // 呼叫夥伴/系統的 Parry 邏輯 (含相機 5 度扭轉與 Hitbox 1f 恢復)
            partnerController.TriggerParry();

            // 亦可同時觸發相機扭轉
            if (Tether.Core.CameraShakeAndTilt.Instance != null)
            {
                Tether.Core.CameraShakeAndTilt.Instance.TriggerParryTilt(5f, 0.15f);
            }

            Debug.Log("<color=cyan>[Player] 發動招架 (Parry)！</color>");
        }
    }

    private void UpdateThrowTrajectory()
    {
        if (trajectoryLine == null) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        Vector3 startPos = transform.position;
        Vector2 throwDirection = (mouseWorldPos - startPos).normalized;
        Vector2 startingVelocity = throwDirection * throwForce;

        trajectoryLine.RenderLine(startPos, startingVelocity, partnerGravityScale);
    }

    private void ThrowPartner()
    {
        if (switchManager == null || partnerController == null || partnerController.IsDisabled) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;

        Vector2 throwDirection = (mouseWorldPos - transform.position).normalized;
        Vector2 throwVelocity = throwDirection * throwForce;

        // 1. 將 SwitchManager 狀態切換為 Deployed (放出夥伴)
        switchManager.ToggleState();

        // 2. 設定夥伴位置並施加拋物線物理力量
        partnerTransform.position = transform.position;
        Rigidbody2D partnerRb = partnerTransform.GetComponent<Rigidbody2D>();
        if (partnerRb != null)
        {
            partnerRb.gravityScale = partnerGravityScale;
            partnerRb.linearVelocity = throwVelocity;
        }

        Debug.Log("<color=orange>[Player] 沿著拋物線投擲出夥伴！</color>");
    }

    #endregion

    #region 遠程與資源 (Deployed)

    private void HandleDeployedRangedInputs()
    {
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

        if (Input.GetMouseButtonDown(1))
        {
            if (Time.time >= nextUtilityFireTime)
            {
                ShootUtilityBulletToPartner();
                nextUtilityFireTime = Time.time + utilityFireRate;
            }
        }

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

        GameObject bulletObj = Instantiate(primaryBulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null) bullet.Initialize(fireDirection, GetComponent<Collider2D>());

        if (partnerController != null) partnerController.TriggerCoopAttack();
    }

    private void ShootUtilityBulletToPartner()
    {
        if (utilityBulletPrefab == null || firePoint == null || partnerTransform == null) return;
        Vector2 directionToPartner = (partnerTransform.position - firePoint.position).normalized;

        GameObject bulletObj = Instantiate(utilityBulletPrefab, firePoint.position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null) bullet.Initialize(directionToPartner, GetComponent<Collider2D>());

        if (partnerController != null) partnerController.TriggerCoopAttack();
    }

    private void HandleFormationKey()
    {
        if (activeFormation != null)
        {
            activeFormation.RecallFormation();
            activeFormation = null;
            isPreparingFormation = false;
            return;
        }

        if (isPreparingFormation)
        {
            isPreparingFormation = false;
        }
        else
        {
            if (formationPrefab == null) return;
            isPreparingFormation = true;
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
    }

    #endregion

    #region 受到攻擊、死亡與喝水自我治療

    /// <summary>
    /// 敵人命中主角時呼叫此方法
    /// </summary>
    public void TakeDamageFromEnemy(float damage)
    {
        if (isDead) return;

        // 受到攻擊時中斷喝水
        if (isHealing)
        {
            CancelHeal();
        }

        // 直接扣除主角本身的血量
        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"<color=orange>[Player] 主角受到 {damage} 點傷害，剩餘血量：{currentHealth}</color>");

        // 判定死亡
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("<color=red>[Player] 主角血量歸零，觸發死亡！</color>");
        // TODO: 可在此處加入死亡動畫、音效或 Game Over 畫面觸發
    }

    private void TryStartHeal()
    {
        if (currentHealCharges <= 0)
        {
            Debug.Log("[Player] 治療藥水已用盡！");
            return;
        }

        if (currentHealth >= maxHealth)
        {
            Debug.Log("[Player] 血量已滿，無需治療！");
            return;
        }

        if (isHealing) return; // 已在讀條中

        healCoroutine = StartCoroutine(HealRoutine());
    }

    private IEnumerator HealRoutine()
    {
        isHealing = true;
        Debug.Log("[Player] 開始喝水讀條 (2 秒)...");

        // 等待 2 秒鐘
        float healDuration = 2.0f;
        float timer = 0f;

        while (timer < healDuration)
        {
            timer += Time.deltaTime;
            yield return null; // 每影格持續檢查與等待
        }

        // 讀條順利完成，扣除次數並恢復 40% 最大生命值
        currentHealCharges--;
        float actualHealAmount = maxHealth * 0.4f;
        currentHealth = Mathf.Min(maxHealth, currentHealth + actualHealAmount);

        Debug.Log($"<color=green>[Player] 喝水完成！恢復 {actualHealAmount} HP，當前血量：{currentHealth}，剩餘藥水：{currentHealCharges}</color>");

        isHealing = false;
        healCoroutine = null;
    }

    private void CancelHeal()
    {
        if (healCoroutine != null)
        {
            StopCoroutine(healCoroutine);
            healCoroutine = null;
        }
        isHealing = false;
        Debug.Log("<color=yellow>[Player] 喝水動作被中斷！</color>");
    }

    #endregion

    #region 通用與 Gizmos

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