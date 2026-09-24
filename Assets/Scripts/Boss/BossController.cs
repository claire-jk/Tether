using UnityEngine;

namespace Tether.Boss
{
    /// <summary>
    /// Boss 憤怒值階段枚舉 (GDD 4 Phase Cycle)
    /// </summary>
    public enum AngerPhase
    {
        Calm,       // 平靜 (暗紅)
        Berserk,    // 狂暴 (藍黑燃燒)
        Angry,      // 憤怒 (亮紅)
        Breakdown   // 爆條 / 破防狀態 (灰碎玻璃)
    }

    public class BossController : MonoBehaviour
    {
        [Header("核心組件引用")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform partnerTransform;

        [Header("血量與破防 (PBR) 機制")]
        [SerializeField] private float maxHealth = 500f;
        private float currentHealth;

        [Header("憤怒值機制 (Anger System)")]
        [SerializeField] private float maxAnger = 100f;
        private float currentAnger = 0f;
        private AngerPhase currentAngerPhase = AngerPhase.Calm;

        [Header("招式Prefab與設定")]
        [SerializeField] private GameObject projectilePrefab;
        public GameObject ProjectilePrefab => projectilePrefab;

        // 招式 State 實例
        public BossChainsawSweepState LowSweepState { get; private set; }
        public BossChainsawSweepState HighSweepState { get; private set; }
        public BossThrowState ThrowState { get; private set; }
        public BossDashAttackState DashAttackState { get; private set; }
        public BossBackhandState BackhandState { get; private set; }
        public BossHarpoonPullState HarpoonPullState { get; private set; }

        // UI 事件（當血量、憤怒值或階段改變時通知 UI）
        public System.Action<float, float> OnHealthChanged;
        public System.Action<float, float, AngerPhase> OnAngerChanged;

        // FSM 狀態機
        public BossStateMachine StateMachine { get; private set; }
        public BossIdleState IdleState { get; private set; }
        public BossChaseState ChaseState { get; private set; }
        public BossAttackState AttackState { get; private set; }

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public float CurrentAnger => currentAnger;
        public float MaxAnger => maxAnger;
        public AngerPhase CurrentAngerPhase => currentAngerPhase;
        public Transform PlayerTransform => playerTransform;
        public Transform PartnerTransform => partnerTransform;

        private void Awake()
        {
            currentHealth = maxHealth;
            currentAnger = 0f;

            StateMachine = new BossStateMachine();
            IdleState = new BossIdleState(this);
            ChaseState = new BossChaseState(this);
            AttackState = new BossAttackState(this);

            // 階段 1 招式
            LowSweepState = new BossChainsawSweepState(this, SweepType.Low);
            HighSweepState = new BossChainsawSweepState(this, SweepType.High);
            ThrowState = new BossThrowState(this);

            // 階段 2 招式
            DashAttackState = new BossDashAttackState(this);
            BackhandState = new BossBackhandState(this);
            HarpoonPullState = new BossHarpoonPullState(this);
        }

        private void Start()
        {
            StateMachine.Initialize(IdleState);
            UpdateAngerPhase();

            // 初始化 UI 顯示
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Update()
        {
            if (StateMachine.CurrentState != null)
                StateMachine.CurrentState.LogicUpdate();

            // =================【階段 1 測試按鍵】=================
            if (Input.GetKeyDown(KeyCode.Alpha1)) StateMachine.ChangeState(LowSweepState);
            if (Input.GetKeyDown(KeyCode.Alpha2)) StateMachine.ChangeState(HighSweepState);
            if (Input.GetKeyDown(KeyCode.Alpha3)) StateMachine.ChangeState(ThrowState);

            // =================【階段 2 測試按鍵】=================
            if (Input.GetKeyDown(KeyCode.Alpha4)) StateMachine.ChangeState(DashAttackState);
            if (Input.GetKeyDown(KeyCode.Alpha5)) StateMachine.ChangeState(BackhandState);
            if (Input.GetKeyDown(KeyCode.Alpha6)) StateMachine.ChangeState(HarpoonPullState);
        }

        private void FixedUpdate()
        {
            if (StateMachine.CurrentState != null)
                StateMachine.CurrentState.PhysicsUpdate();
        }

        #region 憤怒值與血量控制

        public void AddAnger(float amount)
        {
            currentAnger = Mathf.Clamp(currentAnger + amount, 0f, maxAnger);
            UpdateAngerPhase();
        }

        public void ReduceAnger(float amount)
        {
            currentAnger = Mathf.Clamp(currentAnger - amount, 0f, maxAnger);
            UpdateAngerPhase();
        }

        private void UpdateAngerPhase()
        {
            float ratio = currentAnger / maxAnger;

            // 根據累積百分比判定 4 階段
            if (ratio >= 1.0f) currentAngerPhase = AngerPhase.Breakdown;
            else if (ratio >= 0.7f) currentAngerPhase = AngerPhase.Angry;
            else if (ratio >= 0.35f) currentAngerPhase = AngerPhase.Berserk;
            else currentAngerPhase = AngerPhase.Calm;

            // 廣播給 UI 更新
            OnAngerChanged?.Invoke(currentAnger, maxAnger, currentAngerPhase);
        }

        /// <summary>
        /// Boss 受傷核心邏輯
        /// </summary>
        public void TakeDamage(float amount)
        {
            currentHealth = Mathf.Clamp(currentHealth - amount, 0f, maxHealth);

            // 企劃需求：造成傷害時同時增加 Boss 憤怒值
            AddAnger(amount);

            // 廣播給 UI 更新 HP
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            Debug.Log($"<color=red>[Boss] 受到 {amount} 點傷害！剩餘 HP: {currentHealth}/{maxHealth}，當前憤怒值: {currentAnger}/{maxAnger}</color>");

            if (currentHealth <= 0f) Die();
        }

        private void Die()
        {
            Debug.Log("<color=red>[Boss] 血量歸零，擊敗老卡爾！</color>");
        }

        #endregion
    }
}