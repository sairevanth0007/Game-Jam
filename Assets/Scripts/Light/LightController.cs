using UnityEngine;

public enum BeamStyle { Smooth, Fold, Zigzag, Coil }

[DefaultExecutionOrder(-80)]
[RequireComponent(typeof(BeamSpline))]
public class LightController : MonoBehaviour
{
    public LightProfile profile;

    [Header("How the beam reacts to a chord")]
    [SerializeField] BeamStyle style = BeamStyle.Fold;

    [Header("Fold settings")]
    [SerializeField, Range(.1f, .9f)] float foldPoint = .35f; // where the elbow ends up (0 = top, 1 = tip)

    [Header("Zigzag settings")]
    [SerializeField] float zigzagBends = 4f;   // higher = more corners

    [Header("Coil settings")]
    [SerializeField] float coilTurns = 3f;     // how many twists along the beam
    [SerializeField] float coilSpin = 12f;     // how fast it spins

    [Header("Coil climb")] // CHANGED: new section
    [SerializeField] float climbTime = 1f;     // CHANGED: seconds for the ripple to run from tip to lamp

    [Header("Coil dodge")]
    [SerializeField] float dodgeRange = 8f;  // how close sideways the player must be to get pushed
    [SerializeField] float dodgeSpeed = 3f;  // how fast the beam slides away (lower = easier to cross)

    Vector3 playerPos;
    bool hasPlayer;
    float dodge, lastDx, spinDir = 1, spinPhase;
    float front; // CHANGED: how far up the beam the ripple has climbed (0 = tip, 1.2 = past the lamp)

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

    public void TrackPlayer(Vector3 worldPos)
    {
        playerPos = worldPos;
        hasPlayer = true;
    }

    void Update()
    {
        if (profile == null || beam.points == null) return;
        // CHANGED: removed the Debug.Log line, it was only for testing

        if (hasPlayer)
        {
            float dx = transform.InverseTransformPoint(playerPos).x;

            // Push away from the player, stronger the closer they are to the beam.
            float closeness = 1f - Mathf.Clamp01(Mathf.Abs(dx) / dodgeRange);
            float away = (dx < 0 ? 1f : -1f) * closeness;
            dodge = Mathf.MoveTowards(dodge, away, dodgeSpeed * (1f + closeness * 2f) * Time.deltaTime);

            // The coil spins the opposite way to the player's movement.
            float vel = (dx - lastDx) / Mathf.Max(Time.deltaTime, .0001f);
            if (Mathf.Abs(vel) > .1f)
                spinDir = Mathf.MoveTowards(spinDir, vel > 0 ? -1f : 1f, 8f * Time.deltaTime);
            lastDx = dx;
        }
        spinPhase += coilSpin * Time.deltaTime;

        float bend = Mathf.Abs(profile.maxBend) * BendDirection;

        // CHANGED: the ripple starts at the tip when a chord lands and climbs toward the lamp.
        // When the glow ends (resonance back to 0) it resets for the next chord.
        front = resonance > 0.001f
            ? Mathf.MoveTowards(front, 1.2f, 1.2f / Mathf.Max(climbTime, .05f) * Time.deltaTime)
            : 0f;

        for (int i = 1; i < beam.points.Length; i++)
        {
            float t = i / (float)(beam.points.Length - 1); // 0 = top, 1 = tip

            if (profile.chordPersonality)
                beam.points[i].x = ChordX(t, bend);
            else
                beam.points[i].x = Mathf.Sin(Time.time * profile.animationSpeed + t * 2) * bend * resonance;
        }
    }

    // Decides how far sideways one point of the beam goes.
    // Update() calls this once per point, so each look is just a different recipe.
    float ChordX(float t, float bend)
    {
        switch (style)
        {
            case BeamStyle.Fold:
            {
                // The elbow starts at the tip and slides up to foldPoint as resonance grows.
                // Above the elbow the beam stays straight; below it, it slants away.
                float hinge = Mathf.Lerp(1f, foldPoint, resonance);
                float shape = Mathf.Max(0, t - hinge) / (1f - foldPoint);
                return shape * bend;
            }

            case BeamStyle.Zigzag:
                // Triangle wave: sharp corners, like a laser bouncing between mirrors.
                return Mathf.PingPong(t * zigzagBends, 1f) * bend * resonance;

            case BeamStyle.Coil:
            {
                float wave = Mathf.Sin(t * coilTurns * Mathf.PI * 2f + spinPhase);
                float size = Mathf.Abs(bend);
                float drift = dodge * size * t;               // the whole beam leans away from the player

                // CHANGED: only the part of the beam the ripple has reached moves...
                float edge = t - (1f - front);                // 0 right at the ripple's front
                float climb = Mathf.Clamp01(edge / .2f);

                // CHANGED: ...and a visible bump rides along at the front, fading out near the lamp.
                float fade = 1f - Mathf.Clamp01((front - 1f) / .2f);
                float crest = Mathf.Exp(-Mathf.Pow(edge * 5f, 2f)) * size * .6f * Mathf.Sign(bend) * t * fade;

                return (wave * t * size * .5f + drift) * resonance * climb + crest * resonance;
            }

            default: // Smooth = your original behavior
            {
                float shape =
                    profile.useCustomBendCurve && profile.bendCurve != null && profile.bendCurve.length > 0
                        ? profile.bendCurve.Evaluate(t)
                        : profile.lightColor == LightColorType.Red
                            ? Mathf.Clamp01((t - .12f) / .24f)
                            : Mathf.Sin(t * Mathf.PI * .5f);
                return shape * bend * resonance;
            }
        }
    }
}