using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Registry of all active light sources and the physics of how much light reaches a point.
    /// Beams: inverse-square over the full optical path (through mirrors/prisms) × Beer–Lambert fog.
    /// Lamps: radial inverse-square with an occlusion raycast.
    /// </summary>
    public static class LightField
    {
        public static readonly List<LightBeam> Beams = new List<LightBeam>();
        public static readonly List<AreaLamp> Lamps = new List<AreaLamp>();

        /// <summary>Fog extinction coefficient σ (per unit) used for Beer–Lambert. Set per level ("fog:" header).</summary>
        public static float FogSigma = 0.04f;
        /// <summary>Distance below which inverse-square is clamped (avoids infinite intensity at the source).</summary>
        public const float MinDistance = 1.5f;
        /// <summary>Distance at which a beam of intensity 1 delivers exposure 1.</summary>
        public const float ReferenceDistance = 6f;
        /// <summary>Lamps of intensity 1 deliver exposure 1 at this distance.</summary>
        public const float LampReferenceDistance = 3f;
        /// <summary>Extra reach added to a beam's half width when testing a body (≈ Umbra's half width).</summary>
        public const float BodyRadius = 0.25f;

        /// <summary>Tests can force the beat state; null = use the BeatClock.</summary>
        public static Func<bool> BeatOverride;
        public static bool BeatHot => BeatOverride != null ? BeatOverride() : BeatClock.Hot;

        /// <summary>Source strength so that a beam of intensity 1 yields exposure 1 at <see cref="ReferenceDistance"/>.</summary>
        public static float BeamSourceStrength(float sigma) =>
            ReferenceDistance * ReferenceDistance * Mathf.Exp(sigma * ReferenceDistance);

        /// <summary>Intensity delivered by a beam at optical path length d.</summary>
        public static float BeamIntensityAt(float beamIntensity, float pathDistance, float sigma)
        {
            return LightMath.Attenuate(beamIntensity * BeamSourceStrength(sigma), pathDistance, sigma, MinDistance);
        }

        public static float LampIntensityAt(float lampIntensity, float distance, float radius, float sigma)
        {
            if (distance > radius) return 0f;
            float i0 = lampIntensity * LampReferenceDistance * LampReferenceDistance * Mathf.Exp(sigma * LampReferenceDistance);
            float i = LightMath.Attenuate(i0, distance, sigma, 1f);
            // Soft edge over the last unit so the zone boundary isn't a hard step.
            return i * Mathf.Clamp01(radius - distance);
        }

        static int _syncFrame = -1;
        /// <summary>Make sure collider positions match transforms before raycasting (once per frame).</summary>
        public static void SyncPhysics()
        {
            if (_syncFrame == Time.frameCount) return;
            _syncFrame = Time.frameCount;
            Physics.SyncTransforms();
        }

        /// <summary>All light reaching point p (body radius = how fat the sampled body is).</summary>
        public static LightExposure Sample(Vector3 p, float bodyRadius = BodyRadius)
        {
            var e = new LightExposure();
            bool hot = BeatHot;
            for (int i = 0; i < Beams.Count; i++)
            {
                var b = Beams[i];
                if (b != null) b.AddExposure(p, bodyRadius, hot, ref e);
            }
            for (int i = 0; i < Lamps.Count; i++)
            {
                var l = Lamps[i];
                if (l != null) l.AddExposure(p, hot, ref e);
            }
            return e;
        }

        /// <summary>Per-channel maximum over several sample points (feet/chest/head).</summary>
        public static LightExposure SampleMax(Vector3[] points, float bodyRadius = BodyRadius)
        {
            var m = new LightExposure();
            foreach (var p in points)
            {
                var e = Sample(p, bodyRadius);
                m.r = Mathf.Max(m.r, e.r);
                m.g = Mathf.Max(m.g, e.g);
                m.b = Mathf.Max(m.b, e.b);
                m.yellowPulse = Mathf.Max(m.yellowPulse, e.yellowPulse);
            }
            return m;
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Distance from p to segment ab (in XY), and the param t of the closest point.</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Beams.Clear(); Lamps.Clear(); FogSigma = 0.04f; BeatOverride = null; _syncFrame = -1;
        }
    }
}
