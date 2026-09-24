using UnityEngine;

public class TrajectoryLine : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private int resolution = 30; // 拋物線點的數量
    [SerializeField] private float timeStep = 0.1f;  // 計算的時間間隔

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();
    }

    /// <summary>
    /// 繪製拋物線軌跡
    /// </summary>
    public void RenderLine(Vector3 startPos, Vector2 startVelocity, float gravityScale)
    {
        if (lineRenderer == null) return;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = resolution;

        Vector3[] points = new Vector3[resolution];
        Vector2 currentPos = startPos;
        Vector2 currentVel = startVelocity;

        for (int i = 0; i < resolution; i++)
        {
            points[i] = currentPos;
            // 計算下一點的位置 (P = P0 + V*t + 0.5*g*t^2)
            float t = timeStep;
            currentPos += currentVel * t + 0.5f * Physics2D.gravity * gravityScale * t * t;
            currentVel += Physics2D.gravity * gravityScale * t;
        }

        lineRenderer.SetPositions(points);
    }

    /// <summary>
    /// 隱藏拋物線
    /// </summary>
    public void HideLine()
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }
}