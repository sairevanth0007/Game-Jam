using UnityEngine;

[DefaultExecutionOrder(-60)]
[RequireComponent(typeof(BeamSpline))]
public class ShadowGenerator : MonoBehaviour
{
    public float shadowWidth = 2f;

    private BeamSpline beam;
    private LightController lightController;

    public Vector3[] shadowPoints;


    void Awake()
    {
        beam = GetComponent<BeamSpline>();
        lightController = GetComponent<LightController>();
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
            var curve = lightController != null && lightController.profile != null ? lightController.profile.spreadCurve : null;
            float t = i / (float)(beamPoints.Length - 1);
            float spread = curve != null && curve.length > 0 ? Mathf.Max(0, curve.Evaluate(t)) : 1;
            Vector3 offset =
                Vector3.right * Mathf.Max(0, shadowWidth) * spread;

            shadowPoints[i] =
                beamPoints[i] + offset;


            shadowPoints[
                shadowPoints.Length - 1 - i
            ] =
                beamPoints[i] - offset;
        }
    }
}
