using UnityEngine;

namespace LumenWave
{
    /// <summary>The gameplay result of standing in some light this frame.</summary>
    public struct LightEffect
    {
        public float damagePerSecond;
        public float healPerSecond;
        public float resonancePerSecond;
        public float gravityScale;
        public bool lethal;
        public bool inDarkness;
        /// <summary>The dominant harmful colour (for feedback / MA events).</summary>
        public LightColor harmfulColor;
    }

    /// <summary>
    /// The four light rules plus additive mixing, as pure logic:
    ///   RED burns (DoT, intense = lethal), GREEN heals + refills resonance, BLUE lowers gravity,
    ///   YELLOW (native or Red+Green overlap) only hurts on the beat. White (R+G+B) is neutral, pure light.
    /// </summary>
    public static class LightRules
    {
        public const float Threshold = 0.04f;
        public const float RedDps = 30f;
        public const float LethalRed = 4f;
        public const float YellowDps = 70f;
        public const float GreenHps = 18f;
        public const float GreenResonance = 30f;
        public const float BlueGravity = 0.35f;
        public const float DarknessThreshold = 0.12f;
        public const float DarknessResonance = 14f;

        public static LightEffect Evaluate(LightExposure e, bool beatHot)
        {
            float r = Mathf.Max(0f, e.r), g = Mathf.Max(0f, e.g), b = Mathf.Max(0f, e.b);

            // White light = the part present in all three channels: neutral.
            float w = Mathf.Min(r, Mathf.Min(g, b));
            r -= w; g -= w; b -= w;

            // Additive mixing: overlapping red + green is perceived (and behaves) as yellow.
            float y = Mathf.Min(r, g);
            r -= y; g -= y;
            y += Mathf.Max(0f, e.yellowPulse);

            var fx = new LightEffect { gravityScale = 1f };
            if (r > Threshold)
            {
                fx.damagePerSecond += r * RedDps;
                fx.harmfulColor = LightColor.Red;
                fx.lethal = r >= LethalRed;
            }
            if (y > Threshold && beatHot)
            {
                fx.damagePerSecond += y * YellowDps;
                if (fx.harmfulColor == LightColor.None) fx.harmfulColor = LightColor.Yellow;
            }
            if (g > Threshold)
            {
                fx.healPerSecond += g * GreenHps;
                fx.resonancePerSecond += g * GreenResonance;
            }
            if (b > Threshold)
            {
                fx.gravityScale = Mathf.Lerp(1f, BlueGravity, Mathf.Clamp01(b));
            }
            fx.inDarkness = e.Total < DarknessThreshold;
            if (fx.inDarkness) fx.resonancePerSecond += DarknessResonance;
            return fx;
        }
    }
}
