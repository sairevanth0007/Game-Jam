using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Minimal IPlayerBody so light tests don't depend on the real controller.</summary>
    public class LightTestBody : MonoBehaviour, IPlayerBody
    {
        public void Teleport(Vector3 position) { transform.position = position; }
        public Vector2 Velocity { get; set; }
        public int Facing => 1;
        public bool Grounded => true;
        public float GravityScale { get; set; } = 1f;
        public ICommandSource Commands { get; set; }
    }

    public class LightPlayTests
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            LightField.BeatOverride = null;
            foreach (var g in _spawned) if (g != null) Object.Destroy(g);
            _spawned.Clear();
            foreach (var r in Object.FindObjectsByType<LevelRuntime>(FindObjectsSortMode.None)) Object.Destroy(r.gameObject);
        }

        LevelContext Build(string text)
        {
            var data = LevelData.Parse(text, "lighttest");
            var ctx = LevelBuilder.Build(data, null, 0);
            _spawned.Add(ctx.root.gameObject);
            return ctx;
        }

        GameObject Dummy(Vector3 feet)
        {
            var go = new GameObject("TestUmbra");
            go.layer = Layers.Player;
            go.transform.position = feet;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.9f, 0);
            cap.height = 1.8f;
            cap.radius = 0.3f;
            go.AddComponent<PlayerHealth>();
            go.AddComponent<Resonance>();
            go.AddComponent<LightTestBody>();
            go.AddComponent<LightReceiver>();
            _spawned.Add(go);
            return go;
        }

        static LightBeam FirstBeam(LevelContext ctx) => ctx.byType["beam"][0].GetComponent<LightBeam>();

        [UnityTest]
        public IEnumerator Beam_StopsAtWall()
        {
            var ctx = Build("---\n   #\n   #\n   #\n##########\n---\nbeam 1 2 color=red dir=0 range=12 fixture=0\n");
            yield return null;
            var beam = FirstBeam(ctx);
            beam.Trace();
            Assert.AreEqual(1, beam.Segments.Count);
            Assert.IsTrue(beam.Segments[0].endsOnSurface);
            Assert.AreEqual(3f, beam.Segments[0].end.x, 0.05f);
        }

        [UnityTest]
        public IEnumerator Mirror_ReflectsAngleInEqualsAngleOut()
        {
            var ctx = Build("---\n#\n---\nbeam 1 3 color=white dir=0 range=14 fixture=0\nmirror 6 3 angle=45\n");
            yield return null;
            var beam = FirstBeam(ctx);
            beam.Trace();
            Assert.GreaterOrEqual(beam.Segments.Count, 2);
            var s0 = beam.Segments[0];
            var s1 = beam.Segments[1];
            Assert.IsTrue(s0.endsOnSurface);
            var n = (Vector2)s0.hitNormal;
            var din = s0.Direction;
            var dout = s1.Direction;
            Assert.AreEqual(Vector2.Angle(-din, n), Vector2.Angle(dout, n), 0.5f, "angle in = angle out");
            Assert.AreEqual(1f, dout.y, 0.02f, "45° mirror turns a rightward beam upward");
        }

        [UnityTest]
        public IEnumerator Prism_SplitsWhiteIntoRGB()
        {
            var ctx = Build("---\n#\n---\nbeam 1 3 color=white dir=0 range=16 fixture=0\nprism 6 3 angle=0\n");
            yield return null;
            var beam = FirstBeam(ctx);
            beam.Trace();
            Vector2? r = null, g = null, b = null;
            foreach (var s in beam.Segments)
            {
                if (s.depth != 1) continue;
                if (s.color == LightColor.Red) r = s.Direction;
                if (s.color == LightColor.Green) g = s.Direction;
                if (s.color == LightColor.Blue) b = s.Direction;
            }
            Assert.IsTrue(r.HasValue && g.HasValue && b.HasValue, "three coloured exit beams");
            float ar = Mathf.Atan2(r.Value.y, r.Value.x), ag = Mathf.Atan2(g.Value.y, g.Value.x), ab = Mathf.Atan2(b.Value.y, b.Value.x);
            Assert.Greater(ab, ag, "blue deviates most");
            Assert.Greater(ag, ar, "red deviates least");
        }

        [UnityTest]
        public IEnumerator RedBeam_Damages_NotLethalAtDistance()
        {
            Build("---\n##########\n---\nbeam 2 7 color=red dir=-90 range=10 intensity=0.5 fixture=0\n");
            var p = Dummy(new Vector3(2f, 1f, 0f));
            var h = p.GetComponent<PlayerHealth>();
            yield return new WaitForSeconds(0.5f);
            Assert.Less(h.current, h.max);
            Assert.IsFalse(h.IsDead);
        }

        [UnityTest]
        public IEnumerator Shield_BlocksBeam()
        {
            Build("---\n##########\n---\nbeam 2 7 color=red dir=-90 range=10 intensity=0.5 fixture=0\n");
            // A sphere on the Shield layer around the player stands in for the resonance shield.
            var shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shield.layer = Layers.Shield;
            shield.transform.position = new Vector3(2f, 2f, 0f);
            shield.transform.localScale = Vector3.one * 3f;
            _spawned.Add(shield);
            var p = Dummy(new Vector3(2f, 1f, 0f));
            var h = p.GetComponent<PlayerHealth>();
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(h.max, h.current, 1e-3f);
        }

        [UnityTest]
        public IEnumerator GreenLamp_HealsAndRefills()
        {
            Build("---\n##########\n---\nlamp 2 3.5 color=green radius=5 hanging=0\n");
            var p = Dummy(new Vector3(2f, 1f, 0f));
            var h = p.GetComponent<PlayerHealth>();
            var r = p.GetComponent<Resonance>();
            h.current = 50f;
            r.current = 10f;
            yield return new WaitForSeconds(0.6f);
            Assert.Greater(h.current, 50f);
            Assert.Greater(r.current, 10f);
            Assert.Greater(p.GetComponent<LightReceiver>().LastExposure.g, 0.5f);
        }

        [UnityTest]
        public IEnumerator BlueLamp_LowersGravity()
        {
            Build("---\n##########\n---\nlamp 2 3.5 color=blue radius=5 intensity=2 hanging=0\n");
            var p = Dummy(new Vector3(2f, 1f, 0f));
            yield return null;
            yield return null;
            Assert.Less(p.GetComponent<LightTestBody>().GravityScale, 0.8f);
        }

        [UnityTest]
        public IEnumerator RedPlusGreen_OnlyHurtsOnBeat()
        {
            Build("---\n##########\n---\nlamp 2 3.5 color=red radius=5 hanging=0\nlamp 2 3.5 color=green radius=5 hanging=0\n");
            var p = Dummy(new Vector3(2f, 1f, 0f));
            var h = p.GetComponent<PlayerHealth>();
            LightField.BeatOverride = () => false;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(h.max, h.current, 1e-3f, "overlap is yellow: safe off-beat");
            LightField.BeatOverride = () => true;
            yield return new WaitForSeconds(0.3f);
            Assert.Less(h.current, h.max, "yellow burns on the beat");
        }

        [UnityTest]
        public IEnumerator InverseSquare_FartherIsWeaker()
        {
            var ctx = Build("---\n#\n---\nbeam 0 10 color=red dir=-90 range=20 fixture=0\n");
            yield return null;
            FirstBeam(ctx).Trace();
            float near = LightField.Sample(new Vector3(0f, 7f, 0f)).r;   // path 3
            float far = LightField.Sample(new Vector3(0f, 4f, 0f)).r;    // path 6
            Assert.Greater(near, 0f);
            Assert.AreEqual(4f * Mathf.Exp(LightField.FogSigma * 3f), near / far, 0.05f);
        }

        [UnityTest]
        public IEnumerator BeatGatedBeam_OnlyLitWhenHot()
        {
            var ctx = Build("---\n#\n---\nbeam 0 6 color=yellow dir=-90 range=10 fixture=0\n");
            LightField.BeatOverride = () => false;
            yield return null;
            var beam = FirstBeam(ctx);
            Assert.IsFalse(beam.IsLit);
            Assert.AreEqual(0f, LightField.Sample(new Vector3(0f, 2f, 0f)).yellowPulse, 1e-5f);
            LightField.BeatOverride = () => true;
            yield return null;
            Assert.IsTrue(beam.IsLit);
            Assert.Greater(LightField.Sample(new Vector3(0f, 2f, 0f)).yellowPulse, 0f);
        }
    }
}
