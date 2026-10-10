using UnityEngine;

public enum LightColorType
{
    Blue,
    Red,
    Yellow,
    Green
}

[CreateAssetMenu(menuName = "LumenWave/Light Profile")]
public class LightProfile : ScriptableObject
{
    public LightColorType lightColor;

    public float beamLength = 8f;
    public float beamWidth = 0.3f;

    public float maxBend = 1.5f;
    public float animationSpeed = 6f;
    public bool chordPersonality;

    [Tooltip("Half-width multiplier from emitter (0) to beam end (1).")]
    public AnimationCurve spreadCurve = AnimationCurve.Linear(0, 1, 1, 1);
    public bool useCustomBendCurve;
    [Tooltip("Sideways displacement along the beam, multiplied by Max Bend and resonance.")]
    public AnimationCurve bendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
}
