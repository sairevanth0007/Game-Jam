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

    /// <summary>Shared backdrop: dark gradient + vignette + drifting light motes.</summary>
    public class MenuBackdrop : MonoBehaviour
    {
        RectTransform[] _motes;
        Vector2[] _vel;

        public static MenuBackdrop Build(Transform parent, float darkness = 0.55f)
        {
            var root = UIKit.Rect(parent, "Backdrop");
            UIKit.Stretch(root);
            var dark = UIKit.Image(root, "Dark", new Color(0.01f, 0.015f, 0.03f, darkness));
            UIKit.Stretch(dark.rectTransform);
            var vig = UIKit.Image(root, "Vignette", new Color(0, 0, 0, 0.9f), UIKit.Radial, false);
            UIKit.Stretch(vig.rectTransform, -40);
            var b = root.gameObject.AddComponent<MenuBackdrop>();
            b.Init(root);
            return b;
        }

        void Init(RectTransform root)
        {
            const int n = 28;
            _motes = new RectTransform[n];
            _vel = new Vector2[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float hue = (float)rnd.NextDouble();
                var col = hue < 0.6f ? new Color(0.5f, 0.95f, 1f, 0.35f) : hue < 0.8f ? new Color(1f, 0.85f, 0.4f, 0.3f) : new Color(1f, 0.4f, 0.35f, 0.25f);
                var img = UIKit.Image(root, "Mote", col, UIKit.Glow, false);
                float s = 8f + (float)rnd.NextDouble() * 26f;
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

    public class TitleScreen : MonoBehaviour
    {
        public static TitleScreen Instance { get; private set; }
        public MenuStack Stack { get; private set; }
        public Button PlayButton { get; private set; }
        Text _logo;
        Image _logoHalo;
        GameObject _main;

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
            MenuBackdrop.Build(root, 0.45f);
            Stack = gameObject.AddComponent<MenuStack>();

            // Logo
            // Soft halo behind the logo (a blurred glow sprite, not a second copy of the text, which
            // read as a ghosted offset duplicate).
            _logoHalo = UIKit.Image(root, "LogoHalo", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.22f), UIKit.Glow, false);
            UIKit.Place(_logoHalo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1500, 330), new Vector2(0.5f, 0.5f));
            _logoHalo.raycastTarget = false;
            _logo = UIKit.Label(root, UIKit.Spaced("LUMENWAVE", 2), 112, TextAnchor.MiddleCenter, Color.white, "Logo");
            UIKit.Place(_logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(1600, 160), new Vector2(0.5f, 0.5f));
            // Tight symmetric glow: a 1.5 px outline in the accent colour (symmetric, so no offset ghost).
            var glow = _logo.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.28f);
            glow.effectDistance = new Vector2(1.5f, 1.5f);
            var tag = UIKit.Label(root, UIKit.Spaced("a song of shadow and light"), 28, TextAnchor.MiddleCenter, UIStyle.TextDim, "Tagline");
            UIKit.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(1200, 40), new Vector2(0.5f, 0.5f));
            var line = UIKit.Image(root, "TitleLine", UIStyle.PanelLine);
            UIKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(560, 2), new Vector2(0.5f, 0.5f));

            // Main menu
            var main = UIKit.Rect(root, "MainMenu");
            UIKit.Place(main, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(460, 420), new Vector2(0.5f, 0.5f));
            UIKit.VList(main, 12);
            _main = main.gameObject;

            GameObject settingsFirst, creditsFirst, levelsFirst;
            var settings = UIPanels.BuildSettings(root, Stack, out settingsFirst);
            var credits = UIPanels.BuildCredits(root, Stack, out creditsFirst);
            var levels = BuildLevelSelect(root, out levelsFirst);

            PlayButton = UIKit.Button(main, "Play", Play, 66, 32, "Play");
            UIKit.Button(main, "Select level", () => Stack.Push(levels, levelsFirst), 62);
            UIKit.Button(main, "Settings", () => Stack.Push(settings, settingsFirst), 62);
            UIKit.Button(main, "Credits", () => Stack.Push(credits, creditsFirst), 62);
            UIKit.Button(main, "Quit", Quit, 62);

            var foot = UIKit.Label(root, "Move A/D · Jump Space · Dash Shift · Echo J · Shield K · Pulse L · Pause Esc   (gamepad supported)", 20, TextAnchor.MiddleCenter, UIStyle.TextDim, "Footer");
            UIKit.Place(foot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1600, 30), new Vector2(0.5f, 0.5f));

            Stack.Push(_main, PlayButton.gameObject);
        }

        GameObject BuildLevelSelect(RectTransform root, out GameObject first)
        {
            var p = UIKit.Panel(root, "LevelSelect", new Vector2(760, 760));
            var title = UIKit.Label(p, UIKit.Spaced("SELECT LEVEL", 2), 40, TextAnchor.MiddleCenter, UIStyle.Text, "Title");
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(700, 60), new Vector2(0.5f, 0.5f));
            var list = UIKit.Rect(p, "List");
            list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one;
            list.offsetMin = new Vector2(60, 40); list.offsetMax = new Vector2(-60, -110);
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
                var b = UIKit.Button(list, $"{i:00}  {name}", () => { GameSession.NewRun(); GameSession.CurrentLevel = n; SceneFlow.LoadLevel(n); }, 52, 24);
                b.interactable = open;
                if (!open) b.GetComponentInChildren<Text>().text = UIKit.Spaced($"{i:00}  ????");
                if (first == null) first = b.gameObject;
            }
            var back = UIKit.Button(list, "Back", () => Stack.Pop(), 52, 24);
            if (first == null) first = back.gameObject;
            p.gameObject.SetActive(false);
            return p.gameObject;
        }

        void Play()
        {
            GameSession.NewRun();
            SceneFlow.LoadLevel(1);
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
            if (_logoHalo != null)
            {
                float a = 0.16f + 0.08f * Mathf.Sin(t * 2.1f);
                _logoHalo.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, a);
                float s = 1f + 0.03f * Mathf.Sin(t * 2.1f);
                _logoHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            }
            // Beat-synced shimmer if the clock runs.
            if (_logo != null && BeatClock.Instance != null)
            {
                float k = 1f - BeatClock.Instance.Phase;
                _logo.color = Color.Lerp(new Color(0.85f, 0.92f, 1f), Color.white, k * k);
            }
        }
    }

    public class EndScreen : MonoBehaviour
    {
        public static EndScreen Instance { get; private set; }
        MenuStack _stack;
        Text _line;
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

        void Build()
        {
            var root = (RectTransform)transform;
            MenuBackdrop.Build(root, 0.4f);
            _stack = gameObject.AddComponent<MenuStack>();

            var main = UIKit.Rect(root, "EndMain");
            UIKit.Stretch(main);
            _line = UIKit.Label(main, "Umbra sang, and every light remembered its harmony.", 46, TextAnchor.MiddleCenter, Color.white, "Line");
            UIKit.Place(_line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(1500, 140), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(_line, UIStyle.Accent, 2f);

            int secrets = PlayerPrefs.GetInt(UIRoot.SecretsKey, 0);
            string stats = $"{UIKit.Spaced("TIME")}   {UIKit.FormatTime(GameSession.TotalTime)}\n\n" +
                           $"{UIKit.Spaced("DEATHS")}   {GameSession.Deaths}\n\n" +
                           $"{UIKit.Spaced("SECRETS")}   {secrets}";
            var st = UIKit.Label(main, stats, 32, TextAnchor.MiddleCenter, UIStyle.Text, "Stats");
            UIKit.Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 220), new Vector2(0.5f, 0.5f));

            var buttons = UIKit.Rect(main, "Buttons");
            UIKit.Place(buttons, new Vector2(0.5f, 0.5f), new Vector2(0, -210), new Vector2(440, 150), new Vector2(0.5f, 0.5f));
            UIKit.VList(buttons, 12);
            GameObject creditsFirst;
            var credits = UIPanels.BuildCredits(root, _stack, out creditsFirst);
            var title = UIKit.Button(buttons, "Title", SceneFlow.LoadTitle, 62);
            UIKit.Button(buttons, "Credits", () => _stack.Push(credits, creditsFirst), 62);
            _stack.Push(main.gameObject, title.gameObject);
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            if (_line != null) _line.color = new Color(1, 1, 1, Mathf.Clamp01(_t / 2.5f));
        }
    }
}
