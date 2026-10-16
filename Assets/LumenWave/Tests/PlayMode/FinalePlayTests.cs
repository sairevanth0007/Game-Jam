using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LumenWave.UI;

namespace LumenWave.Tests
{
    public class FinalePlayTests
    {
        System.Collections.Generic.HashSet<GameObject> _before;

        const string Level =
            "name: FinaleTest\n---\n" +
            "            \n            \n            \n            \n            \n" +
            " P          \n" +
            "############\n" +
            "---\n" +
            "beam 1.5 6.5 color=red dir=-90 range=10 intensity=2\n" +
            "sentinel 8 6 range=8\n";

        [SetUp]
        public void SetUp()
        {
            _before = new System.Collections.Generic.HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_before.Contains(go) && go.name != "LumenWaveSystems") Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator Finale_StopsRedDamage_CalmsSentinel_AndHudGoesCinematic()
        {
            var rt = SceneBootstrap.BootLevel(LevelData.Parse(Level, "finaletest"), 99);
            Assert.IsNotNull(rt.Player, "player entity missing");
            if (Hud.Instance == null) UIRoot.BuildLevelUI();
            var health = rt.Player.GetComponent<PlayerHealth>();

            // The red beam burns Umbra before the finale.
            yield return new WaitForSeconds(0.8f);
            Assert.Less(health.current, health.max, "red beam should burn before the finale");

            GameEvents.RaiseFinale();
            yield return null;
            Assert.IsTrue(FinaleDirector.Active, "finale director should start");

            yield return new WaitForSeconds(3.5f);
            float h1 = health.current;
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(h1, health.current, 1e-4f, "no light damage during the finale");
            Assert.IsFalse(health.IsDead);

            var rec = rt.Player.GetComponent<LightReceiver>();
            Assert.IsTrue(rec == null || !rec.enabled || rec.LastEffect.damagePerSecond <= 0f, "red damage must be 0");

            foreach (var b in Object.FindObjectsByType<LightBeam>(FindObjectsSortMode.None))
                Assert.AreEqual(LightColor.White, b.color, "every beam turns to harmonious white light");
            foreach (var s in Object.FindObjectsByType<Sentinel>(FindObjectsSortMode.None))
                Assert.IsFalse(s.enabled, "sentinels calm down");

            Assert.IsNotNull(Hud.Instance);
            Assert.IsTrue(Hud.Instance.Cinematic, "HUD should be in cinematic mode");
            Assert.Greater(Object.FindObjectsByType<HarmonicRing>(FindObjectsSortMode.None).Length, 0, "harmonic rings on the beat");
            var rig = rt.Player.GetComponentInChildren<UmbraRig>();
            if (rig != null) Assert.Greater(rig.Singing, 0.9f, "Umbra sings");
        }
    }
}
