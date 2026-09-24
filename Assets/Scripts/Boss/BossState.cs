namespace Tether.Boss
{
    /// <summary>
    /// Boss 的所有可能狀態枚舉
    /// </summary>
    public enum BossStateType
    {
        Idle,               // 待機 / 巡邏
        Chase,              // 追攻玩家 / Partner
        Attack,             // 一般招式攻擊
        ParriedStun,        // 被 Parry 成功的硬直狀態
        BreakdownPBR,       // 進入 PBR (Partner Breakdown Recovery) 停機/破防狀態
        PhaseTransition,    // 一階段轉二階段演繹狀態
        Enraged             // 二階段狂暴狀態
    }

    /// <summary>
    /// Boss 狀態基類 (FSM State Base)
    /// </summary>
    public abstract class BossState
    {
        protected BossController boss;
        protected BossStateType stateType;

        public BossStateType StateType => stateType;

        public BossState(BossController boss, BossStateType stateType)
        {
            this.boss = boss;
            this.stateType = stateType;
        }

        // 進入狀態
        public virtual void Enter() { }

        // 每幀更新
        public virtual void LogicUpdate() { }

        // 物理更新 (FixedUpdate)
        public virtual void PhysicsUpdate() { }

        // 離開狀態
        public virtual void Exit() { }
    }
}