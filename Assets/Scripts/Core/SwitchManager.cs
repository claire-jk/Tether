using UnityEngine;

// 定義雙機體狀態
public enum GameControlState
{
    Deployed,   // 放出狀態（控制主角，夥伴跟隨）
    Recalled    // 收回狀態（控制夥伴/合併，獲得二段跳）
}

public class SwitchManager : MonoBehaviour
{
    [Header("狀態設定")]
    public GameControlState currentState = GameControlState.Deployed;

    [Header("冷卻與格擋參數")]
    [SerializeField] private float switchCooldown = 1.5f;       // 切換冷卻時間（秒）
    [SerializeField] private float parryWindowDuration = 0.2f;   // 完美格擋有效時間（秒）

    private float nextSwitchTime = 0f;
    private float parryWindowTimer = 0f;

    // 外部程式存取：檢查當前是否處於完美格擋狀態
    public bool IsParryActive => parryWindowTimer > 0f;

    private void Update()
    {
        // 更新完美格擋倒數計時
        if (parryWindowTimer > 0f)
        {
            parryWindowTimer -= Time.deltaTime;
        }

        // 偵測玩家按下 E 鍵
        if (Input.GetKeyDown(KeyCode.E))
        {
            TrySwitchState();
        }
    }

    private void TrySwitchState()
    {
        // 檢查冷卻時間
        if (Time.time < nextSwitchTime)
        {
            float remainingCD = nextSwitchTime - Time.time;
            Debug.Log($"<color=yellow>[System] 切換冷卻中！剩餘 {remainingCD:F1} 秒</color>");
            return;
        }

        // 執行狀態切換
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

        // 設定下次可切換的時間
        nextSwitchTime = Time.time + switchCooldown;
    }
}