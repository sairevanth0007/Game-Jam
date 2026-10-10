using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using LumenWave.UI;

namespace LumenWave.Tests
{
    public class UIPlayTests
    {
        System.Collections.Generic.HashSet<GameObject> _before;

        [SetUp]
        public void SetUp()
        {
            _before = new System.Collections.Generic.HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            GameSession.Paused = false;
            // Only destroy what this test created (the test runner itself lives in this scene).
            // Immediate, so the next test's EnsureEventSystem doesn't find a dying EventSystem.
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_before.Contains(go)) Object.DestroyImmediate(go);
            LumenInput.SetGameplayEnabled(true);
        }

        [UnityTest]
        public IEnumerator Title_Builds_WithPlaySelected()
        {
            var title = UIRoot.BuildTitle();
            yield return null;
            yield return null;
            Assert.IsNotNull(title.PlayButton);
            Assert.IsNotNull(EventSystem.current, "EventSystem missing");
            Assert.AreEqual(title.PlayButton.gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator Pause_TogglesTimeScale()
        {
            UIRoot.BuildLevelUI();
            yield return null;
            var pause = PauseMenu.Instance;
            Assert.IsNotNull(pause);
            pause.SetPaused(true);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(GameSession.Paused);
            Assert.IsFalse(LumenInput.Jump.enabled, "gameplay input should be disabled while paused");
            yield return null;
            Assert.AreEqual(pause.ResumeButton.gameObject, EventSystem.current.currentSelectedGameObject);
            pause.SetPaused(false);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(GameSession.Paused);
            Assert.IsTrue(LumenInput.Jump.enabled);
        }

        [UnityTest]
        public IEnumerator Hud_TracksPlayerBars()
        {
            var rtGo = new GameObject("LevelRuntimeStub");
            var rt = rtGo.AddComponent<LevelRuntime>();
            var player = new GameObject("Player");
            var h = player.AddComponent<PlayerHealth>();
            var r = player.AddComponent<Resonance>();
            rt.Player = player;
            var hud = UIRoot.BuildLevelUI();
            yield return null;
            Assert.IsNotNull(hud.Health);
            Assert.IsNotNull(hud.ResonanceBar);
            h.current = 50f;
            r.current = 25f;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(0.5f, hud.HealthTarget, 1e-3f);
            Assert.AreEqual(0.25f, hud.ResonanceTarget, 1e-3f);
            Assert.AreEqual(0.5f, hud.Health.displayed, 0.1f);
        }

        [UnityTest]
        public IEnumerator Message_TokensReplaced()
        {
            var hud = UIRoot.BuildLevelUI();
            yield return null;
            GameEvents.RaiseMessage("Press {Jump} to jump, {Move} to move, {Echo} to echo");
            yield return null;
            var text = hud.MessageText.text;
            Assert.IsFalse(text.Contains("{"), text);
            Assert.IsTrue(text.Contains("Space"), text);
            Assert.IsTrue(text.Contains("A/D"), text);
            StringAssert.DoesNotContain("{Pulse}", PromptFormatter.Format("{Pulse}"));
        }
    }
}
