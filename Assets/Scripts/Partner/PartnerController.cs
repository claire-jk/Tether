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

    private float rushTimer = 0f;                          // 衝刺計時器
    private bool isRushing = false;                        // 是否正在衝刺狀態
    private bool isRecalling = false;                      // 【關鍵】是否正在高速飛回主角身邊

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

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        partnerCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
    }

    // 每次物件被 SetActive(true)（即按下 E 放出夥伴）時觸發
    private void OnEnable()
    {
        TriggerRushToBoss();
    }

    /// <summary>
    /// 觸發登場高速突進衝向 Boss
    /// </summary>
    public void TriggerRushToBoss()
    {
        if (bossTransform != null && !isDisabled)
        {
            isRushing = true;
            isRecalling = false; // 確保放出時重置收回狀態
            rushTimer = rushDuration;
            Debug.Log("<color=orange>[Partner AI] 登場！發動超高速突進衝向 Boss！</color>");
        }
    }

    /// <summary>
    /// 【新增】：由 SwitchManager 在按下 E 收回時呼叫，觸發飛回動畫
    /// </summary>
    public void StartRecalling()
    {
        if (isDisabled) return;

        isRecalling = true;
        isRushing = false;
        SetPartnerActive(true); // 確保飛回期間是顯示狀態
        Debug.Log("<color=cyan>[Partner AI] 開始飛回主角身邊...</color>");
    }

    private void Update()
    {
        if (switchManager == null || playerTransform == null) return;

        // 若停機狀態，維持隱藏且不執行 AI 邏輯
        if (isDisabled)
        {
            SetPartnerActive(false);
            return;
        }

        // 1. 滑鼠游標自動決定面向
        HandleFacing();

        // 2. 根據 SwitchManager 狀態切換顯示與 AI 行為
        if (switchManager.currentState == GameControlState.Deployed)
        {
            isRecalling = false; // 若在中途再次切換為放出，立刻打斷收回
            SetPartnerActive(true);

            // 放出狀態：優先衝向 Boss 攻擊，若無視野目標則回歸平滑懸浮跟隨
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
            // 收回狀態：
            isRushing = false;

            if (isRecalling)
            {
                // 【核心修復】：如果正處於收回狀態，平滑飛向主角
                HandleRecallMovement();
            }
            else
            {
                // 已飛抵身邊，維持隱藏並重置位置
                SetPartnerActive(false);
                transform.position = playerTransform.position;
            }
        }
    }

    /// <summary>
    /// 【新增】：處理飛回主角身邊的高速移動
    /// </summary>
    private void HandleRecallMovement()
    {
        // 飛向主角身邊的預設懸浮點
        Vector3 targetOffset = followOffset;
        if (playerTransform.localScale.x < 0)
        {
            targetOffset.x = -followOffset.x;
        }
        Vector3 targetPosition = playerTransform.position + targetOffset;

        // 使用 MoveTowards 以固定高速度 (recallSpeed) 朝主角飛去
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, recallSpeed * Time.deltaTime);

        // 當距離極近時，代表已抵達主角身邊，正式完成收回並隱藏
        if (Vector3.Distance(transform.position, targetPosition) < 0.2f)
        {
            isRecalling = false;
            SetPartnerActive(false);
            transform.position = playerTransform.position;
            Debug.Log("<color=cyan>[Partner AI] 成功飛回主角身邊並隱藏！</color>");
        }
    }

    #region 放出狀態 AI 邏輯

    private void FollowPlayer()
    {
        if (isAttacking) return; // 若正在播放攻擊動態則暫停平滑跟隨

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
        // 只計算水平 X 軸距離
        float distanceX = Mathf.Abs(transform.position.x - bossTransform.position.x);

        // 優先處理登場高速衝刺
        if (isRushing)
        {
            rushTimer -= Time.deltaTime;

            // 衝向 Boss 的水平方向
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

            // 若已抵達攻擊範圍或衝刺時間結束，終止衝刺模式
            if (distanceX <= attackRange || rushTimer <= 0f)
            {
                isRushing = false;
            }

            return; // 衝刺期間跳過一般追擊與攻擊邏輯
        }

        // 一般接近 Boss 與攻擊邏輯
        if (distanceX > attackRange)
        {
            if (isAttacking) return;

            // 自動衝向 Boss 方向
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
            // 抵達水平攻擊範圍，停下並發動攻擊
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

        // 觸發衝擊打擊動態演出
        if (bossTransform != null)
        {
            Vector3 attackDir = (bossTransform.position - transform.position).normalized;
            StartCoroutine(AttackPunchAnimation(attackDir * 0.6f, 0.15f));
        }
    }

    // 由 PlayerController 在【放出狀態】射擊時呼叫：發動協同追擊
    public void TriggerCoopAttack()
    {
        if (switchManager.currentState == GameControlState.Deployed && !isDisabled)
        {
            Debug.Log("<color=cyan>[Partner AI] 響應主角射擊，進行協同追擊！</color>");

            // 觸發較大範圍的突刺追擊動態
            Vector3 attackDir = bossTransform != null ?
                (bossTransform.position - transform.position).normalized :
                (isFacingRight ? Vector3.right : Vector3.left);

            StartCoroutine(AttackPunchAnimation(attackDir * 1.2f, 0.12f));
        }
    }

    // 微前衝 + 微上升折返的跳動攻擊動態
    private IEnumerator AttackPunchAnimation(Vector3 offset, float duration)
    {
        isAttacking = true;
        Vector3 startPos = transform.position;
        // 加入些微 Y 軸向上彈跳量，呈現打擊動態
        Vector3 targetPos = startPos + offset + new Vector3(0, 0.3f, 0);

        // 1. 前衝/打擊 phase (一半時間)
        float elapsed = 0f;
        float forwardTime = duration * 0.4f;
        while (elapsed < forwardTime)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / forwardTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 收招/折返 phase (剩餘時間)
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