using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private SwitchManager switchManager;
    [SerializeField] private PartnerController partnerController;
    [SerializeField] private PlayerController playerController;

    [Header("UI 組件（單一主要血條機制）")]
    [Tooltip("主要血條 Slider（放出的時候顯示夥伴血量，收回的時候顯示主角血量）")]
    [SerializeField] private Slider mainHPSlider;
    [SerializeField] private Image mainFillImage;

    [Header("顏色設定")]
    [SerializeField] private Color playerHPColor = Color.green;   // 主角血條顏色
    [SerializeField] private Color partnerHPColor = Color.cyan;    // 夥伴正常血條顏色
    [SerializeField] private Color disabledColor = Color.gray;     // 夥伴停機血條顏色

    private void Awake()
    {
        // 自動防呆：若未設定 switchManager，嘗試從場景搜尋
        if (switchManager == null)
        {
            switchManager = FindAnyObjectByType<SwitchManager>(); // ✅ 使用最新推薦方法
        }
    }

    private void Update()
    {
        UpdateDynamicHealthUI();
    }

    /// <summary>
    /// 根據當前切換狀態（Deployed / Recalled）切換主要血條顯示
    /// </summary>
    private void UpdateDynamicHealthUI()
    {
        if (mainHPSlider == null || switchManager == null) return;

        // 1. 放出狀態 (Deployed) -> 主要血條顯示夥伴血量
        if (switchManager.currentState == GameControlState.Deployed)
        {
            if (partnerController != null)
            {
                mainHPSlider.maxValue = partnerController.MaxHealth;
                mainHPSlider.value = partnerController.CurrentHealth;

                if (mainFillImage != null)
                {
                    // 停機時血條變灰，正常時顯示夥伴顏色
                    mainFillImage.color = partnerController.IsDisabled ? disabledColor : partnerHPColor;
                }
            }
        }
        // 2. 收回狀態 (Recalled) -> 主要血條顯示主角血量
        else
        {
            if (playerController != null)
            {
                mainHPSlider.maxValue = playerController.MaxHealth;
                mainHPSlider.value = playerController.CurrentHealth;

                if (mainFillImage != null)
                {
                    mainFillImage.color = playerHPColor;
                }
            }
        }
    }
}