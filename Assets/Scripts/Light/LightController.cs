using UnityEngine;

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
        for(int i=1;i<beam.points.Length;i++)
        {
            float t = i/(float)(beam.points.Length-1);

            beam.points[i].x =
                Mathf.Sin(Time.time*profile.animationSpeed+t*2)
                *profile.maxBend
                *resonance;
        }
    }
}