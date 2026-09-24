using UnityEngine;

namespace Tether.Player
{
    public enum BulletType
    {
        Normal,  // 普攻射彈 (傷害敵方/Boss)
        Ground,  // 陣地射彈 (擊中地面觸發陣地效果)
        Heal     // 治療射彈 (擊中夥伴回復血量)
    }

    public class PlayerShooting : MonoBehaviour
    {
        [Header("射擊點位設定")]
        [SerializeField] private Transform muzzlePoint; // 槍口位置

        [Header("射擊參數")]
        [SerializeField] private float fireRange = 50f;     // 射程
        [SerializeField] private float normalDamage = 15f;  // 普攻傷害
        [SerializeField] private float healAmount = 20f;    // 治療量
        [SerializeField] private LayerMask hitLayers;       // 檢測圖層

        [Header("當前子彈類型")]
        [SerializeField] private BulletType currentBulletType = BulletType.Normal;

        private Camera mainCamera;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (muzzlePoint == null) muzzlePoint = transform;
        }

        private void Update()
        {
            HandleBulletSwitch();

            if (Input.GetMouseButtonDown(0))
            {
                Shoot();
            }
        }

        private void HandleBulletSwitch()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                currentBulletType = (BulletType)(((int)currentBulletType + 1) % 3);
                Debug.Log($"<color=cyan>[射擊系統] 切換子彈類型為：{currentBulletType}</color>");
            }
        }

        private void Shoot()
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0f;

            Vector2 shootDirection = (mouseWorldPos - muzzlePoint.position).normalized;

            RaycastHit2D hit = Physics2D.Raycast(muzzlePoint.position, shootDirection, fireRange, hitLayers);

            Debug.DrawLine(muzzlePoint.position, muzzlePoint.position + (Vector3)shootDirection * fireRange, Color.red, 0.1f);

            if (hit.collider != null)
            {
                Vector2 hitPoint = hit.point;
                GameObject hitObj = hit.collider.gameObject;

                switch (currentBulletType)
                {
                    case BulletType.Normal:
                        ProcessNormalBullet(hitObj, hitPoint);
                        break;

                    case BulletType.Ground:
                        ProcessGroundBullet(hitObj, hitPoint);
                        break;

                    case BulletType.Heal:
                        ProcessHealBullet(hitObj, hitPoint);
                        break;
                }
            }
            else
            {
                Debug.Log($"[射擊] 射向空處，未命中目標。");
            }

            if (currentBulletType != BulletType.Normal)
            {
                currentBulletType = BulletType.Normal;
                Debug.Log("<color=grey>[射擊系統] 特殊子彈已發射，自動恢復為【普攻子彈】</color>");
            }
        }

        private void ProcessNormalBullet(GameObject target, Vector2 hitPoint)
        {
            if (target.CompareTag("Boss") || target.CompareTag("Enemy"))
            {
                Debug.Log($"<color=red>★★ [普攻射彈] 命中 {target.name}！造成 {normalDamage} 傷害 ★★</color>");

                var boss = target.GetComponentInParent<Boss.BossController>();
                if (boss != null)
                {
                    boss.TakeDamage(normalDamage);
                }
            }
            else
            {
                Debug.Log($"[普攻射彈] 擊中 {target.name}，產生一般著彈點。");
            }
        }

        private void ProcessGroundBullet(GameObject target, Vector2 hitPoint)
        {
            if (target.CompareTag("Ground"))
            {
                Debug.Log($"<color=yellow>★★ [陣地射彈] 命中地面 ({hitPoint})！觸發陣地效果 ★★</color>");
            }
            else
            {
                Debug.Log($"[陣地射彈] 未命中地面，無效著彈。");
            }
        }

        private void ProcessHealBullet(GameObject target, Vector2 hitPoint)
        {
            if (target.CompareTag("Partner"))
            {
                var partner = target.GetComponent<PartnerController>();
                if (partner == null) partner = target.GetComponentInParent<PartnerController>();

                if (partner != null)
                {
                    partner.RepairHealth(healAmount);
                    Debug.Log($"<color=green>★★ [治療射彈] 命中夥伴 {target.name}！回復 {healAmount} 血量 ★★</color>");
                }
            }
            else
            {
                Debug.Log($"[治療射彈] 未命中夥伴，無法治療。");
            }
        }
    }
}