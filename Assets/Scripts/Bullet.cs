using UnityEngine;
using Tether.Boss;

public class Bullet : MonoBehaviour
{
    [Header("基礎設定")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private Vector2 bulletSize = new Vector2(1f, 1f);

    [Header("防卡地保護設定")]
    [SerializeField] private float spawnProtectionTime = 0.15f;

    [Header("彈藥類型與數值")]
    [Tooltip("勾選此項為右鍵功能彈（修復彈）；不勾選為左鍵普攻彈")]
    [SerializeField] private bool isUtilityBullet = false;
    [SerializeField] private float damage = 10f;          // 普攻彈傷害值
    [SerializeField] private float repairAmount = 25f;    // 功能彈修復血量值

    public float Speed { get => speed; set => speed = value; }
    public float Damage { get => damage; set => damage = value; }
    public bool IsUtilityBullet { get => isUtilityBullet; set => isUtilityBullet = value; }

    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        spawnTime = Time.time;

        Vector3 currentScale = transform.localScale;
        transform.localScale = new Vector3(currentScale.x * bulletSize.x, currentScale.y * bulletSize.y, currentScale.z);

        Destroy(gameObject, lifeTime);
    }

    public void Initialize(Vector2 direction, Collider2D ownerCollider)
    {
        SetupBulletDirectionAndCollision(direction, ownerCollider);
    }

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

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Collider2D bulletCollider = GetComponent<Collider2D>();

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
        if (collision.GetComponent<Bullet>() != null) return;
        if (collision.CompareTag("Player")) return;
        if (collision.GetComponent<FormationArea>() != null) return;

        PartnerController partner = collision.GetComponentInParent<PartnerController>();
        if (partner != null || collision.CompareTag("Partner"))
        {
            if (isUtilityBullet)
            {
                if (partner != null)
                {
                    partner.RepairHealth(repairAmount);
                    Debug.Log($"<color=cyan>[Bullet] 功能彈成功修復 Partner ({repairAmount} 點血量)！</color>");
                }
                Destroy(gameObject);
                return;
            }
            else
            {
                Debug.Log("<color=grey>[Bullet] 普攻彈穿透夥伴！</color>");
                return;
            }
        }

        // 3. 判斷是否命中敵人或 Boss
        if (collision.CompareTag("Enemy") || collision.CompareTag("Boss"))
        {
            BossController boss = collision.GetComponentInParent<BossController>();
            if (boss != null)
            {
                boss.TakeDamage(damage);
            }

            Debug.Log($"<color=red>[Bullet] 子彈命中 Boss：{collision.name}，造成 {damage} 點傷害！</color>");
            Destroy(gameObject);
            return;
        }

        if (Time.time - spawnTime < spawnProtectionTime)
        {
            return;
        }

        Debug.Log($"<color=yellow>[Bullet] 子彈擊中障礙物：{collision.name}</color>");
        Destroy(gameObject);
    }
}