using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Shared helpers for level PlayMode tests.</summary>
    public static class LevelTestUtil
    {
        /// <summary>Destroys everything in the active scene except the test runner.</summary>
        public static void ClearScene()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name.Contains("tests runner") || go.GetComponent("PlaymodeTestsController") != null) continue;
                Object.Destroy(go);
            }
            Time.timeScale = 1f;
            GameSession.BotMode = false;
            GameSession.Paused = false;
        }
    }

    /// <summary>A scripted bot plays every shipped level start to finish.</summary>
    public class BotPlaythroughTests
    {
        public static int[] Levels => Enumerable.Range(1, Mathf.Max(1, LevelDatabase.Count)).ToArray();

        const float TimeLimit = 240f;   // game seconds per level
        const float TimeScale = 2.5f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LevelTestUtil.ClearScene();
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Bot_CompletesLevel([ValueSource(nameof(Levels))] int level)
        {
            LevelTestUtil.ClearScene();
            yield return null;
            GameSession.BotMode = true;
            int deathsBefore = GameSession.Deaths;
            var rt = SceneBootstrap.BootLevel(level);
            yield return null;
            if (rt.Player == null) Assert.Inconclusive("Level has no player yet (player worker not landed)");
            var body = rt.Player.GetComponent<IPlayerBody>();
            if (body == null) Assert.Inconclusive("Player has no IPlayerBody yet");
            var health = rt.Player.GetComponent<PlayerHealth>();
            if (health != null) health.godMode = false;
            var bot = BotCommandSource.ForCurrentLevel();
            body.Commands = bot;

            var deathLog = new System.Text.StringBuilder();
            System.Action onDied = () =>
            {
                var pp = rt.Player.transform.position;
                deathLog.Append($"({pp.x:F1},{pp.y:F1}) ");
            };
            GameEvents.PlayerDied += onDied;
            Time.timeScale = TimeScale;
            float t = 0f;
            float maxX = rt.Player.transform.position.x;
            while (!rt.Completed && t < TimeLimit && GameSession.Deaths - deathsBefore < 12)
            {
                t += Time.deltaTime;
                maxX = Mathf.Max(maxX, rt.Player.transform.position.x);
                yield return null;
            }
            Time.timeScale = 1f;
            GameEvents.PlayerDied -= onDied;
            int deaths = GameSession.Deaths - deathsBefore;
            var p = rt.Player.transform.position;
            Debug.Log($"[LumenWave] BOT level {level} '{rt.Data.Name}': completed={rt.Completed} time={t:F1}s deaths={deaths} pos=({p.x:F1},{p.y:F1}) maxX={maxX:F1}/{rt.Data.Width} actions={bot.ActionsFired} last={bot.LastAction} deathsAt={deathLog}");
            bot.Dispose();
            Assert.IsTrue(rt.Completed, $"Bot did not finish level {level} '{rt.Data.Name}' in {TimeLimit}s (deaths {deaths}, reached x={maxX:F1} of {rt.Data.Width}, at ({p.x:F1},{p.y:F1}), last action {bot.LastAction}, deaths at {deathLog})");
        }
    }
}
