using UnityEngine;

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

        shadowPoints = new Vector3[beamPoints.Length * 2];


        for(int i = 0; i < beamPoints.Length; i++)
        {
            Vector3 offset =
                transform.right * shadowWidth;

            shadowPoints[i] =
                beamPoints[i] + offset;


            shadowPoints[
                shadowPoints.Length - 1 - i
            ] =
                beamPoints[i] - offset;
        }
    }
}