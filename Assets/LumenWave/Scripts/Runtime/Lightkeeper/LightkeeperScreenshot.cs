using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Verification: <c>LumenWave.exe -lk-screenshot &lt;dir&gt;</c> starts the Lightkeeper arena, aims two red lamps
    /// ahead of Umbra, saves lightkeeper_6s.png (and lightkeeper_12s.png) and quits.
    /// </summary>
    public class LightkeeperScreenshot : MonoBehaviour
    {
        string _dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-lk-screenshot")
                {
                    var go = new GameObject("LightkeeperScreenshot");
                    DontDestroyOnLoad(go);
                    go.AddComponent<LightkeeperScreenshot>()._dir = args[i + 1];
                }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            LightkeeperMode.Start();
            float w = 0f;
            while (LightkeeperController.Instance == null && w < 15f) { w += Time.unscaledDeltaTime; yield return null; }
            var c = LightkeeperController.Instance;
            if (c == null) { Debug.LogError("[LumenWave] Lightkeeper screenshot: arena failed to load"); Application.Quit(2); yield break; }
            yield return null;
            c.humanInput = false;
            // Two lit lamps: one straight down a little ahead of Umbra, one angled; a third (off) parked nearby.
            c.SetLampX(0, 13f); c.SetLampOn(0, true);
            c.SetLampX(1, 19f); c.SetLampAngle(1, -120f); c.SetLampOn(1, true);
            c.SetLampX(2, 25f); c.SetLampColor(2, LightColor.Blue);
            c.Select(1);
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("lightkeeper_6s");
            yield return new WaitForSecondsRealtime(6f);
            yield return Shot("lightkeeper_12s");
            Debug.Log($"[LumenWave] Lightkeeper screenshot done: time left {c.TimeLeft:F1}s deaths {c.BotDeaths} bot={c.Bot?.State}");
            Application.Quit(0);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[LumenWave] Lightkeeper screenshot " + path);
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
