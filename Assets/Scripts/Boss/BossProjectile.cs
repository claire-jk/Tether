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

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                Debug.Log($"<color=red>[投擲物] 擊中玩家！造成 {damage} 點傷害！</color>");
                Destroy(gameObject);
            }
            else if (collision.CompareTag("Ground"))
            {
                Destroy(gameObject);
            }
        }
    }
}