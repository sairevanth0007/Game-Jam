using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>
    /// QA tool for the real build: <c>-lw-flowtest &lt;dir&gt;</c> drives the menus through the same code paths a player
    /// uses (button clicks, queued keyboard events): Title → Settings → Controls → Play → Pause → Settings → Controls →
    /// rebind Jump to W → Resume → die → respawn → complete level → next level. Screenshots each step to &lt;dir&gt;.
    /// <c>-lw-flowtest-verify</c> (second launch) checks that the rebinding survived the restart, then resets it.
    /// Every check logs "[LumenWave] FLOW PASS/FAIL ...".
    /// </summary>
    public class FlowTestDirector : MonoBehaviour
    {
        string _dir;
        bool _verify;
        int _fails;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            string dir = null; bool verify = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lw-flowtest" && i + 1 < args.Length) dir = args[i + 1];
                if (args[i] == "-lw-flowtest-verify") verify = true;
            }
            if (dir == null && !verify) return;
            // The flow test injects keyboard events; keep devices enabled even if another window has focus
            // (otherwise the Input System drops the queued events and the run fails for reasons unrelated to the UI).
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var go = new GameObject("FlowTestDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<FlowTestDirector>();
            d._dir = dir ?? Path.Combine(Application.persistentDataPath, "flow");
            d._verify = verify;
        }

        void Check(bool ok, string what)
        {
            if (!ok) _fails++;
            Debug.Log($"[LumenWave] FLOW {(ok ? "PASS" : "FAIL")} {what}");
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return null;
        }

        static IEnumerator WaitReal(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static Button FindButton(string label)
        {
            string want = label.Replace(" ", "").ToLowerInvariant();
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled) continue;
                var t = b.GetComponentInChildren<TMP_Text>();
                if (t != null && UIKit.Plain(t.text).Replace(" ", "").ToLowerInvariant() == want) return b;
            }
            return null;
        }

        bool Click(string label)
        {
            var b = FindButton(label);
            Check(b != null, "button '" + label + "' visible");
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        static IEnumerator Press(Key key)
        {
            var kb = Keyboard.current;
            if (kb == null) yield break;
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null; yield return null;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            yield return WaitReal(1.5f);
            LumenInput.Ensure();
            if (_verify) { yield return Verify(); yield break; }

            Debug.Log("[LumenWave] FLOW start");
            LumenInput.ResetOverrides();
            SceneFlow.LoadTitle();
            yield return WaitReal(1.5f);
            Check(TitleScreen.Instance != null, "title screen built");
            yield return Shot("01_title");

            Click("Settings");
            yield return WaitReal(0.6f);
            yield return Shot("02_title_settings");
            if (Click("Controls")) { yield return WaitReal(0.6f); yield return Shot("03_title_controls"); Click("Back"); yield return WaitReal(0.3f); }
            Click("Back");
            yield return WaitReal(0.4f);
            Click("Credits");
            yield return WaitReal(0.6f);
            yield return Shot("04_title_credits");
            Click("Back");
            yield return WaitReal(0.4f);

            Click("Play");
            float w = 0f;
            while ((LevelRuntime.Current == null || LevelRuntime.Current.Player == null) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
            var rt = LevelRuntime.Current;
            Check(rt != null && rt.LevelNumber == 1, "Play loads level 1");
            if (rt == null) { Finish(); yield break; }
            yield return WaitReal(2f);
            yield return Shot("05_level1");

            // Pause via the real Esc key.
            yield return Press(Key.Escape);
            yield return WaitReal(0.5f);
            var pm = PauseMenu.Instance;
            Check(pm != null && pm.IsPaused && Time.timeScale == 0f, "Esc pauses (timeScale 0)");
            var px = rt.Player.transform.position;
            float timer = GameSession.LevelTime;
            yield return WaitReal(1f);
            Check((rt.Player.transform.position - px).sqrMagnitude < 1e-6f && Mathf.Approximately(timer, GameSession.LevelTime), "pause freezes player + timer");
            yield return Shot("06_paused");

            Click("Settings");
            yield return WaitReal(0.5f);
            yield return Shot("07_pause_settings");
            Click("Controls");
            yield return WaitReal(0.5f);
            // Click the keyboard binding button of the Jump row (same path as a mouse click), then press W.
            Button jumpKey = null;
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.isActiveAndEnabled && b.name == "Key" && b.transform.parent != null && b.transform.parent.name == "Jump") jumpKey = b;
            Check(jumpKey != null, "Jump keyboard binding button");
            if (jumpKey != null)
            {
                jumpKey.onClick.Invoke();
                yield return WaitReal(0.3f);
                yield return Shot("08_rebinding_waiting");
                yield return Press(Key.W);
                yield return WaitReal(0.5f);
            }
            int idx = LumenInput.BindingIndex(LumenInput.Jump, LumenInput.KeyboardGroup);
            string path = idx >= 0 ? LumenInput.Jump.bindings[idx].effectivePath : "";
            Check(path == "<Keyboard>/w", "Jump rebound to W (" + path + ")");
            Check(PlayerPrefs.GetString(LumenInput.PrefsKey, "").Contains("<Keyboard>/w"), "rebinding saved to PlayerPrefs");
            yield return Shot("09_rebound");
            Click("Back");
            yield return WaitReal(0.3f);
            Click("Back");
            yield return WaitReal(0.3f);
            Click("Resume");
            yield return WaitReal(0.5f);
            Check(!pm.IsPaused && Time.timeScale == 1f, "Resume unpauses");

            // Death + respawn at the respawn point.
            var health = rt.Player.GetComponent<PlayerHealth>();
            health.Kill();
            yield return WaitReal(0.4f);
            yield return Shot("10_dead");
            yield return WaitReal(2f);
            Check(!health.IsDead && (rt.Player.transform.position - rt.RespawnPoint).magnitude < 1.5f, "respawn at checkpoint");
            yield return Shot("11_respawned");

            rt.Complete();
            yield return WaitReal(1f);
            yield return Shot("12_level_complete");
            w = 0f;
            while ((LevelRuntime.Current == null || LevelRuntime.Current.LevelNumber != 2 || LevelRuntime.Current.Player == null) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
            Check(LevelRuntime.Current != null && LevelRuntime.Current.LevelNumber == 2, "next level (2) loads after completion");
            yield return WaitReal(1.5f);
            yield return Shot("13_level2");
            Finish();
        }

        IEnumerator Verify()
        {
            int idx = LumenInput.BindingIndex(LumenInput.Jump, LumenInput.KeyboardGroup);
            string path = idx >= 0 ? LumenInput.Jump.bindings[idx].effectivePath : "";
            Check(path == "<Keyboard>/w", "rebinding persisted across restart (" + path + ")");
            LumenInput.ResetOverrides();
            PlayerPrefs.Save();
            Check(LumenInput.Jump.bindings[idx].effectivePath == "<Keyboard>/space", "reset to default Space");
            yield return null;
            Finish();
        }

        void Finish()
        {
            Debug.Log($"[LumenWave] FLOW DONE fails={_fails}");
            Application.Quit(_fails == 0 ? 0 : 1);
        }
    }
}
