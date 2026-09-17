using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("核心引用")]
    [SerializeField] private PartnerController partnerController;
    [SerializeField] private PlayerController playerController;

    [Header("UI 組件")]
    [SerializeField] private Slider partnerHPSlider;
    [SerializeField] private Slider playerHPSlider;
    [SerializeField] private Image partnerFillImage;

    [Header("顏色設定")]
    [SerializeField] private Color normalColor = Color.cyan;
    [SerializeField] private Color disabledColor = Color.gray;

    private void Update()
    {
        UpdatePartnerHealthUI();
        UpdatePlayerHealthUI();
    }

    private void UpdatePartnerHealthUI()
    {
        if (partnerController == null || partnerHPSlider == null) return;

        // 更新夥伴 Slider 數值
        partnerHPSlider.maxValue = partnerController.MaxHealth;
        partnerHPSlider.value = partnerController.CurrentHealth;

        // 停機時視覺變灰
        if (partnerFillImage != null)
        {
            partnerFillImage.color = partnerController.IsDisabled ? disabledColor : normalColor;
        }
    }

    private void UpdatePlayerHealthUI()
    {
        if (playerController == null || playerHPSlider == null) return;

        // 更新主角 Slider 數值（需確保 PlayerController 有對應屬性）
        playerHPSlider.maxValue = playerController.MaxHealth;
        playerHPSlider.value = playerController.CurrentHealth;
    }
}