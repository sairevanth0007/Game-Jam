using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Scripted command source for tests: set fields, presses auto-clear after one poll per frame.</summary>
    public class SoundTestSource : ICommandSource
    {
        public PlayerCommands next;
        int _frame = -1;
        PlayerCommands _current;

        public PlayerCommands Poll()
        {
            if (Time.frameCount != _frame)
            {
                _frame = Time.frameCount;
                _current = next;
                next.echoPressed = false; next.pulsePressed = false; next.jumpPressed = false; next.dashPressed = false;
            }
            return _current;
        }
    }

    /// <summary>Minimal stand-in for Umbra's controller.</summary>
    public class SoundDummyBody : MonoBehaviour, IPlayerBody
    {
        public int facing = 1;
        public void Teleport(Vector3 p) { transform.position = p; }
        public Vector2 Velocity { get; set; }
        public int Facing => facing;
        public bool Grounded => true;
        public float GravityScale { get; set; } = 1f;
        public ICommandSource Commands { get; set; }
    }

    public class SoundPowersPlayTests
    {
        class Receiver : MonoBehaviour, IPulseReceiver
        {
            public float hitTime = -1f;
            public void OnPulse(Vector3 origin, float strength) { if (hitTime < 0f) hitTime = Time.time; }
        }

        GameObject _player;
        SoundTestSource _src;
        SoundPowers _powers;
        Resonance _res;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("TestUmbra");
            _player.layer = Layers.Player;
            _player.transform.position = new Vector3(0f, 0f, 0f);
            var body = _player.AddComponent<SoundDummyBody>();
            _src = new SoundTestSource();
            body.Commands = _src;
            _res = _player.AddComponent<Resonance>();
            _res.max = 100; _res.current = 100;
            _powers = _player.AddComponent<SoundPowers>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var b in Object.FindObjectsByType<EchoBridge>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            foreach (var p in Object.FindObjectsByType<PulseWave>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
            Object.Destroy(_player);
            ShieldState.Active = false;
        }

        [UnityTest]
        public IEnumerator Echo_CreatesWalkableBridge_InFacingDirection()
        {
            _player.GetComponent<SoundDummyBody>().facing = 1;
            _src.next.echoPressed = true;
            yield return new WaitForSeconds(0.7f);   // 7 u at 18 u/s ≈ 0.39 s

            Assert.Less(_res.current, 100f, "echo should cost resonance");
            // Something on the Bridge layer under a point 3 units ahead.
            bool hit = Physics.Raycast(new Vector3(3f, 2f, 0f), Vector3.down, out var rh, 5f, 1 << Layers.Bridge);
            Assert.IsTrue(hit, "bridge collider ahead");
            Assert.AreEqual(0f, rh.point.y, 0.05f, "bridge top at foot height");
            // Nothing behind Umbra.
            Assert.IsFalse(Physics.Raycast(new Vector3(-3f, 2f, 0f), Vector3.down, 5f, 1 << Layers.Bridge));

            // A falling body lands on it and stays.
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = new Vector3(3f, 1f, 0f);
            box.transform.localScale = Vector3.one * 0.5f;
            box.AddComponent<Rigidbody>();
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(box.transform.position.y, 0f, "box rests on the bridge");
            Object.Destroy(box);
        }

        [UnityTest]
        public IEnumerator Echo_FacingLeft_BuildsLeft()
        {
            _player.GetComponent<SoundDummyBody>().facing = -1;
            _src.next.echoPressed = true;
            yield return new WaitForSeconds(0.7f);
            Assert.IsTrue(Physics.Raycast(new Vector3(-3f, 2f, 0f), Vector3.down, 5f, 1 << Layers.Bridge));
            Assert.IsFalse(Physics.Raycast(new Vector3(3f, 2f, 0f), Vector3.down, 5f, 1 << Layers.Bridge));
        }

        [UnityTest]
        public IEnumerator Echo_BridgeFadesAfterLifetime()
        {
            _src.next.echoPressed = true;
            yield return new WaitForSeconds(EchoBridge.Lifetime + 0.4f);
            Assert.IsFalse(Physics.Raycast(new Vector3(3f, 2f, 0f), Vector3.down, 5f, 1 << Layers.Bridge));
        }

        [UnityTest]
        public IEnumerator Shield_SetsState_BlocksRay_AndDrains()
        {
            _src.next.shieldHeld = true;
            yield return null;
            yield return null;
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(ShieldState.Active);
            Assert.IsTrue(ShieldState.Contains(_player.transform.position + Vector3.up * 0.9f));
            // A beam ray from the side toward Umbra stops at the dome.
            var from = new Vector3(-6f, 0.9f, 0f);
            Assert.IsTrue(Physics.Raycast(from, Vector3.right, out var hit, 10f, Layers.LightBlockers));
            Assert.AreEqual(Layers.Shield, hit.collider.gameObject.layer);
            Assert.Less(hit.distance, 6f - 1f);
            Assert.Less(_res.current, 100f, "shield drains resonance");

            _src.next.shieldHeld = false;
            yield return null;
            yield return null;
            Assert.IsFalse(ShieldState.Active);
        }

        [UnityTest]
        public IEnumerator Pulse_ReachesTargets_AtFiniteSpeed()
        {
            float d = 7f;
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = new Vector3(d, 0.9f, 0f);
            target.transform.localScale = Vector3.one * 0.2f;
            var rec = target.AddComponent<Receiver>();

            _src.next.pulsePressed = true;
            yield return null;
            float start = Time.time;
            yield return new WaitForSeconds(0.2f);
            Assert.Less(rec.hitTime, 0f, "front can't have arrived yet");
            yield return new WaitForSeconds(0.8f);
            Assert.GreaterOrEqual(rec.hitTime, 0f, "front arrived");
            float expected = (d - 0.1f) / PulseWave.Speed;
            Assert.AreEqual(expected, rec.hitTime - start, 0.15f);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator SpawnedUmbra_GetsSoundPowers_AndAudioDirectorExists()
        {
            var data = LevelData.Parse("name: SoundTest\n---\n P      E\n##########\n", "soundtest");
            var rt = SceneBootstrap.BootLevel(data, 1);
            yield return null;
            Assert.IsNotNull(rt.Player, "player spawned");
            Assert.IsNotNull(rt.Player.GetComponent<SoundPowers>(), "sound powers installed");
            Assert.IsNotNull(AudioDirector.Instance, "audio director installed");
            Object.Destroy(rt.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Powers_FailWithoutResonance()
        {
            _res.current = 0f;
            int fails = 0;
            System.Action<PowerType> onFail = p => fails++;
            GameEvents.PowerFailed += onFail;
            _src.next.echoPressed = true;
            _src.next.pulsePressed = true;
            yield return null;
            yield return new WaitForSeconds(0.5f);
            GameEvents.PowerFailed -= onFail;
            Assert.AreEqual(2, fails);
            Assert.IsFalse(Physics.Raycast(new Vector3(3f, 2f, 0f), Vector3.down, 5f, 1 << Layers.Bridge));
            Assert.AreEqual(0, Object.FindObjectsByType<PulseWave>(FindObjectsSortMode.None).Length);
        }
    }
}
