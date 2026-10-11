using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Loads third-party scenery models (Resources "Art/&lt;Pack&gt;/&lt;name&gt;") once, flattens them into
    /// (mesh, matrix) parts with normalised bounds, and spawns lightweight visual-only copies (no colliders,
    /// no shadows) with our own materials, scaled to a requested height and standing on a given point.
    /// </summary>
    public static class PropLibrary
    {
        public class Model
        {
            public readonly List<Mesh> meshes = new List<Mesh>();
            public readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
            public Bounds bounds;
            public bool valid;
        }

        static readonly Dictionary<string, Model> _cache = new Dictionary<string, Model>();

        public static Model Get(string path)
        {
            if (_cache.TryGetValue(path, out var m)) return m;
            m = new Model();
            _cache[path] = m;
            var prefab = Resources.Load<GameObject>("Art/" + path);
            if (prefab == null) { Debug.LogWarning("[LumenWave] Missing scenery model Art/" + path); return m; }
            // Keep the root's own rotation/scale (FBX axis conversion lives there); drop only its position.
            var rootInv = Matrix4x4.Translate(-prefab.transform.position);
            bool first = true;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mat = rootInv * mf.transform.localToWorldMatrix;
                m.meshes.Add(mf.sharedMesh);
                m.matrices.Add(mat);
                var b = Transform(mf.sharedMesh.bounds, mat);
                if (first) { m.bounds = b; first = false; } else m.bounds.Encapsulate(b);
            }
            m.valid = !first && m.bounds.size.y > 1e-4f;
            return m;
        }

        static Bounds Transform(Bounds b, Matrix4x4 m)
        {
            var c = b.center; var e = b.extents;
            var r = new Bounds(m.MultiplyPoint3x4(c), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var p = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                r.Encapsulate(m.MultiplyPoint3x4(p));
            }
            return r;
        }

        /// <summary>
        /// Spawns a model so that its bounds' bottom-centre stands at <paramref name="foot"/> (local to parent),
        /// scaled uniformly to <paramref name="height"/>, rotated by <paramref name="yaw"/> degrees.
        /// Returns null if the model is missing.
        /// </summary>
        public static GameObject Spawn(string path, Transform parent, Vector3 foot, float height, float yaw, Material mat, bool flipX = false, float widthScale = 1f, float maxWidth = 0f)
        {
            var model = Get(path);
            if (!model.valid) return null;
            var go = new GameObject(path.Substring(path.LastIndexOf('/') + 1));
            go.transform.SetParent(parent, false);
            float s = height / model.bounds.size.y;
            // Wide models (walls, cliffs) are limited by width instead, so they don't become huge slabs.
            float wide = Mathf.Max(model.bounds.size.x, model.bounds.size.z);
            if (maxWidth > 0f && wide * s > maxWidth) s = maxWidth / wide;
            go.transform.localPosition = foot;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(s * widthScale * (flipX ? -1f : 1f), s, s * widthScale);
            var pivot = new Vector3(model.bounds.center.x, model.bounds.min.y, model.bounds.center.z);
            for (int i = 0; i < model.meshes.Count; i++)
            {
                var part = new GameObject("part");
                part.transform.SetParent(go.transform, false);
                var mtx = Matrix4x4.Translate(-pivot) * model.matrices[i];
                part.transform.localPosition = mtx.GetColumn(3);
                part.transform.localRotation = mtx.rotation;
                part.transform.localScale = mtx.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = model.meshes[i];
                var r = part.AddComponent<MeshRenderer>();
                int subs = Mathf.Max(1, model.meshes[i].subMeshCount);
                if (subs == 1) r.sharedMaterial = mat;
                else
                {
                    var mats = new Material[subs];
                    for (int k = 0; k < subs; k++) mats[k] = mat;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            return go;
        }

        /// <summary>Width/height ratio of a model (for spacing), 1 if missing.</summary>
        public static float Aspect(string path)
        {
            var m = Get(path);
            return m.valid ? Mathf.Max(m.bounds.size.x, m.bounds.size.z) / m.bounds.size.y : 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _cache.Clear(); }
    }
}
