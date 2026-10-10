using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(BeamSpline))]
public class BeamRenderer : MonoBehaviour
{
    private LineRenderer line;
    private BeamSpline beam;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        beam = GetComponent<BeamSpline>();
        line.useWorldSpace = false;
    }

    void LateUpdate()
    {
        if (beam.points == null) return;
        line.positionCount = beam.points.Length;

        for (int i = 0; i < beam.points.Length; i++)
        {
            line.SetPosition(i, beam.points[i]);
        }
    }
}
