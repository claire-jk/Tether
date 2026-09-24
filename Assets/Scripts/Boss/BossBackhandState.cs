using UnityEngine;

namespace Tether.Boss
{
    public class BossBackhandState : BossState
    {
        private float timer;
        private float attackDelay = 0.2f;
        private bool hitChecked = false;

        public BossBackhandState(BossController boss) : base(boss, BossStateType.Attack) { }

        public override void Enter()
        {
            base.Enter();
            timer = 0f;
            hitChecked = false;
            Debug.Log("<color=orange>[Boss 階段2] 發動回手掏！打擊身後/近身目標！</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            timer += Time.deltaTime;

            if (timer >= attackDelay && !hitChecked)
            {
                hitChecked = true;
                ExecuteBackhandHit();
            }

            if (timer >= attackDelay + 0.4f)
            {
                boss.StateMachine.ChangeState(boss.IdleState);
            }
        }

        private void ExecuteBackhandHit()
        {
            // 檢查反手/近身 hitBox
            Collider2D[] hits = Physics2D.OverlapCircleAll(boss.transform.position, 2.5f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player") || hit.CompareTag("Partner"))
                {
                    Debug.Log($"<color=red>★★ [回手掏] 命中目標 {hit.name}！造成擊退與傷害 ★★</color>");
                }
            }
        }
    }
}