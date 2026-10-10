using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Shared visual resources for the light system: procedural soft textures, beam/glow materials
    /// (uses the "Beam"/"Glow" shader materials when the visuals worker provides them, otherwise
    /// falls back to URP additive particles with a generated soft texture), and small helpers.
    /// </summary>
    public static class LightVisuals
    {
        static Texture2D _radial, _softLine;
        static Material _beamFallback, _glowFallback;
        static bool? _hasBeamShader, _hasGlowShader;

        public static Texture2D Radial
        {
            get
            {
                if (_radial != null) return _radial;
                const int n = 64;
                _radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LW_Radial" };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                    }
                _radial.SetPixels(px);
                _radial.Apply();
                return _radial;
            }
        }

        /// <summary>Soft across U (bright centre, transparent edges), constant along V.</summary>
        public static Texture2D SoftLine
        {
            get
            {
                if (_softLine != null) return _softLine;
                const int n = 64;
                _softLine = new Texture2D(n, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "LW_SoftLine" };
                var px = new Color[n * 4];
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Exp(-u * u * 5f) * Mathf.Clamp01((1f - Mathf.Abs(u)) * 4f);
                    for (int y = 0; y < 4; y++) px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
                _softLine.SetPixels(px);
                _softLine.Apply();
                return _softLine;
            }
        }

        public static bool HasBeamShader
        {
            get
            {
                if (!_hasBeamShader.HasValue) _hasBeamShader = Resources.Load<Material>("Materials/Beam") != null;
                return _hasBeamShader.Value;
            }
        }

        public static bool HasGlowShader
        {
            get
            {
                if (!_hasGlowShader.HasValue) _hasGlowShader = Resources.Load<Material>("Materials/Glow") != null;
                return _hasGlowShader.Value;
            }
        }

        /// <summary>Material for beam quads. Per-renderer colour goes through <see cref="SetBeamProps"/>.</summary>
        public static Material BeamMaterial
        {
            get
            {
                if (HasBeamShader) return MaterialLibrary.Get("Beam");
                if (_beamFallback == null)
                {
                    _beamFallback = MaterialLibrary.Create("ParticleAdditive");
                    _beamFallback.name = "LW_BeamFallback";
                    if (_beamFallback.HasProperty("_BaseMap")) _beamFallback.SetTexture("_BaseMap", SoftLine);
                    if (_beamFallback.HasProperty("_Cull")) _beamFallback.SetFloat("_Cull", 0f);
                }
                return _beamFallback;
            }
        }

        public static Material GlowMaterial
        {
            get
            {
                if (HasGlowShader) return MaterialLibrary.Get("Glow");
                if (_glowFallback == null)
                {
                    _glowFallback = MaterialLibrary.Create("ParticleAdditive");
                    _glowFallback.name = "LW_GlowFallback";
                    if (_glowFallback.HasProperty("_BaseMap")) _glowFallback.SetTexture("_BaseMap", Radial);
                    if (_glowFallback.HasProperty("_Cull")) _glowFallback.SetFloat("_Cull", 0f);
                }
                return _glowFallback;
            }
        }

        /// <summary>Particle material with the radial soft texture (sparks, motes).</summary>
        public static Material ParticleMaterial => GlowMaterial;

        public static void SetBeamProps(Renderer r, MaterialPropertyBlock mpb, Color color, float intensity, float length, float sigma)
        {
            r.GetPropertyBlock(mpb);
            var c = color * intensity;
            c.a = Mathf.Clamp01(intensity);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_BaseColor", c);
            mpb.SetFloat("_Intensity", intensity);
            mpb.SetFloat("_Length", length);
            mpb.SetFloat("_FogSigma", sigma);
            r.SetPropertyBlock(mpb);
        }

        public static void SetGlowColor(Renderer r, MaterialPropertyBlock mpb, Color color)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", color);
            mpb.SetColor("_BaseColor", color);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>Camera-facing (−Z) soft glow quad.</summary>
        public static MeshRenderer Glow(Transform parent, Vector3 localPos, float size, Color color, string name = "Glow")
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = GlowMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            SetGlowColor(r, new MaterialPropertyBlock(), color);
            return r;
        }

        /// <summary>A primitive without collider (visual only).</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static Light AddLight(Transform parent, LightType type, Color color, float intensity, float range, string name = "Light")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        public static ParticleSystem Sparks(Transform parent, Color color)
        {
            var go = new GameObject("HitSparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = color;
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = 28f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 55f;
            sh.radius = 0.05f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.3f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _hasBeamShader = null; _hasGlowShader = null; }
    }
}
