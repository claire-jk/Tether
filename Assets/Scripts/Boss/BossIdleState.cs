using UnityEngine;

namespace Tether.Boss
{
    public class BossIdleState : BossState
    {
        private float idleTimer;
        private float idleDuration = 1.5f;

        public BossIdleState(BossController boss) : base(boss, BossStateType.Idle) { }

        public override void Enter()
        {
            base.Enter();
            idleTimer = 0f;
            Debug.Log("<color=cyan>[Boss] 進入待機狀態 (Idle)</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleDuration && boss.PlayerTransform != null)
            {
                boss.StateMachine.ChangeState(boss.ChaseState);
            }
        }
    }
}