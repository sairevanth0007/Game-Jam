using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// PULSE: a circular wavefront expanding at a finite speed. Objects are affected only when the
    /// front actually reaches them (t = d / v), so far things react later — like real sound.
    /// </summary>
    public class PulseWave : MonoBehaviour
    {
        public const float Speed = 14f;
        public const float MaxRadius = 9f;

        public float Radius { get; private set; }
        public Vector3 Origin { get; private set; }

        GameObject _owner;
        readonly HashSet<Collider> _hitColliders = new HashSet<Collider>();
        readonly HashSet<IPulseReceiver> _hitReceivers = new HashSet<IPulseReceiver>();
        readonly Collider[] _buffer = new Collider[128];
        readonly List<MonoBehaviour> _tmp = new List<MonoBehaviour>();
        LineRenderer _ring, _ring2;
        Material _quadMat;
        Transform _quad;
        Color _color = new Color(0.65f, 0.9f, 1.6f, 1f);

        public static PulseWave Spawn(Vector3 origin, GameObject owner)
        {
            var go = new GameObject("PulseWave");
            var parent = SoundPowers.SpawnParent;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = origin;
            var p = go.AddComponent<PulseWave>();
            p.Origin = origin;
            p._owner = owner;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            var waveMat = MaterialLibrary.Get("Wave");
            if (waveMat != null && waveMat.shader != null && waveMat.shader.name == "LumenWave/Wave")
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "WaveQuad";
                q.transform.SetParent(transform, false);
                q.transform.localPosition = new Vector3(0f, 0f, -0.8f);
                q.transform.localScale = Vector3.one * (MaxRadius * 2f + 1f);
                _quadMat = new Material(waveMat);
                if (_quadMat.HasProperty("_Color")) _quadMat.SetColor("_Color", _color);
                if (_quadMat.HasProperty("_Thickness")) _quadMat.SetFloat("_Thickness", 0.035f);
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _quadMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _quad = q.transform;
            }
            else
            {
                _ring = MakeRing("Ring", 0.12f);
                _ring2 = MakeRing("Ring2", 0.05f);
            }
        }

        LineRenderer MakeRing(string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 72;
            lr.widthMultiplier = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = EchoBridge.LineMaterial(_color);
            lr.startColor = lr.endColor = Color.white;
            return lr;
        }

        void Update()
        {
            Radius += Speed * Time.deltaTime;
            float r = Mathf.Min(Radius, MaxRadius);
            DetectHits(r);

            float alpha = 1f - Mathf.Clamp01(Radius / MaxRadius);
            alpha *= alpha;
            if (_quadMat != null)
            {
                if (_quadMat.HasProperty("_Radius")) _quadMat.SetFloat("_Radius", r / (MaxRadius + 0.5f));
                if (_quadMat.HasProperty("_Alpha")) _quadMat.SetFloat("_Alpha", alpha);
            }
            if (_ring != null) DrawRing(_ring, r, alpha);
            if (_ring2 != null) DrawRing(_ring2, Mathf.Max(0f, r - 0.6f), alpha * 0.5f);

            if (Radius >= MaxRadius) Destroy(gameObject);
        }

        void DrawRing(LineRenderer lr, float r, float alpha)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                // Slight ripple on the front: k·r modulation so it reads as a wave, not a hoop.
                float rr = r + 0.06f * Mathf.Sin(a * 12f + r * 6f);
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, -0.8f));
            }
            lr.startColor = lr.endColor = new Color(1f, 1f, 1f, alpha);
        }

        void DetectHits(float r)
        {
            int n = Physics.OverlapSphereNonAlloc(Origin, r, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _buffer[i];
                if (c == null || _hitColliders.Contains(c)) continue;
                if (_owner != null && c.transform.IsChildOf(_owner.transform)) continue;
                _hitColliders.Add(c);
                _tmp.Clear();
                c.GetComponentsInParent(true, _tmp);
                foreach (var mb in _tmp)
                {
                    if (mb is IPulseReceiver rec && !_hitReceivers.Contains(rec))
                    {
                        _hitReceivers.Add(rec);
                        float strength = 1f - 0.5f * Mathf.Clamp01(r / MaxRadius);
                        try { rec.OnPulse(Origin, strength); }
                        catch (System.Exception e) { Debug.LogException(e); }
                    }
                }
            }
        }

        void OnDestroy()
        {
            if (_quadMat != null) Destroy(_quadMat);
            if (_ring != null) Destroy(_ring.sharedMaterial);
            if (_ring2 != null) Destroy(_ring2.sharedMaterial);
        }
    }
}
