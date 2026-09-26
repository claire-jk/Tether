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

        [Header("陣勢彈 (Ground Formation) 設定")]
        [SerializeField] private GameObject formationPrefab;      // 陣勢彈 / 陣地範圍 Prefab
        [SerializeField] private float formationCooldown = 5f;    // 陣勢彈冷卻時間 (秒)

        // 陣勢彈狀態管理變數
        private GameObject currentFormationInstance; // 場上當前存在的陣勢彈實例
        private float formationCooldownTimer = 0f;    // 陣勢彈當前冷卻計時器
        private bool isFormationActive = false;       // 陣勢彈當前是否在場上

        private Camera mainCamera;

        public float FormationCooldownTimer => formationCooldownTimer;
        public bool IsFormationActive => isFormationActive;

        private void Awake()
        {
            mainCamera = Camera.main;
            if (muzzlePoint == null) muzzlePoint = transform;
        }

        private void Update()
        {
            // 1. 冷卻時間倒數
            if (formationCooldownTimer > 0f)
            {
                formationCooldownTimer -= Time.deltaTime;
            }

            // 2. 切換子彈 (Q 鍵)
            HandleBulletSwitch();

            // 3. 【修復 Bug】按下 R 鍵取出 / 收回陣勢彈
            HandleFormationToggleInput();

            // 4. 左鍵射擊
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

        /// <summary>
        /// 處理 R 鍵取出 / 收回陣勢彈邏輯
        /// </summary>
        private void HandleFormationToggleInput()
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                // 【核心修復點】：如果陣勢彈正在冷卻中，按下 R 鍵直接攔截，無事發生！
                if (formationCooldownTimer > 0f)
                {
                    Debug.Log($"<color=yellow>[陣勢彈] 正在冷卻中！剩餘 {formationCooldownTimer:F1} 秒，無法執行收回/取出！</color>");
                    return; // 直接返回，保護場上的陣勢彈範圍不被消失
                }

                // 若非冷卻中，判斷目前場上有無陣勢彈
                if (isFormationActive)
                {
                    // 場上有陣勢彈 -> 執行收回
                    RecallFormation();
                }
                else
                {
                    // 場上無陣勢彈 -> 切換子彈模式為 Ground (準備發射/部署)
                    currentBulletType = BulletType.Ground;
                    Debug.Log("<color=yellow>[射擊系統] 已手動裝填【陣地/陣勢子彈】，請瞄準地面射擊部署！</color>");
                }
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

                // 保存當前子彈類型後進行處理
                BulletType shotType = currentBulletType;

                switch (shotType)
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

            // 發射後若為特殊子彈（陣勢彈/治療彈），將子彈重置為【普攻子彈】
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
            // 檢查命中物件是否為地面
            if (target.CompareTag("Ground"))
            {
                // 若場上已有陣勢彈，先清掉舊的
                if (currentFormationInstance != null)
                {
                    Destroy(currentFormationInstance);
                }

                // 生成陣勢彈範圍 Prefab
                if (formationPrefab != null)
                {
                    currentFormationInstance = Instantiate(formationPrefab, hitPoint, Quaternion.identity);
                }

                isFormationActive = true;
                Debug.Log($"<color=yellow>★★ [陣地射彈] 命中地面 ({hitPoint})！部署陣勢彈範圍！ ★★</color>");
            }
            else
            {
                Debug.Log($"[陣地射彈] 未命中地面，無效著彈（未觸發陣地效果，也不造成普攻傷害）。");
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

        /// <summary>
        /// 正常收回陣勢彈並觸發 CD
        /// </summary>
        public void RecallFormation()
        {
            if (currentFormationInstance != null)
            {
                Destroy(currentFormationInstance);
            }

            isFormationActive = false;
            formationCooldownTimer = formationCooldown; // 開始進入冷卻

            Debug.Log($"<color=cyan>[陣勢彈] 正常收回！開始進入 {formationCooldown} 秒冷卻！</color>");
        }
    }
}