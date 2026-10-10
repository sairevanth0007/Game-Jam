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
    }

    void Update()
    {
        line.positionCount = beam.points.Length;

        for (int i = 0; i < beam.points.Length; i++)
        {
            line.SetPosition(i, beam.points[i]);
        }
    }
}