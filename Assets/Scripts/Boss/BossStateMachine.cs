using UnityEngine;

namespace Tether.Boss
{
    /// <summary>
    /// 有限狀態機（FSM）管理器
    /// </summary>
    public class BossStateMachine
    {
        public BossState CurrentState { get; private set; }

        public void Initialize(BossState startingState)
        {
            CurrentState = startingState;
            CurrentState.Enter();
            Debug.Log($"<color=red>[Boss FSM] 初始狀態：{CurrentState.StateType}</color>");
        }

        public void ChangeState(BossState newState)
        {
            if (newState == null || newState == CurrentState) return;

            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();

            Debug.Log($"<color=red>[Boss FSM] 切換狀態至：{CurrentState.StateType}</color>");
        }
    }
}