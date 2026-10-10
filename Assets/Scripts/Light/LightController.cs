using UnityEngine;

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(BeamSpline))]
public class LightController : MonoBehaviour
{
    public LightProfile profile;

    private BeamSpline beam;

    private float resonance;

    void Awake()
    {
        beam = GetComponent<BeamSpline>();
    }

    public void SetResonance(float value)
    {
        resonance = Mathf.Clamp01(value);
    }

    void Update()
    {
        if (profile == null || beam.points == null) return;
        for(int i=1;i<beam.points.Length;i++)
        {
            float t = i/(float)(beam.points.Length-1);

            if (profile.chordPersonality)
            {
                float shape = profile.lightColor == LightColorType.Red
                    ? Mathf.Clamp01((t - .12f) / .24f)
                    : Mathf.Sin(t * Mathf.PI * .5f);
                beam.points[i].x = shape * profile.maxBend * resonance;
            }
            else beam.points[i].x =
                Mathf.Sin(Time.time*profile.animationSpeed+t*2)
                *profile.maxBend
                *resonance;
        }
    }
}
