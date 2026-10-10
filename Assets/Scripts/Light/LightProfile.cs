using UnityEngine;

public enum LightColorType
{
    Blue,
    Red,
    Yellow
}

[CreateAssetMenu(menuName = "LumenWave/Light Profile")]
public class LightProfile : ScriptableObject
{
    public LightColorType lightColor;

    public float beamLength = 8f;
    public float beamWidth = 0.3f;

    public float maxBend = 1.5f;
    public float animationSpeed = 6f;
}