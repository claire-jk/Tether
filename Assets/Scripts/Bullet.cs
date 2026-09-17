using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("基礎設定")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private Vector2 bulletSize = new Vector2(1f, 1f); // 可調 X, Y 大小

    [Header("防卡地保護設定")]
    [SerializeField] private float spawnProtectionTime = 0.15f; // 生成後前 0.15 秒忽略地面碰撞

    [Header("彈藥類型與數值")]
    [SerializeField] private bool isUtilityBullet = false; // 是否為右鍵功能彈
    [SerializeField] private float damage = 10f;          // 普攻彈傷害值
    [SerializeField] private float repairAmount = 25f;    // 功能彈修復血量值

    // 提供外部讀取的 Getter/Setter
    public float Speed { get => speed; set => speed = value; }
    public float Damage { get => damage; set => damage = value; }
    public bool IsUtilityBullet { get => isUtilityBullet; set => isUtilityBullet = value; }

    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private float spawnTime; // 記錄生成時間

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time; // 記錄生成時間點

        // 縮放當前子彈的 LocalScale (X 與 Y 獨立分開)
        Vector3 currentScale = transform.localScale;
        transform.localScale = new Vector3(currentScale.x * bulletSize.x, currentScale.y * bulletSize.y, currentScale.z);

        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 基礎初始化：方向與碰撞豁免
    /// </summary>
    public void Initialize(Vector2 direction, Collider2D ownerCollider)
    {
        SetupBulletDirectionAndCollision(direction, ownerCollider);
    }

    /// <summary>
    /// 動態擴充初始化
    /// </summary>
    public void Initialize(Vector2 direction, Collider2D ownerCollider, float customDamage, float customSpeed, Vector3 customScale, bool isUtility = false)
    {
        this.damage = customDamage;
        this.speed = customSpeed;
        this.isUtilityBullet = isUtility;
        transform.localScale = customScale;

        SetupBulletDirectionAndCollision(direction, ownerCollider);

        if (isUtilityBullet)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = Color.cyan;
            }
        }
    }

    private void SetupBulletDirectionAndCollision(Vector2 direction, Collider2D ownerCollider)
    {
        moveDirection = direction.normalized;

        // 轉向：讓子彈方向朝向飛行軌跡
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Collider2D bulletCollider = GetComponent<Collider2D>();

        // 忽略玩家身上所有的 Collider（包含 GroundCheck、AttackPoint 等）
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && bulletCollider != null)
        {
            Collider2D[] playerColliders = playerObj.GetComponentsInChildren<Collider2D>();
            foreach (var col in playerColliders)
            {
                Physics2D.IgnoreCollision(bulletCollider, col);
            }
        }
        else if (bulletCollider != null && ownerCollider != null)
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
        // 1. 忽略與其他子彈或玩家本體的碰撞
        if (collision.GetComponent<Bullet>() != null) return;
        if (collision.CompareTag("Player")) return;

        // 2. 判斷是否命中 Partner（夥伴）
        PartnerController partner = collision.GetComponent<PartnerController>();
        if (partner != null)
        {
            if (isUtilityBullet)
            {
                partner.RepairHealth(repairAmount);
                Debug.Log($"<color=cyan>[Bullet] 功能彈成功修復 Partner ({repairAmount} 點血量)！</color>");
                Destroy(gameObject);
                return;
            }
            else
            {
                return; // 普攻彈穿透夥伴
            }
        }

        // 3. 判斷是否命中敵人
        if (collision.CompareTag("Enemy"))
        {
            Debug.Log($"<color=red>[Bullet] 子彈命中敵人：{collision.name}，造成 {damage} 點傷害！</color>");
            Destroy(gameObject);
            return;
        }

        // 4. 【核心修復】：如果在生成保護時間內撞到地面/牆壁，忽略該次碰撞！
        if (Time.time - spawnTime < spawnProtectionTime)
        {
            return;
        }

        // 5. 超過保護時間後，正常擊中場景牆壁、障礙物等其他物件才銷毀
        Debug.Log($"<color=yellow>[Bullet] 子彈擊中物件：{collision.name}</color>");
        Destroy(gameObject);
    }
}