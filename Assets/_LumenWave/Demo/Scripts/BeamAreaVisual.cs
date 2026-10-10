using UnityEngine;

namespace LumenWave.Demo
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BeamAreaVisual : MonoBehaviour
    {
        public ShadowGenerator source;
        public Color tint = Color.cyan;
        Mesh mesh;
        int count;
        void Awake() { mesh = new Mesh { name = "Shared light boundary" }; mesh.MarkDynamic(); GetComponent<MeshFilter>().sharedMesh = mesh; }
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
                for (int i = 0; i < count; i++) colors[i] = tint;
                for (int i = 0; i < n - 1; i++)
                {
                    int k = i * 6, a = i, b = i + 1, c = count - 1 - i, d = count - 2 - i;
                    triangles[k] = a; triangles[k + 1] = b; triangles[k + 2] = c;
                    triangles[k + 3] = b; triangles[k + 4] = d; triangles[k + 5] = c;
                }
                mesh.triangles = triangles; mesh.colors = colors;
            }
            mesh.RecalculateBounds();
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
