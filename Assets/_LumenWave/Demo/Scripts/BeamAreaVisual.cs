using UnityEngine;

namespace LumenWave.Demo
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BeamAreaVisual : MonoBehaviour
    {
        public ShadowGenerator source;
        public Color tint = Color.cyan;
        public Material emissionMaterial;
        Mesh mesh;
        int count;
        void Awake()
        {
            mesh = new Mesh { name = "Soft light ribbon" };
            mesh.MarkDynamic(); GetComponent<MeshFilter>().sharedMesh = mesh;
            if (emissionMaterial == null) emissionMaterial = Resources.Load<Material>("LumenWaveSoftLight");
            if (emissionMaterial == null) { Debug.LogError("Soft light material is missing.", this); return; }
            GetComponent<MeshRenderer>().sharedMaterial = emissionMaterial;
            // The contour remains authoritative for exposure; its old debug lines are hidden.
            foreach (var line in source.GetComponentsInChildren<LineRenderer>()) line.enabled = false;
            var emitter = source.transform.Find("Light emitter");
            if (emitter != null)
            {
                var rectangle = emitter.GetComponent<SpriteRenderer>();
                if (rectangle != null) rectangle.enabled = false;
            }
            CreateEmitterGlow();
        }
        void CreateEmitterGlow()
        {
            var halo = new GameObject("Soft emitter glow", typeof(MeshFilter), typeof(MeshRenderer));
            halo.transform.SetParent(transform, false);
            float size = Mathf.Max(1.15f, source.shadowWidth * 3.6f);
            halo.transform.localScale = new Vector3(size, size, 1);
            var glow = new Mesh { name = "Emitter glow quad" };
            glow.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f) };
            glow.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            glow.triangles = new[] { 0,2,1, 1,2,3 };
            var color = new Color(tint.r,tint.g,tint.b,.8f);
            glow.colors = new[] { color,color,color,color };
            glow.RecalculateBounds();
            halo.GetComponent<MeshFilter>().sharedMesh = glow;
            var renderer = halo.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = emissionMaterial;
            renderer.sortingOrder = 18;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_Radial",1);
            properties.SetFloat("_Intensity",1.3f);
            renderer.SetPropertyBlock(properties);
            emitterMesh = glow;
        }
        Mesh emitterMesh;
        void LateUpdate()
        {
            var points = source.shadowPoints;
            if (points == null) return;
            mesh.vertices = points;
            if (count != points.Length)
            {
                count = points.Length;
                int n = count / 2;
                var triangles = new int[(n - 1) * 6];
                var colors = new Color[count];
                var uv = new Vector2[count];
                for (int i = 0; i < count; i++) colors[i] = tint;
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)(n - 1);
                    uv[i] = new Vector2(1,t);
                    uv[count-1-i] = new Vector2(0,t);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int k = i * 6, a = i, b = i + 1, c = count - 1 - i, d = count - 2 - i;
                    triangles[k] = a; triangles[k + 1] = b; triangles[k + 2] = c;
                    triangles[k + 3] = b; triangles[k + 4] = d; triangles[k + 5] = c;
                }
                mesh.triangles = triangles; mesh.colors = colors; mesh.uv = uv;
            }
            mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); if (emitterMesh != null) Destroy(emitterMesh); }
    }
}
