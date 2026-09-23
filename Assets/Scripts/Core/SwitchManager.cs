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
    [SerializeField] private float parryWindowDuration = 0.2f;   // 完美格擋有效時間（秒）

    private float nextSwitchTime = 0f;
    private float parryWindowTimer = 0f;

    // 外部程式存取：檢查當前是否處於完美格擋狀態
    public bool IsParryActive => parryWindowTimer > 0f;

    private void Awake()
    {
        // 強制在最早期生命週期（第一幀前）確認初始狀態為 Recalled
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

        // 執行狀態切換邏輯
        if (currentState == GameControlState.Deployed)
        {
            // 從「放出」切換為「收回」
            currentState = GameControlState.Recalled;
            parryWindowTimer = parryWindowDuration; // 啟動完美格擋判定視窗
            Debug.Log("<color=cyan>[Switch] 收回夥伴！【完美格擋判定啟動】</color>");
        }
        else
        {
            // 從「收回」切換為「放出」
            currentState = GameControlState.Deployed;
            Debug.Log("<color=green>[Switch] 放出夥伴！進入遠程輸出模式</color>");
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
            Debug.LogError("<color=red>[SwitchManager] 未設定 partnerObject！請在 Inspector 面板將 Hierarchy 中的 Partner 拖入 GameManager 的 Partner Object 欄位中！</color>");
            return;
        }

        PartnerController partnerCtrl = partnerObject.GetComponent<PartnerController>();

        if (currentState == GameControlState.Deployed)
        {
            // 放出狀態：
            // 1. 將 Partner 重置移至主角身側，確保每次都從主角身邊出發
            Transform playerTransform = GameObject.FindWithTag("Player")?.transform;
            if (playerTransform != null)
            {
                partnerObject.transform.position = playerTransform.position + new Vector3(-1.5f, 1f, 0f);
            }

            // 2. 啟用 Partner 物件（會觸發 PartnerController 的 OnEnable 發動登場突進）
            partnerObject.SetActive(true);
        }
        else
        {
            // 【核心修改】收回狀態：
            // 不再直接 SetActive(false)，而是通知 PartnerController 觸發飛回主角的動畫
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
}