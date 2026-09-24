using UnityEngine;

namespace Tether.Boss
{
    public class BossHarpoonPullState : BossState
    {
        private float timer;
        private float pullSpeed = 12f;
        private Transform targetTransform;
        private bool isPulling = false;

        public BossHarpoonPullState(BossController boss) : base(boss, BossStateType.Attack) { }

        public override void Enter()
        {
            base.Enter();
            timer = 0f;
            isPulling = false;
            // 優先拉扯 Player，若無則拉扯 Partner
            targetTransform = boss.PlayerTransform != null ? boss.PlayerTransform : boss.PartnerTransform;

            Debug.Log("<color=purple>[Boss 階段2] 發動 DBD 死亡槍手鉤爪！準備拉扯目標！</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            timer += Time.deltaTime;

            if (timer >= 0.3f && targetTransform != null)
            {
                isPulling = true;
            }

            if (isPulling && targetTransform != null)
            {
                // 將目標往 Boss 本身的位置拖拉
                targetTransform.position = Vector3.MoveTowards(
                    targetTransform.position,
                    boss.transform.position,
                    pullSpeed * Time.deltaTime
                );

                float distance = Vector2.Distance(boss.transform.position, targetTransform.position);
                // 拖到身邊 1.5 單位處停止拉扯並切回 Idle
                if (distance <= 1.5f || timer >= 2.0f)
                {
                    Debug.Log("<color=purple>[Boss] 拉扯完成！進入近身連擊距離！</color>");
                    boss.StateMachine.ChangeState(boss.IdleState);
                }
            }
        }
    }
}