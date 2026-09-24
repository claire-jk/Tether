using UnityEngine;

namespace Tether.Boss
{
    public class BossProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 12f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float lifeTime = 5f;

        private Vector2 moveDirection;

        public void Initialize(Vector2 direction)
        {
            moveDirection = direction.normalized;
            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            float damage = 15f; // 設定投射物傷害

            if (other.CompareTag("Player"))
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null)
                {
                    player.TakeDamageFromEnemy(damage);
                    Debug.Log($"<color=red>[Boss 投射物] 命中玩家！造成 {damage} 點傷害</color>");
                }
                Destroy(gameObject); // 命中後銷毀子彈
            }
            else if (other.CompareTag("Partner"))
            {
                PartnerController partner = other.GetComponent<PartnerController>();
                if (partner == null) partner = other.GetComponentInParent<PartnerController>();

                if (partner != null)
                {
                    partner.TakeDamage(damage);
                    Debug.Log($"<color=red>[Boss 投射物] 命中夥伴！造成 {damage} 點傷害</color>");
                }
                Destroy(gameObject); // 命中後銷毀子彈
            }
        }
    }
}