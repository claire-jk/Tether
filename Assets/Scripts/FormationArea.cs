using UnityEngine;
using Tether.Boss; // 引入 Boss 命名空間

public class FormationArea : MonoBehaviour
{
    [Header("基礎時序設定")]
    [SerializeField] private float duration = 8f;      // 陣式存在時間
    [SerializeField] private float pulseInterval = 1f; // 脈衝觸發頻率（秒）
    private float pulseTimer;

    [Header("陣式區域數值 (需在 Inspector 調整)")]
    [SerializeField] private float radius = 3f;        // 陣式影響半徑
    [SerializeField] private float damage = 15f;       // 每脈衝一次對敵人造成的傷害
    [SerializeField] private float healAmount = 10f;   // 每脈衝一次對 Partner/玩家的治療量

    [Header("陣地特效設定 (企劃第 12 項)")]
    [SerializeField] private GameObject spawnVfxPrefab;   // 登場/落地方案特效
    [SerializeField] private GameObject pulseVfxPrefab;   // 每次脈衝發動特效
    [SerializeField] private GameObject destroyVfxPrefab; // 回收/消散特效
    [SerializeField] private float vfxDestroyTime = 1.0f;  // 特效自動銷毀時間

    [Header("目標圖層檢測 (可選)")]
    [SerializeField] private LayerMask targetLayers;   // 可在 Inspector 指定要影響的 Layer

    // 提供外部腳本存取或動態修改的屬性
    public float Radius { get => radius; set => radius = value; }
    public float Damage { get => damage; set => damage = value; }
    public float HealAmount { get => healAmount; set => healAmount = value; }

    private void Start()
    {
        // 1. 動態將陣式的視覺/物理範圍調整為指定的 radius 大小 (直徑 = 半徑 * 2)
        transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        // 2. 觸發登場著陸特效
        SpawnVFX(spawnVfxPrefab);

        // 3. 指定時間後自動銷毀
        Destroy(gameObject, duration);
        Debug.Log($"<color=purple>[Formation] 陣式已部署！半徑: {radius}, 傷害: {damage}, 治療: {healAmount}</color>");
    }

    private void Update()
    {
        pulseTimer += Time.deltaTime;
        if (pulseTimer >= pulseInterval)
        {
            pulseTimer = 0f;
            TriggerFormationPulse();
        }
    }

    private void TriggerFormationPulse()
    {
        Debug.Log("<color=purple>[Formation] 陣式發動脈衝效果！</color>");

        // 觸發脈衝波動特效
        SpawnVFX(pulseVfxPrefab);

        // 透過圓形範圍檢測找到區域內的所有碰撞體
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, radius);

        foreach (var col in hitColliders)
        {
            // 情況 A：命中敵人或 Boss -> 造成傷害
            if (col.CompareTag("Enemy") || col.CompareTag("Boss"))
            {
                BossController boss = col.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    boss.TakeDamage(damage);
                }

                Debug.Log($"<color=red>[Formation] 脈衝對 {col.name} 造成 {damage} 點傷害！</color>");
            }

            // 情況 B：命中 Partner -> 進行治療
            PartnerController partner = col.GetComponentInParent<PartnerController>();
            if (partner != null || col.CompareTag("Partner"))
            {
                if (partner != null)
                {
                    partner.RepairHealth(healAmount);
                    Debug.Log($"<color=green>[Formation] 脈衝對 Partner {col.name} 進行治療 +{healAmount}！</color>");
                }
            }
        }
    }

    // 由玩家主動回收陣式時呼叫
    public void RecallFormation()
    {
        Debug.Log("<color=purple>[Formation] 陣式被玩家主動收回！</color>");
        SpawnVFX(destroyVfxPrefab);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 自然時間結束銷毀時補上退場特效
        if (gameObject.scene.isLoaded) // 確保不是因為關閉遊戲而觸發
        {
            SpawnVFX(destroyVfxPrefab);
        }
    }

    /// <summary>
    /// 生成粒子特效的方法
    /// </summary>
    private void SpawnVFX(GameObject vfxPrefab)
    {
        if (vfxPrefab == null) return;
        GameObject vfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
        Destroy(vfx, vfxDestroyTime);
    }

    // 在 Scene 畫面畫出半徑紅圈，方便在 Editor 編輯時預覽範圍
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}