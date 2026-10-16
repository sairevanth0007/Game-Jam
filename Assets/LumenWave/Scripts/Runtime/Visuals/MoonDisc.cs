using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// The single moon. It's attached to the camera at a fixed viewport position (like the real moon, it's
    /// effectively at infinity, so it shows no parallax), high and left of centre, clear of the HUD (bars
    /// top-left, level title top-right, tutorial banner bottom).
    /// </summary>
    public class MoonDisc : MonoBehaviour
    {
        public Vector2 viewport = new Vector2(0.36f, 0.8f);
        public float distance = 140f;
        Camera _cam;

        public static MoonDisc Create(Camera cam, Mood mood, float godRays = 1f)
        {
            var root = new GameObject("Moon");
            root.transform.SetParent(cam.transform, false);
            var m = root.AddComponent<MoonDisc>();
            m._cam = cam;
            Quad(root.transform, "Halo", 34f, mood.moon * 0.10f, 2.5f);
            Quad(root.transform, "Disc", 5.2f, mood.moon * 1.15f, 0.22f);
            Quad(root.transform, "Corona", 70f, mood.moon * 0.035f, 1.6f);
            if (godRays > 0f) m.BuildRays(mood, godRays);
            m.Place();
            return m;
        }

        static void Quad(Transform parent, string name, float size, Color c, float falloff)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Backdrop.RemoveCollider(q);
            q.transform.SetParent(parent, false);
            q.transform.localScale = new Vector3(size, size, 1f);
            var mat = MaterialLibrary.Create("Glow");
            c.a = 1f;
            mat.SetColor("_Color", c);
            if (mat.HasProperty("_Falloff")) mat.SetFloat("_Falloff", falloff);
            // Draw before every other transparent so scene fixtures/glows always sit in front of the moon.
            mat.renderQueue = 2900;
            q.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Backdrop.Shadowless(q);
        }

        Transform _rays;

        /// <summary>
        /// God rays: soft additive shafts fanning down from the moon. They sit at about 60–70 % of the moon's
        /// distance (in front of the mountain ridge, behind the far scenery) so silhouettes cut them into beams.
        /// </summary>
        void BuildRays(Mood mood, float strength)
        {
            _rays = new GameObject("GodRays").transform;
            _rays.SetParent(transform.parent, false);
            var mesh = Shaft(1.4f, 24f);
            var rng = new System.Random(5);
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Ray");
                go.transform.SetParent(_rays, false);
                float ang = Mathf.Lerp(-118f, -38f, (i + (float)rng.NextDouble() * 0.6f) / n);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, ang + 90f);
                float len = 70f + (float)rng.NextDouble() * 50f;
                go.transform.localScale = new Vector3(0.6f + (float)rng.NextDouble() * 0.9f, len, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                var mat = MaterialLibrary.Create("LightShaft");
                var c = mood.moon * (0.022f + 0.02f * (float)rng.NextDouble()) * strength; c.a = 1f;
                mat.SetColor("_Color", c);
                mat.SetFloat("_Softness", 3f);
                mat.SetFloat("_Falloff", 1.4f);
                mat.SetFloat("_Shimmer", 0.45f);
                r.sharedMaterial = mat;
                Backdrop.Shadowless(go);
            }
        }

        /// <summary>Trapezoid from width w0 at the source (y = 0) to w1 at y = −1 (unit length, scaled by transform).</summary>
        public static Mesh Shaft(float w0, float w1)
        {
            var m = new Mesh { name = "Shaft" };
            m.vertices = new[] { new Vector3(-w0 * 0.5f, 0f, 0f), new Vector3(w0 * 0.5f, 0f, 0f), new Vector3(w1 * 0.5f, -1f, 0f), new Vector3(-w1 * 0.5f, -1f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            m.bounds = new Bounds(m.bounds.center, m.bounds.size + Vector3.one * 2f);
            return m;
        }

        void LateUpdate() => Place();

        void Place()
        {
            if (_cam == null) return;
            float halfH = distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;
            transform.localPosition = new Vector3((viewport.x - 0.5f) * 2f * halfW, (viewport.y - 0.5f) * 2f * halfH, distance);
            transform.localRotation = Quaternion.identity;
            if (_rays != null)
            {
                const float k = 0.62f;   // rays at 62 % of the moon distance, same screen point
                _rays.localPosition = transform.localPosition * k;
                _rays.localRotation = Quaternion.identity;
                _rays.localScale = Vector3.one * k;
            }
        }
    }
}
