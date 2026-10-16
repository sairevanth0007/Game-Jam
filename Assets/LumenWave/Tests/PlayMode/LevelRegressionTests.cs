using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Regression tests for bugs found in the bug-hunt pass.</summary>
    public class LevelRegressionTests
    {
        const string Flat = "name: Regress\n---\n\n\n P                         E\n##############################\n---\n";
        LevelRuntime _rt;

        [TearDown]
        public void TearDown()
        {
            if (_rt != null) Object.Destroy(_rt.gameObject);
            _rt = null;
            GameSession.BotMode = false;
        }

        [UnityTest]
        public IEnumerator ManualRespawnWhileDead_CancelsPendingAutoRespawn()
        {
            GameSession.BotMode = true;
            _rt = SceneBootstrap.BootLevel(LevelData.Parse(Flat, "regress"), 1);
            yield return null;
            var player = _rt.Player;
            Assert.IsNotNull(player);
            var health = player.GetComponent<PlayerHealth>();
            health.Kill();
            yield return null;
            _rt.Respawn();                       // e.g. pause → Restart checkpoint while dead
            Assert.IsFalse(health.IsDead);
            var body = player.GetComponent<IPlayerBody>();
            var moved = new Vector3(_rt.RespawnPoint.x + 6f, _rt.RespawnPoint.y, 0f);
            body.Teleport(moved);                // the player walks on
            yield return new WaitForSeconds(_rt.respawnDelay + 0.4f);
            Assert.Greater(player.transform.position.x, _rt.RespawnPoint.x + 4f,
                "the stale automatic respawn teleported the player back to the checkpoint");
        }

        [UnityTest]
        public IEnumerator FreshRunFromLevelSelect_ResetsSecretTally()
        {
            Secrets.TotalCollected = 7;
            GameSession.NewRun();
            GameSession.CurrentLevel = 3;
            GameEvents.RaiseLevelStarted(3);     // level select started the run at level 3
            yield return null;
            Assert.AreEqual(0, Secrets.TotalCollected);
        }

        [UnityTest]
        public IEnumerator ShieldHum_StopsWhenPowersAreDisabled()
        {
            var audio = GameSystems.Get<AudioDirector>();
            yield return null;
            var go = new GameObject("HumTest");
            go.layer = Layers.Player;
            go.AddComponent<SoundDummyBody>().Commands = new SoundTestSource();
            go.AddComponent<Resonance>();
            go.AddComponent<SoundPowers>();
            audio.StartSustain("shield", Harmony.ShieldNote, 0.3f);
            Assert.IsTrue(audio.IsSustaining("shield"));
            Object.Destroy(go);                  // scene change / level end while the shield is held
            yield return null;
            Assert.IsFalse(audio.IsSustaining("shield"));
        }
    }
}
