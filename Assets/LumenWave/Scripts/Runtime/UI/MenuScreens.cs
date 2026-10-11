using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>Hooks scene boot to build the right UI: title, end, or HUD + pause for levels.</summary>
    public static class UIRoot
    {
        public const string ProgressKey = "lw.unlocked";
        public const string SecretsKey = "lw.secrets";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneBootstrap.Booting -= OnBoot;
            SceneBootstrap.Booting += OnBoot;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.LevelCompleted += OnLevelCompleted;
        }

        static void OnBoot(string kind, int level)
        {
            switch (kind)
            {
                case "title": BuildTitle(); break;
                case "end": BuildEnd(); break;
                case "level": BuildLevelUI(); break;
            }
        }

        static void OnLevelCompleted(int level, float time)
        {
            if (level + 1 > PlayerPrefs.GetInt(ProgressKey, 1))
            {
                PlayerPrefs.SetInt(ProgressKey, level + 1);
                PlayerPrefs.Save();
            }
        }

        public static TitleScreen BuildTitle()
        {
            var c = UIKit.CreateCanvas("TitleUI", 20);
            return c.gameObject.AddComponent<TitleScreen>();
        }

        public static EndScreen BuildEnd()
        {
            var c = UIKit.CreateCanvas("EndUI", 20);
            return c.gameObject.AddComponent<EndScreen>();
        }

        public static Hud BuildLevelUI()
        {
            var hudCanvas = UIKit.CreateCanvas("HUD", 10);
            var hud = hudCanvas.gameObject.AddComponent<Hud>();
            var pauseCanvas = UIKit.CreateCanvas("PauseUI", 50);
            pauseCanvas.gameObject.AddComponent<PauseMenu>();
            return hud;
        }
    }

    /// <summary>Shared backdrop over the live menu scene: soft vignette, a readable dark gradient on the menu side, drifting light motes.</summary>
    public class MenuBackdrop : MonoBehaviour
    {
        RectTransform[] _motes;
        Vector2[] _vel;

        public static MenuBackdrop Build(Transform parent, float darkness = 0.55f, bool leftPanel = false)
        {
            var root = UIKit.Rect(parent, "Backdrop");
            UIKit.Stretch(root);
            var dark = UIKit.Image(root, "Dark", new Color(0.01f, 0.015f, 0.03f, darkness));
            UIKit.Stretch(dark.rectTransform);
            var vig = UIKit.Image(root, "Vignette", new Color(0, 0, 0, 0.85f), UIKit.Radial, false);
            UIKit.Stretch(vig.rectTransform, -40);
            if (leftPanel)
            {
                // Soft gradient panel on the left: the menu stays readable over any backdrop.
                var grad = UIKit.Image(root, "MenuGradient", new Color(0.0f, 0.005f, 0.02f, 0.82f), UIKit.HGradient, false);
                grad.rectTransform.anchorMin = new Vector2(0f, 0f); grad.rectTransform.anchorMax = new Vector2(0.62f, 1f);
                grad.rectTransform.offsetMin = grad.rectTransform.offsetMax = Vector2.zero;
                var floor = UIKit.Image(root, "FloorGradient", new Color(0f, 0f, 0.01f, 0.6f), UIKit.VGradient, false);
                floor.rectTransform.anchorMin = new Vector2(0f, 0f); floor.rectTransform.anchorMax = new Vector2(1f, 0.22f);
                floor.rectTransform.offsetMin = floor.rectTransform.offsetMax = Vector2.zero;
            }
            var b = root.gameObject.AddComponent<MenuBackdrop>();
            b.Init(root);
            return b;
        }

        void Init(RectTransform root)
        {
            const int n = 24;
            _motes = new RectTransform[n];
            _vel = new Vector2[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float hue = (float)rnd.NextDouble();
                var col = hue < 0.65f ? new Color(0.5f, 0.95f, 1f, 0.3f) : hue < 0.88f ? new Color(1f, 0.85f, 0.4f, 0.26f) : new Color(1f, 0.45f, 0.35f, 0.2f);
                var img = UIKit.Image(root, "Mote", col, UIKit.Glow, false);
                float s = 6f + (float)rnd.NextDouble() * 22f;
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                img.rectTransform.sizeDelta = new Vector2(s, s);
                img.rectTransform.anchoredPosition = new Vector2((float)rnd.NextDouble() * 1920f, (float)rnd.NextDouble() * 1080f);
                _motes[i] = img.rectTransform;
                _vel[i] = new Vector2(((float)rnd.NextDouble() - 0.5f) * 20f, 8f + (float)rnd.NextDouble() * 22f);
            }
        }

        void Update()
        {
            if (_motes == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _motes.Length; i++)
            {
                var p = _motes[i].anchoredPosition + _vel[i] * dt;
                p.x += Mathf.Sin(Time.unscaledTime * 0.7f + i) * 6f * dt;
                if (p.y > 1120f) { p.y = -30f; }
                if (p.x < -30f) p.x = 1950f; else if (p.x > 1950f) p.x = -30f;
                _motes[i].anchoredPosition = p;
            }
        }
    }

    /// <summary>A row of key-cap boxes with action names (title footer).</summary>
    public static class KeyHintRow
    {
        public static RectTransform Build(Transform parent, string name, (string keys, string action)[] hints, int keySize = 18)
        {
            var root = UIKit.Rect(parent, name);
            float x = 0f;
            foreach (var h in hints)
            {
                var parts = h.keys.Split('|');
                for (int p = 0; p < parts.Length; p++)
                {
                    string k = parts[p].Trim();
                    if (k.Length == 0) continue;
                    var cap = UIKit.KeyCap(root, k, keySize, out _, "Key");
                    cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f);
                    cap.pivot = new Vector2(0f, 0.5f);
                    cap.anchoredPosition = new Vector2(x, 0f);
                    x += cap.sizeDelta.x + 5f;
                }
                var lbl = UIKit.Label(root, UIKit.Spaced(h.action.ToUpperInvariant()), keySize, TextAnchor.MiddleLeft, UIStyle.TextDim, "Action");
                lbl.font = UIKit.Font;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                float w = lbl.preferredWidth;
                UIKit.Place(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 2f, 0f), new Vector2(w + 4f, 30f), new Vector2(0f, 0.5f));
                x += w + 30f;
            }
            root.sizeDelta = new Vector2(x, 34f);
            return root;
        }
    }

    public class TitleScreen : MonoBehaviour
    {
        public static TitleScreen Instance { get; private set; }
        public MenuStack Stack { get; private set; }
        public Button PlayButton { get; private set; }
        TMP_Text _logo;
        UIGradient _logoGradient;
        Image _logoHalo;
        UIWaveLine _wave, _wave2;
        GameObject _logoGroup;
        GameObject _main;
        float _shimmerT;

        void Awake()
        {
            Instance = this;
            LumenInput.Ensure();
            LumenInput.SetGameplayEnabled(false);
            Time.timeScale = 1f;
            GameSession.Paused = false;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            LumenInput.SetGameplayEnabled(true);
        }

        void Build()
        {
            var root = (RectTransform)transform;
            MenuBackdrop.Build(root, 0.12f, true);
            Stack = gameObject.AddComponent<MenuStack>();

            // ---- Logo: big letter-spaced OSerif with a vertical gradient, glow, a light sweep and a live waveform through it.
            const float logoX = 150f, logoY = -260f;
            var logoGroup = UIKit.Rect(root, "LogoGroup");
            UIKit.Stretch(logoGroup);
            _logoGroup = logoGroup.gameObject;

            _logoHalo = UIKit.Image(logoGroup, "LogoHalo", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.18f), UIKit.Glow, false);
            _logo = UIKit.Heading(logoGroup, "LUMENWAVE", 112, TextAnchor.MiddleLeft, Color.white, "Logo", 1);
            UIKit.Place(_logo.rectTransform, new Vector2(0f, 1f), new Vector2(logoX, logoY), new Vector2(1500, 170), new Vector2(0f, 0.5f));
            float logoW = Mathf.Min(1600f, _logo.preferredWidth);
            UIKit.Place(_logoHalo.rectTransform, new Vector2(0f, 1f), new Vector2(logoX + logoW * 0.5f, logoY), new Vector2(logoW + 520, 420), new Vector2(0.5f, 0.5f));

            // Waveform behind the letters (drawn first = behind) and a thin bright one in front.
            _wave = UIKit.Rect(logoGroup, "LogoWave").gameObject.AddComponent<UIWaveLine>();
            _wave.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.55f);
            _wave.thickness = 6f; _wave.glowWidth = 10f; _wave.amplitude = 0.75f; _wave.speed = 1.2f; _wave.envelopeWidth = 2.2f;
            _wave.raycastTarget = false;
            UIKit.Place(_wave.rectTransform, new Vector2(0f, 1f), new Vector2(logoX - 90f, logoY - 6f), new Vector2(logoW + 180f, 150f), new Vector2(0f, 0.5f));
            _wave.transform.SetSiblingIndex(_logo.transform.GetSiblingIndex());

            UIKit.GlowText(_logo, UIStyle.Accent, 2f);
            _logoGradient = _logo.gameObject.AddComponent<UIGradient>();
            _logoGradient.top = Color.white;
            _logoGradient.bottom = new Color(0.5f, 0.9f, 1f);

            _wave2 = UIKit.Rect(logoGroup, "LogoWaveFront").gameObject.AddComponent<UIWaveLine>();
            _wave2.color = new Color(0.85f, 1f, 1f, 0.6f);
            _wave2.thickness = 2.5f; _wave2.glowWidth = 3f; _wave2.amplitude = 0.42f; _wave2.speed = 1.2f; _wave2.phaseOffset = 0.0f; _wave2.envelopeWidth = 2.2f;
            _wave2.raycastTarget = false;
            UIKit.Place(_wave2.rectTransform, new Vector2(0f, 1f), new Vector2(logoX - 90f, logoY - 6f), new Vector2(logoW + 180f, 150f), new Vector2(0f, 0.5f));

            var tag = UIKit.Label(logoGroup, UIKit.Spaced("A SONG OF SHADOW AND LIGHT", 1), 26, TextAnchor.MiddleLeft, new Color(0.75f, 0.85f, 0.95f, 0.95f), "Tagline");
            tag.font = UIKit.Light;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(logoX + 8f, logoY - 104f), new Vector2(1200, 40), new Vector2(0f, 0.5f));
            var line = UIKit.Image(logoGroup, "TitleLine", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.7f), UIKit.HGradient, false);
            UIKit.Place(line.rectTransform, new Vector2(0f, 1f), new Vector2(logoX + 8f, logoY - 78f), new Vector2(560, 2), new Vector2(0f, 0.5f));

            // ---- Main menu (left column, left-aligned labels).
            var main = UIKit.Rect(root, "MainMenu");
            UIKit.Place(main, new Vector2(0f, 1f), new Vector2(logoX - 6f, -470f), new Vector2(470, 470), new Vector2(0f, 1f));
            UIKit.VList(main, 10, null, TextAnchor.UpperLeft);
            _main = main.gameObject;

            GameObject settingsFirst, creditsFirst, levelsFirst;
            var settings = UIPanels.BuildSettings(root, Stack, out settingsFirst);
            var credits = UIPanels.BuildCredits(root, Stack, out creditsFirst);
            var levels = BuildLevelSelect(root, out levelsFirst);

            PlayButton = UIKit.Button(main, "Play", Play, 70, 34, "Play");
            UIKit.Button(main, "Select level", () => Stack.Push(levels, levelsFirst), 60, 26);
            UIKit.Button(main, "Lightkeeper", () => ScreenFader.FadeThen(LightkeeperMode.Start), 60, 26);   // Lightkeeper mode (Lightkeeper/LightkeeperMode.cs)
            UIKit.Button(main, "Settings", () => Stack.Push(settings, settingsFirst), 60, 26);
            UIKit.Button(main, "Credits", () => Stack.Push(credits, creditsFirst), 60, 26);
            UIKit.Button(main, "Quit", Quit, 60, 26);
            foreach (var t in main.GetComponentsInChildren<TMP_Text>(true))
            {
                if (t.transform.parent.parent != main) continue;
                t.alignment = TextAlignmentOptions.Left;
                t.rectTransform.offsetMin = new Vector2(30, 0);
            }
            // The Play button gets a brighter resting state.
            var playBg = PlayButton.GetComponent<Image>();
            var pc = PlayButton.colors; pc.normalColor = new Color(0.03f, 0.1f, 0.14f, 0.75f); PlayButton.colors = pc;
            if (playBg != null) playBg.color = Color.white;

            LumenInput.Ensure();
            var foot = KeyHintRow.Build(root, "Footer", new (string, string)[]
            {
                ("A|D", "Move"), (LumenInput.Prompt(LumenInput.Jump), "Jump"), (LumenInput.Prompt(LumenInput.Dash), "Dash"),
                (LumenInput.Prompt(LumenInput.Echo), "Echo"), (LumenInput.Prompt(LumenInput.Shield), "Shield"),
                (LumenInput.Prompt(LumenInput.Pulse), "Pulse"), ("Esc", "Pause"),
            }, 20);
            UIKit.Place(foot, new Vector2(0f, 0f), new Vector2(logoX, 52), foot.sizeDelta, new Vector2(0f, 0.5f));
            var pad = UIKit.Label(root, UIKit.Spaced("GAMEPAD SUPPORTED"), 16, TextAnchor.MiddleLeft, new Color(0.6f, 0.68f, 0.8f, 0.7f), "Gamepad");
            pad.font = UIKit.Narrow;
            pad.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(pad.rectTransform, new Vector2(0f, 0f), new Vector2(logoX, 92), new Vector2(600, 24), new Vector2(0f, 0.5f));

            Stack.Push(_main, PlayButton.gameObject);
        }

        GameObject BuildLevelSelect(RectTransform root, out GameObject first)
        {
            var p = UIKit.Panel(root, "LevelSelect", new Vector2(940, 880));
            UIKit.PanelTitle(p, "Select level");
            var list = UIKit.Rect(p, "List");
            list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one;
            list.offsetMin = new Vector2(60, 40); list.offsetMax = new Vector2(-60, -120);
            UIKit.VList(list, 8);
            first = null;
            int unlocked = Mathf.Max(1, PlayerPrefs.GetInt(UIRoot.ProgressKey, 1));
            int count = 0;
            try { count = LevelDatabase.Count; } catch { count = 0; }
            for (int i = 1; i <= count; i++)
            {
                string name;
                try { name = LevelDatabase.Load(i).Name; } catch { name = LevelDatabase.NameOf(i); }
                int n = i;
                bool open = i <= unlocked;
                // Records (best time, secrets found) appended unspaced so the line stays compact.
                string details = "";
                float best = RunStats.BestLevel(i);
                int total = RunStats.SecretsIn(i);
                if (best > 0f) details += $"     <color=#8CF2FF>{UIKit.FormatTime(best)}</color>";
                if (total > 0 && (best > 0f || RunStats.SecretsFound(i) > 0)) details += $"     <color=#FFD970>{RunStats.SecretsFound(i)}/{total} shards</color>";
                var b = UIKit.Button(list, $"{i:00}  {name}", () => { GameSession.NewRun(); GameSession.CurrentLevel = n; ScreenFader.FadeThen(() => SceneFlow.LoadLevel(n)); }, 50, 22);
                var lt = b.GetComponentInChildren<TMP_Text>();
                lt.alignment = TextAlignmentOptions.Left;
                lt.rectTransform.offsetMin = new Vector2(30, 0);
                if (open && details.Length > 0) lt.text += details;
                b.interactable = open;
                if (!open) lt.text = UIKit.Spaced($"{i:00}  ????");
                if (first == null) first = b.gameObject;
            }
            var back = UIKit.Button(list, "Back", () => Stack.Pop(), 50, 22);
            if (first == null) first = back.gameObject;
            p.gameObject.SetActive(false);
            return p.gameObject;
        }

        void Play()
        {
            GameSession.NewRun();
            ScreenFader.FadeThen(() => SceneFlow.LoadLevel(1), 0.5f);
        }

        void Quit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void Update()
        {
            float t = Time.unscaledTime;
            // Sub-panels (settings, credits, level select) are centred over the logo: hide the logo while open.
            bool showLogo = Stack == null || Stack.Depth <= 1;
            if (_logoGroup != null && _logoGroup.activeSelf != showLogo) _logoGroup.SetActive(showLogo);
            if (!showLogo) return;
            float beat = BeatClock.Instance != null ? 1f - BeatClock.Instance.Phase : 0.5f + 0.5f * Mathf.Sin(t * 2.1f);
            if (_logoHalo != null)
            {
                float a = 0.14f + 0.08f * Mathf.Sin(t * 2.1f) + 0.05f * beat * beat;
                _logoHalo.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, a);
                float s = 1f + 0.03f * Mathf.Sin(t * 2.1f);
                _logoHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            // The waveform breathes with the beat.
            if (_wave != null) _wave.energy = 0.65f + 0.35f * beat * beat;
            if (_wave2 != null) _wave2.energy = 0.6f + 0.4f * beat * beat;
            // A light sweep crosses the logo every few seconds.
            if (_logoGradient != null)
            {
                _shimmerT += Time.unscaledDeltaTime;
                const float period = 5.5f;
                if (_shimmerT > period) _shimmerT -= period;
                float k = _shimmerT / 1.4f;
                _logoGradient.shimmer = k < 1f ? 0.85f * Mathf.Sin(k * Mathf.PI) : 0f;
                _logoGradient.shimmerPos = Mathf.Lerp(-0.15f, 1.15f, Mathf.Clamp01(k));
                if (k < 1.05f) _logoGradient.Refresh();
            }
        }
    }

    public class EndScreen : MonoBehaviour
    {
        public static EndScreen Instance { get; private set; }
        MenuStack _stack;
        TMP_Text _line, _title;
        CanvasGroup _statsGroup;
        float _t;

        void Awake()
        {
            Instance = this;
            LumenInput.Ensure();
            LumenInput.SetGameplayEnabled(false);
            Time.timeScale = 1f;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            LumenInput.SetGameplayEnabled(true);
        }

        static RectTransform StatCard(Transform parent, string label, string value, string note, Color valueColor, float x)
        {
            var card = UIKit.Image(parent, "Card" + label, new Color(0.015f, 0.025f, 0.05f, 0.82f), UIKit.Soft).rectTransform;
            UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(330, 170), new Vector2(0.5f, 0.5f));
            var top = UIKit.Image(card, "Line", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.7f), UIKit.HGradientCentered);
            UIKit.Place(top.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -1), new Vector2(280, 2), new Vector2(0.5f, 1f));
            var l = UIKit.Label(card, UIKit.Spaced(label), 20, TextAnchor.MiddleCenter, UIStyle.TextDim, "Label");
            l.font = UIKit.Narrow;
            UIKit.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -34), new Vector2(320, 30), new Vector2(0.5f, 0.5f));
            var v = UIKit.Heading(card, value, 50, TextAnchor.MiddleCenter, valueColor, "Value", 0);
            v.font = UIKit.Font;
            UIKit.Place(v.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(320, 64), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(v, UIStyle.Accent, 1.2f);
            if (!string.IsNullOrEmpty(note))
            {
                var n = UIKit.Label(card, note, 18, TextAnchor.MiddleCenter, UIStyle.TextDim, "Note");
                n.font = UIKit.Narrow;
                n.richText = true;
                UIKit.Place(n.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(320, 26), new Vector2(0.5f, 0.5f));
            }
            return card;
        }

        void Build()
        {
            var root = (RectTransform)transform;
            MenuBackdrop.Build(root, 0.3f);
            _stack = gameObject.AddComponent<MenuStack>();

            var main = UIKit.Rect(root, "EndMain");
            UIKit.Stretch(main);

            var halo = UIKit.Image(main, "Halo", new Color(1f, 0.85f, 0.55f, 0.12f), UIKit.Glow, false);
            UIKit.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 300), new Vector2(1500, 360), new Vector2(0.5f, 0.5f));
            _title = UIKit.Heading(main, "HARMONY RESTORED", 76, TextAnchor.MiddleCenter, Color.white, "Title", 1);
            UIKit.Place(_title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 318), new Vector2(1800, 110), new Vector2(0.5f, 0.5f));
            var g = _title.gameObject.AddComponent<UIGradient>();
            g.top = new Color(1f, 0.97f, 0.9f); g.bottom = new Color(1f, 0.8f, 0.5f);
            UIKit.GlowText(_title, new Color(1f, 0.8f, 0.5f), 2f);
            var wave = UIKit.Rect(main, "Wave").gameObject.AddComponent<UIWaveLine>();
            wave.color = new Color(1f, 0.88f, 0.6f, 0.8f);
            wave.thickness = 3f; wave.glowWidth = 4f; wave.amplitude = 0.6f; wave.raycastTarget = false;
            UIKit.Place(wave.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 252), new Vector2(900, 40), new Vector2(0.5f, 0.5f));

            _line = UIKit.Label(main, "Umbra sang, and every light remembered its harmony.", 34, TextAnchor.MiddleCenter, Color.white, "Line");
            _line.font = UIKit.Light;
            _line.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 196), new Vector2(1600, 60), new Vector2(0.5f, 0.5f));
            UIKit.Shadowed(_line);

            int secrets = PlayerPrefs.GetInt(UIRoot.SecretsKey, 0);
            int totalSecrets = RunStats.TotalSecrets;
            string timeNote = "";
            if (RunStats.LastRunWasNewBest) timeNote = "<color=#FFD970>" + UIKit.Spaced("NEW BEST") + "</color>";
            else if (RunStats.BestRun > 0f) timeNote = $"best {UIKit.FormatTime(RunStats.BestRun)}";

            var stats = UIKit.Rect(main, "Stats");
            UIKit.Place(stats, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1100, 180), new Vector2(0.5f, 0.5f));
            _statsGroup = stats.gameObject.AddComponent<CanvasGroup>();
            StatCard(stats, "TIME", UIKit.FormatTime(GameSession.TotalTime), timeNote, Color.white, -360f);
            StatCard(stats, "DEATHS", GameSession.Deaths.ToString(), "", Color.white, 0f);
            StatCard(stats, "SECRETS", $"{secrets} / {totalSecrets}", secrets >= totalSecrets && totalSecrets > 0 ? "<color=#FFD970>" + UIKit.Spaced("ALL FOUND") + "</color>" : "light shards", UIStyle.Gold, 360f);

            var buttons = UIKit.Rect(main, "Buttons");
            UIKit.Place(buttons, new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(440, 140), new Vector2(0.5f, 0.5f));
            UIKit.VList(buttons, 12);
            GameObject creditsFirst;
            var credits = UIPanels.BuildCredits(root, _stack, out creditsFirst);
            var title = UIKit.Button(buttons, "Title", () => ScreenFader.FadeThen(SceneFlow.LoadTitle), 62);
            UIKit.Button(buttons, "Credits", () => _stack.Push(credits, creditsFirst), 62);
            _stack.Push(main.gameObject, title.gameObject);
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_title != null) _title.color = new Color(1, 1, 1, Mathf.Clamp01(_t / 1.5f));
            if (_line != null) _line.color = new Color(1, 1, 1, Mathf.Clamp01((_t - 0.8f) / 2f));
            if (_statsGroup != null) _statsGroup.alpha = Mathf.Clamp01((_t - 1.2f) / 1.2f);
        }
    }
}
