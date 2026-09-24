using UnityEngine;

namespace Tether.Boss
{
    public class BossThrowState : BossState
    {
        private float timer;
        private float throwDelay = 0.5f;
        private bool hasThrown = false;

        public BossThrowState(BossController boss) : base(boss, BossStateType.Attack) { }

        public override void Enter()
        {
            base.Enter();
            timer = 0f;
            hasThrown = false;
            Debug.Log("<color=orange>[Boss] 進入投擲狀態 (Throw)！舉起雜魚/骨頭...</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            timer += Time.deltaTime;

            if (timer >= throwDelay && !hasThrown)
            {
                hasThrown = true;
                ThrowProjectile();
            }

            if (timer >= throwDelay + 0.5f)
            {
                boss.StateMachine.ChangeState(boss.IdleState);
            }
        }

        private void ThrowProjectile()
        {
            if (boss.ProjectilePrefab == null || boss.PlayerTransform == null) return;

            GameObject projObj = Object.Instantiate(boss.ProjectilePrefab, boss.transform.position, Quaternion.identity);
            Vector2 direction = (boss.PlayerTransform.position - boss.transform.position).normalized;

            BossProjectile proj = projObj.GetComponent<BossProjectile>();
            if (proj != null)
            {
                proj.Initialize(direction);
            }

            Debug.Log("<color=red>[Boss] 拋出骨頭/雜魚投擲物！</color>");
        }
    }
}