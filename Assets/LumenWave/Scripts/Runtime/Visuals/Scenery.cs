using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Layered environment behind (and in front of) the playfield, per <see cref="Theme"/>:
    ///  sky (Poly Haven night panorama tinted by mood) -> mountain ridge -> far / mid / near layers of real 3D
    ///  scenery (Kenney models, gears, crystals) fading into fog with distance -> mist sheets between layers ->
    ///  playfield dressing (small props on platform tops, no colliders) -> soft foreground silhouettes
    ///  (<see cref="Foreground"/>) and camera-following ambient particles (dust / fireflies / embers / snow).
    /// </summary>
    public static class Scenery
    {
        public struct LayerSpec { public float z0, z1, height, spacing, atmo, ridgeY, ridgeAmp, light; public string[] props; public float rim; }

        /// <summary>Colour distant scenery fades into (luminous, so silhouettes read against it).</summary>
        public static Color FogColor(Mood mood) => Color.Lerp(mood.fog, mood.skyHorizon, 0.7f) * 1.5f;

        public static Material PropMaterial(Mood mood, Color albedo, float atmo, float rim, float mistTop, float mistAmount, float light = 0.6f)
        {
            var m = MaterialLibrary.Create("Prop");
            var fog = FogColor(mood); fog.a = 1f;
            albedo.a = 1f;
            m.SetColor("_Color", albedo);
            // Rim in a desaturated moon tone (not the platform-lip colour, so scenery never mimics a walkable edge).
            var rc = Color.Lerp(mood.moon * 0.35f, mood.edge * 0.5f, 0.3f); rc.a = 1f;
            m.SetColor("_RimColor", rc * rim);
            m.SetColor("_FogColor", fog);
            m.SetFloat("_Atmo", atmo);
            m.SetFloat("_MistTop", mistTop);
            m.SetFloat("_MistRange", 7f);
            m.SetFloat("_MistAmount", mistAmount);
            m.SetFloat("_LightAmount", light);
            return m;
        }

        // ------------------------------------------------------------------ sky
        public static GameObject Sky(Transform root, float width, float height, float groundY, Mood mood, Theme theme, System.Random rng)
        {
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Backdrop.RemoveCollider(sky);
            sky.name = "Sky";
            sky.transform.SetParent(root, false);
            // Horizon (uv 0.42) a little above the typical floor so the glow sits behind the scenery.
            sky.transform.position = new Vector3(width * 0.5f, groundY + 6f + (0.5f - 0.42f) * 300f, 150f);
            sky.transform.localScale = new Vector3(width * 3f + 420f, 300f, 1f);
            var skyMat = MaterialLibrary.Create("Sky");
            skyMat.SetColor("_TopColor", mood.skyTop);
            skyMat.SetColor("_HorizonColor", FogColor(mood));
            skyMat.SetColor("_BottomColor", mood.skyBottom);
            skyMat.SetColor("_MoonColor", Color.black);
            skyMat.SetFloat("_Aspect", sky.transform.localScale.x / sky.transform.localScale.y);
            skyMat.SetFloat("_Horizon", 0.42f);
            var pano = Resources.Load<Texture2D>("Art/PolyHaven/" + theme.sky);
            if (pano != null && skyMat.HasProperty("_PanoTex"))
            {
                skyMat.SetTexture("_PanoTex", pano);
                // Tint: mood hue at a brightness that keeps the sky clearly behind the playfield.
                var hue = mood.skyHorizon; float mx = Mathf.Max(0.001f, hue.maxColorComponent);
                var tint = Color.Lerp(Color.white, new Color(hue.r / mx, hue.g / mx, hue.b / mx), 0.75f) * 0.32f * theme.skyExposure;
                tint.a = 1f;
                skyMat.SetColor("_PanoTint", tint);
                skyMat.SetFloat("_PanoZoom", 0.75f);
                skyMat.SetFloat("_PanoLift", 0.06f);
                skyMat.SetFloat("_PanoOffset", (float)rng.NextDouble());
                skyMat.SetFloat("_StarAmount", theme.stars);
            }
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            Backdrop.Shadowless(sky);
            return sky;
        }

        // ------------------------------------------------------------------ layers
        public static void Layers(Transform root, float x0, float x1, float groundY, Mood mood, Theme theme, System.Random rng, System.Func<float, float> floorAt = null)
        {
            floorAt = floorAt ?? (x => groundY);
            var specs = new[]
            {
                // Skyline heights are chosen so layers top out around 55-75 % of the screen (sky stays open).
                new LayerSpec { z0 = 75f, z1 = 75f, atmo = 0.9f, ridgeY = groundY + 3f, ridgeAmp = 8f, props = null, rim = 0.15f, light = 0.1f },
                new LayerSpec { z0 = 40f, z1 = 54f, height = theme.farHeight * 0.45f, spacing = theme.farSpacing, atmo = 0.78f, ridgeY = groundY - 1f, ridgeAmp = 3.5f, props = theme.farProps, rim = 0.3f, light = 0.15f },
                new LayerSpec { z0 = 17f, z1 = 26f, height = theme.midHeight * 0.6f, spacing = theme.midSpacing, atmo = 0.55f, ridgeY = groundY - 2f, ridgeAmp = 2f, props = theme.midProps, rim = 0.45f, light = 0.15f },
                new LayerSpec { z0 = 6.5f, z1 = 10f, height = theme.nearHeight * 0.65f, spacing = theme.nearSpacing * 2.2f, atmo = 0.28f, ridgeY = groundY - 3.5f, ridgeAmp = 1.2f, props = theme.nearProps, rim = 0.3f, light = 0.1f },
            };
            // Near-black albedo: layers read as silhouettes; depth comes from the fog (atmosphere) amount.
            var baseAlbedo = Color.Lerp(mood.layer, theme.rockTint * 0.15f, 0.5f);
            for (int i = 1; i < specs.Length - 1; i++) specs[i].rim *= theme.rimBoost;
            foreach (var L in specs)
            {
                float zMid = (L.z0 + L.z1) * 0.5f;
                float margin = 16f + 0.6f * zMid;
                float a = x0 - margin, b = x1 + margin;
                var mat = PropMaterial(mood, baseAlbedo, L.atmo, L.rim, L.ridgeY + L.ridgeAmp * 0.4f + 3f, 0.55f, L.light);
                var layer = new GameObject("Layer_z" + Mathf.RoundToInt(zMid)).transform;
                layer.SetParent(root, false);
                // Ground ridge (so the scenery stands on something when seen through gaps).
                float phase = (float)rng.NextDouble() * 100f;
                float freq = L.ridgeAmp > 10f ? 0.02f : 0.05f;
                var mb = new MeshBuilder();
                int steps = Mathf.CeilToInt((b - a) / 1.2f);
                float bottom = L.ridgeY - 30f;
                var white = Color.white;
                // Near layers follow the level's local floor (so they stand behind the terrain); the far ones less so.
                float follow = L.z0 < 12f ? 0.6f : L.z0 < 30f ? 0.8f : (L.z0 < 60f ? 0.6f : 0.3f);
                System.Func<float, float> ridge = x => Ridge(x, L, phase, freq) + (floorAt(x) - groundY) * follow;
                float px = a, py = ridge(a);
                for (int i = 1; i <= steps; i++)
                {
                    float x = Mathf.Lerp(a, b, i / (float)steps);
                    float y = ridge(x);
                    mb.Quad(new Vector3(px, bottom, zMid), new Vector3(x, bottom, zMid), new Vector3(x, y, zMid), new Vector3(px, py, zMid), white);
                    px = x; py = y;
                }
                var rgo = mb.ToGameObject("Ridge", mat);
                rgo.transform.SetParent(layer, false);
                if (L.props == null || L.props.Length == 0) continue;
                float xx = a + (float)rng.NextDouble() * L.spacing;
                while (xx < b)
                {
                    var path = L.props[rng.Next(L.props.Length)];
                    float h = L.height * (0.65f + (float)rng.NextDouble() * 0.6f);
                    float z = Mathf.Lerp(L.z0, L.z1, (float)rng.NextDouble());
                    float y = ridge(xx) - 0.4f;
                    bool architectural = path.StartsWith("KenneyCastle") || path.StartsWith("KenneyGraveyard");
                    float yaw = architectural ? rng.Next(4) * 90f + ((float)rng.NextDouble() - 0.5f) * 20f : (float)rng.NextDouble() * 360f;
                    PropLibrary.Spawn(path, layer, new Vector3(xx, y, z), h, yaw, mat, rng.Next(2) == 0, 1f, h * 1.4f);
                    float w = h * Mathf.Clamp(PropLibrary.Aspect(path), 0.3f, 1.4f);
                    xx += Mathf.Max(L.spacing * (0.55f + (float)rng.NextDouble() * 0.9f), w * 0.45f);
                }
            }
        }

        static float Ridge(float x, LayerSpec L, float phase, float freq)
        {
            float n = Mathf.PerlinNoise(x * freq + phase, phase * 0.37f) * 0.75f + Mathf.PerlinNoise(x * freq * 3.1f + phase, 11.3f) * 0.25f;
            return L.ridgeY + n * L.ridgeAmp;
        }

        // ------------------------------------------------------------------ mist
        public static void Mist(Transform root, float x0, float x1, float groundY, Mood mood, Theme theme)
        {
            var fog = FogColor(mood);
            var specs = new[] { (z: 12f, h: 9f, a: 0.32f), (z: 28f, h: 14f, a: 0.4f), (z: 58f, h: 22f, a: 0.5f) };
            foreach (var s in specs)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Backdrop.RemoveCollider(q);
                q.name = "Mist_z" + s.z;
                q.transform.SetParent(root, false);
                float margin = 20f + s.z;
                float w = (x1 - x0) + margin * 2f;
                q.transform.position = new Vector3((x0 + x1) * 0.5f, groundY - 3f + s.h * 0.5f, s.z);
                q.transform.localScale = new Vector3(w, s.h, 1f);
                var m = MaterialLibrary.Create("Mist");
                var c = fog * 1.15f; c.a = s.a;
                m.SetColor("_Color", c);
                m.SetVector("_NoiseScale", new Vector4(0.06f, 0.35f, 0, 0));
                m.SetFloat("_Speed", 0.25f + s.z * 0.004f);
                q.GetComponent<MeshRenderer>().sharedMaterial = m;
                Backdrop.Shadowless(q);
            }
        }

        // ------------------------------------------------------------------ dressing
        /// <summary>Small purely visual props on platform tops, clear of entities and hazards.</summary>
        public static void Dressing(Transform root, LevelData d, Mood mood, Theme theme, System.Random rng)
        {
            if (d == null || theme.topProps == null || theme.topProps.Length == 0) return;
            var go = new GameObject("Dressing").transform;
            go.SetParent(root, false);
            var mat = PropMaterial(mood, theme.topPropTint, 0f, 0.35f, -100f, 0f, 0.8f);
            // Boulders half-embedded in the platform fronts, lit like the rock (purely visual).
            var boulderMat = PropMaterial(mood, theme.rockTint * 0.55f, 0f, 0.5f, -100f, 0f, 1.1f);
            string[] boulders = { "KenneyNature/rock_largeA", "KenneyNature/rock_largeB", "KenneyNature/rock_largeC", "KenneyNature/rock_largeD", "KenneyNature/stone_largeA" };
            var blocked = new List<Vector2>();
            foreach (var e in d.entities)
            {
                if (e.type == "bot" || e.type == "tutorial" || e.type == "lamp") continue;
                blocked.Add(e.pos);
            }
            int w = d.Width, h = d.Height;
            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 1; x < w - 1; x++)
                {
                    char c = d.Tile(x, y);
                    if (!LevelData.IsSolid(c) || d.Tile(x, y + 1) != ' ') continue;
                    if (rng.NextDouble() > 0.5) continue;
                    float top = y + 1;
                    bool near = false;
                    foreach (var p in blocked) if (Mathf.Abs(p.x - (x + 0.5f)) < 1.6f && p.y > top - 3f && p.y < top + 4f) { near = true; break; }
                    if (near) continue;
                    bool ledge = c == '=';
                    if (!ledge && rng.Next(9) == 0 && FaceInterior(d, x, y))
                    {
                        float bh = 0.7f + (float)rng.NextDouble() * 0.7f;
                        float by = top - 0.45f - bh - (float)rng.NextDouble() * 0.8f;
                        PropLibrary.Spawn(boulders[rng.Next(boulders.Length)], go, new Vector3(x + 0.5f, by, -2.05f), bh, (float)rng.NextDouble() * 360f, boulderMat, false, 1.3f, 1.3f);
                    }
                    float px = x + 0.15f + (float)rng.NextDouble() * 0.7f;
                    var path = theme.topProps[rng.Next(theme.topProps.Length)];
                    bool front = !ledge && rng.Next(5) == 0;
                    float z = ledge ? 0.45f + (float)rng.NextDouble() * 0.4f
                            : front ? -1.75f + (float)rng.NextDouble() * 0.25f
                            : 0.6f + (float)rng.NextDouble() * 1.2f;
                    float ph = (front || ledge ? 0.18f : 0.32f) + (float)rng.NextDouble() * (front || ledge ? 0.14f : 0.4f);
                    PropLibrary.Spawn(path, go, new Vector3(px, top - 0.03f, z), ph, (float)rng.NextDouble() * 360f, mat, false, 1.2f, ledge || front ? 0.6f : 1.1f);
                }
            }
        }

        /// <summary>True if the 3x3 cells below (x, y) are solid, i.e. a boulder there sits inside a wall face, clear of walkways.</summary>
        static bool FaceInterior(LevelData d, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = 0; dy <= 2; dy++)
                    if (!LevelData.IsSolid(d.Tile(x + dx, y - dy))) return false;
            return LevelData.IsSolid(d.Tile(x - 1, y)) == false && LevelData.IsSolid(d.Tile(x + 1, y)) == false
                ? true : !LevelData.IsSolid(d.Tile(x - 1, y + 1)) && !LevelData.IsSolid(d.Tile(x + 1, y + 1));
        }

        // ------------------------------------------------------------------ glows
        public static void DistantGlows(Transform root, float width, float height, Mood mood, System.Random rng, Theme theme = null, System.Func<float, float> floorAt = null)
        {
            float mult = theme != null ? theme.glowCount : 1f;
            int glows = Mathf.Max(2, Mathf.RoundToInt(width / 22f * mult));
            for (int i = 0; i < glows; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Backdrop.RemoveCollider(g);
                g.name = "DistantGlow";
                g.transform.SetParent(root, false);
                float z = 22f + (float)rng.NextDouble() * 30f;
                float gx = (float)rng.NextDouble() * width;
                float baseY = floorAt != null ? floorAt(gx) : 0f;
                bool warm = theme != null && theme.glowColor.HasValue;
                // Warm fires / soft light pools sit low, near the ground of the far layers.
                g.transform.position = new Vector3(gx, baseY + (warm ? -1f + (float)rng.NextDouble() * 5f : 1f + (float)rng.NextDouble() * 9f), z);
                float s = 5f + (float)rng.NextDouble() * 8f;
                g.transform.localScale = new Vector3(s, s, 1f);
                var gc = (warm ? theme.glowColor.Value * 0.2f : mood.edge * 0.12f); gc.a = 1f;
                g.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Tinted("Glow", gc);
                Backdrop.Shadowless(g);
            }
        }

        // ------------------------------------------------------------------ particles
        public static ParticleSystem Motes(Transform root, Mood mood, Theme theme, bool menu)
        {
            var go = new GameObject("AmbientMotes");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(34f, 20f, 16f);
            var noise = ps.noise;
            noise.enabled = true;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            Color c0 = mood.dust, c1 = mood.dust;
            switch (theme.motes)
            {
                case MoteKind.Fireflies:
                    c0 = theme.name == "garden" ? new Color(0.6f, 1.6f, 0.7f) : new Color(1.4f, 1.6f, 0.8f);
                    c1 = c0 * 0.7f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                    main.maxParticles = 60;
                    em.rateOverTime = 7f;
                    noise.strength = 0.6f; noise.frequency = 0.4f; noise.scrollSpeed = 0.2f;
                    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.15f, 0.35f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0.2f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    break;
                case MoteKind.Embers:
                    c0 = new Color(2.2f, 0.7f, 0.25f); c1 = new Color(1.4f, 0.35f, 0.12f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                    main.gravityModifier = -0.025f;
                    main.maxParticles = 140;
                    em.rateOverTime = 18f;
                    noise.strength = 0.5f; noise.frequency = 0.6f; noise.scrollSpeed = 0.3f;
                    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.5f, 0.4f), 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
                    break;
                case MoteKind.Snow:
                    c0 = new Color(0.95f, 1f, 1.1f); c1 = new Color(0.6f, 0.75f, 0.95f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
                    main.gravityModifier = 0.012f;
                    main.maxParticles = 320;
                    em.rateOverTime = 40f;
                    noise.strength = 0.35f; noise.frequency = 0.3f; noise.scrollSpeed = 0.15f;
                    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f) });
                    break;
                case MoteKind.Sparkles:
                    c0 = mood.edge * 1.6f; c1 = mood.dust;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.15f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                    main.maxParticles = 120;
                    em.rateOverTime = 22f;
                    noise.strength = 0.2f; noise.frequency = 0.3f; noise.scrollSpeed = 0.1f;
                    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.1f, 0.4f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
                    break;
                default:
                    c0 = mood.dust; c0.a = 0.6f; c1 = mood.dust * 0.6f; c1.a = 0.3f;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                    main.maxParticles = 220;
                    em.rateOverTime = 26f;
                    noise.strength = 0.25f; noise.frequency = 0.25f; noise.scrollSpeed = 0.1f;
                    grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                                 new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
                    break;
            }
            if (menu) em.rateOverTime = em.rateOverTime.constant * 1.5f;
            // Vertex colours are LDR: normalise them and push the HDR brightness through the material.
            float boost = Mathf.Max(1f, Mathf.Max(c0.r, Mathf.Max(c0.g, c0.b)));
            c0 = new Color(c0.r / boost, c0.g / boost, c0.b / boost, c0.a);
            c1 = new Color(Mathf.Min(1f, c1.r / boost), Mathf.Min(1f, c1.g / boost), Mathf.Min(1f, c1.b / boost), c1.a);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c0);
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var pm = MaterialLibrary.Create("GlowParticle");
            pm.SetColor("_Color", Color.white * boost * (theme.motes == MoteKind.Fireflies ? 1.6f : 1.2f));
            r.sharedMaterial = pm;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<FollowCamera>().offset = new Vector3(0f, 0f, 22f);
            ps.Play();
            return ps;
        }
    }

    /// <summary>Keeps an emitter (e.g. ambient motes, world-space particles) centred on the camera view.</summary>
    [DefaultExecutionOrder(100)]
    public class FollowCamera : MonoBehaviour
    {
        public Vector3 offset;
        void LateUpdate()
        {
            var cam = SceneRig.Instance != null ? SceneRig.Instance.Camera : Camera.main;
            if (cam == null) return;
            var p = cam.transform.position;
            transform.position = new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z);
        }
    }
}
