using UnityEngine;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>Pause overlay: Resume, Restart checkpoint, Settings, Quit to title. Toggled by the Pause action.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public bool IsPaused { get; private set; }
        public Button ResumeButton { get; private set; }

        MenuStack _stack;
        GameObject _root, _main;
        CanvasGroup _group;

        void Awake()
        {
            Instance = this;
            LumenInput.Ensure();
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                if (IsPaused) { Time.timeScale = 1f; GameSession.Paused = false; LumenInput.SetGameplayEnabled(true); }
            }
        }

        void Build()
        {
            var canvas = (RectTransform)transform;
            var root = UIKit.Rect(canvas, "PauseRoot");
            UIKit.Stretch(root);
            _root = root.gameObject;
            _group = _root.AddComponent<CanvasGroup>();
            var dim = UIKit.Image(root, "Dim", new Color(0.01f, 0.015f, 0.03f, 0.7f));
            dim.raycastTarget = true;
            UIKit.Stretch(dim.rectTransform);
            var vig = UIKit.Image(root, "Vignette", new Color(0, 0, 0, 0.8f), UIKit.Radial, false);
            UIKit.Stretch(vig.rectTransform, -40);

            _stack = _root.AddComponent<MenuStack>();
            _stack.handleBack = false;
            _stack.onRootBack = () => SetPaused(false);

            var main = UIKit.Panel(root, "PausePanel", new Vector2(560, 520));
            _main = main.gameObject;
            var title = UIKit.Label(main, UIKit.Spaced("PAUSED", 3), 44, TextAnchor.MiddleCenter, Color.white, "Title");
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -64), new Vector2(500, 60), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(title, UIStyle.Accent, 2f);
            var list = UIKit.Rect(main, "List");
            list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one;
            list.offsetMin = new Vector2(60, 50); list.offsetMax = new Vector2(-60, -120);
            UIKit.VList(list, 12);

            GameObject settingsFirst;
            var settings = UIPanels.BuildSettings(root, _stack, out settingsFirst);
            ResumeButton = UIKit.Button(list, "Resume", () => SetPaused(false), 62, 28, "Resume");
            UIKit.Button(list, "Restart checkpoint", RestartCheckpoint, 62, 26);
            UIKit.Button(list, "Settings", () => _stack.Push(settings, settingsFirst), 62, 26);
            UIKit.Button(list, "Quit to title", () => { SetPaused(false); ScreenFader.FadeThen(SceneFlow.LoadTitle); }, 62, 26);

            _root.SetActive(false);
        }

        void RestartCheckpoint()
        {
            SetPaused(false);
            var rt = LevelRuntime.Current;
            if (rt != null) rt.Respawn();
        }

        public void SetPaused(bool paused)
        {
            if (paused == IsPaused) return;
            IsPaused = paused;
            GameSession.Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            LumenInput.SetGameplayEnabled(!paused);
            AudioListener.pause = false;
            if (paused)
            {
                _root.SetActive(true);
                if (_group != null) _group.alpha = 0f;
                _stack.Clear();
                _stack.Push(_main, ResumeButton.gameObject);
            }
            else
            {
                _stack.Clear();
                _root.SetActive(false);
            }
        }

        void Update()
        {
            if (UIRebind.Busy) return;
            LumenInput.Ensure();
            bool pausePressed = LumenInput.Pause != null && LumenInput.Pause.WasPressedThisFrame();
            if (!IsPaused)
            {
                var rt = LevelRuntime.Current;
                // No pausing once the level is done or during the finale cinematic (its HUD runs on unscaled time).
                bool canPause = (rt == null || !rt.Completed) && !FinaleTrigger.Started;
                if (pausePressed && canPause) SetPaused(true);
                return;
            }
            // While paused: Pause / Esc / B goes back one panel (or resumes from the root).
            if (pausePressed || UIKit.BackPressed()) _stack.Pop();
            if (_group != null) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime * 6f);
        }

        void OnDisable() { if (_group != null) _group.alpha = 0f; }
    }
}
