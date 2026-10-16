using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Colour palette of a scene. Picked per level with the header line <c>mood: blue|green|red|gold|violet|ice|prism|dawn</c>
    /// (default blue). Drives fog, ambient, sky, backdrop silhouettes and the glowing edge on platforms.
    /// </summary>
    public struct Mood
    {
        public Color fog, ambient, skyTop, skyHorizon, skyBottom, moon, edge, layer, moonlight, dust;
        public float fogDensity;
        /// <summary>Grading: contrast / saturation (URP Color Adjustments units) and post exposure.</summary>
        public float contrast, saturation, exposure;

        public static Mood Get(string name)
        {
            var m = Blue;
            switch ((name ?? "").ToLowerInvariant())
            {
                case "green":
                    m.fog = new Color(0.025f, 0.06f, 0.055f); m.skyHorizon = new Color(0.05f, 0.14f, 0.12f);
                    m.skyTop = new Color(0.005f, 0.02f, 0.025f); m.edge = new Color(0.25f, 1.0f, 0.55f);
                    m.layer = new Color(0.02f, 0.05f, 0.05f); m.moon = new Color(0.9f, 1.4f, 1.1f); m.dust = new Color(0.6f, 1f, 0.7f);
                    m.saturation = 14f;
                    break;
                case "red":
                    // Dusky crimson-grey rather than saturated red, so red hazards still pop against it.
                    m.fog = new Color(0.055f, 0.038f, 0.036f); m.skyHorizon = new Color(0.22f, 0.14f, 0.12f);
                    m.skyTop = new Color(0.018f, 0.008f, 0.014f); m.edge = new Color(0.9f, 0.55f, 0.5f);
                    m.layer = new Color(0.045f, 0.025f, 0.03f); m.moon = new Color(1.5f, 0.95f, 0.85f); m.dust = new Color(0.9f, 0.7f, 0.65f);
                    m.moonlight = new Color(0.75f, 0.62f, 0.62f);
                    m.saturation = 0f;   // muted world, so red hazards are the most saturated thing on screen
                    break;
                case "gold":
                    m.fog = new Color(0.07f, 0.055f, 0.03f); m.skyHorizon = new Color(0.2f, 0.14f, 0.05f);
                    m.skyTop = new Color(0.02f, 0.015f, 0.02f); m.edge = new Color(1.0f, 0.8f, 0.3f);
                    m.layer = new Color(0.06f, 0.045f, 0.03f); m.moon = new Color(1.7f, 1.3f, 0.6f); m.dust = new Color(1f, 0.85f, 0.5f);
                    m.moonlight = new Color(0.85f, 0.75f, 0.55f);
                    m.saturation = 6f;
                    break;
                case "violet":
                    m.fog = new Color(0.05f, 0.03f, 0.08f); m.skyHorizon = new Color(0.14f, 0.07f, 0.22f);
                    m.skyTop = new Color(0.015f, 0.008f, 0.035f); m.edge = new Color(0.75f, 0.45f, 1.0f);
                    m.layer = new Color(0.045f, 0.03f, 0.07f); m.moon = new Color(1.3f, 1.0f, 1.8f); m.dust = new Color(0.85f, 0.7f, 1f);
                    break;
                case "ice":
                    // Cold cyan-white cavern; darker than "blue" so blue beams still pop.
                    m.fog = new Color(0.035f, 0.06f, 0.085f); m.skyHorizon = new Color(0.08f, 0.14f, 0.2f);
                    m.skyTop = new Color(0.005f, 0.012f, 0.025f); m.edge = new Color(0.6f, 0.9f, 1.0f);
                    m.layer = new Color(0.04f, 0.06f, 0.08f); m.moon = new Color(1.4f, 1.6f, 1.8f); m.dust = new Color(0.85f, 0.95f, 1f);
                    m.moonlight = new Color(0.7f, 0.82f, 1f);
                    m.contrast = 30f; m.saturation = 6f;
                    break;
                case "prism":
                    m.fog = new Color(0.04f, 0.03f, 0.07f); m.skyHorizon = new Color(0.1f, 0.07f, 0.18f);
                    m.skyTop = new Color(0.01f, 0.008f, 0.03f); m.edge = new Color(0.85f, 0.75f, 1.0f);
                    m.layer = new Color(0.04f, 0.03f, 0.06f); m.moon = new Color(1.4f, 1.3f, 1.8f); m.dust = new Color(0.9f, 0.85f, 1f);
                    m.moonlight = new Color(0.75f, 0.72f, 0.95f);
                    m.saturation = 14f;
                    break;
                case "dawn":
                    m.fog = new Color(0.16f, 0.12f, 0.16f); m.skyHorizon = new Color(0.55f, 0.35f, 0.3f);
                    m.skyTop = new Color(0.07f, 0.08f, 0.2f); m.skyBottom = new Color(0.2f, 0.12f, 0.15f);
                    m.edge = new Color(1.0f, 0.85f, 0.6f); m.layer = new Color(0.1f, 0.07f, 0.1f);
                    m.moon = new Color(2.5f, 1.9f, 1.2f); m.ambient = new Color(0.16f, 0.13f, 0.15f);
                    m.moonlight = new Color(1.0f, 0.85f, 0.7f); m.dust = new Color(1f, 0.9f, 0.7f); m.fogDensity = 0.012f;
                    m.contrast = 18f; m.exposure = 0.45f;
                    break;
            }
            return m;
        }

        public static Mood Blue => new Mood
        {
            fog = new Color(0.03f, 0.045f, 0.085f),
            ambient = new Color(0.07f, 0.08f, 0.11f),
            skyTop = new Color(0.006f, 0.01f, 0.03f),
            skyHorizon = new Color(0.07f, 0.1f, 0.18f),
            skyBottom = new Color(0.025f, 0.035f, 0.07f),
            moon = new Color(1.3f, 1.4f, 1.8f),
            edge = new Color(0.35f, 0.75f, 1.0f),
            layer = new Color(0.035f, 0.045f, 0.075f),
            moonlight = new Color(0.6f, 0.7f, 0.95f),
            dust = new Color(0.7f, 0.85f, 1f),
            fogDensity = 0.016f,
            contrast = 26f, saturation = 10f, exposure = 0.55f,
        };
    }
}
