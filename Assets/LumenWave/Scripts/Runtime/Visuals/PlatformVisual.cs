using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Solid 3D look for level blocks (the BoxCollider stays untouched; only the cube renderer is hidden):
    ///  - Body: a bevelled, slightly lumpy rock box (rounded-box projection of subdivided faces + noise), whose
    ///    vertices carry the depth below the walkable surface so the shader sinks the mass into darkness.
    ///  - Cap: on every exposed top run a moss / grass / snow / paving layer with a flat walkable top, an
    ///    irregular overhanging front skirt, and hanging roots / moss / icicles. The front-top edge carries a
    ///    faint mood-tinted "lip" so walkable surfaces stay instantly readable.
    /// </summary>
    public static class PlatformVisual
    {
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

        static Texture2D Tex(string id, bool normal)
        {
            var t = Resources.Load<Texture2D>("Art/AmbientCG/" + id + (normal ? "_Normal" : "_Color"));
            return t;
        }

        public static Material BodyMaterial(Theme th, Mood mood)
        {
            string key = "body" + th.name + ColorUtility.ToHtmlStringRGB(mood.edge);
            if (_mats.TryGetValue(key, out var m) && m != null) return m;
            m = MaterialLibrary.Create("Rock");
            m.name = "Rock_" + th.name;
            m.SetTexture("_MainTex", Tex(th.rock, false));
            m.SetTexture("_BumpMap", Tex(th.rock, true));
            m.SetTexture("_CapTex", Tex(th.cap, false));
            m.SetTexture("_CapBump", Tex(th.cap, true));
            m.SetColor("_Tint", th.rockTint);
            m.SetColor("_CapTint", th.capTint);
            m.SetFloat("_Scale", th.rockScale);
            m.SetFloat("_CapScale", 2.2f);
            m.SetFloat("_Smoothness", 0.06f);
            m.SetFloat("_WetSmoothness", th.wet);
            m.SetFloat("_DarkDepth", 2.8f);
            m.SetFloat("_NormalStrength", 0.45f);
            m.SetFloat("_DarkMin", 0.08f);
            m.SetColor("_LipColor", Color.black);
            m.SetColor("_FillColor", Color.Lerp(Color.white, mood.moonlight, 0.5f) * 1.3f);
            m.SetColor("_Bounce", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), mood.edge, 0.35f) * 0.04f);
            _mats[key] = m;
            return m;
        }

        public static Material CapMaterial(Theme th, Mood mood)
        {
            string key = "cap" + th.name + ColorUtility.ToHtmlStringRGB(mood.edge);
            if (_mats.TryGetValue(key, out var m) && m != null) return m;
            m = MaterialLibrary.Create("Rock");
            m.name = "Cap_" + th.name;
            var tex = Tex(th.cap, false); var nrm = Tex(th.cap, true);
            m.SetTexture("_MainTex", tex);
            m.SetTexture("_BumpMap", nrm);
            m.SetTexture("_CapTex", tex);
            m.SetTexture("_CapBump", nrm);
            m.SetColor("_Tint", th.capTint * 0.8f);
            m.SetColor("_CapTint", th.capTint);
            m.SetFloat("_Scale", 1.6f);
            m.SetFloat("_CapScale", 2.2f);
            m.SetFloat("_Smoothness", th.capKind == CapKind.Snow ? 0.45f : 0.2f);
            m.SetFloat("_WetSmoothness", th.wet);
            m.SetFloat("_DarkDepth", 1.2f);
            m.SetFloat("_DarkMin", 0.35f);
            // Soft moonlit rim on the front lip (readable, but not a neon line).
            m.SetColor("_LipColor", Color.Lerp(mood.edge, Color.white, 0.35f) * (th.capKind == CapKind.Snow ? 0.16f : th.capKind == CapKind.Paving || th.capKind == CapKind.Dust ? 0.22f : 0.12f));
            m.SetColor("_Bounce", mood.edge * 0.05f);
            m.SetColor("_FillColor", Color.Lerp(Color.white, mood.moonlight, 0.5f) * 1.1f);
            m.SetFloat("_NormalStrength", 1.2f);
            _mats[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- noise
        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Noise(float x, float y) => Mathf.PerlinNoise(x + 31.7f, y + 17.3f);
        static float Fbm(Vector3 p) => Noise(p.x * 0.9f + p.z * 0.37f, p.y * 0.9f) * 0.6f + Noise(p.x * 2.7f + 5f, p.y * 2.7f + p.z * 0.9f) * 0.4f;

        // ---------------------------------------------------------------- body
        /// <summary>Builds the visual body for a block (local to the block's parent).</summary>
        public static GameObject Body(Transform parent, Vector3 center, Vector3 size, bool ledge, float[] columnSurface, int colStart, Material mat, System.Func<float, float, float> edgeDist = null)
        {
            var mn = center - size * 0.5f;
            var mx = center + size * 0.5f;
            mx.y -= 0.015f;  // tuck the top under the cap
            float r = ledge ? Mathf.Min(0.1f, size.y * 0.3f) : Mathf.Min(0.22f, size.x * 0.2f, size.y * 0.25f);
            float amp = ledge ? 0.035f : 0.11f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var cols = new List<Color>(); var uv1 = new List<Vector2>(); var tris = new List<int>();
            var inMin = mn + Vector3.one * r; var inMax = mx - Vector3.one * r;
            float step = ledge ? 0.5f : 0.6f;
            bool skipBottom = mn.y <= 0.05f;

            System.Action<Vector3, Vector3, Vector3, float, float> face = (o, du, dv, lu, lv) =>
            {
                var us = Samples(lu, r, step); var vs = Samples(lv, r, step);
                int baseIdx = verts.Count;
                foreach (var v in vs)
                    foreach (var u in us)
                    {
                        var p = o + du * u + dv * v;
                        var c = new Vector3(Mathf.Clamp(p.x, inMin.x, inMax.x), Mathf.Clamp(p.y, inMin.y, inMax.y), Mathf.Clamp(p.z, inMin.z, inMax.z));
                        var d = p - c;
                        Vector3 n = Vector3.Cross(dv, du).normalized;
                        if (d.sqrMagnitude > 1e-8f) { n = d.normalized; p = c + n * r; }
                        // Lumpy rock: displace along the normal, never on the walkable top, fading out near the top edge.
                        float topK = Mathf.Clamp01((mx.y - p.y) / 0.5f) * (1f - Mathf.Clamp01(n.y));
                        float disp = (Fbm(p) - 0.5f) * 2f * amp * topK;
                        if (n.z < -0.5f) disp = Mathf.Max(disp, -amp * 0.5f);
                        p += n * disp;
                        verts.Add(p); norms.Add(n);
                        int col = Mathf.Clamp(Mathf.FloorToInt(p.x) - colStart, 0, columnSurface.Length - 1);
                        float depth = Mathf.Max(0f, columnSurface[col] - p.y);
                        // uv1.y: distance to the nearest exposed side / underside edge (texture band in the shader).
                        float side = edgeDist != null ? edgeDist(p.x, p.y) : 0f;
                        uv1.Add(new Vector2(depth, side));
                        // Cavity: darker right under the cap overhang and in noise hollows.
                        float ao = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01(depth / 0.45f)) * (0.8f + 0.2f * Fbm(p * 1.7f));
                        if (depth > 50f) ao = 1f;
                        cols.Add(new Color(ao, ao, ao, 0f));
                    }
                int nu = us.Count;
                for (int j = 0; j < vs.Count - 1; j++)
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int a = baseIdx + j * nu + i, b = a + 1, c2 = a + nu, d2 = c2 + 1;
                        tris.Add(a); tris.Add(c2); tris.Add(b);
                        tris.Add(b); tris.Add(c2); tris.Add(d2);
                    }
            };
            var sz = mx - mn;
            // front (-z): u = +x, v = +y  (normal = cross(dv, du) = cross(y, x) = -z)
            face(new Vector3(mn.x, mn.y, mn.z), Vector3.right, Vector3.up, sz.x, sz.y);
            // top (+y): u = +x, v = +z -> normal cross(z, x) = +y
            face(new Vector3(mn.x, mx.y, mn.z), Vector3.right, Vector3.forward, sz.x, sz.z);
            // bottom (-y): u = +z, v = +x -> normal cross(x, z) = -y
            if (!skipBottom) face(new Vector3(mn.x, mn.y, mn.z), Vector3.forward, Vector3.right, sz.z, sz.x);
            // left (-x): u = +z, v = +y -> normal cross(y, z) = +x ... use u = +y, v = +z -> cross(z, y) = -x
            face(new Vector3(mn.x, mn.y, mn.z), Vector3.up, Vector3.forward, sz.y, sz.z);
            // right (+x): u = +z, v = +y -> cross(y, z) = +x
            face(new Vector3(mx.x, mn.y, mn.z), Vector3.forward, Vector3.up, sz.z, sz.y);

            var mesh = new Mesh { name = ledge ? "LedgeBody" : "RockBody" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetColors(cols); mesh.SetUVs(1, uv1); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(ledge ? "LedgeVisual" : "RockVisual");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            return go;
        }

        static List<float> Samples(float L, float r, float step)
        {
            var s = new List<float> { 0f };
            if (L <= 2.2f * r) { s.Add(L * 0.5f); s.Add(L); return s; }
            s.Add(r * 0.4f); s.Add(r);
            float inner = L - 2f * r;
            int n = Mathf.Max(1, Mathf.CeilToInt(inner / step));
            for (int i = 1; i < n; i++) s.Add(r + inner * i / n);
            s.Add(L - r); s.Add(L - r * 0.4f); s.Add(L);
            return s;
        }

        // ---------------------------------------------------------------- cap
        /// <summary>Cap layer on an exposed top run [xa, xb] at height <paramref name="top"/>.</summary>
        public static void Cap(MeshBuilderN mb, float xa, float xb, float top, float depth, bool openLeft, bool openRight, CapKind kind, bool ledge, System.Random rng)
        {
            float zf = -depth * 0.5f;
            float ov = ledge ? 0.08f : 0.13f;
            float xl = xa - (openLeft ? 0.07f : 0f), xr = xb + (openRight ? 0.07f : 0f);
            float y = top + 0.012f;
            float baseDrop, varDrop;
            switch (kind)
            {
                case CapKind.Snow: baseDrop = 0.2f; varDrop = 0.16f; break;
                case CapKind.Paving: baseDrop = 0.13f; varDrop = 0.04f; break;
                case CapKind.Dust: baseDrop = 0.09f; varDrop = 0.07f; break;
                case CapKind.Grass: baseDrop = 0.16f; varDrop = 0.22f; break;
                default: baseDrop = 0.17f; varDrop = 0.26f; break;
            }
            if (ledge) { baseDrop *= 0.7f; varDrop *= 0.6f; }
            float seed = xa * 1.37f + top * 3.1f;
            int n = Mathf.Max(2, Mathf.CeilToInt((xr - xl) / 0.25f));
            var xs = new float[n + 1]; var drops = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                xs[i] = Mathf.Lerp(xl, xr, i / (float)n);
                float nz = Mathf.PerlinNoise(xs[i] * 1.3f + seed, 0.5f) * 0.7f + Mathf.PerlinNoise(xs[i] * 4.1f, seed) * 0.3f;
                drops[i] = baseDrop + varDrop * nz;
                // Ends of an open run curl down a little less so the corner reads as rounded.
                if ((i == 0 && openLeft) || (i == n && openRight)) drops[i] *= 0.6f;
            }
            float zBack = depth * 0.5f - 0.02f;
            float zLip = zf - ov;
            // Top surface (flat walkable), 3 rows: lip, just behind lip, back.
            int row0 = mb.Count;
            for (int i = 0; i <= n; i++)
            {
                mb.Add(new Vector3(xs[i], y, zLip), Vector3.up, 1f, 0f);
                mb.Add(new Vector3(xs[i], y, zLip + 0.14f), Vector3.up, 0f, 0f);
                mb.Add(new Vector3(xs[i], y, zBack), Vector3.up, 0f, 0f);
            }
            for (int i = 0; i < n; i++)
            {
                int a = row0 + i * 3, b = a + 3;
                mb.Quad(a, b, b + 1, a + 1);
                mb.Quad(a + 1, b + 1, b + 2, a + 2);
            }
            // Front skirt: lip -> rounded bulge -> irregular lower edge tucked back towards the face.
            int s0 = mb.Count;
            for (int i = 0; i <= n; i++)
            {
                float d = drops[i];
                mb.Add(new Vector3(xs[i], y, zLip), new Vector3(0, 0.5f, -1f).normalized, 1f, 0f);
                mb.Add(new Vector3(xs[i], y - d * 0.45f, zLip - 0.035f), new Vector3(0, -0.1f, -1f).normalized, 0.25f, d * 0.45f);
                mb.Add(new Vector3(xs[i], y - d, zf - 0.01f), new Vector3(0, -0.6f, -1f).normalized, 0f, d);
            }
            for (int i = 0; i < n; i++)
            {
                int a = s0 + i * 3, b = a + 3;
                mb.Quad(a + 1, b + 1, b, a);
                mb.Quad(a + 2, b + 2, b + 1, a + 1);
            }
            // Side skirts on open ends.
            if (openLeft) SideSkirt(mb, xl, y, zLip, zBack, drops[0], -1f);
            if (openRight) SideSkirt(mb, xr, y, zLip, zBack, drops[n], 1f);

            // Hanging roots / moss / icicles below the skirt.
            if (kind == CapKind.Paving) return;
            float spacing = kind == CapKind.Dust ? 1.4f : (kind == CapKind.Snow ? 0.45f : 0.38f);
            float maxLen = kind == CapKind.Snow ? 0.55f : (kind == CapKind.Dust ? 0.25f : 0.7f);
            if (ledge) maxLen = Mathf.Min(maxLen, 0.3f);
            float x = xl + 0.1f + (float)rng.NextDouble() * spacing;
            while (x < xr - 0.1f)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt((x - xl) / (xr - xl) * n), 0, n);
                float yb = y - drops[i] + 0.04f;
                float len = (0.1f + (float)rng.NextDouble() * maxLen) * (rng.Next(4) == 0 ? 1.6f : 1f);
                float w = kind == CapKind.Snow ? 0.06f + 0.06f * (float)rng.NextDouble() : 0.025f + 0.03f * (float)rng.NextDouble();
                float sway = ((float)rng.NextDouble() - 0.5f) * 0.12f;
                Strand(mb, x, yb, zf - 0.03f, len, w, sway, drops[i]);
                x += spacing * (0.4f + (float)rng.NextDouble() * 1.2f);
            }
        }

        /// <summary>Tapered hanging strand (root, moss, icicle) facing the camera.</summary>
        static void Strand(MeshBuilderN mb, float x, float yb, float z, float len, float w, float sway, float depth0)
        {
            int b0 = mb.Count;
            var nrm = new Vector3(0, 0, -1f);
            mb.Add(new Vector3(x - w, yb, z), nrm, 0f, depth0);
            mb.Add(new Vector3(x + w, yb, z), nrm, 0f, depth0);
            mb.Add(new Vector3(x + sway * 0.5f - w * 0.5f, yb - len * 0.55f, z - 0.01f), nrm, 0f, depth0 + len * 0.55f);
            mb.Add(new Vector3(x + sway * 0.5f + w * 0.5f, yb - len * 0.55f, z - 0.01f), nrm, 0f, depth0 + len * 0.55f);
            mb.Add(new Vector3(x + sway, yb - len, z - 0.02f), nrm, 0f, depth0 + len);
            mb.Quad(b0, b0 + 2, b0 + 3, b0 + 1);
            mb.Tri(b0 + 2, b0 + 4, b0 + 3);
        }

        /// <summary>Sparse roots / moss (or short icicles) under an exposed ceiling run. Thin and short: never hazard-like.</summary>
        public static void Underside(MeshBuilderN mb, float xa, float xb, float yb, float depth, CapKind kind, System.Random rng)
        {
            if (kind == CapKind.Paving || kind == CapKind.Dust) return;
            float zf = -depth * 0.5f;
            float x = xa + 0.2f + (float)rng.NextDouble() * 0.6f;
            while (x < xb - 0.2f)
            {
                float len = kind == CapKind.Snow ? 0.12f + (float)rng.NextDouble() * 0.25f : 0.15f + (float)rng.NextDouble() * 0.45f;
                float w = kind == CapKind.Snow ? 0.05f + 0.04f * (float)rng.NextDouble() : 0.02f + 0.025f * (float)rng.NextDouble();
                Strand(mb, x, yb + 0.02f, zf - 0.02f, len, w, ((float)rng.NextDouble() - 0.5f) * 0.1f, 2f);
                x += 0.5f + (float)rng.NextDouble() * 1.4f;
            }
        }

        static void SideSkirt(MeshBuilderN mb, float x, float y, float zLip, float zBack, float drop, float side)
        {
            int k = mb.Count;
            var n = new Vector3(side, 0f, 0f);
            mb.Add(new Vector3(x, y, zLip), n, 0.6f, 0f);
            mb.Add(new Vector3(x, y, zBack), n, 0f, 0f);
            mb.Add(new Vector3(x - side * 0.04f, y - drop, zBack), n, 0f, drop);
            mb.Add(new Vector3(x - side * 0.04f, y - drop, zLip + 0.05f), n, 0f, drop);
            if (side < 0) mb.Quad(k, k + 1, k + 2, k + 3); else mb.Quad(k, k + 3, k + 2, k + 1);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _mats.Clear(); }
    }

    /// <summary>Mesh accumulator with explicit normals, lip (colour alpha) and depth-below-top (uv1.x).</summary>
    public class MeshBuilderN
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<Vector2> uv1 = new List<Vector2>();
        public readonly List<int> tris = new List<int>();
        public int Count => verts.Count;

        public void Add(Vector3 p, Vector3 n, float lip, float depth, float ao = 1f)
        {
            verts.Add(p); normals.Add(n); colors.Add(new Color(ao, ao, ao, lip)); uv1.Add(new Vector2(depth, 0f));
        }

        /// <summary>Quad a-b-c-d, clockwise as seen from the side the normal points to (Unity front face).</summary>
        public void Quad(int a, int b, int c, int d) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
        public void Tri(int a, int b, int c) { tris.Add(a); tris.Add(c); tris.Add(b); }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts); m.SetNormals(normals); m.SetColors(colors); m.SetUVs(1, uv1); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
