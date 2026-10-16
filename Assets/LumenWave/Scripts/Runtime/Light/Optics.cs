using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// A flat mirror. Beams reflect off it with angle in = angle out (LightMath.Reflect).
    /// Rotatable mirrors turn 45° each time a Pulse wave passes.
    /// </summary>
    public class Mirror : MonoBehaviour, IPulseReceiver, IResettable
    {
        public float angle;            // slab orientation in degrees (0 = horizontal surface)
        public bool rotatable;
        public float step = 45f;

        float _initial, _target, _lastHit = -10f;
        Transform _slab;
        MeshRenderer _glint, _edge;
        MaterialPropertyBlock _mpb;
        Color _hitColor = Color.white;

        public float Angle => _target;

        public void Build()
        {
            _mpb = new MaterialPropertyBlock();
            _initial = _target = angle;
            _slab = new GameObject("Slab").transform;
            _slab.SetParent(transform, false);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "MirrorGlass";
            glass.layer = Layers.Mirror;
            glass.transform.SetParent(_slab, false);
            glass.transform.localScale = new Vector3(1.8f, 0.14f, 1.2f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = MaterialLibrary.Tinted("Lit", new Color(0.55f, 0.65f, 0.75f));
            var edgeMat = MaterialLibrary.Tinted("LitEmissive", new Color(0.6f, 0.8f, 1f), new Color(0.6f, 0.85f, 1f) * 2.5f);
            _edge = LightVisuals.Part(PrimitiveType.Cube, _slab, new Vector3(0, 0.085f, -0.62f), new Vector3(1.8f, 0.03f, 0.03f), edgeMat, "Edge").GetComponent<MeshRenderer>();
            LightVisuals.Part(PrimitiveType.Cube, _slab, new Vector3(0, -0.085f, -0.62f), new Vector3(1.8f, 0.03f, 0.03f), edgeMat, "Edge2");
            _glint = LightVisuals.Glow(_slab, new Vector3(0, 0, -0.7f), 1.2f, Color.clear, "Glint");
            // Pivot post so it reads as a mounted, turnable device.
            var dark = MaterialLibrary.Tinted("Lit", new Color(0.06f, 0.06f, 0.08f));
            LightVisuals.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0, 0.5f), new Vector3(0.3f, 0.3f, 0.3f), dark, "Pivot").transform.localRotation = Quaternion.Euler(90, 0, 0);
            if (rotatable)
            {
                var ring = MaterialLibrary.Tinted("LitEmissive", new Color(0.8f, 0.7f, 1f), new Color(0.7f, 0.5f, 1f) * 2f);
                LightVisuals.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0, 0.45f), new Vector3(0.42f, 0.02f, 0.42f), ring, "RotateRing").transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            _slab.localRotation = Quaternion.Euler(0, 0, angle);
        }

        public void NotifyHit(LightColor c) { _lastHit = Time.time; _hitColor = LightMath.DisplayColor(c); }

        void Update()
        {
            if (_slab == null) return;
            float cur = _slab.localEulerAngles.z;
            float next = Mathf.MoveTowardsAngle(cur, _target, 240f * Time.deltaTime);
            _slab.localRotation = Quaternion.Euler(0, 0, next);
            if (_glint != null)
            {
                float k = Mathf.Clamp01(1f - (Time.time - _lastHit) * 4f);
                float sparkle = 0.7f + 0.3f * Mathf.Sin(Time.time * 20f);
                LightVisuals.SetGlowColor(_glint, _mpb, _hitColor * (k * 1.6f * sparkle));
            }
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            if (!rotatable) return;
            _target = Mathf.Repeat(_target + step, 360f);
            angle = _target;
        }

        public void ResetState()
        {
            _target = angle = _initial;
            if (_slab) _slab.localRotation = Quaternion.Euler(0, 0, _initial);
        }

        /// <summary>Snap immediately (tests / editor).</summary>
        public void SetAngleImmediate(float a)
        {
            _target = angle = a;
            if (_slab) _slab.localRotation = Quaternion.Euler(0, 0, a);
        }
    }

    /// <summary>
    /// A glass prism. Newton's dispersion: light entering is split into its R/G/B components, each
    /// leaving at its own deviation (blue bends most, red least) around the prism's output direction.
    /// White in → three coloured beams out. Rotatable prisms turn 45° per Pulse, re-aiming the fan.
    /// </summary>
    public class Prism : MonoBehaviour, IPulseReceiver, IResettable
    {
        public float angle;            // output direction in degrees (0 = right)
        public float spread = 18f;     // angular separation between neighbouring colours
        public float radius = 0.75f;
        public bool rotatable;
        public float step = 45f;

        float _initial, _target, _lastHit = -10f;
        Transform _body;
        MeshRenderer _glow;
        MaterialPropertyBlock _mpb;

        public float Angle => _target;
        public Vector2 OutDirection => new Vector2(Mathf.Cos(_target * Mathf.Deg2Rad), Mathf.Sin(_target * Mathf.Deg2Rad));

        /// <summary>Exit direction of one colour channel.</summary>
        public Vector2 ExitDirection(LightColor channel)
        {
            float dev = LightMath.PrismDeviation(channel, spread);
            return LightField.Rotate(OutDirection, dev).normalized;
        }

        public void Build()
        {
            _mpb = new MaterialPropertyBlock();
            _initial = _target = angle;
            _body = new GameObject("PrismBody").transform;
            _body.SetParent(transform, false);
            var go = new GameObject("Glass");
            go.layer = Layers.Prism;
            go.transform.SetParent(_body, false);
            var mesh = TriangularPrism(radius, 1.2f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialLibrary.Tinted("Lit", new Color(0.75f, 0.85f, 0.95f));
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            // Emissive rainbow edges: R, G, B along the three vertical edges.
            var cols = new[] { LightColor.Red, LightColor.Green, LightColor.Blue };
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                var c = LightMath.DisplayColor(cols[i]);
                LightVisuals.Part(PrimitiveType.Sphere, _body, p + new Vector3(0, 0, -0.62f), Vector3.one * 0.12f, MaterialLibrary.Tinted("LitEmissive", c, c * 4f), "Vertex" + i);
            }
            _glow = LightVisuals.Glow(_body, new Vector3(0, 0, -0.7f), 2.2f, new Color(0.6f, 0.6f, 0.8f) * 0.2f, "Glow");
            if (rotatable)
            {
                var ring = MaterialLibrary.Tinted("LitEmissive", new Color(0.8f, 0.7f, 1f), new Color(0.7f, 0.5f, 1f) * 2f);
                LightVisuals.Part(PrimitiveType.Cylinder, transform, new Vector3(0, 0, 0.7f), new Vector3(radius * 2.4f, 0.02f, radius * 2.4f), ring, "RotateRing").transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            ApplyRotation(_target);
        }

        // The triangle's apex points along the output direction.
        void ApplyRotation(float a) { if (_body) _body.localRotation = Quaternion.Euler(0, 0, a - 90f); }

        public void NotifyHit(LightColor c) { _lastHit = Time.time; }

        void Update()
        {
            if (_body == null) return;
            float cur = _body.localEulerAngles.z + 90f;
            float next = Mathf.MoveTowardsAngle(cur, _target, 240f * Time.deltaTime);
            ApplyRotation(next);
            if (_glow)
            {
                float k = Mathf.Clamp01(1f - (Time.time - _lastHit) * 3f);
                float hue = Mathf.Repeat(Time.time * 0.25f, 1f);
                var c = Color.HSVToRGB(hue, 0.5f, 1f);
                LightVisuals.SetGlowColor(_glow, _mpb, Color.Lerp(new Color(0.5f, 0.5f, 0.7f) * 0.15f, c * 0.9f, k));
            }
        }

        public void OnPulse(Vector3 origin, float strength)
        {
            if (!rotatable) return;
            _target = Mathf.Repeat(_target + step, 360f);
            angle = _target;
        }

        public void ResetState()
        {
            _target = angle = _initial;
            ApplyRotation(_initial);
        }

        public static Mesh TriangularPrism(float r, float depth)
        {
            var tri = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                tri[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            float h = depth * 0.5f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Face(Vector3 a, Vector3 b, Vector3 c)
            {
                int k = verts.Count; verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1); // winding flipped so faces point outward
            }
            var f = new Vector3(0, 0, -h); var bk = new Vector3(0, 0, h);
            Face(tri[0] + f, tri[1] + f, tri[2] + f);              // front (faces -Z)
            Face(tri[0] + bk, tri[2] + bk, tri[1] + bk);           // back
            for (int i = 0; i < 3; i++)
            {
                var a = tri[i]; var b = tri[(i + 1) % 3];
                Face(a + f, a + bk, b + bk);
                Face(a + f, b + bk, b + f);
            }
            var m = new Mesh { name = "TriPrism" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
