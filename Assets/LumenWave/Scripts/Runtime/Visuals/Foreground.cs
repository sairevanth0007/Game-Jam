using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Dark, soft-edged foreground silhouettes between the camera and the playfield (z âˆ’3 â€¦ âˆ’8): grass and
    /// roots rising from the bottom edge of the screen, vines / chains / branches hanging from the top edge,
    /// ruin chunks. They keep true horizontal parallax (world x), but follow the camera vertically so they always
    /// stay in the bottom / top bands of the screen and never sit on the player's path.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class Foreground : MonoBehaviour
    {
        /// <summary>Fraction of the screen height the bottom / top bands may cover.</summary>
        public const float BottomBand = 0.2f, TopBand = 0.17f;

        Camera _cam;

        public static Foreground Build(Transform parent, float x0, float x1, Mood mood, Theme theme, int seed, Camera cam, float camDist = CameraRig.DefaultDistance)
        {
            var go = new GameObject("Foreground");
            go.transform.SetParent(parent, false);
            var fg = go.AddComponent<Foreground>();
            fg._cam = cam;
            if (cam == null) return fg;
            var rng = new System.Random(seed);
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var sb = new SoftBuilder();
            float x = x0 - 6f + (float)rng.NextDouble() * 6f;
            while (x < x1 + 10f)
            {
                float z = -3.5f - (float)rng.NextDouble() * 4.5f;
                float D = camDist + z;                         // distance from camera at this depth
                float h = D * tanHalf;                         // half visible height at this depth
                float yCenter = -D * Mathf.Tan(CameraRig.Pitch * Mathf.Deg2Rad);
                float bottomEdge = yCenter - h, topEdge = yCenter + h;
                bool top = rng.Next(100) < 55;
                float alpha = 0.92f + (float)rng.NextDouble() * 0.08f;
                if (!top)
                {
                    float maxH = 2f * h * BottomBand * (rng.Next(3) == 0 ? 1.25f : 1f);
                    int kind = rng.Next(3);
                    if (theme.foregroundRuins && kind == 0) Ruin(sb, x, bottomEdge - 0.6f, maxH + 0.6f, z, rng, alpha);
                    else if (theme.foregroundGrass && kind != 2) Grass(sb, x, bottomEdge - 0.4f, maxH + 0.4f, z, rng, alpha);
                    else Roots(sb, x, bottomEdge - 0.4f, maxH + 0.4f, z, rng, alpha);
                }
                else
                {
                    float maxL = 2f * h * TopBand;
                    int kind = rng.Next(3);
                    if (theme.foregroundChains && kind == 0) Chain(sb, x, topEdge + 0.5f, maxL + 0.5f, z, rng, alpha);
                    else if (theme.foregroundVines && kind != 2) Vine(sb, x, topEdge + 0.5f, maxL + 0.5f, z, rng, alpha);
                    else Branch(sb, x, topEdge + 0.4f, maxL, z, rng, alpha);
                }
                x += 5f + (float)rng.NextDouble() * 9f;
            }
            var mesh = sb.ToMesh("ForegroundSilhouettes");
            var child = new GameObject("Silhouettes");
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = child.AddComponent<MeshRenderer>();
            var mat = MaterialLibrary.Create("SoftSilhouette");
            var c = Color.Lerp(Color.black, mood.fog, 0.25f); c.a = 1f;
            mat.SetColor("_Color", c);
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            fg.LateUpdate();
            return fg;
        }

        void LateUpdate()
        {
            if (_cam == null) return;
            transform.position = new Vector3(0f, _cam.transform.position.y, 0f);
        }

        // ---------------------------------------------------------------- shapes
        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static void Grass(SoftBuilder sb, float x, float y0, float maxH, float z, System.Random rng, float a)
        {
            int blades = 5 + rng.Next(7);
            for (int i = 0; i < blades; i++)
            {
                float bx = x + R(rng, -0.9f, 0.9f);
                float h = maxH * R(rng, 0.45f, 1f);
                float bend = R(rng, -0.5f, 0.5f) * h * 0.5f;
                sb.Strip(t => new Vector3(bx + bend * t * t, y0 + h * t, z), t => Mathf.Lerp(0.16f, 0.015f, t), 8, 0.85f, a);
            }
        }

        static void Roots(SoftBuilder sb, float x, float y0, float maxH, float z, System.Random rng, float a)
        {
            // A gnarled stump with a few curling roots.
            float h = maxH * R(rng, 0.6f, 1f), w = R(rng, 0.5f, 1f);
            sb.Fan(new Vector3(x, y0 + h * 0.35f, z), t => new Vector2(Mathf.Cos(t) * w * (1f + 0.2f * Mathf.Sin(t * 5f)), Mathf.Sin(t) * h * 0.65f), 18, 0.35f, a);
            int roots = 2 + rng.Next(3);
            for (int i = 0; i < roots; i++)
            {
                float dir = rng.Next(2) == 0 ? -1f : 1f;
                float len = R(rng, 0.8f, 2f), up = R(rng, 0.2f, 0.8f) * h;
                float sx = x + R(rng, -0.3f, 0.3f);
                sb.Strip(t => new Vector3(sx + dir * len * t, y0 + up * Mathf.Sin(t * Mathf.PI * 0.8f) + h * 0.4f * (1 - t), z), t => Mathf.Lerp(0.18f, 0.02f, t), 9, 0.7f, a);
            }
        }

        static void Ruin(SoftBuilder sb, float x, float y0, float maxH, float z, System.Random rng, float a)
        {
            // Broken column / block chunk with a jagged top.
            float w = R(rng, 0.7f, 1.6f), h = maxH * R(rng, 0.5f, 1f);
            var pts = new List<Vector2> { new Vector2(-w, 0f), new Vector2(w, 0f) };
            int teeth = 4 + rng.Next(3);
            for (int i = 0; i <= teeth; i++)
            {
                float t = 1f - i / (float)teeth;
                pts.Add(new Vector2(Mathf.Lerp(-w, w, t), h * R(rng, 0.7f, 1f)));
            }
            sb.Polygon(new Vector3(x, y0, z), pts, 0.18f, a);
        }

        static void Vine(SoftBuilder sb, float x, float y0, float maxL, float z, System.Random rng, float a)
        {
            int strands = 1 + rng.Next(3);
            for (int s = 0; s < strands; s++)
            {
                float vx = x + R(rng, -0.8f, 0.8f), len = maxL * R(rng, 0.55f, 1f), ph = R(rng, 0f, 6f), amp = R(rng, 0.1f, 0.3f);
                System.Func<float, Vector3> path = t => new Vector3(vx + Mathf.Sin(t * 5f + ph) * amp * t, y0 - len * t, z);
                sb.Strip(path, t => Mathf.Lerp(0.07f, 0.035f, t), 12, 0.7f, a);
                int leaves = 4 + rng.Next(5);
                for (int l = 0; l < leaves; l++)
                {
                    float t = R(rng, 0.15f, 1f);
                    var p = path(t);
                    float side = rng.Next(2) == 0 ? -1f : 1f;
                    float lw = R(rng, 0.12f, 0.22f), lh = lw * 0.55f;
                    sb.Fan(p + new Vector3(side * lw, -lh * 0.3f, 0f), u => new Vector2(Mathf.Cos(u) * lw, Mathf.Sin(u) * lh), 10, 0.5f, a);
                }
            }
        }

        static void Branch(SoftBuilder sb, float x, float y0, float maxL, float z, System.Random rng, float a)
        {
            float dir = rng.Next(2) == 0 ? -1f : 1f;
            float len = R(rng, 2.5f, 4.5f);
            float drop = maxL * R(rng, 0.5f, 0.9f);
            System.Func<float, Vector3> path = t => new Vector3(x + dir * len * t, y0 - drop * Mathf.Sin(t * 1.4f), z);
            sb.Strip(path, t => Mathf.Lerp(0.22f, 0.03f, t), 12, 0.6f, a);
            int twigs = 3 + rng.Next(4);
            for (int i = 0; i < twigs; i++)
            {
                float t0 = R(rng, 0.2f, 0.85f);
                var p0 = path(t0);
                float tl = R(rng, 0.4f, 1.1f), td = R(rng, 0.3f, 1f);
                sb.Strip(t => p0 + new Vector3(dir * tl * t * 0.6f, -td * t, 0f), t => Mathf.Lerp(0.07f, 0.015f, t), 6, 0.6f, a);
                sb.Fan(p0 + new Vector3(dir * tl * 0.6f, -td, 0f), u => new Vector2(Mathf.Cos(u) * 0.18f, Mathf.Sin(u) * 0.1f), 10, 0.5f, a);
            }
        }

        static void Chain(SoftBuilder sb, float x, float y0, float maxL, float z, System.Random rng, float a)
        {
            float len = maxL * R(rng, 0.7f, 1.05f);
            float link = R(rng, 0.32f, 0.45f);
            int n = Mathf.Max(2, Mathf.FloorToInt(len / (link * 0.78f)));
            for (int i = 0; i < n; i++)
            {
                float cy = y0 - i * link * 0.78f;
                if (i % 2 == 0)
                {
                    // Ring seen face-on: a closed strip around an ellipse.
                    float rx = link * 0.32f, ry = link * 0.5f;
                    sb.Strip(t => new Vector3(x + Mathf.Cos(t * Mathf.PI * 2f) * rx, cy + Mathf.Sin(t * Mathf.PI * 2f) * ry, z), t => 0.05f, 16, 0.75f, a);
                }
                else
                {
                    // Edge-on link: a short bar.
                    sb.Strip(t => new Vector3(x, cy + link * 0.5f - link * t, z), t => 0.05f, 2, 0.75f, a);
                }
            }
            // Something heavy hanging at the end (lantern cage / weight).
            float ey = y0 - n * link * 0.78f - 0.3f;
            sb.Fan(new Vector3(x, ey, z), u => new Vector2(Mathf.Cos(u) * 0.28f, Mathf.Sin(u) * 0.38f), 12, 0.3f, a);
        }
    }

    /// <summary>Builds soft-edged silhouette meshes for LumenWave/SoftSilhouette (uv.x across, uv.y softness).</summary>
    public class SoftBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<Color> _c = new List<Color>();
        readonly List<int> _t = new List<int>();

        /// <summary>Tapered strip along a parametric path (t in 0..1) with half-width w(t).</summary>
        public void Strip(System.Func<float, Vector3> path, System.Func<float, float> w, int segs, float soft, float alpha)
        {
            int b = _v.Count;
            var col = new Color(1, 1, 1, alpha);
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                var p = path(t);
                var p2 = path(Mathf.Min(1f, t + 0.01f)) - path(Mathf.Max(0f, t - 0.01f));
                var dir = new Vector2(p2.x, p2.y);
                if (dir.sqrMagnitude < 1e-8f) dir = Vector2.up;
                dir.Normalize();
                var nrm = new Vector3(-dir.y, dir.x, 0f);
                // Widen by the soft fade so the visible core keeps its width.
                float hw = w(t) * (1f + soft * 0.6f);
                _v.Add(p - nrm * hw); _uv.Add(new Vector2(-1f, soft)); _c.Add(col);
                _v.Add(p + nrm * hw); _uv.Add(new Vector2(1f, soft)); _c.Add(col);
            }
            for (int i = 0; i < segs; i++)
            {
                int a = b + i * 2;
                _t.Add(a); _t.Add(a + 2); _t.Add(a + 1);
                _t.Add(a + 1); _t.Add(a + 2); _t.Add(a + 3);
            }
        }

        /// <summary>Soft blob: centre + rim given by f(angle) (radial offset from centre).</summary>
        public void Fan(Vector3 c, System.Func<float, Vector2> f, int segs, float soft, float alpha)
        {
            int b = _v.Count;
            var col = new Color(1, 1, 1, alpha);
            _v.Add(c); _uv.Add(new Vector2(0f, soft)); _c.Add(col);
            for (int i = 0; i <= segs; i++)
            {
                var o = f(i / (float)segs * Mathf.PI * 2f) * (1f + soft * 0.5f);
                _v.Add(c + new Vector3(o.x, o.y, 0f)); _uv.Add(new Vector2(1f, soft)); _c.Add(col);
            }
            for (int i = 0; i < segs; i++) { _t.Add(b); _t.Add(b + 1 + i); _t.Add(b + 2 + i); }
        }

        /// <summary>Soft polygon from a centroid fan (points relative to origin).</summary>
        public void Polygon(Vector3 origin, List<Vector2> pts, float soft, float alpha)
        {
            var c = Vector2.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            int b = _v.Count;
            var col = new Color(1, 1, 1, alpha);
            _v.Add(origin + (Vector3)c); _uv.Add(new Vector2(0f, soft)); _c.Add(col);
            foreach (var p in pts)
            {
                var q = c + (p - c) * (1f + soft * 0.5f);
                _v.Add(origin + new Vector3(q.x, q.y, 0f)); _uv.Add(new Vector2(1f, soft)); _c.Add(col);
            }
            for (int i = 0; i < pts.Count; i++) { _t.Add(b); _t.Add(b + 1 + i); _t.Add(b + 1 + (i + 1) % pts.Count); }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v); m.SetUVs(0, _uv); m.SetColors(_c); m.SetTriangles(_t, 0);
            m.RecalculateBounds();
            // The root moves vertically with the camera: never cull this mesh by stale bounds.
            m.bounds = new Bounds(m.bounds.center, m.bounds.size + new Vector3(0f, 400f, 0f));
            return m;
        }
    }
}
