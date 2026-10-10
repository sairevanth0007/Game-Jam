using UnityEngine;

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(BeamSpline))]
public class LightController : MonoBehaviour
{
    public LightProfile profile;

    private BeamSpline beam;

    private float resonance;
    private float bendDirection;
    public float BendDirection => bendDirection != 0 ? bendDirection : (profile != null && profile.maxBend < 0 ? -1 : 1);

    void Awake()
    {
        beam = GetComponent<BeamSpline>();
    }

    public void SetResonance(float value)
    {
        resonance = Mathf.Clamp01(value);
    }

    // Capture once per chord, in the fixture's local space. Shared profiles stay unchanged.
    public void BendAwayFrom(Vector3 playerPosition)
    {
        float side = transform.InverseTransformPoint(playerPosition).x;
        // Directly below the emitter, retain the last direction (or the authored default).
        if (Mathf.Abs(side) > .1f) bendDirection = side < 0 ? 1 : -1;
    }

    void Update()
    {
        if (profile == null || beam.points == null) return;
        float bend = Mathf.Abs(profile.maxBend) * BendDirection;
        for(int i=1;i<beam.points.Length;i++)
        {
            float t = i/(float)(beam.points.Length-1);

            if (profile.chordPersonality)
            {
                float shape = profile.useCustomBendCurve && profile.bendCurve != null && profile.bendCurve.length > 0
                    ? profile.bendCurve.Evaluate(t)
                    : profile.lightColor == LightColorType.Red
                    ? Mathf.Clamp01((t - .12f) / .24f)
                    : Mathf.Sin(t * Mathf.PI * .5f);
                beam.points[i].x = shape * bend * resonance;
            }
            else beam.points[i].x =
                Mathf.Sin(Time.time*profile.animationSpeed+t*2)
                *bend
                *resonance;
        }
    }
}
