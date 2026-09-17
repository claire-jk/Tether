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
    [SerializeField] private float aiMoveSpeed = 5f;     // 衝向 Boss 速度
    [SerializeField] private float attackRange = 1.5f;   // AI 攻擊距離
    [SerializeField] private float attackInterval = 1f;  // AI 自動攻擊間隔
    private float nextAttackTime;

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

    private void Update()
    {
        if (switchManager == null || playerTransform == null || isDisabled) return;

        // 1. 滑鼠游標自動決定面向
        HandleFacing();

        // 2. 根據 SwitchManager 狀態切換顯示與 AI 行為
        if (switchManager.currentState == GameControlState.Deployed)
        {
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
            // 收回狀態：隱藏夥伴並重置位置至主角本體
            SetPartnerActive(false);
            transform.position = playerTransform.position;
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
        // 修正：只計算水平 X 軸的距離，避免 Boss 在空中時 partner 算出來的距離永遠過大
        float distanceX = Mathf.Abs(transform.position.x - bossTransform.position.x);

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
        elapsed = 0f;
        float returnTime = duration * 0.6f;
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

    #region 血量管理與停機機制

    // 由收回狀態承傷時呼叫
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