using System;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Light colour as additive RGB channels. Yellow = Red|Green, White = all three.</summary>
    [Flags]
    public enum LightColor
    {
        None = 0,
        Red = 1,
        Green = 2,
        Blue = 4,
        Yellow = Red | Green,
        Cyan = Green | Blue,
        Magenta = Red | Blue,
        White = Red | Green | Blue,
    }

    /// <summary>Per-channel light intensity reaching a point (already attenuated).</summary>
    [Serializable]
    public struct LightExposure
    {
        public float r, g, b;
        /// <summary>Extra "yellow pulse" intensity from native yellow emitters (already gated by the beat).</summary>
        public float yellowPulse;

        public LightExposure(float r, float g, float b, float yellowPulse = 0f)
        {
            this.r = r; this.g = g; this.b = b; this.yellowPulse = yellowPulse;
        }

        public float Total => r + g + b + yellowPulse;

        public void Add(LightColor c, float intensity)
        {
            if ((c & LightColor.Red) != 0) r += intensity;
            if ((c & LightColor.Green) != 0) g += intensity;
            if ((c & LightColor.Blue) != 0) b += intensity;
        }

        public static LightExposure operator +(LightExposure a, LightExposure b) =>
            new LightExposure(a.r + b.r, a.g + b.g, a.b + b.b, a.yellowPulse + b.yellowPulse);
    }

    /// <summary>Pure physics helpers (EditMode-tested). All the real-world formulas live here.</summary>
    public static class LightMath
    {
        /// <summary>Inverse-square law I = I0 / d², with d clamped to minDistance so it never explodes.</summary>
        public static float InverseSquare(float i0, float distance, float minDistance = 1f)
        {
            float d = Mathf.Max(distance, minDistance);
            return i0 / (d * d);
        }

        /// <summary>Beer–Lambert attenuation through participating media: I = I0 · e^(−σd).</summary>
        public static float BeerLambert(float i0, float sigma, float distance)
        {
            return i0 * Mathf.Exp(-sigma * Mathf.Max(0f, distance));
        }

        /// <summary>Intensity of a light source at distance d through fog: inverse square × Beer–Lambert.</summary>
        public static float Attenuate(float i0, float distance, float sigma, float minDistance = 1f)
        {
            return BeerLambert(InverseSquare(i0, distance, minDistance), sigma, distance);
        }

        /// <summary>Law of reflection: r = d − 2(d·n)n. Angle in equals angle out.</summary>
        public static Vector2 Reflect(Vector2 direction, Vector2 normal)
        {
            Vector2 n = normal.normalized;
            return direction - 2f * Vector2.Dot(direction, n) * n;
        }

        public static Vector3 Reflect(Vector3 direction, Vector3 normal)
        {
            Vector3 n = normal.normalized;
            return direction - 2f * Vector3.Dot(direction, n) * n;
        }

        /// <summary>Schlick's approximation of Fresnel reflectance: F = F0 + (1−F0)(1−cosθ)^5.</summary>
        public static float Schlick(float cosTheta, float f0)
        {
            float m = Mathf.Clamp01(1f - cosTheta);
            return f0 + (1f - f0) * m * m * m * m * m;
        }

        /// <summary>Additive colour mixing of light (not paint): channels sum and clamp.</summary>
        public static LightColor Mix(LightColor a, LightColor b) => a | b;

        public static Color ToColor(LightColor c, float intensity = 1f)
        {
            return new Color((c & LightColor.Red) != 0 ? 1f : 0f,
                             (c & LightColor.Green) != 0 ? 1f : 0f,
                             (c & LightColor.Blue) != 0 ? 1f : 0f, 1f) * intensity;
        }

        /// <summary>Pleasant display tint for a light colour (pure RGB is harsh on screen).</summary>
        public static Color DisplayColor(LightColor c)
        {
            switch (c)
            {
                case LightColor.Red: return new Color(1f, 0.16f, 0.12f);
                case LightColor.Green: return new Color(0.25f, 1f, 0.45f);
                case LightColor.Blue: return new Color(0.25f, 0.55f, 1f);
                case LightColor.Yellow: return new Color(1f, 0.85f, 0.2f);
                case LightColor.Cyan: return new Color(0.3f, 1f, 1f);
                case LightColor.Magenta: return new Color(1f, 0.3f, 0.9f);
                case LightColor.White: return new Color(1f, 0.97f, 0.9f);
                default: return Color.black;
            }
        }

        /// <summary>
        /// Newton's prism: white light disperses into its components. Shorter wavelengths (blue) bend
        /// more than longer ones (red). Returns the deviation angle (degrees) for each split channel.
        /// </summary>
        public static float PrismDeviation(LightColor channel, float spreadDegrees = 18f)
        {
            switch (channel)
            {
                case LightColor.Red: return -spreadDegrees;   // least deviation
                case LightColor.Green: return 0f;
                case LightColor.Blue: return spreadDegrees;   // most deviation
                default: return 0f;
            }
        }
    }
}
