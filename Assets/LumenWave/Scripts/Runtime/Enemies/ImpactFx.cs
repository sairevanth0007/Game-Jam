using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Pooled one-shot impact effects shared by powers, enemies and crystals: sparks, flashes, expanding rings and
    /// spinning glass shards. Four world-space particle systems per scene, driven by <c>Emit(EmitParams)</c>, so a hit
    /// costs no allocations and the particle count is capped. Purely cosmetic (no colliders, no gameplay state).
    /// </summary>
    public class ImpactFx : MonoBehaviour
    {
        static ImpactFx _instance;
        ParticleSystem _sparks, _flash, _ring, _shards, _motes;
        Material _sparkMat, _ringMat, _shardMat;
        Mesh _shardMesh;
        static Texture2D _ringTex;

        public const float FrontZ = -0.6f;   // slightly towards the camera so effects sit in front of props

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameEvents.CrystalShattered += OnCrystal;
            GameEvents.EnemyStunned += OnStunned;
        }

        static void OnCrystal(Vector3 p)
        {
            var c = new Color(0.55f, 0.85f, 1f);
            Shatter(p, c, 26);
        }

        static void OnStunned(Vector3 p)
        {
            Flash(p, new Color(0.9f, 0.97f, 1f), 2.2f, 0.18f);
            Ring(p, new Color(0.7f, 0.9f, 1.4f), 1.8f, 0.35f);
        }

        static ImpactFx Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;
                var go = new GameObject("ImpactFx");
                _instance = go.AddComponent<ImpactFx>();
                _instance.Build();
                return _instance;
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_sparkMat != null) Destroy(_sparkMat);
            if (_ringMat != null) Destroy(_ringMat);
            if (_shardMat != null) Destroy(_shardMat);
            if (_shardMesh != null) Destroy(_shardMesh);
        }

        ParticleSystem MakeSystem(string name, Material mat, int max, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.enabled = false;
            var sh = ps.shape;
            sh.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static Gradient Fade(float peak = 1f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(peak, 0f), new GradientAlphaKey(peak * 0.6f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void Build()
        {
            _sparkMat = EnemyUtil.GlowMaterial(Color.white);
            if (_ringTex == null)
            {
                const int n = 128;
                _ringTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "ImpactRing", wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float k = (d - 0.88f) / 0.07f;
                        float a = Mathf.Exp(-k * k) + (d < 0.88f ? 0.12f * Mathf.Exp(-(0.88f - d) * 5f) : 0f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
                    }
                _ringTex.SetPixels32(px);
                _ringTex.Apply();
            }
            _ringMat = MaterialLibrary.Create("ParticleAdditive");
            if (_ringMat.HasProperty("_BaseMap")) _ringMat.SetTexture("_BaseMap", _ringTex);
            if (_ringMat.HasProperty("_MainTex")) _ringMat.SetTexture("_MainTex", _ringTex);
            _shardMat = EnemyUtil.GlowMaterial(Color.white);

            _sparks = MakeSystem("Sparks", _sparkMat, 400, 0.8f);
            var col = _sparks.colorOverLifetime; col.enabled = true; col.color = Fade();
            var size = _sparks.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            var sr = _sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.05f;
            sr.lengthScale = 1.5f;
            var drag = _sparks.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 2.5f; drag.multiplyDragByParticleSize = false;

            _motes = MakeSystem("Motes", _sparkMat, 300, 0f);
            var mc = _motes.colorOverLifetime; mc.enabled = true; mc.color = Fade();

            _flash = MakeSystem("Flash", _sparkMat, 24, 0f);
            var fc = _flash.colorOverLifetime; fc.enabled = true; fc.color = Fade();
            var fs = _flash.sizeOverLifetime; fs.enabled = true; fs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.2f)));

            _ring = MakeSystem("Rings", _ringMat, 32, 0f);
            var rc = _ring.colorOverLifetime; rc.enabled = true; rc.color = Fade();
            var rs = _ring.sizeOverLifetime; rs.enabled = true; rs.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f, 0.3f, 0f)));

            _shardMesh = BuildShardMesh();
            _shards = MakeSystem("Shards", _shardMat, 160, 1.6f);
            var shm = _shards.main; shm.startRotation3D = true;
            var shc = _shards.colorOverLifetime; shc.enabled = true; shc.color = Fade();
            var rot = _shards.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f); rot.y = new ParticleSystem.MinMaxCurve(-10f, 10f); rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var shr = _shards.GetComponent<ParticleSystemRenderer>();
            shr.renderMode = ParticleSystemRenderMode.Mesh;
            shr.mesh = _shardMesh;
            shr.alignment = ParticleSystemRenderSpace.Local;
            var sdrag = _shards.limitVelocityOverLifetime; sdrag.enabled = true; sdrag.drag = 1.2f; sdrag.multiplyDragByParticleSize = false;
        }

        /// <summary>A thin double-sided glass sliver (kite shape), UVs at the texture centre so it renders solid.</summary>
        static Mesh BuildShardMesh()
        {
            var m = new Mesh { name = "Shard" };
            var v = new[] { new Vector3(0f, 0.5f, 0f), new Vector3(0.18f, 0.05f, 0f), new Vector3(0f, -0.5f, 0f), new Vector3(-0.14f, -0.05f, 0f) };
            var uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.62f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.38f, 0.5f) };
            m.vertices = v;
            m.uv = uv;
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        static ParticleSystem.EmitParams _ep;
        // Own xorshift RNG: cosmetic randomness must not consume UnityEngine.Random (keeps gameplay/bot runs deterministic).
        static uint _seed = 2463534242u;
        static float R(float a, float b)
        {
            _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
            return a + (b - a) * ((_seed & 0xFFFFFF) / 16777216f);
        }
        static Vector2 Dir() { float a = R(0f, Mathf.PI * 2f); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }

        static Vector3 Front(Vector3 p) { p.z = Mathf.Min(p.z, 0f) + FrontZ; return p; }

        /// <summary>Burst of glowing sparks flying outward.</summary>
        public static void Sparks(Vector3 pos, Color color, int count, float speed = 5f, float size = 0.12f, float life = 0.6f)
        {
            var fx = Instance; if (fx == null || count <= 0) return;
            pos = Front(pos);
            for (int i = 0; i < count; i++)
            {
                var d = Dir();
                if (d.sqrMagnitude < 0.01f) d = Vector2.up;
                _ep = new ParticleSystem.EmitParams
                {
                    position = pos + (Vector3)(d * 0.1f),
                    velocity = (Vector3)(d * speed * R(0.35f, 1f)) + Vector3.up * speed * 0.15f,
                    startColor = color,
                    startSize = size * R(0.5f, 1f),
                    startLifetime = life * R(0.6f, 1.2f),
                };
                fx._sparks.Emit(_ep, 1);
            }
        }

        /// <summary>A single drifting glow mote (no drag, no gravity): trails, wavefront riders.</summary>
        public static void Mote(Vector3 pos, Vector3 vel, Color color, float size, float life)
        {
            var fx = Instance; if (fx == null) return;
            _ep = new ParticleSystem.EmitParams { position = Front(pos), velocity = vel, startColor = color, startSize = size, startLifetime = life };
            fx._motes.Emit(_ep, 1);
        }

        /// <summary>Motes flying out radially at a fixed speed (they ride an expanding wavefront).</summary>
        public static void RingBurst(Vector3 pos, Color color, int count, float speed, float life, float size = 0.12f)
        {
            var fx = Instance; if (fx == null) return;
            var p = Front(pos);
            for (int i = 0; i < count; i++)
            {
                float a = (i + R(0f, 0.6f)) / count * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                _ep = new ParticleSystem.EmitParams
                {
                    position = p + d * 0.2f,
                    velocity = d * speed * R(0.9f, 1.02f),
                    startColor = color,
                    startSize = size * R(0.6f, 1.2f),
                    startLifetime = life * R(0.85f, 1f),
                };
                fx._motes.Emit(_ep, 1);
            }
        }

        /// <summary>Short bright bloom (hit flash).</summary>
        public static void Flash(Vector3 pos, Color color, float size = 2f, float life = 0.2f)
        {
            var fx = Instance; if (fx == null) return;
            _ep = new ParticleSystem.EmitParams { position = Front(pos), velocity = Vector3.zero, startColor = color, startSize = size, startLifetime = life };
            fx._flash.Emit(_ep, 1);
        }

        /// <summary>Expanding thin ring (shock / ripple) reaching <paramref name="radius"/> at the end of its life.</summary>
        public static void Ring(Vector3 pos, Color color, float radius = 1.5f, float life = 0.4f)
        {
            var fx = Instance; if (fx == null) return;
            _ep = new ParticleSystem.EmitParams { position = Front(pos), velocity = Vector3.zero, startColor = color, startSize = radius * 2f, startLifetime = life };
            fx._ring.Emit(_ep, 1);
        }

        /// <summary>Glass-shard explosion: spinning slivers + sparks + flash + ring.</summary>
        public static void Shatter(Vector3 pos, Color color, int shards = 24)
        {
            var fx = Instance; if (fx == null) return;
            var p = Front(pos);
            for (int i = 0; i < shards; i++)
            {
                var d = Dir() * R(0.2f, 1f);
                var dir = new Vector3(d.x, Mathf.Abs(d.y) * 0.8f + 0.4f, 0f).normalized;
                _ep = new ParticleSystem.EmitParams
                {
                    position = p + new Vector3(R(-0.35f, 0.35f), R(-0.4f, 0.4f), 0f),
                    velocity = dir * R(3f, 9f),
                    startColor = Color.Lerp(color, Color.white, R(0f, 1f) * 0.5f) * 1.6f,
                    startSize = R(0.18f, 0.5f),
                    startLifetime = R(0.7f, 1.3f),
                    rotation3D = new Vector3(R(0f, 360f), R(0f, 360f), R(0f, 360f)),
                };
                fx._shards.Emit(_ep, 1);
            }
            Sparks(pos, color * 1.5f, 18, 7f, 0.1f, 0.7f);
            Flash(pos, Color.Lerp(color, Color.white, 0.5f), 3.2f, 0.22f);
            Ring(pos, color, 2.4f, 0.45f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; }
    }
}
