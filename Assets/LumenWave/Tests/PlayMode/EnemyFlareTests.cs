using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    public class EnemyFlareTests
    {
        class DummyBody : MonoBehaviour, IPlayerBody
        {
            public void Teleport(Vector3 p) { transform.position = p; }
            public Vector2 Velocity { get; set; }
            public int Facing => 1;
            public bool Grounded => true;
            public float GravityScale { get; set; } = 1f;
            public ICommandSource Commands { get; set; }
        }

        GameObject _player, _flare;

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
            if (_flare != null) Object.Destroy(_flare);
            foreach (var b in Object.FindObjectsByType<FlareBolt>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            if (_player != null) Object.Destroy(_player);
        }

        Flare MakeFlare(Vector3 pos)
        {
            _flare = Flare.Create(pos);
            var f = _flare.GetComponent<Flare>();
            f.enabled = false; // fire manually for determinism
            return f;
        }

        [UnityTest]
        public IEnumerator Bolt_DamagesPlayer_WithoutShield()
        {
            var p = MakePlayer(new Vector3(4, 0, 0));
            var hp = p.GetComponent<PlayerHealth>();
            var f = MakeFlare(new Vector3(0, 0.9f, 0));
            f.boltSpeed = 8f;
            f.Fire(Vector2.right);
            yield return new WaitForSeconds(1.0f);
            Assert.Less(hp.current, hp.max, "bolt should burn the player");
            Assert.AreEqual(0, Object.FindObjectsByType<FlareBolt>(FindObjectsSortMode.None).Length, "bolt is consumed on hit");
        }

        [UnityTest]
        public IEnumerator Bolt_AbsorbedByShield_NoDamage()
        {
            var p = MakePlayer(new Vector3(4, 0, 0));
            var hp = p.GetComponent<PlayerHealth>();
            ShieldState.Active = true;
            ShieldState.Center = new Vector3(4, 0.9f, 0);
            ShieldState.Radius = 1.6f;
            int absorbed = 0;
            System.Action<Vector3> h = _ => absorbed++;
            FlareBolt.Absorbed += h;
            var f = MakeFlare(new Vector3(0, 0.9f, 0));
            f.boltSpeed = 8f;
            f.Fire(Vector2.right);
            yield return new WaitForSeconds(1.0f);
            FlareBolt.Absorbed -= h;
            Assert.AreEqual(hp.max, hp.current, 1e-3f, "shield absorbs the bolt");
            Assert.AreEqual(1, absorbed);
        }

        [UnityTest]
        public IEnumerator Bolt_TravelsAtFiniteSpeed_AndStopsAtWalls()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = Layers.Ground;
            wall.transform.position = new Vector3(3, 1, 0);
            var f = MakeFlare(new Vector3(0, 1f, 0));
            f.boltSpeed = 5f;
            var b = f.Fire(Vector2.right);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(b != null, "bolt still in flight after 0.2 s");
            Assert.Less(b.transform.position.x, 2.2f);
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(b == null, "bolt destroyed by the wall");
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator Pulse_StunsFlare_AndDissolvesBolt()
        {
            var f = MakeFlare(new Vector3(0, 1f, 0));
            f.enabled = true;
            var b = f.Fire(Vector2.right);
            ((IPulseReceiver)f).OnPulse(Vector3.zero, 1f);
            ((IPulseReceiver)b).OnPulse(Vector3.zero, 1f);
            yield return null;
            Assert.IsTrue(f.IsStunned);
            Assert.IsTrue(b == null);
        }
    }
}
