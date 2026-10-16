using UnityEngine;

namespace LumenWave
{
    /// <summary>Shared helpers for enemies: finding Umbra, procedural visual parts, glow halos, sparks.</summary>
    public static class EnemyUtil
    {
        /// <summary>Tests can force the target.</summary>
        public static GameObject PlayerOverride;
        static PlayerHealth _cached;

        public static GameObject FindPlayer()
        {
            if (PlayerOverride != null) return PlayerOverride;
            var rt = LevelRuntime.Current;
            if (rt != null && rt.Player != null) return rt.Player;
            if (_cached == null) _cached = Object.FindAnyObjectByType<PlayerHealth>();
            return _cached != null ? _cached.gameObject : null;
        }

        /// <summary>Centre of the player's body (pivot is at the feet).</summary>
        public static Vector3 Center(GameObject player)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) return player.transform.TransformPoint(cc.center);
            var col = player.GetComponent<Collider>();
            if (col != null && col.enabled) return col.bounds.center;
            return player.transform.position + Vector3.up * 0.9f;
        }

        /// <summary>True if nothing solid is between a and b.</summary>
        public static bool LineOfSight(Vector3 a, Vector3 b)
        {
            return !Physics.Linecast(a, b, Layers.Mask(Layers.Ground, Layers.Crystal), QueryTriggerInteraction.Ignore);
        }

        /// <summary>Knocks the player back (if it exposes a body) and damages it.</summary>
        public static void Hit(GameObject player, float damage, Vector3 from, float knockback, LightColor color = LightColor.White)
        {
            var hp = player.GetComponent<PlayerHealth>();
            if (hp != null && hp.IsDead) return;   // don't fling the body around while the respawn is pending
            if (hp != null) hp.Damage(damage, color);
            var body = player.GetComponent<IPlayerBody>();
            if (body != null && knockback > 0f)
            {
                var d = (Center(player) - from);
                d.z = 0f;
                float sx = Mathf.Sign(d.x == 0f ? 1f : d.x);
                body.Velocity = new Vector2(sx * knockback, knockback * 0.6f);
            }
        }

        /// <summary>A primitive used only as a visual (collider removed).</summary>
        public static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.layer = Layers.Enemy;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Texture2D _soft;
        /// <summary>Soft radial falloff texture (procedural) for halos and particles.</summary>
        public static Texture2D SoftDot
        {
            get
            {
                if (_soft != null) return _soft;
                const int n = 64;
                _soft = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                _soft.SetPixels(px);
                _soft.Apply();
                return _soft;
            }
        }

        /// <summary>Additive material instance with the soft dot texture.</summary>
        public static Material GlowMaterial(Color color)
        {
            var m = MaterialLibrary.Create("ParticleAdditive");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftDot);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", SoftDot);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        public static void SetGlow(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>Camera-facing (−Z) soft glow quad.</summary>
        public static Renderer Halo(Transform parent, Vector3 localPos, float size, Color color)
        {
            var go = Part(PrimitiveType.Quad, parent, localPos, Vector3.one * size, GlowMaterial(color), "Halo");
            return go.GetComponent<Renderer>();
        }

        /// <summary>Emissive material instance (per-object so it can flicker).</summary>
        public static Material Emissive(Color baseColor, Color emission)
        {
            var m = MaterialLibrary.Create("LitEmissive");
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission); }
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        /// <summary>Burst particle system (sparks / dust) using the soft dot.</summary>
        public static ParticleSystem Sparks(Transform parent, Color color, float speed = 4f, float size = 0.12f, float gravity = 1f)
        {
            var go = new GameObject("Sparks");
            go.layer = Layers.Enemy;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = GlowMaterial(Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Small point light for local glow (purely cosmetic).</summary>
        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("Glow Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Moves a flying body by delta, stopping on ground/crystal per axis (slides along walls).</summary>
        public static Vector3 FlyMove(Vector3 pos, Vector3 delta, float radius)
        {
            int mask = Layers.Mask(Layers.Ground, Layers.Crystal, Layers.Bridge);
            var nx = pos + new Vector3(delta.x, 0f, 0f);
            if (!Physics.CheckSphere(nx, radius, mask, QueryTriggerInteraction.Ignore)) pos = nx;
            var ny = pos + new Vector3(0f, delta.y, 0f);
            if (!Physics.CheckSphere(ny, radius, mask, QueryTriggerInteraction.Ignore)) pos = ny;
            pos.z = 0f;
            return pos;
        }

        /// <summary>Keeps a point outside the resonance shield. Returns true if it was pushed.</summary>
        public static bool RepelFromShield(ref Vector3 pos, ref Vector3 vel, float radius)
        {
            if (!ShieldState.Active) return false;
            var d = pos - ShieldState.Center;
            d.z = 0f;
            float min = ShieldState.Radius + radius;
            if (d.sqrMagnitude >= min * min) return false;
            var n = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.up;
            pos = ShieldState.Center + n * (min + 0.02f);
            pos.z = 0f;
            float vn = Vector3.Dot(vel, n);
            if (vn < 0f) vel -= n * vn * 1.8f;
            vel += n * 2f;
            return true;
        }

        // ---------------------------------------------------------------------------------------------
        // Silhouette look (Limbo / Ori): black bodies with a thin tinted Schlick rim (LumenWave/Umbra) and
        // procedural meshes (lathe for robes/hoods/bells, flat double-sided polygons for wings).
        // ---------------------------------------------------------------------------------------------
        static readonly System.Collections.Generic.Dictionary<Color, Material> _silhouettes = new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>Shared black silhouette material with a rim tinted <paramref name="rim"/> (HDR).</summary>
        public static Material Silhouette(Color rim)
        {
            if (_silhouettes.TryGetValue(rim, out var m) && m != null) return m;
            m = MaterialLibrary.Create("Umbra");
            m.name = "EnemySilhouette";
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.006f, 0.006f, 0.009f));
            if (m.HasProperty("_RimColor")) m.SetColor("_RimColor", rim);
            if (m.HasProperty("_RimPower")) m.SetFloat("_RimPower", 3.2f);
            if (m.HasProperty("_RimF0")) m.SetFloat("_RimF0", 0f);
            if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0.55f);
            if (m.HasProperty("_KeyAmbient")) m.SetFloat("_KeyAmbient", 0.25f);
            if (m.HasProperty("_Ambient")) m.SetColor("_Ambient", new Color(0.008f, 0.009f, 0.014f));
            if (m.HasProperty("_FogAmount")) m.SetFloat("_FogAmount", 0.45f);
            _silhouettes[rim] = m;
            return m;
        }

        /// <summary>A mesh used only as a visual.</summary>
        public static GameObject MeshPart(Mesh mesh, Transform parent, Vector3 localPos, Quaternion localRot, Vector3 scale, Material mat, string name)
        {
            var go = new GameObject(name);
            go.layer = Layers.Enemy;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static readonly System.Collections.Generic.Dictionary<string, Mesh> _meshCache = new System.Collections.Generic.Dictionary<string, Mesh>();

        /// <summary>Cached procedural mesh (built once per key; enemies share their meshes).</summary>
        public static Mesh CachedMesh(string key, System.Func<Mesh> build)
        {
            if (_meshCache.TryGetValue(key, out var m) && m != null) return m;
            m = build();
            _meshCache[key] = m;
            return m;
        }

        /// <summary>
        /// Surface of revolution around Y. <paramref name="profile"/> = (radius, y) from top to bottom.
        /// <paramref name="ragged"/> adds a deterministic wavy/tattered offset that grows toward the bottom
        /// (cloth hems), <paramref name="seed"/> varies it.
        /// </summary>
        public static Mesh Lathe(Vector2[] profile, int segments, float ragged = 0f, int seed = 0, string name = "Lathe")
        {
            int rings = profile.Length;
            var v = new Vector3[rings * (segments + 1)];
            var uv = new Vector2[v.Length];
            for (int r = 0; r < rings; r++)
            {
                float k = rings > 1 ? r / (float)(rings - 1) : 0f;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    float rad = profile[r].x;
                    float y = profile[r].y;
                    if (ragged > 0f)
                    {
                        float w = Mathf.Sin(a * 5f + seed) * 0.5f + Mathf.Sin(a * 11f + seed * 1.7f) * 0.3f + Mathf.Sin(a * 3f + seed * 0.3f) * 0.2f;
                        rad *= 1f + ragged * 0.35f * w * k;
                        y += ragged * 0.12f * w * k * k;
                    }
                    v[r * (segments + 1) + i] = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                    uv[r * (segments + 1) + i] = new Vector2(i / (float)segments, k);
                }
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int r = 0; r < rings - 1; r++)
                for (int i = 0; i < segments; i++)
                {
                    int a0 = r * (segments + 1) + i, a1 = a0 + 1, b0 = a0 + segments + 1, b1 = b0 + 1;
                    tris.Add(a0); tris.Add(a1); tris.Add(b0);
                    tris.Add(a1); tris.Add(b1); tris.Add(b0);
                }
            var m = new Mesh { name = name, vertices = v, uv = uv };
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat double-sided polygon (fan from the first point) in the XY plane, UVs over its bounds.</summary>
        public static Mesh Polygon(Vector2[] pts, string name = "Polygon")
        {
            int n = pts.Length;
            var v = new Vector3[n * 2];
            var nrm = new Vector3[n * 2];
            var uv = new Vector2[n * 2];
            Vector2 min = pts[0], max = pts[0];
            foreach (var p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            var size = Vector2.Max(max - min, new Vector2(1e-4f, 1e-4f));
            for (int i = 0; i < n; i++)
            {
                v[i] = v[i + n] = pts[i];
                nrm[i] = Vector3.back; nrm[i + n] = Vector3.forward;
                uv[i] = uv[i + n] = new Vector2((pts[i].x - min.x) / size.x, (pts[i].y - min.y) / size.y);
            }
            // Front faces (normal −Z, toward the camera) need clockwise winding seen from −Z.
            bool ccw = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).z > 0f;
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 1; i < n - 1; i++)
            {
                if (ccw) { tris.Add(0); tris.Add(i + 1); tris.Add(i); tris.Add(n); tris.Add(n + i); tris.Add(n + i + 1); }
                else { tris.Add(0); tris.Add(i); tris.Add(i + 1); tris.Add(n); tris.Add(n + i + 1); tris.Add(n + i); }
            }
            var m = new Mesh { name = name, vertices = v, normals = nrm, uv = uv };
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Continuous world-space trail of soft motes (wisps, embers).</summary>
        public static ParticleSystem MoteTrail(Transform parent, Color color, float rateOverDistance, float rateOverTime, float size, float life)
        {
            var go = new GameObject("Motes");
            go.layer = Layers.Enemy;
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var em = ps.emission;
            em.rateOverTime = rateOverTime;
            em.rateOverDistance = rateOverDistance;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.12f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = GlowMaterial(Color.white);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { PlayerOverride = null; _cached = null; _silhouettes.Clear(); _meshCache.Clear(); }
    }
}
