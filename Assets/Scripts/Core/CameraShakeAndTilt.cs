using System.Collections;
using UnityEngine;

namespace Tether.Core
{
    public class CameraShakeAndTilt : MonoBehaviour
    {
        public static CameraShakeAndTilt Instance { get; private set; }

        private Quaternion originalRotation;
        private Coroutine tiltCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            originalRotation = transform.localRotation;
        }

        /// <summary>
        /// 觸發 Parry 扭轉畫面效果 (逆時針扭轉 5 度並快速恢復)
        /// </summary>
        /// <param name="tiltAngle">扭轉角度 (預設 5 度)</param>
        /// <param name="duration">總持續時間 (預設 0.15 秒)</param>
        public void TriggerParryTilt(float tiltAngle = 5f, float duration = 0.15f)
        {
            if (tiltCoroutine != null) StopCoroutine(tiltCoroutine);
            tiltCoroutine = StartCoroutine(ParryTiltRoutine(tiltAngle, duration));
        }

        private IEnumerator ParryTiltRoutine(float tiltAngle, float duration)
        {
            // 逆時針扭轉 (Z 軸 +5 度)
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, tiltAngle);
            float halfDuration = duration * 0.3f; // 前 30% 時間快速扭轉到位
            float returnDuration = duration * 0.7f; // 後 70% 時間恢復

            float timer = 0f;

            // 1. 快速扭轉到 5 度
            while (timer < halfDuration)
            {
                timer += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(originalRotation, targetRotation, timer / halfDuration);
                yield return null;
            }

            timer = 0f;

            // 2. 平滑恢復原狀
            while (timer < returnDuration)
            {
                timer += Time.deltaTime;
                transform.localRotation = Quaternion.Slerp(targetRotation, originalRotation, timer / returnDuration);
                yield return null;
            }

            transform.localRotation = originalRotation;
            tiltCoroutine = null;
        }
    }
}