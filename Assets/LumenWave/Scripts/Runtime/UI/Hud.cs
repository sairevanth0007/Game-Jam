using UnityEngine;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>A glowing bar with a smooth fill and a lagging "ghost" trail.</summary>
    public class HudBar
    {
        public RectTransform root;
        public RectTransform fill, ghost;
        public Image fillImg, ghostImg, glowImg, frameImg;
        public Text label;
        public Color color;
        public float displayed = 1f, ghostValue = 1f, target = 1f;
        public float flash;   // 0..1, decays
        public float shake;   // 0..1, decays

        public static HudBar Build(Transform parent, string name, Color color, Vector2 pos, float width)
        {
            var b = new HudBar { color = color };
            b.root = UIKit.Rect(parent, name);
            UIKit.Place(b.root, new Vector2(0f, 1f), pos, new Vector2(width, 18), new Vector2(0f, 1f));
            b.glowImg = UIKit.Image(b.root, "Glow", new Color(color.r, color.g, color.b, 0.15f), UIKit.Glow);
            UIKit.Stretch(b.glowImg.rectTransform, -14);
            b.frameImg = UIKit.Image(b.root, "Frame", new Color(1f, 1f, 1f, 0.08f), UIKit.Soft);
            UIKit.Stretch(b.frameImg.rectTransform);
            var inner = UIKit.Rect(b.root, "Inner");
            UIKit.Stretch(inner, 4);
            b.ghostImg = UIKit.Image(inner, "Ghost", new Color(1f, 1f, 1f, 0.45f));
            b.ghost = b.ghostImg.rectTransform;
            b.ghost.anchorMin = Vector2.zero; b.ghost.anchorMax = Vector2.one; b.ghost.offsetMin = b.ghost.offsetMax = Vector2.zero;
            b.fillImg = UIKit.Image(inner, "Fill", color);
            b.fill = b.fillImg.rectTransform;
            b.fill.anchorMin = Vector2.zero; b.fill.anchorMax = Vector2.one; b.fill.offsetMin = b.fill.offsetMax = Vector2.zero;
            b.label = UIKit.Label(b.root, UIKit.Spaced(name.ToUpperInvariant()), 16, TextAnchor.LowerLeft, new Color(color.r, color.g, color.b, 0.8f));
            UIKit.Place(b.label.rectTransform, new Vector2(0f, 1f), new Vector2(2, 24), new Vector2(width, 22), new Vector2(0f, 1f));
            return b;
        }

        public void Tick(float dt)
        {
            displayed = Mathf.Lerp(displayed, target, 1f - Mathf.Exp(-14f * dt));
            if (ghostValue < displayed) ghostValue = displayed;
            else ghostValue = Mathf.MoveTowards(ghostValue, displayed, dt * 0.35f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(displayed), 1f);
            ghost.anchorMax = new Vector2(Mathf.Clamp01(ghostValue), 1f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 2.5f);
            var c = Color.Lerp(color, Color.white, flash);
            fillImg.color = c;
            float glowA = 0.12f + 0.25f * flash + 0.06f * Mathf.Sin(Time.unscaledTime * 3f);
            glowImg.color = new Color(c.r, c.g, c.b, glowA);
            frameImg.color = Color.Lerp(new Color(1f, 1f, 1f, 0.08f), new Color(UIStyle.Danger.r, UIStyle.Danger.g, UIStyle.Danger.b, 0.5f), shake);
            float s = 1f + 0.06f * shake * Mathf.Sin(Time.unscaledTime * 60f);
            root.localScale = new Vector3(s, 1f + 0.3f * shake * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 40f)), 1f);
        }
    }

    /// <summary>In-level HUD: health + resonance, level name + timer, messages, prompts, banners.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }
        public HudBar Health { get; private set; }
        public HudBar ResonanceBar { get; private set; }
        public Text MessageText { get; private set; }
        public Text LevelText { get; private set; }
        public Text TimerText { get; private set; }
        public Text LegendText { get; private set; }

        CanvasGroup _messageGroup;
        float _messageUntil;
        string _rawMessage;
        Text _banner, _bannerSub;
        CanvasGroup _bannerGroup;
        float _bannerUntil;
        Image _deathVignette;
        Text _deathText;
        float _death;
        Image _damageVignette;
        float _damagePulse;
        float _legendRefresh;

        void Awake()
        {
            Instance = this;
            Build();
        }

        void OnEnable()
        {
            GameEvents.Message += OnMessage;
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PowerFailed += OnPowerFailed;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerRespawned += OnRespawned;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.LevelStarted += OnLevelStarted;
        }

        void OnDisable()
        {
            GameEvents.Message -= OnMessage;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PowerFailed -= OnPowerFailed;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerRespawned -= OnRespawned;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.LevelStarted -= OnLevelStarted;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            var root = (RectTransform)transform;

            _damageVignette = UIKit.Image(root, "DamageVignette", new Color(1f, 0.1f, 0.05f, 0f), UIKit.Radial, false);
            UIKit.Stretch(_damageVignette.rectTransform, -60);

            // Top-left: bars
            Health = HudBar.Build(root, "Health", UIStyle.Health, new Vector2(56, -64), 380);
            ResonanceBar = HudBar.Build(root, "Resonance", UIStyle.Resonance, new Vector2(56, -124), 380);

            // Top-right: level + timer
            LevelText = UIKit.Label(root, "", 26, TextAnchor.UpperRight, UIStyle.Text, "Level");
            UIKit.Place(LevelText.rectTransform, new Vector2(1f, 1f), new Vector2(-56, -44), new Vector2(900, 40), new Vector2(1f, 1f));
            TimerText = UIKit.Label(root, "00:00.00", 22, TextAnchor.UpperRight, UIStyle.Accent, "Timer");
            UIKit.Place(TimerText.rectTransform, new Vector2(1f, 1f), new Vector2(-56, -84), new Vector2(400, 34), new Vector2(1f, 1f));

            // Message banner (upper middle)
            var msg = UIKit.Rect(root, "Message");
            UIKit.Place(msg, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1300, 84), new Vector2(0.5f, 0.5f));
            _messageGroup = msg.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.alpha = 0f;
            var mbg = UIKit.Image(msg, "Bg", new Color(0.02f, 0.03f, 0.06f, 0.6f), UIKit.Soft);
            UIKit.Stretch(mbg.rectTransform);
            var mline = UIKit.Image(msg, "Line", UIStyle.PanelLine);
            mline.rectTransform.anchorMin = new Vector2(0.1f, 0f); mline.rectTransform.anchorMax = new Vector2(0.9f, 0f);
            mline.rectTransform.sizeDelta = new Vector2(0, 2); mline.rectTransform.anchoredPosition = Vector2.zero;
            MessageText = UIKit.Label(msg, "", 30, TextAnchor.MiddleCenter, Color.white, "Text");
            UIKit.Stretch(MessageText.rectTransform, 12);

            // Bottom-left legend
            LegendText = UIKit.Label(root, "", 20, TextAnchor.LowerLeft, UIStyle.TextDim, "Legend");
            UIKit.Place(LegendText.rectTransform, new Vector2(0f, 0f), new Vector2(56, 40), new Vector2(1200, 30), new Vector2(0f, 0f));

            // Centre banner (level complete / checkpoint)
            var banner = UIKit.Rect(root, "Banner");
            UIKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 200), new Vector2(0.5f, 0.5f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _banner = UIKit.Label(banner, "", 64, TextAnchor.MiddleCenter, Color.white, "Title");
            UIKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 90), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(_banner, UIStyle.Accent, 2f);
            _bannerSub = UIKit.Label(banner, "", 28, TextAnchor.MiddleCenter, UIStyle.Accent, "Sub");
            UIKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1400, 40), new Vector2(0.5f, 0.5f));

            // Death overlay
            _deathVignette = UIKit.Image(root, "DeathVignette", new Color(0f, 0f, 0f, 0f), UIKit.Radial, false);
            UIKit.Stretch(_deathVignette.rectTransform, -200);
            _deathText = UIKit.Label(root, UIKit.Spaced("THE LIGHT TOOK YOU", 2), 44, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0), "DeathText");
            UIKit.Place(_deathText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 80), new Vector2(0.5f, 0.5f));

            RefreshLegend();
            RefreshLevelName();
        }

        void RefreshLegend()
        {
            LumenInput.Ensure();
            LegendText.text =
                $"{PromptFormatter.MovePrompt} Move   ·   {LumenInput.Prompt(LumenInput.Jump)} Jump   ·   {LumenInput.Prompt(LumenInput.Dash)} Dash   ·   " +
                $"{LumenInput.Prompt(LumenInput.Echo)} Echo   ·   {LumenInput.Prompt(LumenInput.Shield)} Shield   ·   {LumenInput.Prompt(LumenInput.Pulse)} Pulse";
        }

        void RefreshLevelName()
        {
            var rt = LevelRuntime.Current;
            if (rt != null && rt.Data != null) LevelText.text = UIKit.Spaced($"{rt.LevelNumber:00}  {rt.Data.Name.ToUpperInvariant()}");
        }

        public float HealthTarget => Health.target;
        public float ResonanceTarget => ResonanceBar.target;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var player = LevelRuntime.Current != null ? LevelRuntime.Current.Player : null;
            if (player != null)
            {
                var h = player.GetComponent<PlayerHealth>();
                var r = player.GetComponent<Resonance>();
                if (h != null) Health.target = h.Normalized;
                if (r != null) ResonanceBar.target = r.Normalized;
            }
            Health.Tick(dt);
            ResonanceBar.Tick(dt);
            TimerText.text = UIKit.FormatTime(GameSession.LevelTime);
            if (string.IsNullOrEmpty(LevelText.text)) RefreshLevelName();

            _legendRefresh -= dt;
            if (_legendRefresh <= 0f) { _legendRefresh = 0.5f; RefreshLegend(); if (_rawMessage != null) MessageText.text = PromptFormatter.Format(_rawMessage); }

            // Message fade
            float targetA = Time.unscaledTime < _messageUntil ? 1f : 0f;
            _messageGroup.alpha = Mathf.MoveTowards(_messageGroup.alpha, targetA, dt * 3f);

            // Banner fade
            float bA = Time.unscaledTime < _bannerUntil ? 1f : 0f;
            _bannerGroup.alpha = Mathf.MoveTowards(_bannerGroup.alpha, bA, dt * 2.5f);

            // Damage vignette (also shows low health)
            _damagePulse = Mathf.MoveTowards(_damagePulse, 0f, dt * 2f);
            float low = Health.target < 0.3f ? (0.3f - Health.target) / 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) * 0.5f : 0f;
            _damageVignette.color = new Color(1f, 0.1f, 0.05f, Mathf.Clamp01(_damagePulse * 0.6f + low));

            // Death overlay
            bool dead = player != null && player.GetComponent<PlayerHealth>() is PlayerHealth ph && ph.IsDead;
            _death = Mathf.MoveTowards(_death, dead ? 1f : 0f, dt * (dead ? 2f : 3f));
            _deathVignette.color = new Color(0, 0, 0, _death * 0.95f);
            _deathText.color = new Color(1f, 1f, 1f, _death);
        }

        public void ShowMessage(string raw, float seconds = 5f)
        {
            _rawMessage = raw;
            MessageText.text = PromptFormatter.Format(raw);
            _messageUntil = Time.unscaledTime + seconds;
        }

        public void ShowBanner(string title, string sub, float seconds = 2.5f)
        {
            _banner.text = title;
            _bannerSub.text = sub;
            _bannerUntil = Time.unscaledTime + seconds;
        }

        void OnMessage(string m) => ShowMessage(m, Mathf.Clamp(2.5f + m.Length * 0.06f, 3.5f, 8f));
        void OnDamaged(float amount, LightColor c) { Health.flash = 1f; _damagePulse = Mathf.Clamp01(_damagePulse + amount / 25f); }
        void OnPowerFailed(PowerType p) { ResonanceBar.shake = 1f; }
        void OnLevelCompleted(int level, float time) => ShowBanner(UIKit.Spaced("LEVEL COMPLETE", 2), UIKit.FormatTime(time), 3f);
        void OnDied() { }
        void OnRespawned() { Health.flash = 0.6f; }
        void OnCheckpoint(int i) => ShowBanner("", UIKit.Spaced("CHECKPOINT"), 1.5f);
        void OnLevelStarted(int level) { LevelText.text = ""; RefreshLevelName(); }
    }
}
