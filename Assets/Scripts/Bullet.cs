using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("基礎設定")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifeTime = 3f;

    [Header("彈藥類型與數值")]
    [SerializeField] private bool isUtilityBullet = false; // 是否為右鍵功能彈（修復彈/增幅彈）
    [SerializeField] private float damage = 10f;          // 普攻彈傷害值
    [SerializeField] private float repairAmount = 25f;    // 功能彈修復 Partner 的血量值

    private Vector2 moveDirection;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 初始化子彈方向與發射者碰撞豁免
    /// </summary>
    public void Initialize(Vector2 direction, Collider2D ownerCollider)
    {
        moveDirection = direction.normalized;

        // 轉向：讓子彈方向朝向飛行軌跡
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // 讓子彈強制忽略玩家/發射者本體的碰撞，避免剛生成就炸開
        Collider2D bulletCollider = GetComponent<Collider2D>();
        if (bulletCollider != null && ownerCollider != null)
        {
            Physics2D.IgnoreCollision(bulletCollider, ownerCollider);
        }
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = moveDirection * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 1. 忽略與其他子彈的碰撞
        if (collision.GetComponent<Bullet>() != null) return;

        // 2. 判斷是否命中 Partner（夥伴）
        PartnerController partner = collision.GetComponent<PartnerController>();
        if (partner != null)
        {
            if (isUtilityBullet)
            {
                // 右鍵功能彈：修復 Partner / 賦予 Buff
                partner.RepairHealth(repairAmount);
                Debug.Log($"<color=cyan>[Bullet] 功能彈成功修復 Partner ({repairAmount} 點血量)！</color>");
            }
            else
            {
                // 普攻彈如果不小心穿過 Partner 則直接穿透忽略，不銷毀子彈
                return;
            }

            Destroy(gameObject);
            return;
        }

        // 3. 判斷是否命中敵人
        // （若敵人帶有 Enemy Health 相關腳本，可在此處調用 TakeDamage）
        if (collision.CompareTag("Enemy"))
        {
            Debug.Log($"<color=red>[Bullet] 子彈命中敵人：{collision.name}，造成 {damage} 點傷害！</color>");
            // collision.GetComponent<EnemyHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // 4. 命中場景牆壁、障礙物等其他物件
        Debug.Log($"<color=yellow>[Bullet] 子彈擊中物件：{collision.name}</color>");
        Destroy(gameObject);
    }
}