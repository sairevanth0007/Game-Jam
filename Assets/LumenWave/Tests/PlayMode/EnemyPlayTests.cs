using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    public class EnemyPlayTests
    {
        /// <summary>Minimal stand-in for Umbra so enemy tests don't depend on the player worker.</summary>
        class DummyBody : MonoBehaviour, IPlayerBody
        {
            public void Teleport(Vector3 p) { transform.position = p; }
            public Vector2 Velocity { get; set; }
            public int Facing => 1;
            public bool Grounded => true;
            public float GravityScale { get; set; } = 1f;
            public ICommandSource Commands { get; set; }
        }

        GameObject _player;

        GameObject MakePlayer(Vector3 feet)
        {
            var go = new GameObject("DummyPlayer");
            go.layer = Layers.Player;
            go.transform.position = feet;
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0, 0.9f, 0);
            cap.height = 1.8f;
            cap.radius = 0.35f;
            go.AddComponent<PlayerHealth>();
            go.AddComponent<Resonance>();
            go.AddComponent<DummyBody>();
            EnemyUtil.PlayerOverride = go;
            _player = go;
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            EnemyUtil.PlayerOverride = null;
            ShieldState.Active = false;
            foreach (var c in Object.FindObjectsByType<Sentinel>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            foreach (var c in Object.FindObjectsByType<EchoMoth>(FindObjectsSortMode.None)) Object.Destroy(c.gameObject);
            if (_player != null) Object.Destroy(_player);
        }

        [UnityTest]
        public IEnumerator Pulse_StunsSentinel_BeamOff_ThenRecovers()
        {
            var go = Sentinel.Create(new Vector3(0, 5, 0), LightColor.Red, 10f, 3f);
            var s = go.GetComponent<Sentinel>();
            s.stunDuration = 0.5f;
            yield return null;
            Assert.IsTrue(s.Beam.on, "beam starts on");
            Assert.AreEqual(LightColor.Red, s.Beam.color);
            int stunned = 0;
            System.Action<Vector3> h = p => stunned++;
            GameEvents.EnemyStunned += h;
            ((IPulseReceiver)s).OnPulse(Vector3.zero, 1f);
            yield return null;
            GameEvents.EnemyStunned -= h;
            Assert.IsTrue(s.IsStunned);
            Assert.IsFalse(s.Beam.on, "beam off while stunned");
            Assert.AreEqual(1, stunned);
            yield return new WaitForSeconds(0.9f);
            Assert.IsFalse(s.IsStunned);
            Assert.IsTrue(s.Beam.on, "beam back on after stun");
        }

        [UnityTest]
        public IEnumerator Sentinel_SweepsBeam()
        {
            var go = Sentinel.Create(new Vector3(0, 5, 0), LightColor.Red, 10f, 3f);
            var s = go.GetComponent<Sentinel>();
            s.sweep = 60f; s.speed = 3f;
            float min = 999f, max = -999f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                var d = s.Beam.direction;
                float a = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                min = Mathf.Min(min, a); max = Mathf.Max(max, a);
            }
            Assert.Greater(max - min, 20f, "beam direction should sweep");
            Assert.Less(max, -20f, "beam points downward-ish");
        }

        [UnityTest]
        public IEnumerator Wisp_ChasesPlayer()
        {
            MakePlayer(new Vector3(5f, 0f, 0f));
            var w = Wisp.Create(new Vector3(0f, 0.9f, 0f)).GetComponent<Wisp>();
            float start = Vector2.Distance(w.transform.position, EnemyUtil.Center(_player));
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Wisp.State.Chase, w.Current);
            float now = Vector2.Distance(w.transform.position, EnemyUtil.Center(_player));
            Assert.Less(now, start - 0.5f, "wisp should close distance");
        }

        [UnityTest]
        public IEnumerator Wisp_RepelledByShield_NoDamage()
        {
            var p = MakePlayer(new Vector3(4f, 0f, 0f));
            var hp = p.GetComponent<PlayerHealth>();
            ShieldState.Active = true;
            ShieldState.Center = EnemyUtil.Center(p);
            ShieldState.Radius = 1.6f;
            var w = Wisp.Create(new Vector3(0f, 0.9f, 0f)).GetComponent<Wisp>();
            float minDist = 999f;
            for (float t = 0; t < 2.5f; t += Time.deltaTime)
            {
                yield return null;
                minDist = Mathf.Min(minDist, Vector2.Distance(w.transform.position, ShieldState.Center));
            }
            Assert.GreaterOrEqual(minDist, ShieldState.Radius - 0.01f, "wisp must never enter the shield");
            Assert.AreEqual(hp.max, hp.current, 1e-3f, "no damage through the shield");
        }

        [UnityTest]
        public IEnumerator Wisp_ContactDamagesAndKnocksBack()
        {
            var p = MakePlayer(new Vector3(1.2f, 0f, 0f));
            var hp = p.GetComponent<PlayerHealth>();
            Wisp.Create(new Vector3(0f, 0.9f, 0f));
            float t = 0f;
            while (t < 2f && hp.current >= hp.max) { t += Time.deltaTime; yield return null; }
            Assert.Less(hp.current, hp.max, "wisp contact should damage");
            Assert.AreNotEqual(Vector2.zero, p.GetComponent<IPlayerBody>().Velocity, "knockback applied");
        }

        [UnityTest]
        public IEnumerator Wisp_PulseStuns()
        {
            MakePlayer(new Vector3(5f, 0f, 0f));
            var w = Wisp.Create(new Vector3(0f, 0.9f, 0f)).GetComponent<Wisp>();
            yield return null;
            w.OnPulse(new Vector3(-1f, 0.9f, 0f), 1f);
            yield return null;
            Assert.AreEqual(Wisp.State.Stunned, w.Current);
        }

        [UnityTest]
        public IEnumerator Moth_HuntsOnlyWhenPowerUsed_AndDrainsResonance()
        {
            var p = MakePlayer(new Vector3(3f, 0f, 0f));
            var res = p.GetComponent<Resonance>();
            var m = EchoMoth.Create(new Vector3(0f, 0.9f, 0f)).GetComponent<EchoMoth>();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(EchoMoth.State.Flutter, m.Current, "quiet: moth ignores Umbra");
            GameEvents.RaisePowerUsed(PowerType.Echo);
            Assert.AreEqual(EchoMoth.State.Hunt, m.Current);
            float t = 0f;
            while (t < 3f && res.current >= res.max) { t += Time.deltaTime; yield return null; }
            Assert.Less(res.current, res.max, "moth feeds on resonance");
        }

        [Test]
        public void Factories_Registered()
        {
            Assert.IsTrue(LevelBuilder.Factories.ContainsKey("sentinel"));
            Assert.IsTrue(LevelBuilder.Factories.ContainsKey("wisp"));
            Assert.IsTrue(LevelBuilder.Factories.ContainsKey("moth"));
        }
    }
}
