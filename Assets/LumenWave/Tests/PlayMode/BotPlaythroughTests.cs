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

        /// <summary>Debug aid: per-frame position trace for one level between two x values.</summary>
        public static int TraceLevel = 0;
        public static float TraceFromX = 39f, TraceToX = 53f;

        const float TimeLimit = 240f;   // game seconds per level
        const float TimeScale = 2.5f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LevelTestUtil.ClearScene();
            yield return null;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Bot_CompletesLevel([ValueSource(nameof(Levels))] int level) => Run(level, TimeScale, 12);

        /// <summary>Human-fairness check: real time (beat clock and physics in sync), at most 1 death.</summary>
        [UnityTest, Timeout(900000)]
        public IEnumerator Bot_CompletesLevel_RealTime([ValueSource(nameof(Levels))] int level) => Run(level, 1f, 1);

        IEnumerator Run(int level, float timeScale, int maxDeaths)
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
            Time.timeScale = timeScale;
            float t = 0f;
            int traced = 0;
            float maxX = rt.Player.transform.position.x;
            while (!rt.Completed && t < TimeLimit && GameSession.Deaths - deathsBefore <= maxDeaths)
            {
                t += Time.deltaTime;
                maxX = Mathf.Max(maxX, rt.Player.transform.position.x);
                var tp = rt.Player.transform.position;
                if (TraceLevel == level && tp.x > TraceFromX && tp.x < TraceToX && traced < 400)
                {
                    traced++;
                    Debug.Log($"[LumenWave] TRACE t={t:F2} pos=({tp.x:F2},{tp.y:F2}) v={body.Velocity} grounded={body.Grounded}");
                }
                yield return null;
            }
            Time.timeScale = 1f;
            GameEvents.PlayerDied -= onDied;
            int deaths = GameSession.Deaths - deathsBefore;
            var p = rt.Player.transform.position;
            Debug.Log($"[LumenWave] BOT x{timeScale} level {level} '{rt.Data.Name}': completed={rt.Completed} time={t:F1}s deaths={deaths} pos=({p.x:F1},{p.y:F1}) maxX={maxX:F1}/{rt.Data.Width} actions={bot.ActionsFired} last={bot.LastAction} deathsAt={deathLog}");
            bot.Dispose();
            Assert.LessOrEqual(deaths, maxDeaths, $"Too many deaths at x{timeScale} in level {level}: {deathLog}");
            Assert.IsTrue(rt.Completed, $"[x{timeScale}] Bot did not finish level {level} '{rt.Data.Name}' in {TimeLimit}s (deaths {deaths}, reached x={maxX:F1} of {rt.Data.Width}, at ({p.x:F1},{p.y:F1}), last action {bot.LastAction}, deaths at {deathLog})");
        }
    }
}
