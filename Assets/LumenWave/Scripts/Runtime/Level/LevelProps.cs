using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Queries about Umbra used by level props (works whatever physics the player uses).</summary>
    public static class PlayerQuery
    {
        public static GameObject Player => LevelRuntime.Current != null ? LevelRuntime.Current.Player : null;

        public static bool Alive
        {
            get
            {
                var p = Player;
                if (p == null) return false;
                var h = p.GetComponent<PlayerHealth>();
                return h == null || !h.IsDead;
            }
        }

        /// <summary>Player bounds on the X/Y plane (collider bounds if present, else a 0.7×1.8 box above the feet).</summary>
        public static bool TryGetBounds(out Bounds b)
        {
            b = default;
            var p = Player;
            if (p == null) return false;
            var col = p.GetComponent<Collider>();
            if (col != null && col.enabled && col.bounds.size.sqrMagnitude > 0.01f) { b = col.bounds; return true; }
            b = new Bounds(p.transform.position + Vector3.up * 0.9f, new Vector3(0.7f, 1.8f, 1f));
            return true;
        }

        /// <summary>True if the living player overlaps the given XY rectangle.</summary>
        public static bool Overlaps(Vector2 center, Vector2 size)
        {
            if (!Alive || !TryGetBounds(out var b)) return false;
            return Mathf.Abs(b.center.x - center.x) <= (b.extents.x + size.x * 0.5f) &&
                   Mathf.Abs(b.center.y - center.y) <= (b.extents.y + size.y * 0.5f);
        }
    }

    /// <summary>Small procedural visual helpers for level props.</summary>
    public static class PropVisuals
    {
        public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("Glow");
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

        public static Material Emissive(Color c, float emission) => MaterialLibrary.Tinted("LitEmissive", c, c * emission);
        public static Material Shadow => MaterialLibrary.Tinted("Lit", new Color(0.02f, 0.02f, 0.03f));

        /// <summary>A row of triangular prism spikes along X, base at y=0, width w, height h, depth d.</summary>
        public static Mesh Spikes(float width, float height, float depth, int count)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            float step = width / count;
            for (int i = 0; i < count; i++)
            {
                float x0 = -width * 0.5f + i * step, x1 = x0 + step, xm = (x0 + x1) * 0.5f;
                float hh = height * (0.75f + 0.25f * Mathf.PerlinNoise(i * 1.7f, 0.3f));
                // Pyramid: 4 base corners + apex.
                int b = v.Count;
                v.Add(new Vector3(x0, 0, -depth * 0.5f)); v.Add(new Vector3(x1, 0, -depth * 0.5f));
                v.Add(new Vector3(x1, 0, depth * 0.5f)); v.Add(new Vector3(x0, 0, depth * 0.5f));
                v.Add(new Vector3(xm, hh, 0));
                t.AddRange(new[] { b, b + 4, b + 1, b + 1, b + 4, b + 2, b + 2, b + 4, b + 3, b + 3, b + 4, b });
            }
            return Finish(v, t, "Spikes");
        }

        /// <summary>Octahedron (crystal / orb gem) of the given radius and height ratio.</summary>
        public static Mesh Octahedron(float radius, float heightRatio = 1.6f)
        {
            var v = new List<Vector3>
            {
                new Vector3(radius, 0, 0), new Vector3(0, 0, radius), new Vector3(-radius, 0, 0), new Vector3(0, 0, -radius),
                new Vector3(0, radius * heightRatio, 0), new Vector3(0, -radius * heightRatio, 0),
            };
            var t = new List<int> { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0, 1, 5, 0, 2, 5, 1, 3, 5, 2, 0, 5, 3 };
            // Reverse winding so normals face outward with Unity's clockwise front faces.
            for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            return Finish(v, t, "Octahedron");
        }

        static Mesh Finish(List<Vector3> v, List<int> t, string name)
        {
            // Flat shading: unshare vertices.
            var fv = new List<Vector3>();
            var ft = new List<int>();
            for (int i = 0; i < t.Count; i++) { fv.Add(v[t[i]]); ft.Add(i); }
            var m = new Mesh { name = name };
            m.SetVertices(fv);
            m.SetTriangles(ft, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    // ---------------------------------------------------------------- Exit

    /// <summary>Glowing doorway. Touch it to complete the level (locked until the finale if the level has one).</summary>
    public class ExitPortal : MonoBehaviour
    {
        public Vector2 size = new Vector2(1.6f, 2.6f);
        Light _light;
        Transform _veil;
        float _t;

        public void Init(Light l, Transform veil) { _light = l; _veil = veil; }

        void Update()
        {
            _t += Time.deltaTime;
            bool locked = FinaleTrigger.Present && !FinaleTrigger.Started;
            if (_light != null) _light.intensity = locked ? 0.3f : 3.2f + Mathf.Sin(_t * 2.6f) * 0.8f;
            if (_veil != null) _veil.localScale = new Vector3(1.2f, 2.2f + Mathf.Sin(_t * 1.7f) * 0.05f, locked ? 0.01f : 0.05f);
            if (locked || LevelRuntime.Current == null || LevelRuntime.Current.Completed) return;
            if (PlayerQuery.Overlaps((Vector2)transform.position + Vector2.up * size.y * 0.5f, size))
                LevelRuntime.Current.Complete();
        }
    }

    // ---------------------------------------------------------------- Checkpoint

    /// <summary>Shadow lantern: lights up when Umbra passes and becomes the respawn point.</summary>
    public class CheckpointLantern : MonoBehaviour
    {
        public int index;
        public bool lit;
        Light _light;
        Renderer _core;
        float _flash;

        public void Init(Light l, Renderer core) { _light = l; _core = core; }

        void Update()
        {
            if (!lit && PlayerQuery.Overlaps((Vector2)transform.position + Vector2.up * 1.2f, new Vector2(1.6f, 3f)))
            {
                lit = true;
                _flash = 1f;
                if (LevelRuntime.Current != null) LevelRuntime.Current.SetCheckpoint(transform.position + Vector3.up * 0.05f, index);
                if (_core != null) _core.sharedMaterial = PropVisuals.Emissive(new Color(0.55f, 0.9f, 1f), 6f);
            }
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime);
            if (_light != null)
            {
                float target = lit ? 1.6f + 0.2f * Mathf.Sin(Time.time * 3f) : 0.15f;
                _light.intensity = target + _flash * 6f;
                _light.color = lit ? new Color(0.55f, 0.9f, 1f) : new Color(0.4f, 0.45f, 0.6f);
            }
        }
    }

    // ---------------------------------------------------------------- Thorns

    /// <summary>Shadow thorns: instant death on touch.</summary>
    public class Thorns : MonoBehaviour
    {
        public Vector2 size = new Vector2(0.9f, 0.55f);

        void Update()
        {
            if (PlayerQuery.Overlaps((Vector2)transform.position + Vector2.up * size.y * 0.5f, size))
                PlayerQuery.Player.GetComponent<PlayerHealth>()?.Kill();
        }
    }

    // ---------------------------------------------------------------- Crystal

    /// <summary>Light crystal: solid, blocks beams, shattered by a Pulse wavefront. Reforms on respawn.</summary>
    public class LightCrystal : MonoBehaviour, IPulseReceiver, IResettable
    {
        public bool Shattered { get; private set; }
        Collider _col;
        GameObject _visual;
        Light _light;
        ParticleSystem _shards;

        public void Init(Collider col, GameObject visual, Light light, ParticleSystem shards)
        {
            _col = col; _visual = visual; _light = light; _shards = shards;
        }

        public void OnPulse(Vector3 origin, float strength) => Shatter();

        public void Shatter()
        {
            if (Shattered) return;
            Shattered = true;
            if (_col != null) _col.enabled = false;
            if (_visual != null) _visual.SetActive(false);
            if (_light != null) _light.enabled = false;
            if (_shards != null) _shards.Play();
            GameEvents.RaiseCrystalShattered(transform.position);
        }

        public void ResetState()
        {
            Shattered = false;
            if (_col != null) _col.enabled = true;
            if (_visual != null) _visual.SetActive(true);
            if (_light != null) _light.enabled = true;
        }

        void Update()
        {
            if (!Shattered && _light != null) _light.intensity = 0.9f + 0.3f * Mathf.Sin(Time.time * 2f + transform.position.x);
        }
    }

    // ---------------------------------------------------------------- Hidden platform

    /// <summary>A platform of frozen sound that only exists for a few seconds after a Pulse touches it.</summary>
    public class HiddenPlatform : MonoBehaviour, IPulseReceiver, IResettable
    {
        public float revealTime = 6f;
        public float RevealedFor => Mathf.Max(0f, _until - Time.time);
        public bool Revealed => Time.time < _until;
        Collider _solid;
        Renderer _renderer;
        Material _mat;
        float _until = -1f;

        public void Init(Collider solid, Renderer r)
        {
            _solid = solid; _renderer = r;
            _mat = new Material(r.sharedMaterial);
            r.sharedMaterial = _mat;
        }

        public void OnPulse(Vector3 origin, float strength) => Reveal();

        public void Reveal() { _until = Time.time + revealTime; }

        public void ResetState() { _until = -1f; }

        void Update()
        {
            float left = _until - Time.time;
            bool on = left > 0f;
            if (_solid != null && _solid.enabled != on) _solid.enabled = on;
            // Ghost outline when hidden, solid glow when revealed, flicker in the last second.
            float a = on ? (left < 1f ? (Mathf.Sin(Time.time * 30f) > 0 ? 1f : 0.3f) : 1f) : 0.06f + 0.04f * Mathf.Sin(Time.time * 2f);
            var c = new Color(0.45f, 0.8f, 1f);
            if (_mat != null)
            {
                if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, a * 0.8f));
                if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", new Color(c.r, c.g, c.b, a * 0.8f));
            }
        }
    }

    // ---------------------------------------------------------------- Tutorial

    /// <summary>Shows a tutorial message once when Umbra enters its zone.</summary>
    public class TutorialZone : MonoBehaviour
    {
        public string text;
        public float radius = 3f;
        bool _shown;

        void Update()
        {
            if (_shown || !PlayerQuery.Alive || !PlayerQuery.TryGetBounds(out var b)) return;
            if (Mathf.Abs(b.center.x - transform.position.x) <= radius && Mathf.Abs(b.center.y - transform.position.y - 1f) <= radius + 1.5f)
            {
                _shown = true;
                GameEvents.RaiseMessage(text);
            }
        }
    }

    // ---------------------------------------------------------------- Secrets

    /// <summary>Tally of secret light shards.</summary>
    public static class Secrets
    {
        public static int TotalCollected;
        public static int CollectedInLevel;
        public static int InLevel;
        public static event System.Action<int, int> Changed;  // collected, total in level

        public static void Collect()
        {
            TotalCollected++;
            CollectedInLevel++;
            Changed?.Invoke(CollectedInLevel, InLevel);
        }

        public static void ResetLevel() { CollectedInLevel = 0; InLevel = 0; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { TotalCollected = 0; CollectedInLevel = 0; InLevel = 0; Changed = null; }
    }

    /// <summary>A floating light shard. Optional; collected ones stay collected.</summary>
    public class SecretOrb : MonoBehaviour
    {
        public bool collected;
        Transform _gem;
        Vector3 _base;

        public void Init(Transform gem) { _gem = gem; _base = transform.position; }

        void Update()
        {
            if (collected) return;
            transform.position = _base + Vector3.up * Mathf.Sin(Time.time * 2f + _base.x) * 0.15f;
            if (_gem != null) _gem.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
            if (PlayerQuery.Overlaps(transform.position, new Vector2(0.9f, 0.9f)))
            {
                collected = true;
                Secrets.Collect();
                GameEvents.RaiseMessage("Secret light shard " + Secrets.CollectedInLevel + "/" + Secrets.InLevel);
                gameObject.SetActive(false);
            }
        }
    }

    // ---------------------------------------------------------------- Finale

    /// <summary>The ending: Umbra sings, every light turns harmonious, then the level completes.</summary>
    public class FinaleTrigger : MonoBehaviour
    {
        public static bool Present;
        public static bool Started;
        public float duration = 7f;
        public Vector2 size = new Vector2(2f, 6f);
        float _t;

        void Awake() { Present = true; Started = false; }
        void OnDestroy() { Present = false; Started = false; }

        void Update()
        {
            if (!Started)
            {
                if (PlayerQuery.Overlaps((Vector2)transform.position + Vector2.up * size.y * 0.5f, size))
                {
                    Started = true;
                    Debug.Log("[LumenWave] Finale started");
                    GameEvents.RaiseFinale();
                }
                return;
            }
            _t += Time.deltaTime;
            if (_t >= duration && LevelRuntime.Current != null && !LevelRuntime.Current.Completed)
                LevelRuntime.Current.Complete();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Present = false; Started = false; }
    }

    // ---------------------------------------------------------------- Bot marker

    /// <summary>Invisible bot hint: when the bot's player crosses x (near y), perform an action.</summary>
    public class BotMarker : MonoBehaviour
    {
        public string action = "jump";
        public float duration;
        public float delay;
        public bool anyHeight;
        public int order;
    }
}
