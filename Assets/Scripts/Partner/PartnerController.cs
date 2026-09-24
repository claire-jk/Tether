using System.Collections;
using UnityEngine;

public class PartnerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private Transform playerTransform; // 主角位置
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private Transform bossTransform;   // 未來指定 Boss 位置

    [Header("追隨與 AI 移動設定")]
    [SerializeField] private Vector3 followOffset = new Vector3(-1.5f, 1f, 0f); // 懸浮偏移量
    [SerializeField] private float followSpeed = 8f;     // 平滑追隨速度
    [SerializeField] private float aiMoveSpeed = 5f;     // 一般接近 Boss 速度
    [SerializeField] private float attackRange = 1.5f;   // AI 攻擊距離
    [SerializeField] private float attackInterval = 1f;  // AI 自動攻擊間隔
    private float nextAttackTime;

    [Header("登場與收回衝刺設定")]
    [Tooltip("剛被 E 鍵放出時衝向 Boss 的超高速")]
    [SerializeField] private float rushSpeed = 25f;        // 登場極速衝刺速度
    [Tooltip("登場衝刺的最長持續時間（秒）")]
    [SerializeField] private float rushDuration = 0.5f;     // 衝刺持續時間
    [SerializeField] private float recallSpeed = 30f;       // 收回時飛回主角的速度

    private float rushTimer = 0f;                           // 衝刺計時器
    private bool isRushing = false;                        // 是否正在衝刺狀態
    private bool isRecalling = false;                      // 是否正在高速飛回主角身邊

    // ==========================================
    // Parry 招架機制參數
    // ==========================================
    [Header("Parry 招架機制 (0.3s 視窗)")]
    [SerializeField] private float parryWindowDuration = 0.3f; // Parry 視窗長度
    private float parryTimer = 0f;
    private bool isParrying = false;

    [Header("血量與停機規則")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;
    private bool isDisabled = false; // 是否停機

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

    /// <summary>
    /// 【關鍵修復】：當按下 E 鍵召回時，清除拋物線投擲留下的物理重力與慣性
    /// </summary>
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

    /// <summary>
    /// 觸發 0.3 秒精準 Parry 狀態
    /// </summary>
    public void TriggerParry()
    {
        if (isDisabled) return;

        isParrying = true;
        parryTimer = parryWindowDuration;
        Debug.Log("<color=cyan>[Parry] 觸發 0.3 秒精準 Parry 視窗！</color>");
    }

    /// <summary>
    /// 當受到傷害時檢查是否觸發 Parry 成功
    /// </summary>
    public bool CheckParrySuccess(GameObject attacker, float incomingDamage)
    {
        if (isParrying)
        {
            isParrying = false; // 成功 Parry 後立即消耗視窗
            OnParrySuccess(attacker);
            return true;
        }
        return false;
    }

    private void OnParrySuccess(GameObject attacker)
    {
        Debug.Log("<color=green>★★ 精準 Parry 成功！完美彈開攻擊並造成打斷效果 ★★</color>");
    }

    private void Update()
    {
        // Parry 視窗倒數計時
        if (isParrying)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer <= 0f)
            {
                isParrying = false;
                Debug.Log("<color=gray>[Parry] 招架視窗結束</color>");
            }
        }

        if (switchManager == null || playerTransform == null) return;

        if (isDisabled)
        {
            SetPartnerActive(false);
            return;
        }

        HandleFacing();

        if (switchManager.currentState == GameControlState.Deployed)
        {
            isRecalling = false;
            SetPartnerActive(true);

            if (bossTransform != null)
            {
                HandleBossAttackAI();
            }
            else
            {
                FollowPlayer();
            }
        }
        else
        {
            isRushing = false;

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

    private void HandleRecallMovement()
    {
        Vector3 targetOffset = followOffset;
        if (playerTransform.localScale.x < 0)
        {
            targetOffset.x = -followOffset.x;
        }
        Vector3 targetPosition = playerTransform.position + targetOffset;

        // 確保召回期間不會受到任何物理加速度干擾
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, recallSpeed * Time.deltaTime);

        // 飛回距離判定（距離小於 0.3f 即視為成功到達）
        if (Vector3.Distance(transform.position, targetPosition) < 0.3f)
        {
            isRecalling = false;
            if (!isParrying) SetPartnerActive(false);
            transform.position = playerTransform.position;
            Debug.Log("<color=cyan>[Partner AI] 成功飛回主角身邊並收回！</color>");
        }
    }

    #region 放出狀態 AI 邏輯

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

    private void HandleBossAttackAI()
    {
        float distanceX = Mathf.Abs(transform.position.x - bossTransform.position.x);

        if (isRushing)
        {
            rushTimer -= Time.deltaTime;
            float directionX = Mathf.Sign(bossTransform.position.x - transform.position.x);

            if (rb != null)
            {
                rb.linearVelocity = new Vector2(directionX * rushSpeed, rb.linearVelocity.y);
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(bossTransform.position.x, transform.position.y, transform.position.z),
                    rushSpeed * Time.deltaTime
                );
            }

            if (distanceX <= attackRange || rushTimer <= 0f)
            {
                isRushing = false;
            }

            return;
        }

        if (distanceX > attackRange)
        {
            if (isAttacking) return;

            float directionX = Mathf.Sign(bossTransform.position.x - transform.position.x);
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(directionX * aiMoveSpeed, rb.linearVelocity.y);
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    new Vector3(bossTransform.position.x, transform.position.y, transform.position.z),
                    aiMoveSpeed * Time.deltaTime
                );
            }
        }
        else
        {
            if (rb != null) rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);

            if (Time.time >= nextAttackTime)
            {
                AutoAttackBoss();
                nextAttackTime = Time.time + attackInterval;
            }
        }
    }

    private void AutoAttackBoss()
    {
        Debug.Log("<color=cyan>[Partner AI] 自動攻擊 Boss！（無體術傷害）</color>");

        if (bossTransform != null)
        {
            Vector3 attackDir = (bossTransform.position - transform.position).normalized;
            StartCoroutine(AttackPunchAnimation(attackDir * 0.6f, 0.15f));
        }
    }

    public void TriggerCoopAttack()
    {
        if (switchManager.currentState == GameControlState.Deployed && !isDisabled)
        {
            Debug.Log("<color=cyan>[Partner AI] 響應主角射擊，進行協同追擊！</color>");

            Vector3 attackDir = bossTransform != null ?
                (bossTransform.position - transform.position).normalized :
                (isFacingRight ? Vector3.right : Vector3.left);

            StartCoroutine(AttackPunchAnimation(attackDir * 1.2f, 0.12f));
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
        if (isDisabled) return;

        if (CheckParrySuccess(null, amount))
        {
            return;
        }

        currentHealth -= amount;
        Debug.Log($"<color=yellow>[Partner] 扣除夥伴血量：{amount}，剩餘：{currentHealth}/{maxHealth}</color>");

        if (currentHealth <= 0f)
        {
            DisablePartner();
        }
    }

    private void DisablePartner()
    {
        isDisabled = true;
        currentHealth = 0f;
        SetPartnerActive(false);
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
}