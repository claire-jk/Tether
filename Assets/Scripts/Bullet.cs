using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifeTime = 3f;

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

    // 新增 ownerCollider 參數，直接讓子彈無視發射者本體與所有子物件碰撞
    public void Initialize(Vector2 direction, Collider2D ownerCollider)
    {
        moveDirection = direction.normalized;

        // 轉向
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        // 核心修正：讓子彈物理引擎強制忽略發射者的 Collider
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
        // 避免子彈擊中其他子彈
        if (collision.GetComponent<Bullet>() != null) return;

        Debug.Log($"<color=cyan>[Bullet] 子彈擊中目標：{collision.name}</color>");

        Destroy(gameObject);
    }
}