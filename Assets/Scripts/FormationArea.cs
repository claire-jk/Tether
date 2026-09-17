using UnityEngine;

public class FormationArea : MonoBehaviour
{
    [SerializeField] private float duration = 8f; // 陣式存在時間
    [SerializeField] private float pulseInterval = 1f; // 觸發頻率
    private float pulseTimer;

    private void Start()
    {
        // 指定時間後自動銷毀
        Destroy(gameObject, duration);
        Debug.Log("<color=purple>[Formation] 陣式已部署！</color>");
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
        // 陣式脈衝邏輯（例如區域傷害、緩速或與夥伴連線）
        Debug.Log("<color=purple>[Formation] 陣式發動脈衝效果！</color>");
    }

    // 由玩家主動回收陣式時呼叫
    public void RecallFormation()
    {
        Debug.Log("<color=purple>[Formation] 陣式被玩家主動收回！</color>");
        Destroy(gameObject);
    }
}