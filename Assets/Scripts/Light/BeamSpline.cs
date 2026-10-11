using UnityEngine;

public class BeamSpline : MonoBehaviour
{
    public Vector3[] points;
    [Min(2)] public int pointCount = 33;
    [Min(.1f)] public float length = 8;

    private void Awake()
    {
        Rebuild();
    }

    public void Rebuild()
    {
        points = new Vector3[Mathf.Max(2, pointCount)];

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector3(0, -i * length / (points.Length - 1), 0);
        }
    }
}
