using UnityEngine;

namespace Tether.Boss
{
    public class BossAttackState : BossState
    {
        private float attackTimer;
        private float attackDuration = 1.0f;

        public BossAttackState(BossController boss) : base(boss, BossStateType.Attack) { }

        public override void Enter()
        {
            base.Enter();
            attackTimer = 0f;
            Debug.Log("<color=orange>[Boss] 進入攻擊狀態 (Attack)！發動揮砍！</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            attackTimer += Time.deltaTime;

            if (attackTimer >= attackDuration)
            {
                boss.StateMachine.ChangeState(boss.IdleState);
            }
        }
    }
}