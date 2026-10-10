using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Level props with a dummy player (independent of the real Umbra controller).</summary>
    public class LevelPropsTests
    {
        LevelRuntime _rt;
        GameObject _dummy;

        IEnumerator Boot(string level)
        {
            LevelTestUtil.ClearScene();
            yield return null;
            _rt = SceneBootstrap.BootLevel(LevelData.Parse(level, "props_test"), 99);
            _dummy = new GameObject("DummyPlayer");
            _dummy.AddComponent<PlayerHealth>();
            _dummy.transform.position = new Vector3(1.5f, 1f, 0f);
            _rt.Player = _dummy;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LevelTestUtil.ClearScene();
            yield return null;
        }

        const string Flat = "name: Props\n---\n                    \n                    \n####################\n---\n";

        [UnityTest]
        public IEnumerator Crystal_ShattersOnPulse_AndResets()
        {
            yield return Boot(Flat + "crystal 6.5 1\n");
            var c = Object.FindAnyObjectByType<LightCrystal>();
            Assert.NotNull(c);
            var col = c.GetComponent<Collider>();
            Assert.IsTrue(col.enabled);
            Assert.AreEqual(Layers.Crystal, c.gameObject.layer);
            bool evt = false;
            GameEvents.CrystalShattered += _ => evt = true;
            ((IPulseReceiver)c).OnPulse(Vector3.zero, 1f);
            Assert.IsTrue(c.Shattered);
            Assert.IsFalse(col.enabled);
            Assert.IsTrue(evt);
            c.ResetState();
            Assert.IsTrue(col.enabled);
        }

        [UnityTest]
        public IEnumerator HiddenPlatform_RevealedByPulse_ThenFades()
        {
            yield return Boot(Flat + "hidden 6.5 1 time=0.3\n");
            var h = Object.FindAnyObjectByType<HiddenPlatform>();
            Assert.NotNull(h);
            var solid = h.transform.Find("Solid").GetComponent<Collider>();
            yield return null;
            Assert.IsFalse(solid.enabled);
            ((IPulseReceiver)h).OnPulse(Vector3.zero, 1f);
            yield return null;
            Assert.IsTrue(solid.enabled);
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(solid.enabled);
        }

        [UnityTest]
        public IEnumerator Checkpoint_SetsRespawnPoint()
        {
            yield return Boot(Flat + "checkpoint 8.5 1\n");
            _dummy.transform.position = new Vector3(8.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(8.5f, _rt.RespawnPoint.x, 0.01f);
            Assert.GreaterOrEqual(_rt.CheckpointIndex, 0);
            Assert.IsTrue(Object.FindAnyObjectByType<CheckpointLantern>().lit);
        }

        [UnityTest]
        public IEnumerator Thorns_Kill()
        {
            yield return Boot(Flat + "thorns 5.5 1\n");
            var h = _dummy.GetComponent<PlayerHealth>();
            Assert.IsFalse(h.IsDead);
            _dummy.transform.position = new Vector3(5.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(h.IsDead);
        }

        [UnityTest]
        public IEnumerator Exit_CompletesLevel()
        {
            yield return Boot(Flat + "exit 12.5 1\n");
            GameSession.BotMode = true; // don't auto-advance scenes
            Assert.IsFalse(_rt.Completed);
            _dummy.transform.position = new Vector3(12.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(_rt.Completed);
        }

        [UnityTest]
        public IEnumerator Exit_LockedUntilFinale()
        {
            yield return Boot(Flat + "exit 12.5 1\nending 6.5 1 time=0.2\n");
            GameSession.BotMode = true;
            _dummy.transform.position = new Vector3(12.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.IsFalse(_rt.Completed, "exit should be locked before the finale");
            bool finale = false;
            GameEvents.FinaleStarted += () => finale = true;
            _dummy.transform.position = new Vector3(6.5f, 1f, 0f);
            yield return null;
            Assert.IsTrue(finale);
            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(_rt.Completed);
        }

        [UnityTest]
        public IEnumerator Orb_CountsSecret_AndTutorialMessages()
        {
            yield return Boot(Flat + "orb 4.5 1\ntutorial 9.5 1 text=Press_{Jump} radius=1\n");
            Assert.AreEqual(1, Secrets.InLevel);
            string msg = null;
            GameEvents.Message += m => msg = m;
            _dummy.transform.position = new Vector3(4.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(1, Secrets.CollectedInLevel);
            _dummy.transform.position = new Vector3(9.5f, 1f, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual("Press {Jump}", msg);
        }

        [UnityTest]
        public IEnumerator AllLevels_Build_WithoutErrors()
        {
            for (int i = 1; i <= LevelDatabase.Count; i++)
            {
                LevelTestUtil.ClearScene();
                yield return null;
                var rt = SceneBootstrap.BootLevel(i);
                Assert.NotNull(rt.Context, "level " + i);
                Assert.Greater(rt.Context.geometry.childCount, 0, "level " + i + " has geometry");
                yield return null;
            }
        }
    }
}
