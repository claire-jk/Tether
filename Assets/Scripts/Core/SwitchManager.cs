using UnityEngine;

// 定義雙機體狀態
public enum GameControlState
{
    Deployed,   // 放出狀態（遠程彈藥模式，夥伴獨立存在）
    Recalled    // 收回狀態（近戰體術模式，夥伴隱藏並提供二段跳）
}

public class SwitchManager : MonoBehaviour
{
    [Header("物件引用")]
    [Tooltip("請將 Hierarchy 中的 Partner 物件拖入此欄位")]
    [SerializeField] private GameObject partnerObject;

    [Header("狀態設定")]
    [Tooltip("當前機體狀態，預設開局為 Recalled (收回狀態)")]
    public GameControlState currentState = GameControlState.Recalled;

    [Header("冷卻與格擋參數")]
    [SerializeField] private float switchCooldown = 1.5f;       // 切換冷卻時間（秒）
    [SerializeField] private float parryWindowDuration = 0.3f;   // 企劃需求：0.3 秒精準 Parry 視窗

    private float nextSwitchTime = 0f;
    private float parryWindowTimer = 0f;

    // 外部程式存取：檢查當前是否處於完美格擋狀態
    public bool IsParryActive => parryWindowTimer > 0f;

    private void Awake()
    {
        // 強制在最早期生命週期確認初始狀態為 Recalled
        currentState = GameControlState.Recalled;
    }

    private void Start()
    {
        // 遊戲啟動時若為收回狀態，先將夥伴直接隱藏
        if (partnerObject != null && currentState == GameControlState.Recalled)
        {
            partnerObject.SetActive(false);
        }
    }

    private void Update()
    {
        // 更新完美格擋倒數計時
        if (parryWindowTimer > 0f)
        {
            parryWindowTimer -= Time.deltaTime;
        }

        // 偵測玩家按下 E 鍵進行機體狀態切換
        if (Input.GetKeyDown(KeyCode.E))
        {
            TrySwitchState();
        }
    }

    /// <summary>
    /// 嘗試執行狀態切換與冷卻檢查
    /// </summary>
    private void TrySwitchState()
    {
        // 檢查冷卻時間
        if (Time.time < nextSwitchTime)
        {
            float remainingCD = nextSwitchTime - Time.time;
            Debug.Log($"<color=yellow>[System] 切換冷卻中！剩餘 {remainingCD:F1} 秒</color>");
            return;
        }

        PartnerController partnerCtrl = partnerObject != null ? partnerObject.GetComponent<PartnerController>() : null;

        // 執行狀態切換邏輯
        if (currentState == GameControlState.Deployed)
        {
            // 從「放出」切換為「收回」
            currentState = GameControlState.Recalled;
            parryWindowTimer = parryWindowDuration; // 啟動自身完美格擋判定視窗

            // 企劃需求：收回時觸發 Partner 的 0.3s 精準 Parry
            if (partnerCtrl != null)
            {
                partnerCtrl.TriggerParry();
            }

            Debug.Log("<color=cyan>[Switch] 收回夥伴！【UI 切換為主角血條 + 啟動 0.3s 完美格擋】</color>");
        }
        else
        {
            // 從「收回」切換為「放出」
            currentState = GameControlState.Deployed;
            Debug.Log("<color=green>[Switch] 放出夥伴！【UI 切換為夥伴血條 + 進入遠程輸出模式】</color>");
        }

        // 切換狀態後即時更新夥伴實體顯隱與位置
        UpdatePartnerVisibility();

        // 計算下次可切換的時間點
        nextSwitchTime = Time.time + switchCooldown;
    }

    /// <summary>
    /// 根據當前狀態控制 Partner 物件的顯示、隱藏與出現位置
    /// </summary>
    private void UpdatePartnerVisibility()
    {
        if (partnerObject == null)
        {
            Debug.LogError("<color=red>[SwitchManager] 未設定 partnerObject！請在 Inspector 面板拖入 Partner 物件！</color>");
            return;
        }

        PartnerController partnerCtrl = partnerObject.GetComponent<PartnerController>();

        if (currentState == GameControlState.Deployed)
        {
            // 放出狀態：
            Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (playerTransform != null)
            {
                partnerObject.transform.position = playerTransform.position + new Vector3(-1.5f, 1f, 0f);
            }

            partnerObject.SetActive(true);

            if (partnerCtrl != null)
            {
                partnerCtrl.TriggerRushToBoss();
            }
        }
        else
        {
            // 收回狀態：觸發飛回主角身邊
            if (partnerCtrl != null)
            {
                partnerCtrl.StartRecalling();
            }
            else
            {
                partnerObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 切換 Recalled 與 Deployed 狀態（供外部被動觸發，例如停機時自動收回）
    /// </summary>
    public void ToggleState()
    {
        TrySwitchState();
    }
}