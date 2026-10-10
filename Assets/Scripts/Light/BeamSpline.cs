using UnityEngine;

public class BeamSpline : MonoBehaviour
{
    public Vector3[] points;

    private void Awake()
    {
        points = new Vector3[5];

        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector3(0, -i * 2f, 0);
        }
    }
}