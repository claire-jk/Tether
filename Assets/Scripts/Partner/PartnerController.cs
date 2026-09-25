using System.Collections;
using UnityEngine;
using Tether.Core;
using Tether.Boss; // 引入 Boss 命名空間

public enum PartnerAIState
{
    FollowPlayer,   // 保底：跟隨主角
    ChaseTarget,    // 追蹤敵人/Boss
    AttackTarget    // 攻擊目標
}

public class PartnerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private Transform playerTransform; // 主角位置
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private Transform bossTransform;    // 指定 Boss 位置

    [Header("AI 追蹤與仇恨優先級設定 (企劃第 18 項)")]
    [SerializeField] private LayerMask enemyLayer;         // 敵人/Boss Layer
    [SerializeField] private LayerMask obstacleLayer;      // 牆壁/障礙物 Layer (檢測線段阻擋)
    [SerializeField] private float detectionRadius = 8f;   // 搜尋敵人範圍
    [SerializeField] private float attackRange = 1.5f;     // AI 攻擊距離
    [SerializeField] private float aiMoveSpeed = 5f;       // 一般移動速度
    [SerializeField] private float attackInterval = 1f;    // AI 自動攻擊間隔
    [SerializeField] private float attackDamage = 15f;     // 無 Hitbox 攻擊傷害值
    [SerializeField] private float maxUnreachableTime = 3f;// 3 秒打不到則下調優先級

    // AI 內部狀態控制
    private PartnerAIState currentState = PartnerAIState.FollowPlayer;
    private Transform currentTarget;
    private float targetUnreachableTimer = 0f;
    private float nextAttackTime = 0f;
    private Transform ignoredTarget;                      // 被下調優先級的暫時忽略目標
    private float ignoreTimer = 0f;

    [Header("追隨與懸浮設定")]
    [SerializeField] private Vector3 followOffset = new Vector3(-1.5f, 1f, 0f); // 懸浮偏移量
    [SerializeField] private float followSpeed = 8f;      // 平滑追隨速度

    [Header("夥伴機體移動參數 (企劃第 1 項)")]
    [SerializeField] private MovementData partnerMechGroundMove = new MovementData(12f, 70f, 90f, 140f); // 夥伴機體地面組
    [SerializeField] private MovementData partnerMechAirMove = new MovementData(12f, 35f, 15f, 70f);    // 夥伴機體空中組

    [Header("夥伴 NPC 移動參數 (企劃第 1 項)")]
    [SerializeField] private MovementData partnerNPCGroundMove = new MovementData(8f, 40f, 60f, 80f);    // 夥伴 NPC 地面組
    [SerializeField] private MovementData partnerNPCAirMove = new MovementData(8f, 20f, 10f, 40f);      // 夥伴 NPC 空中組

    [Header("登場與收回衝刺設定")]
    [Tooltip("剛被 E 鍵放出時衝向 Boss 的超高速")]
    [SerializeField] private float rushSpeed = 25f;        // 登場極速衝刺速度
    [Tooltip("登場衝刺的最長持續時間（秒）")]
    [SerializeField] private float rushDuration = 0.5f;     // 衝刺持續時間
    [SerializeField] private float recallSpeed = 30f;       // 收回時飛回主角的速度

    private float rushTimer = 0f;                           // 衝刺計時器
    private bool isRushing = false;                         // 是否正在衝刺狀態
    private bool isRecalling = false;                       // 是否正在高速飛回主角身邊

    // ==========================================
    // Parry 招架機制參數
    // ==========================================
    [Header("Parry 招架機制 (0.3s 視窗)")]
    [SerializeField] private float parryWindowDuration = 0.3f; // Parry 視窗長度
    private float parryTimer = 0f;
    private bool isParrying = false;

    [Header("血量與停機規則")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool isDisabled = false;
    private bool isInvincible = false; // 1f 無敵幀標籤

    private SpriteRenderer spriteRenderer;
    private Collider2D partnerCollider;
    private Rigidbody2D rb;
    private bool isFacingRight = true;
    private bool isAttacking = false; // 避免動態演出重複疊加

    // 提供外部讀取的屬性
    public bool IsDisabled => isDisabled;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsParrying => isParrying; // 外部可查詢是否處於 Parry 狀態

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        partnerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) playerTransform = p.transform;
        }
    }

    private void OnEnable()
    {
        TriggerRushToBoss();
    }

    public void TriggerRushToBoss()
    {
        if (bossTransform != null && !isDisabled)
        {
            isRushing = true;
            isRecalling = false;
            rushTimer = rushDuration;
            Debug.Log("<color=orange>[Partner AI] 登場！發動超高速突進衝向 Boss！</color>");
        }
    }

    public void StartRecalling()
    {
        if (isDisabled) return;

        isRecalling = true;
        isRushing = false;
        SetPartnerActive(true);

        // 重置 Rigidbody2D 物理狀態，避免重力和殘留速度阻礙飛回
        if (rb != null)
        {
            rb.gravityScale = 0f;             // 關閉重力
            rb.linearVelocity = Vector2.zero; // 清空物理速度
            rb.angularVelocity = 0f;          // 清空旋轉角速度
        }

        Debug.Log("<color=cyan>[Partner AI] 收到召回指令，清除物理慣性並飛回主角身邊...</color>");
    }

    public MovementData GetCurrentMovementData(bool isMechMode, bool isGrounded)
    {
        if (isMechMode)
        {
            return isGrounded ? partnerMechGroundMove : partnerMechAirMove;
        }
        else
        {
            return isGrounded ? partnerNPCGroundMove : partnerNPCAirMove;
        }
    }

    #region Parry 招架機制 (企劃第 15 項)

    public void TriggerParry()
    {
        if (CameraShakeAndTilt.Instance != null)
        {
            CameraShakeAndTilt.Instance.TriggerParryTilt(5f, 0.15f);
        }

        StartCoroutine(ParryHitboxFrameRoutine());
    }

    private IEnumerator ParryHitboxFrameRoutine()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Debug.Log("<color=cyan>[Parry] 格擋成功！畫面扭轉 5 度，暫時關閉 Hitbox...</color>");

        yield return new WaitForEndOfFrame();

        if (col != null) col.enabled = true;

        Debug.Log("<color=green>[Parry] 1f 已過，精確重新獲得 Hitbox！</color>");
    }

    public bool CheckParrySuccess(GameObject attacker, float incomingDamage)
    {
        if (isParrying)
        {
            isParrying = false;
            OnParrySuccess(attacker);
            return true;
        }
        return false;
    }

    private void OnParrySuccess(GameObject attacker)
    {
        Debug.Log("<color=green>★★ 精準 Parry 成功！完美彈開攻擊並造成打斷效果 ★★</color>");
    }

    #endregion

    private void Update()
    {
        // 1. Parry 視窗倒數
        if (isParrying)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0f)
            {
                isParrying = false;
                Debug.Log("<color=gray>[Parry] 招架視窗結束</color>");
            }
        }

        // 2. 被忽略的目標（優先級下調）計時冷卻（5秒後可重新嘗試追蹤）
        if (ignoredTarget != null)
        {
            ignoreTimer += Time.deltaTime;
            if (ignoreTimer >= 5f)
            {
                ignoredTarget = null;
                ignoreTimer = 0f;
            }
        }

        if (switchManager == null || playerTransform == null) return;

        if (isDisabled)
        {
            SetPartnerActive(false);
            return;
        }

        HandleFacing();

        // 3. 形態與 AI 狀態驅動
        if (switchManager.currentState == GameControlState.Deployed)
        {
            isRecalling = false;
            SetPartnerActive(true);

            // 更新 AI 敵人目標選擇與狀態機
            SearchAndSelectTarget();

            // 執行對應 AI 狀態
            switch (currentState)
            {
                case PartnerAIState.FollowPlayer:
                    FollowPlayer();
                    break;
                case PartnerAIState.ChaseTarget:
                    HandleChaseTargetAI();
                    break;
                case PartnerAIState.AttackTarget:
                    HandleAttackTargetAI();
                    break;
            }
        }
        else
        {
            isRushing = false;

            // 收回 (Recalled) 狀態下進行自然回血 (2%/f)
            HandleRecalledRegen();

            if (isRecalling)
            {
                HandleRecallMovement();
            }
            else
            {
                if (!isParrying)
                {
                    SetPartnerActive(false);
                }
                transform.position = playerTransform.position;
            }
        }
    }

    #region AI 仇恨與優先級搜尋 (企劃第 18 項)

    private void SearchAndSelectTarget()
    {
        // 優先對 Inspector 指定的 bossTransform 進行快速判定
        if (bossTransform != null && bossTransform != ignoredTarget)
        {
            float distToBoss = Vector2.Distance(transform.position, bossTransform.position);
            bool isBlocked = Physics2D.Linecast(transform.position, bossTransform.position, obstacleLayer);

            if (!isBlocked && distToBoss <= detectionRadius)
            {
                SetCurrentTarget(bossTransform);
                return;
            }
        }

        // 若無指定 Boss，透過 OverlapCircle 搜尋 Layer 內的敵人
        Collider2D[] enemies = Physics2D.OverlapCircleAll(transform.position, detectionRadius, enemyLayer);
        Transform bestTarget = null;
        float closestDistance = Mathf.Infinity;

        foreach (var col in enemies)
        {
            if (col.transform == ignoredTarget) continue;

            float dist = Vector2.Distance(transform.position, col.transform.position);
            bool isBlocked = Physics2D.Linecast(transform.position, col.transform.position, obstacleLayer);

            if (!isBlocked && dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = col.transform;
            }
        }

        if (bestTarget != null)
        {
            SetCurrentTarget(bestTarget);
        }
        else
        {
            // 最低優先級：完全無可追蹤敵人，降級至跟隨主角
            currentTarget = null;
            currentState = PartnerAIState.FollowPlayer;
        }
    }

    private void SetCurrentTarget(Transform target)
    {
        if (currentTarget != target)
        {
            currentTarget = target;
            targetUnreachableTimer = 0f; // 重置 3 秒追蹤計時器
        }

        float dist = Vector2.Distance(transform.position, currentTarget.position);
        currentState = (dist <= attackRange) ? PartnerAIState.AttackTarget : PartnerAIState.ChaseTarget;
    }

    #endregion

    #region AI 移動與無 Hitbox 攻擊 (企劃第 18 項)

    private void HandleChaseTargetAI()
    {
        if (isAttacking || currentTarget == null) return;

        // 登場極速衝刺（Rush）邏輯優先
        if (isRushing)
        {
            rushTimer -= Time.deltaTime;
            float rushDirX = Mathf.Sign(currentTarget.position.x - transform.position.x);

            if (rb != null) rb.linearVelocity = new Vector2(rushDirX * rushSpeed, rb.linearVelocity.y);
            else transform.position = Vector3.MoveTowards(transform.position, new Vector3(currentTarget.position.x, transform.position.y, transform.position.z), rushSpeed * Time.deltaTime);

            if (Vector2.Distance(transform.position, currentTarget.position) <= attackRange || rushTimer <= 0f)
            {
                isRushing = false;
            }
            return;
        }

        // 一般追蹤移動 (地面水平移動，保留 Y 軸重力，無跳躍)
        float directionX = Mathf.Sign(currentTarget.position.x - transform.position.x);
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(directionX * aiMoveSpeed, rb.linearVelocity.y);
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, new Vector3(currentTarget.position.x, transform.position.y, transform.position.z), aiMoveSpeed * Time.deltaTime);
        }

        // 3 秒無法打到/抵達下調機制
        targetUnreachableTimer += Time.deltaTime;
        if (targetUnreachableTimer >= maxUnreachableTime)
        {
            Debug.Log($"<color=yellow>[Partner AI] 追蹤 {currentTarget.name} 超過 3 秒打不到/無路徑，暫時下調該目標優先級！</color>");
            ignoredTarget = currentTarget;
            currentTarget = null;
            targetUnreachableTimer = 0f;
            currentState = PartnerAIState.FollowPlayer;
        }
    }

    private void HandleAttackTargetAI()
    {
        // 進入攻擊距離，停止水平移動
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (Time.time >= nextAttackTime)
        {
            ExecuteDirectDamageAttack();
            nextAttackTime = Time.time + attackInterval;
        }
    }

    /// <summary>
    /// 無 Hitbox 判定直接造成傷害的攻擊邏輯
    /// </summary>
    private void ExecuteDirectDamageAttack()
    {
        if (currentTarget == null) return;

        Debug.Log($"<color=cyan>[Partner AI] 執行無 Hitbox 直接攻擊！目標：{currentTarget.name}</color>");

        // 觸發前衝擊拳視覺動畫演出
        Vector3 attackDir = (currentTarget.position - transform.position).normalized;
        StartCoroutine(AttackPunchAnimation(attackDir * 0.6f, 0.15f));

        // 直接調用目標血量扣除 API (無須碰撞體實體)
        BossController boss = currentTarget.GetComponentInParent<BossController>();
        if (boss != null)
        {
            boss.TakeDamage(attackDamage);
            Debug.Log($"<color=red>[Partner AI] 對 Boss 造成 {attackDamage} 點無 Hitbox 攻擊傷害！</color>");
        }
    }

    public void TriggerCoopAttack()
    {
        if (switchManager.currentState == GameControlState.Deployed && !isDisabled)
        {
            Debug.Log("<color=cyan>[Partner AI] 響應主角射擊，進行協同追擊！</color>");

            Vector3 attackDir = currentTarget != null ?
                (currentTarget.position - transform.position).normalized :
                (isFacingRight ? Vector3.right : Vector3.left);

            StartCoroutine(AttackPunchAnimation(attackDir * 1.2f, 0.12f));

            // 協同攻擊直接判定傷害
            if (currentTarget != null)
            {
                BossController boss = currentTarget.GetComponentInParent<BossController>();
                if (boss != null) boss.TakeDamage(attackDamage * 0.5f);
            }
        }
    }

    private IEnumerator AttackPunchAnimation(Vector3 offset, float duration)
    {
        isAttacking = true;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + offset + new Vector3(0, 0.3f, 0);

        float elapsed = 0f;
        float forwardTime = duration * 0.4f;
        while (elapsed < forwardTime)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / forwardTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        float returnTime = duration * 0.6f;
        elapsed = 0f;
        while (elapsed < returnTime)
        {
            transform.position = Vector3.Lerp(targetPos, startPos, elapsed / returnTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = startPos;
        isAttacking = false;
    }

    #endregion

    #region 收回狀態自然回血與召回移動

    private void HandleRecalledRegen()
    {
        if (currentHealth < maxHealth && !isDisabled)
        {
            float regenAmount = maxHealth * 0.02f;
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenAmount);
        }
    }

    private void HandleRecallMovement()
    {
        Vector3 targetOffset = followOffset;
        if (playerTransform.localScale.x < 0)
        {
            targetOffset.x = -followOffset.x;
        }
        Vector3 targetPosition = playerTransform.position + targetOffset;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, recallSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.3f)
        {
            isRecalling = false;
            if (!isParrying) SetPartnerActive(false);
            transform.position = playerTransform.position;
            Debug.Log("<color=cyan>[Partner AI] 成功飛回主角身邊並收回！</color>");
        }
    }

    private void FollowPlayer()
    {
        if (isAttacking) return;

        Vector3 targetOffset = followOffset;
        if (playerTransform.localScale.x < 0)
        {
            targetOffset.x = -followOffset.x;
        }

        Vector3 targetPosition = playerTransform.position + targetOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
    }

    #endregion

    #region 血量管理、修復與停機機制

    public void RepairHealth(float amount)
    {
        if (isDisabled)
        {
            currentHealth += amount;
            Debug.Log($"<color=green>[Partner] 停機修復中... 當前進度：{currentHealth}/{maxHealth}</color>");

            if (currentHealth >= maxHealth)
            {
                currentHealth = maxHealth;
                isDisabled = false;
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
                Debug.Log("<color=green>[Partner] 修復完成，成功重新啟動！</color>");
            }
        }
        else
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            Debug.Log($"<color=green>[Partner] 獲得修復！當前血量：{currentHealth}/{maxHealth}</color>");
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDisabled || isInvincible) return;

        if (CheckParrySuccess(null, amount))
        {
            return;
        }

        // 1. 套用 10% 減傷
        float actualDamage = amount * 0.1f;
        currentHealth = Mathf.Max(0f, currentHealth - actualDamage);

        Debug.Log($"<color=yellow>[Partner] 扣除夥伴血量（已套用 10% 減傷）：{actualDamage}，剩餘：{currentHealth}/{maxHealth}</color>");

        // 2. 觸發 1 影格無敵幀
        StartCoroutine(TriggerInvincibilityFrame());

        if (currentHealth <= 0f)
        {
            DisablePartner();
        }
    }

    private IEnumerator TriggerInvincibilityFrame()
    {
        isInvincible = true;
        yield return new WaitForEndOfFrame();
        isInvincible = false;
    }

    private void DisablePartner()
    {
        isDisabled = true;
        currentHealth = 0f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.gray;
        }

        SetPartnerActive(false);

        if (switchManager != null && switchManager.currentState == GameControlState.Deployed)
        {
            switchManager.ToggleState();
        }

        Debug.Log("<color=red>[Partner] 血量歸零，夥伴停機！</color>");
    }

    #endregion

    #region 通用面向與顯示

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

    private void SetPartnerActive(bool active)
    {
        if (spriteRenderer != null && spriteRenderer.enabled != active)
        {
            spriteRenderer.enabled = active;
        }
        if (partnerCollider != null && partnerCollider.enabled != active)
        {
            partnerCollider.enabled = active;
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // 繪製 AI 檢測與攻擊視窗輔助圓圈
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}