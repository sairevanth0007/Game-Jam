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

        public static MoonDisc Create(Camera cam, Mood mood)
        {
            var root = new GameObject("Moon");
            root.transform.SetParent(cam.transform, false);
            var m = root.AddComponent<MoonDisc>();
            m._cam = cam;
            Quad(root.transform, "Halo", 34f, mood.moon * 0.10f, 2.5f);
            Quad(root.transform, "Disc", 5.2f, mood.moon * 1.15f, 0.22f);
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

        void LateUpdate() => Place();

        void Place()
        {
            if (_cam == null) return;
            float halfH = distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * _cam.aspect;
            transform.localPosition = new Vector3((viewport.x - 0.5f) * 2f * halfW, (viewport.y - 0.5f) * 2f * halfH, distance);
            transform.localRotation = Quaternion.identity;
        }
    }
}
