using UnityEngine;

namespace Tether.Boss
{
    public enum SweepType
    {
        Low, // 下段掃腿 (需跳躍避開)
        High // 上段橫掃 (需蹲下/衝刺避開)
    }

    public class BossChainsawSweepState : BossState
    {
        private SweepType sweepType;
        private float stateTimer;
        private float windupDuration = 0.8f;   // 前搖預警時間
        private float attackDuration = 0.3f;   // 攻擊判定持續時間
        private float recoveryDuration = 0.6f; // 後搖冷卻時間
        private bool hasAttacked = false;

        public BossChainsawSweepState(BossController boss, SweepType type) : base(boss, BossStateType.Attack)
        {
            this.sweepType = type;
        }

        public override void Enter()
        {
            base.Enter();
            stateTimer = 0f;
            hasAttacked = false;
            Debug.Log($"<color=orange>[Boss] 進入電鋸橫掃預備 ({sweepType})！前搖警報中...</color>");
        }

        public override void LogicUpdate()
        {
            base.LogicUpdate();
            stateTimer += Time.deltaTime;

            // 1. 前搖階段 (蓄力 / 顯示攻擊紅框)
            if (stateTimer < windupDuration)
            {
                // 可在此處發送事件觸發視覺 Warning UI / 特效
            }
            // 2. 發動攻擊判定階段
            else if (stateTimer < windupDuration + attackDuration && !hasAttacked)
            {
                hasAttacked = true;
                ExecuteSweepHit();
            }
            // 3. 攻擊完畢進入後搖，隨後切回 Idle 狀態
            else if (stateTimer >= windupDuration + attackDuration + recoveryDuration)
            {
                boss.StateMachine.ChangeState(boss.IdleState);
            }
        }

        private void ExecuteSweepHit()
        {
            Debug.Log($"<color=red>★★ [Boss] 電鋸橫掃發動 ({sweepType})！進行傷害判定 ★★</color>");

            // 判定攻擊範圍與 Hitbox
            Vector2 attackOrigin = boss.transform.position;
            Vector2 attackSize = new Vector2(3.5f, sweepType == SweepType.Low ? 1.0f : 1.8f);
            Vector2 attackOffset = new Vector2(boss.transform.localScale.x > 0 ? 1.5f : -1.5f, sweepType == SweepType.Low ? -0.5f : 0.5f);

            Collider2D[] hits = Physics2D.OverlapBoxAll(attackOrigin + attackOffset, attackSize, 0f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    Debug.Log("<color=red>[Boss 橫掃] 命中玩家！造成 25 點傷害！</color>");
                    // 可呼叫玩家受傷介面：hit.GetComponent<PlayerController>()?.TakeDamage(25);
                }
                else if (hit.CompareTag("Partner"))
                {
                    Debug.Log("<color=purple>[Boss 橫掃] 命中夥伴！增加老卡爾憤怒值！</color>");
                    boss.AddAnger(15f); // 根據 GDD，打中夥伴會積攢憤怒值
                }
            }
        }
    }
}