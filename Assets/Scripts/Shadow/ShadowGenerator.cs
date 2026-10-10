using UnityEngine;

[DefaultExecutionOrder(-60)]
[RequireComponent(typeof(BeamSpline))]
public class ShadowGenerator : MonoBehaviour
{
    public float shadowWidth = 2f;

    private BeamSpline beam;

    public Vector3[] shadowPoints;


    void Awake()
    {
        beam = GetComponent<BeamSpline>();
    }


    void Update()
    {
        GenerateShadow();
    }


    void GenerateShadow()
    {
        Vector3[] beamPoints = beam.points;

        if (beamPoints == null) return;
        if (shadowPoints == null || shadowPoints.Length != beamPoints.Length * 2)
            shadowPoints = new Vector3[beamPoints.Length * 2];


        for(int i = 0; i < beamPoints.Length; i++)
        {
            Vector3 offset =
                Vector3.right * shadowWidth;

            shadowPoints[i] =
                beamPoints[i] + offset;


            shadowPoints[
                shadowPoints.Length - 1 - i
            ] =
                beamPoints[i] - offset;
        }
    }
}
