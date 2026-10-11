using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Real-build verification: <c>LumenWave.exe -lw-autoplay &lt;dir&gt; [-lw-autoplay-level N] [-lw-autoplay-seconds 60]</c>
    /// loads level N, lets the scripted bot play it at normal speed (real beat clock, real rendering, real audio),
    /// saves a screenshot every ~5 s to &lt;dir&gt;/autoplay_L{N}_{t}.png, logs progress and quits when done.
    /// </summary>
    public class AutoplayDirector : MonoBehaviour
    {
        public string outDir;
        public int level = 1;
        public float seconds = 60f;
        public float shotInterval = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = null; int lvl = 1; float secs = 60f;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-lw-autoplay") dir = args[i + 1];
                else if (args[i] == "-lw-autoplay-level") int.TryParse(args[i + 1], out lvl);
                else if (args[i] == "-lw-autoplay-seconds") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            }
            if (dir == null) return;
            var go = new GameObject("AutoplayDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<AutoplayDirector>();
            d.outDir = dir; d.level = Mathf.Max(1, lvl); d.seconds = Mathf.Max(5f, secs);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            Debug.Log($"[LumenWave] Autoplay: level {level} for {seconds:F0}s -> {outDir}");
            GameSession.NewRun();
            GameSession.BotMode = true;
            SceneFlow.LoadLevel(level);

            float wait = 0f;
            while ((LevelRuntime.Current == null || LevelRuntime.Current.Player == null) && wait < 15f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            var rt = LevelRuntime.Current;
            if (rt == null || rt.Player == null) { Debug.LogError("[LumenWave] Autoplay: level failed to load"); Application.Quit(2); yield break; }

            var body = rt.Player.GetComponent<IPlayerBody>();
            var bot = BotCommandSource.ForCurrentLevel();
            if (body != null && bot != null) body.Commands = bot;
            Time.timeScale = 1f;
            int deathsBefore = GameSession.Deaths;

            float t = 0f, nextShot = 0.5f, nextLog = 0f, finaleShot = -1f;
            float maxX = rt.Player.transform.position.x;
            while (t < seconds && !rt.Completed)
            {
                t += Time.unscaledDeltaTime;
                var p = rt.Player.transform.position;
                maxX = Mathf.Max(maxX, p.x);
                if (t >= nextShot) { Shot(t); nextShot += shotInterval; }
                // Finale: denser captures so the ending sequence can be reviewed.
                if (FinaleTrigger.Started) { if (finaleShot < 0f) finaleShot = t + 1f; if (t >= finaleShot) { Shot(t, "finale"); finaleShot += 1.5f; } }
                if (t >= nextLog)
                {
                    Debug.Log($"[LumenWave] Autoplay t={t:F1}s x={p.x:F1} y={p.y:F1} deaths={GameSession.Deaths - deathsBefore} last={bot?.LastAction}");
                    nextLog += 5f;
                }
                yield return null;
            }
            if (rt.Completed)
            {
                // Let the completion banner show, then capture it.
                yield return new WaitForSecondsRealtime(1.0f);
                Shot(t + 1f);
                if (FinaleTrigger.Started)
                {
                    yield return new WaitForSecondsRealtime(1.2f); Shot(t + 2.2f, "finale");
                }
            }
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(0.5f);
            Debug.Log($"[LumenWave] Autoplay DONE level {level} '{rt.Data.Name}': completed={rt.Completed} time={t:F1}s deaths={GameSession.Deaths - deathsBefore} maxX={maxX:F1}/{rt.Data.Width}");
            bot?.Dispose();
            Application.Quit(rt.Completed ? 0 : 1);
        }

        void Shot(float t, string tag = null)
        {
            var path = tag == null
                ? Path.Combine(outDir, $"autoplay_L{level}_{Mathf.RoundToInt(t):000}.png")
                : Path.Combine(outDir, $"autoplay_L{level}_{tag}_{Mathf.RoundToInt(t * 10f):0000}.png");
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
