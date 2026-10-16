using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Builds the environment around a level or menu scene (see <see cref="Scenery"/> for the layers):
    /// sky panorama, mountain ridge, far / mid / near 3D scenery fading into fog, mist sheets, distant glows,
    /// themed extras (gears, crystals), playfield dressing, soft foreground silhouettes and ambient motes.
    /// </summary>
    public static class Backdrop
    {
        public static GameObject Build(Transform parent, int width, int height, Mood mood, int seed, LevelData data, bool menu = false, Theme theme = null, float? ground = null, float camDist = CameraRig.DefaultDistance)
        {
            theme = theme ?? Theme.For(data);
            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed);
            float groundY = ground ?? (data != null ? GroundLevel(data) : 0f);

            Scenery.Sky(root, width, height, groundY, mood, theme, rng);
            System.Func<float, float> floorAt = data != null ? FloorProfile(data, groundY) : null;
            Scenery.Layers(root, 0f, width, groundY, mood, theme, rng, floorAt);
            Scenery.Mist(root, 0f, width, groundY, mood, theme);
            Scenery.DistantGlows(root, width, height, mood, rng, theme, floorAt);
            if (theme.gears) Extras.Gears(root, 0f, width, groundY, mood, rng);
            if (theme.crystals) Extras.Crystals(root, 0f, width, groundY, mood, theme, rng, floorAt);
            if (data != null) Scenery.Dressing(root, data, mood, theme, rng);
            var cam = SceneRig.Instance != null ? SceneRig.Instance.Camera : Camera.main;
            Foreground.Build(root, 0f, width, mood, theme, seed + 17, cam, camDist);
            Scenery.Motes(root, mood, theme, menu);
            return root.gameObject;
        }

        /// <summary>
        /// Smoothed local floor height along x: the lowest walkable surface per column (gaps inherit their
        /// neighbours), averaged over a wide window so background terrain follows the level's big shapes.
        /// </summary>
        public static System.Func<float, float> FloorProfile(LevelData d, float fallback)
        {
            int w = d.Width;
            var raw = new float[w];
            var has = new bool[w];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < d.Height - 1; y++)
                {
                    if (y + 1 >= d.Height - 1) break;   // full-height walls are not a floor
                    if (LevelData.IsSolid(d.Tile(x, y)) && !LevelData.IsSolid(d.Tile(x, y + 1))) { raw[x] = y + 1; has[x] = true; break; }
                }
            float last = fallback;
            for (int x = 0; x < w; x++) { if (has[x]) last = raw[x]; else raw[x] = last; }
            var sm = new float[w];
            const int win = 10;
            for (int x = 0; x < w; x++)
            {
                float sum = 0f; int n = 0;
                for (int k = -win; k <= win; k++) { int i = Mathf.Clamp(x + k, 0, w - 1); sum += raw[i]; n++; }
                sm[x] = sum / n;
            }
            return x => w == 0 ? fallback : sm[Mathf.Clamp(Mathf.RoundToInt(x), 0, w - 1)];
        }

        /// <summary>Typical floor height of a level: median of the lowest walkable surface per column.</summary>
        public static float GroundLevel(LevelData d)
        {
            var tops = new List<float>();
            for (int x = 0; x < d.Width; x++)
                for (int y = 0; y < d.Height - 1; y++)
                    if (LevelData.IsSolid(d.Tile(x, y)) && !LevelData.IsSolid(d.Tile(x, y + 1))) { tops.Add(y + 1); break; }
            if (tops.Count == 0) return 0f;
            tops.Sort();
            return tops[tops.Count / 2];
        }

        public static void RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        public static void Shadowless(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }

    /// <summary>Tiny helper to accumulate coloured triangles into one mesh.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Color> colors = new List<Color>();
        public readonly List<int> tris = new List<int>();
        public readonly List<Vector2> uvs = new List<Vector2>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            colors.Add(col); colors.Add(col); colors.Add(col);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
        }

        /// <summary>Quad a-b-c-d counter-clockwise when seen from −Z (the camera).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            colors.Add(col); colors.Add(col); colors.Add(col); colors.Add(col);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetColors(colors);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public GameObject ToGameObject(string name, Material mat)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = ToMesh(name);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }

    /// <summary>
    /// Title / end screen background: a rocky outcrop on the right (bevelled rock + cap, scenery on top)
    /// overlooking a misty valley of layered silhouettes under the moon; the left / centre stays open and calm
    /// for the logo and menu. The camera drifts slowly.
    /// </summary>
    public static class MenuVista
    {
        public const float Width = 64f, CamX = 24f, CamY = 4.5f, CamDist = 30f;

        public static void Build(SceneRig rig, int seed)
        {
            var root = rig.transform;
            var theme = rig.Theme;
            var mood = rig.Mood;
            Backdrop.Build(root, (int)Width, 16, mood, seed, null, true, theme, -4f, CamDist);
            // Outcrop: a deep rock shelf from x = 30 to the right edge, with a raised step.
            var rng = new System.Random(seed);
            var vis = new GameObject("Outcrop").transform;
            vis.SetParent(root, false);
            var body = PlatformVisual.BodyMaterial(theme, mood);
            var capMat = PlatformVisual.CapMaterial(theme, mood);
            void Shelf(float xa, float xb, float top, float bottom, bool openL)
            {
                int cols = Mathf.CeilToInt(xb - xa) + 2;
                var surf = new float[cols];
                for (int i = 0; i < cols; i++) surf[i] = top;
                var size = new Vector3(xb - xa, top - bottom, 4f);
                PlatformVisual.Body(vis, new Vector3((xa + xb) * 0.5f, (top + bottom) * 0.5f, 0f), size, false, surf, Mathf.FloorToInt(xa), body, (x, y) => x - xa);
                var mb = new MeshBuilderN();
                PlatformVisual.Cap(mb, xa, xb, top, 4f, openL, false, theme.capKind, false, rng);
                var go = new GameObject("TopCap");
                go.transform.SetParent(vis, false);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("MenuCap");
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = capMat;
            }
            Shelf(25.5f, Width + 6f, 0.6f, -12f, true);
            Shelf(31.5f, Width + 6f, 2.2f, 0.6f, true);
            // Scenery on the outcrop: a big tree, rocks and grass.
            var propMat = Scenery.PropMaterial(mood, theme.topPropTint * 0.8f, 0.05f, 0.6f, -100f, 0f, 0.6f);
            string tree = theme.name == "dawn" ? "KenneyNature/tree_pineRoundA" : "KenneyNature/tree_oak_dark";
            PropLibrary.Spawn(tree, vis, new Vector3(36f, 2.15f, 1.6f), 7f, 30f, propMat);
            PropLibrary.Spawn("KenneyNature/rock_largeA", vis, new Vector3(27.5f, 0.55f, 1.0f), 1.2f, 70f, propMat);
            PropLibrary.Spawn("KenneyNature/stump_oldTall", vis, new Vector3(29.6f, 0.55f, 1.3f), 1.0f, 10f, propMat);
            for (int i = 0; i < 16; i++)
            {
                float x = 26f + (float)rng.NextDouble() * 16f;
                float top = x > 31.5f ? 2.2f : 0.6f;
                string g = theme.topProps[rng.Next(theme.topProps.Length)];
                PropLibrary.Spawn(g, vis, new Vector3(x, top - 0.03f, 0.3f + (float)rng.NextDouble() * 1.5f), 0.35f + (float)rng.NextDouble() * 0.4f, (float)rng.NextDouble() * 360f, propMat);
            }
            // A lantern glow on the shelf: a warm point of light in the cold scene.
            var lantern = new GameObject("MenuLantern").transform;
            lantern.SetParent(vis, false);
            lantern.localPosition = new Vector3(32.6f, 3.4f, -0.4f);
            var lc = theme.name == "dawn" ? new Color(1f, 0.75f, 0.45f) : new Color(1f, 0.7f, 0.4f);
            LightVisuals.Glow(lantern, Vector3.zero, 2.6f, lc * 1.4f, "LanternGlow");
            LightVisuals.Glow(lantern, new Vector3(0, 0, 0.2f), 9f, lc * 0.2f, "LanternHalo");
            var l = LightVisuals.AddLight(lantern, LightType.Point, lc, 3f, 9f, "LanternLight");
            l.transform.localPosition = new Vector3(0f, 0f, -1f);
            rig.CameraRig.SetBounds(new Rect(-20f, -10f, Width + 40f, 40f));
            rig.CameraRig.enabled = false;
            rig.Camera.transform.SetPositionAndRotation(new Vector3(CamX, CamY, -CamDist), Quaternion.Euler(CameraRig.Pitch, 0f, 0f));
            var moon = rig.Camera.GetComponentInChildren<MoonDisc>();
            if (moon != null) moon.viewport = new Vector2(0.74f, 0.74f);
            rig.gameObject.AddComponent<MenuCameraDrift>();
        }
    }

    /// <summary>Slow cinematic drift for title/end backgrounds.</summary>
    public class MenuCameraDrift : MonoBehaviour
    {
        Vector3 _start;
        Transform _cam;

        void Start()
        {
            var rig = GetComponent<SceneRig>();
            _cam = rig != null && rig.Camera != null ? rig.Camera.transform : null;
            if (_cam != null)
            {
                var cr = _cam.GetComponent<CameraRig>();
                if (cr != null) cr.enabled = false;
                _start = _cam.position;
            }
        }

        void Update()
        {
            if (_cam == null) return;
            float t = Time.unscaledTime;
            _cam.position = _start + new Vector3(Mathf.Sin(t * 0.05f) * 2.5f, Mathf.Sin(t * 0.08f) * 0.4f, 0f);
        }
    }
}
