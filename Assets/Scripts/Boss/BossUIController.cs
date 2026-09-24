using UnityEngine;
using UnityEngine.UI;

namespace Tether.Boss
{
    public class BossUIController : MonoBehaviour
    {
        [Header("Boss 引用")]
        [SerializeField] private BossController bossController;

        [Header("UI 組件")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider angerSlider;
        [SerializeField] private Image angerFillImage; // 憤怒條填滿顏色

        [Header("4 階段顏色設定")]
        [SerializeField] private Color calmColor = new Color(0.5f, 0f, 0f);      // 暗紅
        [SerializeField] private Color berserkColor = new Color(0.1f, 0.2f, 0.5f); // 藍黑
        [SerializeField] private Color angryColor = new Color(1f, 0.1f, 0.1f);   // 亮紅
        [SerializeField] private Color breakdownColor = new Color(0.5f, 0.5f, 0.5f); // 灰碎

        private void OnEnable()
        {
            if (bossController != null)
            {
                bossController.OnHealthChanged += UpdateHealthUI;
                bossController.OnAngerChanged += UpdateAngerUI;
            }
        }

        private void OnDisable()
        {
            if (bossController != null)
            {
                bossController.OnHealthChanged -= UpdateHealthUI;
                bossController.OnAngerChanged -= UpdateAngerUI;
            }
        }

        private void UpdateHealthUI(float current, float max)
        {
            if (healthSlider != null)
            {
                healthSlider.value = current / max;
            }
        }

        private void UpdateAngerUI(float current, float max, AngerPhase phase)
        {
            if (angerSlider != null)
            {
                angerSlider.value = current / max;
            }

            // 依據階段切換憤怒條顏色
            if (angerFillImage != null)
            {
                switch (phase)
                {
                    case AngerPhase.Calm:
                        angerFillImage.color = calmColor;
                        break;
                    case AngerPhase.Berserk:
                        angerFillImage.color = berserkColor;
                        break;
                    case AngerPhase.Angry:
                        angerFillImage.color = angryColor;
                        break;
                    case AngerPhase.Breakdown:
                        angerFillImage.color = breakdownColor;
                        break;
                }
            }
        }
    }
}