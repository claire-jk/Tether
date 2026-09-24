using UnityEngine;

namespace Tether.Boss
{
    public class BossDashAttackState : BossState
    {
        private float dashSpeed = 15f;
        private float dashDuration = 0.4f;
        private float dashTimer;
        private Vector2 dashDirection;

        public BossDashAttackState(BossController boss) : base(boss, BossStateType.Attack) { }

        public override void Enter()
        {
            base.Enter();
            dashTimer = 0f;

            // 鎖定玩家方向衝刺
            if (boss.PlayerTransform != null)
            {
                float dirX = Mathf.Sign(boss.PlayerTransform.position.x - boss.transform.position.x);
                dashDirection = new Vector2(dirX, 0f);
            }
            else
            {
                dashDirection = Vector2.right;
            }

            Debug.Log("<color=red>[Boss 階段2] 發動氣動扣栓槍 Dash！高速衝刺！</color>");
        }

        public override void PhysicsUpdate()
        {
            base.PhysicsUpdate();
            dashTimer += Time.fixedDeltaTime;

            // 位移衝刺
            boss.transform.position += (Vector3)(dashDirection * dashSpeed * Time.fixedDeltaTime);

            if (dashTimer >= dashDuration)
            {
                boss.StateMachine.ChangeState(boss.IdleState);
            }
        }
    }
}