using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Procedural parallax background: sky with moon, several depth planes of silhouetted mountains, hills,
    /// dead trees and ruins (true 3D depth, so the perspective camera produces real parallax), distant glows,
    /// and floating dust motes. Layers further away are lighter / more fogged (atmospheric perspective).
    /// </summary>
    public static class Backdrop
    {
        struct Layer { public float z, baseY, amp, freq; public float brightness; public bool trees, ruins; }

        public static GameObject Build(Transform parent, int width, int height, Mood mood, int seed, LevelData data, bool menu = false)
        {
            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(seed);

            // Sky: a huge quad far behind everything.
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            RemoveCollider(sky);
            sky.name = "Sky";
            sky.transform.SetParent(root, false);
            sky.transform.position = new Vector3(width * 0.5f, height * 0.5f + 8f, 150f);
            sky.transform.localScale = new Vector3(width * 3f + 420f, 260f, 1f);
            var skyMat = MaterialLibrary.Create("Sky");
            skyMat.SetColor("_TopColor", mood.skyTop);
            skyMat.SetColor("_HorizonColor", mood.skyHorizon);
            skyMat.SetColor("_BottomColor", mood.skyBottom);
            skyMat.SetColor("_MoonColor", mood.moon);
            skyMat.SetVector("_MoonPos", new Vector4(0.5f + (float)rng.NextDouble() * 0.12f, 0.6f, 0, 0));
            skyMat.SetFloat("_MoonSize", 0.016f);
            skyMat.SetFloat("_Aspect", sky.transform.localScale.x / sky.transform.localScale.y);
            skyMat.SetFloat("_Horizon", 0.44f);
            sky.GetComponent<MeshRenderer>().sharedMaterial = skyMat;
            Shadowless(sky);

            var layers = new[]
            {
                new Layer { z = 70f, baseY = 4f, amp = 16f, freq = 0.018f, brightness = 1.25f },
                new Layer { z = 38f, baseY = 1f, amp = 10f, freq = 0.035f, brightness = 0.95f },
                new Layer { z = 18f, baseY = -1f, amp = 4.5f, freq = 0.06f, brightness = 0.7f, trees = true, ruins = true },
                new Layer { z = 7f, baseY = -2.5f, amp = 2.2f, freq = 0.11f, brightness = 0.45f, trees = true },
            };
            var mat = MaterialLibrary.Get("Silhouette");
            foreach (var L in layers)
            {
                float margin = 14f + 0.65f * L.z;
                float x0 = -margin, x1 = width + margin;
                float bottom = -12f - L.z * 0.5f;
                var mb = new MeshBuilder();
                var col = mood.layer * L.brightness;
                col.a = 1f;
                float phase = (float)rng.NextDouble() * 100f;
                // Ridge line from layered noise.
                int steps = Mathf.CeilToInt((x1 - x0) / 0.8f);
                float prevX = x0, prevY = Ridge(x0, L, phase);
                for (int i = 1; i <= steps; i++)
                {
                    float x = Mathf.Lerp(x0, x1, i / (float)steps);
                    float y = Ridge(x, L, phase);
                    mb.Quad(new Vector3(prevX, bottom), new Vector3(x, bottom), new Vector3(x, y), new Vector3(prevX, prevY), col);
                    prevX = x; prevY = y;
                }
                if (L.trees)
                {
                    int count = Mathf.CeilToInt((x1 - x0) / (L.z < 10f ? 7f : 4.5f));
                    for (int i = 0; i < count; i++)
                    {
                        float x = Mathf.Lerp(x0, x1, (float)rng.NextDouble());
                        float y = Ridge(x, L, phase) - 0.3f;
                        float h = (L.z < 10f ? 4f : 6f) * (0.6f + (float)rng.NextDouble() * 0.8f);
                        Tree(mb, new Vector2(x, y), h, rng, col);
                    }
                }
                if (L.ruins)
                {
                    int count = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 26f));
                    for (int i = 0; i < count; i++)
                    {
                        float x = Mathf.Lerp(x0, x1, (float)rng.NextDouble());
                        Ruin(mb, new Vector2(x, Ridge(x, L, phase) - 0.5f), rng, col);
                    }
                }
                var go = mb.ToGameObject("Layer_z" + L.z, mat);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(0f, 0f, L.z);
            }

            // Distant glows (lamps in the fog) tinted by the mood.
            int glows = Mathf.Max(2, width / 18);
            for (int i = 0; i < glows; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
                RemoveCollider(g);
                g.name = "DistantGlow";
                g.transform.SetParent(root, false);
                float z = 25f + (float)rng.NextDouble() * 40f;
                g.transform.position = new Vector3((float)rng.NextDouble() * width, height * 0.3f + (float)rng.NextDouble() * 10f, z);
                float s = 6f + (float)rng.NextDouble() * 10f;
                g.transform.localScale = new Vector3(s, s, 1f);
                var gc = mood.edge * 0.22f; gc.a = 1f;
                var m = MaterialLibrary.Tinted("Glow", gc);
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                Shadowless(g);
            }

            Dust(root, width, height, mood);
            return root.gameObject;
        }

        static float Ridge(float x, Layer L, float phase)
        {
            float n = Mathf.PerlinNoise(x * L.freq + phase, phase * 0.37f) * 0.75f
                    + Mathf.PerlinNoise(x * L.freq * 3.1f + phase, 11.3f) * 0.25f;
            return L.baseY + n * L.amp;
        }

        static void Tree(MeshBuilder mb, Vector2 p, float h, System.Random rng, Color c)
        {
            float w = h * 0.06f;
            float lean = ((float)rng.NextDouble() - 0.5f) * h * 0.15f;
            var top = new Vector2(p.x + lean, p.y + h);
            mb.Quad(new Vector3(p.x - w, p.y), new Vector3(p.x + w, p.y), new Vector3(top.x + w * 0.25f, top.y), new Vector3(top.x - w * 0.25f, top.y), c);
            int branches = 3 + rng.Next(4);
            for (int i = 0; i < branches; i++)
            {
                float t = 0.35f + (float)rng.NextDouble() * 0.6f;
                var b0 = Vector2.Lerp(p, top, t);
                float dir = rng.Next(2) == 0 ? -1f : 1f;
                float len = h * (0.18f + (float)rng.NextDouble() * 0.25f) * (1.1f - t);
                var b1 = b0 + new Vector2(dir * len, len * (0.4f + (float)rng.NextDouble() * 0.6f));
                float bw = w * 0.5f * (1.2f - t);
                mb.Tri(new Vector3(b0.x, b0.y - bw), new Vector3(b0.x, b0.y + bw), new Vector3(b1.x, b1.y), c);
            }
        }

        static void Ruin(MeshBuilder mb, Vector2 p, System.Random rng, Color c)
        {
            int pillars = 2 + rng.Next(3);
            float spacing = 2.2f + (float)rng.NextDouble();
            float ph = 4f + (float)rng.NextDouble() * 4f;
            for (int i = 0; i < pillars; i++)
            {
                float x = p.x + i * spacing;
                float h = ph * (rng.Next(3) == 0 ? 0.5f + (float)rng.NextDouble() * 0.4f : 1f);
                mb.Quad(new Vector3(x - 0.35f, p.y), new Vector3(x + 0.35f, p.y), new Vector3(x + 0.35f, p.y + h), new Vector3(x - 0.35f, p.y + h), c);
                if (h >= ph && i < pillars - 1 && rng.Next(2) == 0)
                {
                    // Lintel / arch slab between two full pillars.
                    mb.Quad(new Vector3(x - 0.5f, p.y + ph), new Vector3(x + spacing + 0.5f, p.y + ph),
                            new Vector3(x + spacing + 0.5f, p.y + ph + 0.6f), new Vector3(x - 0.5f, p.y + ph + 0.6f), c);
                }
            }
        }

        static void Dust(Transform root, int width, int height, Mood mood)
        {
            var go = new GameObject("DustMotes");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(width * 0.5f, height * 0.5f + 2f, 1.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.13f);
            var dc = mood.dust; dc.a = 0.55f;
            var dc2 = mood.dust * 0.6f; dc2.a = 0.25f;
            main.startColor = new ParticleSystem.MinMaxGradient(dc2, dc);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = ps.emission;
            em.rateOverTime = Mathf.Clamp(width * height * 0.06f, 20f, 120f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width + 20f, height + 10f, 10f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Get("GlowParticle");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
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
            _cam.position = _start + new Vector3(Mathf.Sin(t * 0.05f) * 6f, Mathf.Sin(t * 0.08f) * 0.6f, 0f);
        }
    }
}
