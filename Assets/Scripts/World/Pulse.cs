using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>Slow breathing glow for a sprite and/or a Light2D (doors, active runes).</summary>
    public class Pulse : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Light2D pulseLight;
        [SerializeField] float speed = 1.6f;
        [SerializeField, Range(0f, 1f)] float depth = 0.3f;

        float baseAlpha = 1f;
        float baseIntensity = 1f;
        float phase;

        public void Setup(SpriteRenderer sprite, Light2D light, float speed, float depth)
        {
            this.sprite = sprite;
            pulseLight = light;
            this.speed = speed;
            this.depth = depth;
            Capture();
        }

        void Start() => Capture();

        void Capture()
        {
            if (sprite != null) baseAlpha = sprite.color.a;
            if (pulseLight != null) baseIntensity = pulseLight.intensity;
            phase = Random.value * 10f;
        }

        void Update()
        {
            phase += Time.deltaTime * speed;
            float k = 1f - depth * (0.5f + 0.5f * Mathf.Sin(phase));
            if (sprite != null)
            {
                Color c = sprite.color;
                c.a = baseAlpha * k;
                sprite.color = c;
            }
            if (pulseLight != null) pulseLight.intensity = baseIntensity * k;
        }
    }
}
