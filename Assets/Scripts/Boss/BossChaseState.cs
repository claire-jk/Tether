using UnityEngine;

namespace Tether.Boss
{
    public class BossChaseState : BossState
    {
        private float moveSpeed = 3.5f;
        private float attackRange = 2.0f;

        public BossChaseState(BossController boss) : base(boss, BossStateType.Chase) { }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("<color=yellow>[Boss] 進入追擊狀態 (Chase)</color>");
        }

        public override void PhysicsUpdate()
        {
            base.PhysicsUpdate();

            if (boss.PlayerTransform == null) return;

            Vector2 targetPos = boss.PlayerTransform.position;
            Vector2 currentPos = boss.transform.position;
            float distance = Vector2.Distance(currentPos, targetPos);

            float moveDir = Mathf.Sign(targetPos.x - currentPos.x);
            boss.transform.position += new Vector3(moveDir * moveSpeed * Time.fixedDeltaTime, 0, 0);

            if (distance <= attackRange)
            {
                boss.StateMachine.ChangeState(boss.AttackState);
            }
        }
    }
}