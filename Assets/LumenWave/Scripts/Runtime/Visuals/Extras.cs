using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Themed procedural scenery: clockwork gears that tick on the beat, glowing crystal clusters.</summary>
    public static class Extras
    {
        // ------------------------------------------------------------------ gears
        public static void Gears(Transform root, float x0, float x1, float groundY, Mood mood, System.Random rng)
        {
            var parent = new GameObject("Gears").transform;
            parent.SetParent(root, false);
            var bronze = new Color(0.16f, 0.11f, 0.05f);
            var near = Scenery.PropMaterial(mood, bronze, 0.1f, 1.0f, groundY - 2f, 0.3f, 0.5f);
            var mid = Scenery.PropMaterial(mood, bronze, 0.42f, 0.5f, groundY, 0.4f, 0.4f);
            var far = Scenery.PropMaterial(mood, bronze, 0.68f, 0.3f, groundY + 4f, 0.5f, 0.3f);
            var meshes = new Dictionary<int, Mesh>();
            float x = x0 - 10f;
            while (x < x1 + 20f)
            {
                int band = rng.Next(3);
                float z = band == 0 ? 7f + (float)rng.NextDouble() * 3f : band == 1 ? 15f + (float)rng.NextDouble() * 8f : 30f + (float)rng.NextDouble() * 12f;
                float radius = band == 0 ? 1.6f + (float)rng.NextDouble() * 2f : band == 1 ? 3f + (float)rng.NextDouble() * 3f : 5f + (float)rng.NextDouble() * 5f;
                int teeth = Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 8, 40);
                if (!meshes.TryGetValue(teeth, out var mesh)) meshes[teeth] = mesh = GearMesh(teeth, 0.26f);
                float y = groundY + (band == 0 ? 5f + (float)rng.NextDouble() * 4f : 2f + (float)rng.NextDouble() * 9f);
                var g = new GameObject("Gear");
                g.transform.SetParent(parent, false);
                g.transform.localPosition = new Vector3(x, y, z);
                g.transform.localScale = new Vector3(radius, radius, radius * 0.6f);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = band == 0 ? near : band == 1 ? mid : far;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var spin = g.AddComponent<GearSpin>();
                spin.step = 360f / teeth * (rng.Next(2) == 0 ? 1f : -1f);
                // A meshing partner next to some gears (turning the other way).
                if (rng.Next(2) == 0)
                {
                    float r2 = radius * (0.45f + (float)rng.NextDouble() * 0.3f);
                    int t2 = Mathf.Clamp(Mathf.RoundToInt(r2 * 4f), 8, 40);
                    if (!meshes.TryGetValue(t2, out var m2)) meshes[t2] = m2 = GearMesh(t2, 0.26f);
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var g2 = new GameObject("Gear");
                    g2.transform.SetParent(parent, false);
                    g2.transform.localPosition = new Vector3(x + Mathf.Cos(ang) * (radius + r2) * 0.93f, y + Mathf.Sin(ang) * (radius + r2) * 0.93f, z + 0.1f);
                    g2.transform.localScale = new Vector3(r2, r2, r2 * 0.6f);
                    g2.AddComponent<MeshFilter>().sharedMesh = m2;
                    var rr = g2.AddComponent<MeshRenderer>();
                    rr.sharedMaterial = r.sharedMaterial;
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rr.receiveShadows = false;
                    g2.AddComponent<GearSpin>().step = -360f / t2 * Mathf.Sign(spin.step);
                }
                x += 6f + (float)rng.NextDouble() * 9f;
            }
        }

        /// <summary>Unit-radius gear (teeth on the rim, hub, spokes cut-outs approximated by a ring + spokes).</summary>
        public static Mesh GearMesh(int teeth, float depth)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            float rIn = 0.82f, rOut = 1f, rHub = 0.22f, rRing = 0.66f;
            int seg = teeth * 4;
            System.Func<int, float> rimR = i => { int k = i % 4; return k == 1 || k == 2 ? rOut : rIn; };
            float zf = -depth * 0.5f, zb = depth * 0.5f;
            // Front & back faces of the toothed ring (between rRing and rim).
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? zf : zb;
                int b = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float off = (i % 4 == 1 || i % 4 == 2) ? 0.35f / teeth : 0f;   // narrower tooth tips
                    float ar = a + ((i % 4 == 1) ? off : (i % 4 == 2) ? -off : 0f);
                    v.Add(new Vector3(Mathf.Cos(ar) * rimR(i), Mathf.Sin(ar) * rimR(i), z));
                    v.Add(new Vector3(Mathf.Cos(a) * rRing, Mathf.Sin(a) * rRing, z));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a0 = b + i * 2;
                    if (side == 0) { t.Add(a0); t.Add(a0 + 1); t.Add(a0 + 2); t.Add(a0 + 1); t.Add(a0 + 3); t.Add(a0 + 2); }
                    else { t.Add(a0); t.Add(a0 + 2); t.Add(a0 + 1); t.Add(a0 + 1); t.Add(a0 + 2); t.Add(a0 + 3); }
                }
            }
            // Rim side wall.
            {
                int b = v.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    float off = (i % 4 == 1 || i % 4 == 2) ? 0.35f / teeth : 0f;
                    float ar = a + ((i % 4 == 1) ? off : (i % 4 == 2) ? -off : 0f);
                    var p = new Vector3(Mathf.Cos(ar) * rimR(i), Mathf.Sin(ar) * rimR(i), 0f);
                    v.Add(new Vector3(p.x, p.y, zf)); v.Add(new Vector3(p.x, p.y, zb));
                }
                for (int i = 0; i < seg; i++) { int a0 = b + i * 2; t.Add(a0); t.Add(a0 + 2); t.Add(a0 + 1); t.Add(a0 + 1); t.Add(a0 + 2); t.Add(a0 + 3); }
            }
            // Hub + spokes (front face only, slightly proud).
            int spokes = teeth > 16 ? 6 : 4;
            for (int s = 0; s < spokes; s++)
            {
                float a = s / (float)spokes * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var n = new Vector3(-dir.y, dir.x, 0f) * 0.07f;
                int b = v.Count;
                float z = zf - 0.02f;
                v.Add(dir * rHub * 0.8f - n + Vector3.forward * z); v.Add(dir * rHub * 0.8f + n + Vector3.forward * z);
                v.Add(dir * (rRing + 0.02f) - n * 0.8f + Vector3.forward * z); v.Add(dir * (rRing + 0.02f) + n * 0.8f + Vector3.forward * z);
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b + 1); t.Add(b + 2); t.Add(b + 3);
            }
            {
                int b = v.Count;
                v.Add(new Vector3(0, 0, zf - 0.03f));
                for (int i = 0; i <= 16; i++) { float a = i / 16f * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * rHub, Mathf.Sin(a) * rHub, zf - 0.03f)); }
                for (int i = 0; i < 16; i++) { t.Add(b); t.Add(b + 2 + i); t.Add(b + 1 + i); }
            }
            var m = new Mesh { name = "Gear" + teeth };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ crystals
        public static void Crystals(Transform root, float x0, float x1, float groundY, Mood mood, Theme theme, System.Random rng)
        {
            var parent = new GameObject("CrystalClusters").transform;
            parent.SetParent(root, false);
            var fog = Scenery.FogColor(mood);
            Material Mat(float atmo, float glow)
            {
                var m = MaterialLibrary.Create("Crystal");
                m.SetColor("_Color", theme.crystalColor * glow);
                m.SetColor("_Base", new Color(0.02f, 0.025f, 0.04f));
                m.SetFloat("_Iridescence", theme.iridescence);
                m.SetFloat("_Atmo", atmo);
                m.SetColor("_FogColor", fog);
                return m;
            }
            var near = Mat(0.3f, 0.3f); var mid = Mat(0.55f, 0.32f);
            var shard = CrystalMesh();
            float x = x0 - 8f + (float)rng.NextDouble() * 6f;
            while (x < x1 + 12f)
            {
                bool isNear = rng.Next(2) == 0;
                float z = isNear ? 6.5f + (float)rng.NextDouble() * 2.5f : 14f + (float)rng.NextDouble() * 8f;
                float scale = isNear ? 0.5f + (float)rng.NextDouble() * 0.6f : 1.2f + (float)rng.NextDouble() * 1.8f;
                var cl = new GameObject("Cluster").transform;
                cl.SetParent(parent, false);
                cl.localPosition = new Vector3(x, groundY - 1.2f + (float)rng.NextDouble() * 1.5f, z);
                int n = 3 + rng.Next(5);
                for (int i = 0; i < n; i++)
                {
                    var c = new GameObject("Shard");
                    c.transform.SetParent(cl, false);
                    c.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * scale * 1.2f, 0f, ((float)rng.NextDouble() - 0.5f) * scale);
                    c.transform.localRotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 50f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 60f);
                    float h = scale * (1.2f + (float)rng.NextDouble() * 2.6f) * (i == 0 ? 1.5f : 1f);
                    float w = scale * (0.25f + (float)rng.NextDouble() * 0.25f);
                    c.transform.localScale = new Vector3(w, h, w);
                    c.AddComponent<MeshFilter>().sharedMesh = shard;
                    var r = c.AddComponent<MeshRenderer>();
                    r.sharedMaterial = isNear ? near : mid;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                // Soft glow behind the cluster.
                var g = LightVisuals.Glow(cl, new Vector3(0f, scale * 1.6f, 0.5f), scale * 6f, theme.crystalColor * 0.1f, "ClusterGlow");
                x += isNear ? 9f + (float)rng.NextDouble() * 14f : 6f + (float)rng.NextDouble() * 10f;
            }
        }

        /// <summary>Hexagonal prism with a pointed tip, base at y = 0, height 1, radius 1, flat-shaded.</summary>
        public static Mesh CrystalMesh()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            const int k = 6;
            float body = 0.72f;
            for (int i = 0; i < k; i++)
            {
                float a0 = i / (float)k * Mathf.PI * 2f, a1 = (i + 1) / (float)k * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var q0 = p0 * 0.92f + Vector3.up * body; var q1 = p1 * 0.92f + Vector3.up * body;
                var tip = Vector3.up;
                int b = v.Count;
                v.Add(p0); v.Add(p1); v.Add(q1); v.Add(q0);
                c.Add(new Color(0, 0, 0)); c.Add(new Color(0, 0, 0)); c.Add(new Color(body, 0, 0)); c.Add(new Color(body, 0, 0));
                t.Add(b); t.Add(b + 3); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 1);
                b = v.Count;
                v.Add(q0); v.Add(q1); v.Add(tip);
                c.Add(new Color(body, 0, 0)); c.Add(new Color(body, 0, 0)); c.Add(new Color(1, 0, 0));
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
            }
            var m = new Mesh { name = "CrystalShard" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>Gear that ticks one tooth forward on every beat (eased), like clockwork synced to the music.</summary>
    public class GearSpin : MonoBehaviour
    {
        public float step = 15f;
        float _angle, _target;

        void OnEnable() { BeatClock.OnBeat += OnBeat; _angle = _target = transform.localEulerAngles.z; }
        void OnDisable() { BeatClock.OnBeat -= OnBeat; }
        void OnBeat(int beat) { _target += step; }

        void Update()
        {
            if (BeatClock.Instance == null) _target += step * Time.deltaTime * 0.4f;   // no clock (menus/tests): drift slowly
            _angle = Mathf.Lerp(_angle, _target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(0f, 0f, _angle);
        }
    }
}
