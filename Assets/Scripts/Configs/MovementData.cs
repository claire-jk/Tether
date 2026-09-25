using UnityEngine;

/// <summary>
/// 存放基礎移動物理手感的資料結構體 (企劃第 1 項需求)
/// </summary>
[System.Serializable]
public struct MovementData
{
    [Tooltip("最高速度 (Max Speed)")]
    public float maxSpeed;

    [Tooltip("加速度 (Acceleration)：速度 0 到最高速度間，按住方向鍵時每秒提升的速度")]
    public float acceleration;

    [Tooltip("減速度 (Deceleration)：放開方向鍵時每秒降低的速度")]
    public float deceleration;

    [Tooltip("轉向加速度 (Turn Acceleration)：目前移動方向與輸入方向不同時的加速度")]
    public float turnAcceleration;

    // 方便 Inspector 設定預設值的建構子
    public MovementData(float maxSpeed, float acceleration, float deceleration, float turnAcceleration)
    {
        this.maxSpeed = maxSpeed;
        this.acceleration = acceleration;
        this.deceleration = deceleration;
        this.turnAcceleration = turnAcceleration;
    }
}