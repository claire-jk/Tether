using UnityEngine;

public class PartnerController : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private Transform playerTransform; // 主角位置
    [SerializeField] private SwitchManager switchManager;

    [Header("追隨設定")]
    [SerializeField] private Vector3 followOffset = new Vector3(-1.5f, 1f, 0f); // 懸浮偏移量
    [SerializeField] private float followSpeed = 8f;     // 平滑追隨速度

    private SpriteRenderer spriteRenderer;
    private Collider2D partnerCollider;
    private bool isFacingRight = true;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        partnerCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (switchManager == null || playerTransform == null) return;

        // 1. 滑鼠游標自動決定面向
        HandleFacing();

        // 2. 根據 SwitchManager 狀態切換顯示與位移
        if (switchManager.currentState == GameControlState.Deployed)
        {
            // 放出狀態：顯示夥伴並平滑跟隨
            SetPartnerActive(true);
            FollowPlayer();
        }
        else
        {
            // 收回狀態：隱藏夥伴並重置位置至主角本體
            SetPartnerActive(false);
            transform.position = playerTransform.position;
        }
    }

    private void FollowPlayer()
    {
        // 根據主角當前面向動態翻轉懸浮位置
        Vector3 targetOffset = followOffset;
        if (playerTransform.localScale.x < 0)
        {
            targetOffset.x = -followOffset.x;
        }

        Vector3 targetPosition = playerTransform.position + targetOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * followSpeed);
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
}