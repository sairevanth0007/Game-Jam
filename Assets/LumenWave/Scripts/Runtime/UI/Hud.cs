using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>
    /// A thick, rounded, glowing bar: smooth fill, a lagging "ghost" trail (damage shows as a red trail),
    /// segment dividers, a top shine, a bright leading edge and optional cost ticks. Built from uGUI images.
    /// </summary>
    public class HudBar
    {
        public RectTransform root;
        public RectTransform fill, ghost;
        public Image fillImg, ghostImg, glowImg, frameImg, edgeImg, iconImg;
        public TMP_Text label;
        public Color color, ghostColor;
        public float displayed = 1f, ghostValue = 1f, target = 1f;
        public float flash;   // 0..1, decays
        public float shake;   // 0..1, decays
        float _width, _height;
        Vector2 _basePos;
        readonly List<RectTransform> _ticks = new List<RectTransform>();

        public static HudBar Build(Transform parent, string name, Color color, Vector2 pos, float width, float height = 24f,
                                   Sprite icon = null, int segments = 0, Color? ghostColor = null)
        {
            var b = new HudBar { color = color, _width = width, _height = height, _basePos = pos };
            b.ghostColor = ghostColor ?? new Color(1f, 1f, 1f, 0.45f);
            b.root = UIKit.Rect(parent, name);
            UIKit.Place(b.root, new Vector2(0f, 1f), pos, new Vector2(width, height), new Vector2(0f, 1f));

            // Dark soft backing (keeps the bar readable over bright light) + coloured glow.
            var back = UIKit.Image(b.root, "Backing", new Color(0f, 0f, 0f, 0.4f), UIKit.Glow);
            UIKit.Stretch(back.rectTransform, -22);
            b.glowImg = UIKit.Image(b.root, "Glow", new Color(color.r, color.g, color.b, 0.15f), UIKit.Glow);
            UIKit.Stretch(b.glowImg.rectTransform, -12);
            b.frameImg = UIKit.Image(b.root, "Frame", new Color(1f, 1f, 1f, 0.16f), UIKit.Soft);
            UIKit.Stretch(b.frameImg.rectTransform, -2);
            var trough = UIKit.Image(b.root, "Trough", new Color(0.01f, 0.015f, 0.03f, 0.92f), UIKit.Soft);
            UIKit.Stretch(trough.rectTransform);

            // Inner area is masked to the rounded shape so fills get rounded ends.
            var innerImg = UIKit.Image(b.root, "Inner", Color.white, UIKit.Soft);
            var inner = innerImg.rectTransform;
            UIKit.Stretch(inner, 3);
            innerImg.pixelsPerUnitMultiplier = 24f / Mathf.Max(8f, height - 6f);
            var mask = innerImg.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            b.ghostImg = UIKit.Image(inner, "Ghost", b.ghostColor);
            b.ghost = b.ghostImg.rectTransform;
            b.ghost.anchorMin = Vector2.zero; b.ghost.anchorMax = Vector2.one; b.ghost.offsetMin = b.ghost.offsetMax = Vector2.zero;
            b.fillImg = UIKit.Image(inner, "Fill", color);
            b.fill = b.fillImg.rectTransform;
            b.fill.anchorMin = Vector2.zero; b.fill.anchorMax = Vector2.one; b.fill.offsetMin = b.fill.offsetMax = Vector2.zero;
            // Depth: darker lower half, shine on the upper third.
            var shade = UIKit.Image(b.fill, "Shade", new Color(0f, 0f, 0f, 0.28f), UIKit.VGradient, false);
            shade.rectTransform.anchorMin = new Vector2(0f, 0f); shade.rectTransform.anchorMax = new Vector2(1f, 0.6f);
            shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
            var shine = UIKit.Image(b.fill, "Shine", new Color(1f, 1f, 1f, 0.35f), UIKit.VGradient, false);
            shine.rectTransform.anchorMin = new Vector2(0f, 0.55f); shine.rectTransform.anchorMax = new Vector2(1f, 1f);
            shine.rectTransform.offsetMin = new Vector2(0, 0); shine.rectTransform.offsetMax = new Vector2(0, -1);
            shine.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            // Segment dividers (a "chunky" read of the value).
            for (int i = 1; i < segments; i++)
            {
                var d = UIKit.Image(inner, "Divider", new Color(0.01f, 0.015f, 0.03f, 0.75f));
                d.rectTransform.anchorMin = new Vector2(i / (float)segments, 0f);
                d.rectTransform.anchorMax = new Vector2(i / (float)segments, 1f);
                d.rectTransform.sizeDelta = new Vector2(3f, 0f);
                d.rectTransform.anchoredPosition = Vector2.zero;
            }

            // Bright leading edge at the end of the fill.
            b.edgeImg = UIKit.Image(b.root, "Edge", new Color(1f, 1f, 1f, 0.8f), UIKit.Glow, false);
            b.edgeImg.rectTransform.anchorMin = b.edgeImg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            b.edgeImg.rectTransform.sizeDelta = new Vector2(height * 1.1f, height * 2.0f);

            b.label = UIKit.Label(b.root, UIKit.Spaced(name.ToUpperInvariant()), 20, TextAnchor.LowerLeft, new Color(color.r, color.g, color.b, 0.95f));
            b.label.font = UIKit.Narrow;
            b.label.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(b.label.rectTransform, new Vector2(0f, 1f), new Vector2(2, 28), new Vector2(width, 24), new Vector2(0f, 1f));
            UIKit.Shadowed(b.label);

            if (icon != null)
            {
                float s = height + 22f;
                var halo = UIKit.Image(b.root, "IconHalo", new Color(color.r, color.g, color.b, 0.25f), UIKit.Glow, false);
                UIKit.Place(halo.rectTransform, new Vector2(0f, 0.5f), new Vector2(-s * 0.5f - 12f, 0f), new Vector2(s * 1.8f, s * 1.8f), new Vector2(0.5f, 0.5f));
                b.iconImg = UIKit.Image(b.root, "Icon", color, icon, false);
                UIKit.Place(b.iconImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(-s * 0.5f - 12f, 0f), new Vector2(s, s), new Vector2(0.5f, 0.5f));
            }
            return b;
        }

        /// <summary>A thin bright notch at a fraction of the bar (e.g. what a power costs).</summary>
        public RectTransform AddTick(float frac, Color c)
        {
            var t = UIKit.Image(root, "Tick", c);
            t.rectTransform.anchorMin = new Vector2(frac, 0f); t.rectTransform.anchorMax = new Vector2(frac, 1f);
            t.rectTransform.sizeDelta = new Vector2(2f, 8f);
            t.rectTransform.anchoredPosition = new Vector2(0f, -2f);
            _ticks.Add(t.rectTransform);
            return t.rectTransform;
        }

        public void SetTick(int i, float frac)
        {
            if (i < 0 || i >= _ticks.Count) return;
            _ticks[i].anchorMin = new Vector2(frac, 0f); _ticks[i].anchorMax = new Vector2(frac, 1f);
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
            float glowA = 0.14f + 0.3f * flash + 0.05f * Mathf.Sin(Time.unscaledTime * 3f);
            glowImg.color = new Color(c.r, c.g, c.b, glowA);
            frameImg.color = Color.Lerp(new Color(1f, 1f, 1f, 0.16f), new Color(UIStyle.Danger.r, UIStyle.Danger.g, UIStyle.Danger.b, 0.85f), shake);
            float edgeX = Mathf.Clamp01(displayed);
            edgeImg.rectTransform.anchorMin = edgeImg.rectTransform.anchorMax = new Vector2(edgeX, 0.5f);
            edgeImg.color = new Color(c.r, c.g, c.b, displayed > 0.01f ? 0.55f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f) + 0.4f * flash : 0f);
            if (iconImg != null) iconImg.color = Color.Lerp(color, Color.white, flash);
            // Power failed / damage: a quick horizontal judder.
            float jx = shake > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * 6f * shake : 0f;
            root.anchoredPosition = _basePos + new Vector2(jx, 0f);
        }
    }

    /// <summary>In-level HUD: health + resonance, power chips, level name + timer, key hints, messages, banners.</summary>
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }
        public HudBar Health { get; private set; }
        public HudBar ResonanceBar { get; private set; }
        public TMP_Text MessageText { get; private set; }
        public TMP_Text LevelText { get; private set; }
        public TMP_Text TimerText { get; private set; }
        public TMP_Text ShardsText { get; private set; }
        int _lastShards = -1, _lastShardTotal = -1;
        /// <summary>Plain-text version of the key hints (the visible hints are key-cap boxes).</summary>
        public TMP_Text LegendText { get; private set; }

        CanvasGroup _messageGroup;
        RectTransform _messageRoot, _messageBacking;
        float _messageUntil;
        string _rawMessage;
        TMP_Text _levelNumber;
        TMP_Text _banner, _bannerSub;
        UIWaveLine _bannerWave;
        RectTransform _beatRoot;
        Image _beatRing, _beatDot;
        float _beatScan;
        bool _levelHasBeat;
        CanvasGroup _bannerGroup;
        float _bannerUntil;
        Image _deathVignette;
        TMP_Text _deathText;
        float _death;
        Image _damageVignette, _burnFlash;
        float _damagePulse;
        // Burn feedback (danger readability): smoothed burn level 0..1 from the light receiver's damage per second.
        public float BurnLevel { get; private set; }
        float _burnPhase, _nearLethalCooldown, _hpSampleT, _hpPrev = -1f, _hpPrev2 = -1f;
        float _legendRefresh;
        string _legendKey;
        RectTransform _legendRoot;

        // Power chips under the resonance bar.
        class Chip { public RectTransform root; public Image bg, dot; public TMP_Text text; public float flash, fail; public PowerType power; }
        readonly Chip[] _chips = new Chip[3];
        float _lastTimer = -1f;

        // Finale cinematic mode: gameplay HUD fades away, two lines of ending text, then a fade to white.
        public bool Cinematic { get; private set; }
        public float CinematicTime { get; private set; }
        CanvasGroup _gameplayGroup;
        TMP_Text _endLine1, _endLine2;
        Image _whiteout;
        float _finaleLength = 11f;
        bool _finaleCompleted;
        float _completedAt;

        const float EchoCost = 25f, PulseCost = 20f, ShieldMin = 8f;   // mirrors SoundPowers defaults (display only)

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
            GameEvents.PowerUsed += OnPowerUsed;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerRespawned += OnRespawned;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.FinaleStarted += OnFinale;
        }

        void OnDisable()
        {
            GameEvents.Message -= OnMessage;
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PowerFailed -= OnPowerFailed;
            GameEvents.PowerUsed -= OnPowerUsed;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerRespawned -= OnRespawned;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.FinaleStarted -= OnFinale;
        }

        void OnDestroy() { SlowMo.Cancel(); if (Instance == this) Instance = null; }

        void Build()
        {
            var root = (RectTransform)transform;

            _burnFlash = UIKit.Image(root, "BurnPulse", new Color(1f, 0.15f, 0.05f, 0f));
            UIKit.Stretch(_burnFlash.rectTransform, -10);
            _damageVignette = UIKit.Image(root, "DamageVignette", new Color(1f, 0.1f, 0.05f, 0f), UIKit.Radial, false);
            UIKit.Stretch(_damageVignette.rectTransform, -60);

            // Everything gameplay-related sits in one group so the finale can fade it out.
            var gameplay = UIKit.Rect(root, "Gameplay");
            UIKit.Stretch(gameplay);
            _gameplayGroup = gameplay.gameObject.AddComponent<CanvasGroup>();
            _gameplayGroup.interactable = false; _gameplayGroup.blocksRaycasts = false;

            // Soft dark corners behind the HUD clusters so they read over bright light.
            var tl = UIKit.Image(gameplay, "ShadeTL", new Color(0f, 0f, 0f, 0.42f), UIKit.Glow);
            UIKit.Place(tl.rectTransform, new Vector2(0f, 1f), new Vector2(-260, 200), new Vector2(1100, 560), new Vector2(0f, 1f));
            var tr = UIKit.Image(gameplay, "ShadeTR", new Color(0f, 0f, 0f, 0.38f), UIKit.Glow);
            UIKit.Place(tr.rectTransform, new Vector2(1f, 1f), new Vector2(260, 200), new Vector2(1100, 440), new Vector2(1f, 1f));
            var bl = UIKit.Image(gameplay, "ShadeBottom", new Color(0f, 0f, 0f, 0.4f), UIKit.VGradient, false);
            bl.rectTransform.anchorMin = new Vector2(0f, 0f); bl.rectTransform.anchorMax = new Vector2(1f, 0f);
            bl.rectTransform.pivot = new Vector2(0.5f, 0f);
            bl.rectTransform.sizeDelta = new Vector2(0f, 130f); bl.rectTransform.anchoredPosition = Vector2.zero;

            // Top-left: health + resonance (icons on the left, cost ticks, power chips).
            Health = HudBar.Build(gameplay, "Health", UIStyle.Health, new Vector2(118, -66), 440, 26, UIKit.Diamond, 5,
                                  new Color(1f, 0.32f, 0.22f, 0.9f));
            ResonanceBar = HudBar.Build(gameplay, "Resonance", UIStyle.Resonance, new Vector2(118, -138), 440, 22, UIKit.WaveIcon, 0,
                                        new Color(1f, 1f, 1f, 0.4f));
            var tickCol = new Color(1f, 1f, 1f, 0.75f);
            ResonanceBar.AddTick(ShieldMin / 100f, tickCol);
            ResonanceBar.AddTick(PulseCost / 100f, tickCol);
            ResonanceBar.AddTick(EchoCost / 100f, tickCol);
            BuildChips(gameplay);

            // Top-right: level number + name, timer.
            _levelNumber = UIKit.Heading(gameplay, "", 24, TextAnchor.UpperRight, UIStyle.Accent, "LevelNumber", 0);
            _levelNumber.font = UIKit.Font;   // digits read better in the sans
            UIKit.Place(_levelNumber.rectTransform, new Vector2(1f, 1f), new Vector2(-60, -40), new Vector2(300, 34), new Vector2(1f, 1f));
            LevelText = UIKit.Heading(gameplay, "", 34, TextAnchor.UpperRight, Color.white, "Level", 0);
            UIKit.Place(LevelText.rectTransform, new Vector2(1f, 1f), new Vector2(-60, -74), new Vector2(1000, 46), new Vector2(1f, 1f));
            UIKit.GlowText(LevelText, UIStyle.Accent, 1.5f);
            var rule = UIKit.Image(gameplay, "Rule", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.55f), UIKit.HGradient, false);
            UIKit.Place(rule.rectTransform, new Vector2(1f, 1f), new Vector2(-60, -126), new Vector2(340, 2), new Vector2(1f, 0.5f));
            rule.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // bright at the right edge
            TimerText = UIKit.Heading(gameplay, "00:00.00", 36, TextAnchor.UpperRight, UIStyle.Accent, "Timer", 0);
            UIKit.Place(TimerText.rectTransform, new Vector2(1f, 1f), new Vector2(-60, -138), new Vector2(400, 40), new Vector2(1f, 1f));
            TimerText.font = UIKit.Font;   // the serif's ornamented 0 reads poorly in numbers
            UIKit.Shadowed(TimerText);

            // Melody shards x/N (hidden in levels without shards).
            ShardsText = UIKit.Heading(gameplay, "", 24, TextAnchor.UpperRight, MelodyShards.ShardColor, "Shards", 0);
            ShardsText.font = UIKit.Font;
            UIKit.Place(ShardsText.rectTransform, new Vector2(1f, 1f), new Vector2(-60, -184), new Vector2(400, 32), new Vector2(1f, 1f));
            UIKit.Shadowed(ShardsText);

            // Message banner (lower middle, over the dark ground so it never hides lights or enemies).
            _messageRoot = UIKit.Rect(root, "Message");
            UIKit.Place(_messageRoot, new Vector2(0.5f, 0f), new Vector2(0, 188), new Vector2(1300, 84), new Vector2(0.5f, 0.5f));
            _messageGroup = _messageRoot.gameObject.AddComponent<CanvasGroup>();
            _messageGroup.alpha = 0f;
            var mbg = UIKit.Image(_messageRoot, "Bg", new Color(0.0f, 0.005f, 0.015f, 0.72f), UIKit.Glow);
            _messageBacking = mbg.rectTransform;
            UIKit.Place(_messageBacking, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 120), new Vector2(0.5f, 0.5f));
            var mline = UIKit.Image(_messageRoot, "Line", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.8f), UIKit.HGradientCentered);
            UIKit.Place(mline.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 2), new Vector2(700, 2), new Vector2(0.5f, 0.5f));
            MessageText = UIKit.Label(_messageRoot, "", 34, TextAnchor.MiddleCenter, Color.white, "Text");
            MessageText.font = UIKit.Font;
            MessageText.richText = true;
            UIKit.Stretch(MessageText.rectTransform, 8);
            UIKit.Shadowed(MessageText);

            // Bottom-left key hints: key-cap boxes + action names (rebuilt when bindings / device change).
            _legendRoot = UIKit.Rect(gameplay, "Legend");
            UIKit.Place(_legendRoot, new Vector2(0f, 0f), new Vector2(60, 46), new Vector2(1700, 40), new Vector2(0f, 0.5f));
            LegendText = UIKit.Label(gameplay, "", 20, TextAnchor.LowerLeft, UIStyle.TextDim, "LegendPlain");
            LegendText.enabled = false;

            // Beat indicator (bottom-right): helps time yellow beat lasers. Only in levels with beat-gated light.
            _beatRoot = UIKit.Rect(gameplay, "Beat");
            UIKit.Place(_beatRoot, new Vector2(1f, 0f), new Vector2(-100, 86), new Vector2(130, 110), new Vector2(0.5f, 0.5f));
            var beatBack = UIKit.Image(_beatRoot, "Back", new Color(0f, 0f, 0f, 0.45f), UIKit.Glow, false);
            UIKit.Place(beatBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(120, 120), new Vector2(0.5f, 0.5f));
            _beatRing = UIKit.Image(_beatRoot, "Halo", new Color(1f, 0.85f, 0.3f, 0.25f), UIKit.Ring, false);
            UIKit.Place(_beatRing.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(76, 76), new Vector2(0.5f, 0.5f));
            _beatDot = UIKit.Image(_beatRoot, "Dot", new Color(1f, 0.85f, 0.3f, 0.9f), UIKit.Glow, false);
            UIKit.Place(_beatDot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(30, 30), new Vector2(0.5f, 0.5f));
            var beatLabel = UIKit.Label(_beatRoot, UIKit.Spaced("BEAT"), 18, TextAnchor.MiddleCenter, UIStyle.Gold, "Label");
            beatLabel.font = UIKit.Narrow;
            UIKit.Place(beatLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 4), new Vector2(130, 24), new Vector2(0.5f, 0.5f));
            _beatRoot.gameObject.SetActive(false);

            // Centre banner (level complete / checkpoint).
            var banner = UIKit.Rect(root, "Banner");
            UIKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(1400, 220), new Vector2(0.5f, 0.5f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            var bback = UIKit.Image(banner, "Back", new Color(0f, 0f, 0.01f, 0.5f), UIKit.Glow);
            UIKit.Place(bback.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1300, 260), new Vector2(0.5f, 0.5f));
            _banner = UIKit.Heading(banner, "", 66, TextAnchor.MiddleCenter, Color.white, "Title", 0);
            UIKit.Place(_banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(1400, 90), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(_banner, UIStyle.Accent, 2f);
            _bannerWave = UIKit.Rect(banner, "Wave").gameObject.AddComponent<UIWaveLine>();
            _bannerWave.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.85f);
            _bannerWave.thickness = 3f; _bannerWave.glowWidth = 3f; _bannerWave.amplitude = 0.5f; _bannerWave.raycastTarget = false;
            UIKit.Place(_bannerWave.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(760, 30), new Vector2(0.5f, 0.5f));
            _bannerSub = UIKit.Heading(banner, "", 32, TextAnchor.MiddleCenter, UIStyle.Accent, "Sub", 0);
            _bannerSub.font = UIKit.Font;
            UIKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -58), new Vector2(1400, 44), new Vector2(0.5f, 0.5f));

            // Death overlay.
            _deathVignette = UIKit.Image(root, "DeathVignette", new Color(0f, 0f, 0f, 0f), UIKit.Radial, false);
            UIKit.Stretch(_deathVignette.rectTransform, -200);
            _deathText = UIKit.Heading(root, "THE LIGHT TOOK YOU", 46, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0), "DeathText", 1);
            UIKit.Place(_deathText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 80), new Vector2(0.5f, 0.5f));

            // Finale whiteout + text (on top of everything; text above the whiteout).
            _whiteout = UIKit.Image(root, "Whiteout", new Color(1f, 0.97f, 0.92f, 0f), null, false);
            UIKit.Stretch(_whiteout.rectTransform, -10);
            _endLine1 = UIKit.Heading(root, "UMBRA SANG.", 50, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f), "EndLine1", 1);
            UIKit.Place(_endLine1.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 70), new Vector2(0.5f, 0.5f));
            UIKit.GlowText(_endLine1, new Color(1f, 0.85f, 0.6f), 2f);
            _endLine2 = UIKit.Label(root, UIKit.Spaced("and every light remembered its harmony."), 30, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f), "EndLine2");
            _endLine2.textWrappingMode = TextWrappingModes.NoWrap;
            UIKit.Place(_endLine2.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 182), new Vector2(1600, 50), new Vector2(0.5f, 0.5f));

            RefreshLegend();
            RefreshLevelName();
        }

        void BuildChips(RectTransform parent)
        {
            var powers = new[] { PowerType.Echo, PowerType.Shield, PowerType.Pulse };
            var names = new[] { "ECHO", "SHIELD", "PULSE" };
            float x = 118f;
            for (int i = 0; i < 3; i++)
            {
                var c = new Chip { power = powers[i] };
                c.root = UIKit.Rect(parent, "Chip" + names[i]);
                c.bg = UIKit.Image(c.root, "Bg", new Color(0.02f, 0.04f, 0.07f, 0.75f), UIKit.Soft);
                UIKit.Stretch(c.bg.rectTransform);
                c.dot = UIKit.Image(c.root, "Dot", UIStyle.Resonance, UIKit.Glow, false);
                UIKit.Place(c.dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(16, 16), new Vector2(0.5f, 0.5f));
                c.text = UIKit.Label(c.root, UIKit.Spaced(names[i]), 19, TextAnchor.MiddleLeft, UIStyle.Text, "Name");
                c.text.font = UIKit.Narrow;
                c.text.textWrappingMode = TextWrappingModes.NoWrap;
                UIKit.Stretch(c.text.rectTransform);
                c.text.rectTransform.offsetMin = new Vector2(32, 0);
                float w = 44f + c.text.preferredWidth;
                UIKit.Place(c.root, new Vector2(0f, 1f), new Vector2(x, -176), new Vector2(w, 34), new Vector2(0f, 1f));
                x += w + 10f;
                _chips[i] = c;
            }
        }

        void TickChips(float dt, Resonance res)
        {
            float scale = Settings.AssistMode ? Settings.AssistCostScale : 1f;
            float cur = res != null ? (res.infinite ? float.MaxValue : res.current) : 100f;
            float t = Time.unscaledTime;
            for (int i = 0; i < _chips.Length; i++)
            {
                var c = _chips[i];
                if (c == null) continue;
                float cost = (c.power == PowerType.Echo ? EchoCost : c.power == PowerType.Pulse ? PulseCost : ShieldMin) * scale;
                bool ready = cur >= cost;
                bool active = c.power == PowerType.Shield && ShieldState.Active;
                c.flash = Mathf.MoveTowards(c.flash, 0f, dt * 3f);
                c.fail = Mathf.MoveTowards(c.fail, 0f, dt * 2.5f);
                var baseCol = ready ? UIStyle.Resonance : new Color(0.4f, 0.48f, 0.58f);
                if (active) baseCol = Color.Lerp(UIStyle.Resonance, Color.white, 0.4f + 0.3f * Mathf.Sin(t * 10f));
                var col = Color.Lerp(Color.Lerp(baseCol, Color.white, c.flash), UIStyle.Danger, c.fail);
                c.dot.color = new Color(col.r, col.g, col.b, ready || active ? 1f : 0.35f);
                c.dot.rectTransform.localScale = Vector3.one * (1f + 0.6f * c.flash + (active ? 0.25f : 0f));
                c.text.color = new Color(1f, 1f, 1f, ready || active ? 0.95f : 0.4f);
                c.bg.color = Color.Lerp(new Color(0.02f, 0.04f, 0.07f, 0.75f), new Color(col.r * 0.35f, col.g * 0.35f, col.b * 0.35f, 0.85f), Mathf.Max(c.flash, c.fail, active ? 0.5f : 0f));
            }
            // Ticks follow assist-mode costs.
            ResonanceBar.SetTick(0, ShieldMin * scale / 100f);
            ResonanceBar.SetTick(1, PulseCost * scale / 100f);
            ResonanceBar.SetTick(2, EchoCost * scale / 100f);
        }

        /// <summary>Enters cinematic mode (normally on GameEvents.FinaleStarted).</summary>
        public void SetCinematic(bool on)
        {
            Cinematic = on;
            CinematicTime = 0f;
            _finaleCompleted = false;
            if (on)
            {
                _messageUntil = 0f;
                var trig = FindAnyObjectByType<FinaleTrigger>();
                if (trig != null) _finaleLength = Mathf.Max(6f, trig.duration);
            }
        }

        void OnFinale() => SetCinematic(true);

        void TickCinematic(float dt)
        {
            float g = Cinematic ? 0f : 1f;
            _gameplayGroup.alpha = Mathf.MoveTowards(_gameplayGroup.alpha, g, dt * (Cinematic ? 0.8f : 3f));
            if (!Cinematic)
            {
                _endLine1.color = new Color(1f, 1f, 1f, 0f); _endLine2.color = new Color(1f, 1f, 1f, 0f);
                _whiteout.color = new Color(1f, 0.97f, 0.92f, 0f);
                return;
            }
            CinematicTime += dt;
            float t = CinematicTime;
            float a1 = Mathf.Clamp01((t - 2.0f) / 1.5f);
            float a2 = Mathf.Clamp01((t - 4.5f) / 1.8f);
            // Whiteout: starts ~1.5 s before the level completes, and is complete ~1 s after.
            float wStart = _finaleCompleted ? Mathf.Min(_completedAt - 0.5f, _finaleLength - 1.5f) : _finaleLength - 1.5f;
            float w = Mathf.Clamp01((t - wStart) / 2.5f);
            // TMP_Text glows through the whiteout, then dissolves into it at the very end.
            float textFade = 1f - Mathf.Clamp01((w - 0.6f) / 0.4f);
            // Gold over the night; deepens to warm umber as the screen turns white, so it stays readable.
            float dawnK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 8f, t));
            var gold = Color.Lerp(new Color(1f, 0.93f, 0.8f), new Color(0.3f, 0.2f, 0.16f), Mathf.Clamp01(Mathf.Max(dawnK * 0.8f, w * 1.6f)));
            _endLine1.color = new Color(gold.r, gold.g, gold.b, a1 * textFade);
            _endLine2.color = new Color(gold.r, gold.g, gold.b, a2 * textFade);
            _whiteout.color = new Color(1f, 0.97f, 0.92f, w);
        }

        void TickBeat(float dt)
        {
            _beatScan -= dt;
            if (_beatScan <= 0f)
            {
                _beatScan = 1f;
                _levelHasBeat = false;
                foreach (var b in FindObjectsByType<LightBeam>(FindObjectsSortMode.None))
                    if (b.beatGated && b.on) { _levelHasBeat = true; break; }
            }
            var clock = BeatClock.Instance;
            bool show = Settings.ShowBeat && _levelHasBeat && clock != null && !Cinematic;
            if (_beatRoot.gameObject.activeSelf != show) _beatRoot.gameObject.SetActive(show);
            if (!show) return;
            float ph = clock.Phase;
            bool hot = clock.IsHot;
            // Swells in the last quarter of the beat (telegraph), flashes while hot.
            float pre = Mathf.Clamp01((ph - 0.75f) / 0.25f);
            float k = hot ? 1f - ph / Mathf.Max(0.01f, clock.HotWindow) * 0.5f : pre * 0.6f;
            var gold = new Color(1f, 0.85f, 0.3f);
            _beatDot.color = new Color(gold.r, gold.g, gold.b, hot ? 1f : 0.35f + 0.4f * pre);
            _beatDot.rectTransform.localScale = Vector3.one * (0.8f + 0.9f * k);
            // The ring closes in on the dot as the beat approaches, then flashes wide on the beat.
            _beatRing.color = new Color(gold.r, gold.g, gold.b, hot ? 0.9f : 0.2f + 0.5f * pre);
            _beatRing.rectTransform.localScale = Vector3.one * (hot ? 1.25f : Mathf.Lerp(1.4f, 0.55f, ph));
        }

        // ------------------------------------------------------------------ key hints

        struct Hint { public string action, keys; }
        readonly List<Hint> _hints = new List<Hint>(6);

        void RefreshLegend()
        {
            LumenInput.Ensure();
            _hints.Clear();
            _hints.Add(new Hint { action = "Move", keys = PromptFormatter.MovePrompt });
            _hints.Add(new Hint { action = "Jump", keys = LumenInput.Prompt(LumenInput.Jump) });
            _hints.Add(new Hint { action = "Dash", keys = LumenInput.Prompt(LumenInput.Dash) });
            _hints.Add(new Hint { action = "Echo", keys = LumenInput.Prompt(LumenInput.Echo) });
            _hints.Add(new Hint { action = "Shield", keys = LumenInput.Prompt(LumenInput.Shield) });
            _hints.Add(new Hint { action = "Pulse", keys = LumenInput.Prompt(LumenInput.Pulse) });
            var sb = new System.Text.StringBuilder();
            foreach (var h in _hints) { sb.Append(h.keys).Append(' ').Append(h.action).Append("   "); }
            string key = sb.ToString();
            if (key == _legendKey) return;
            _legendKey = key;
            LegendText.text = key.TrimEnd();

            for (int i = _legendRoot.childCount - 1; i >= 0; i--) Destroy(_legendRoot.GetChild(i).gameObject);
            float x = 0f;
            foreach (var h in _hints)
            {
                var parts = h.keys.Split('|');
                for (int p = 0; p < parts.Length; p++)
                {
                    string k = parts[p].Trim();
                    if (k.Length == 0) continue;
                    if (p > 0) x -= 4f;
                    var cap = UIKit.KeyCap(_legendRoot, k, 22, out _, "Key");
                    cap.anchorMin = cap.anchorMax = new Vector2(0f, 0.5f);
                    cap.pivot = new Vector2(0f, 0.5f);
                    cap.anchoredPosition = new Vector2(x, 0f);
                    x += cap.sizeDelta.x + 6f;
                }
                var lbl = UIKit.Label(_legendRoot, UIKit.Spaced(h.action.ToUpperInvariant()), 20, TextAnchor.MiddleLeft, UIStyle.Text, "Action");
                lbl.font = UIKit.Font;
                lbl.textWrappingMode = TextWrappingModes.NoWrap;
                UIKit.Shadowed(lbl);
                float w = lbl.preferredWidth;
                UIKit.Place(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 2f, 0f), new Vector2(w + 4f, 32f), new Vector2(0f, 0.5f));
                x += w + 34f;
            }
        }

        void RefreshLevelName()
        {
            var rt = LevelRuntime.Current;
            if (rt != null && rt.Data != null)
            {
                LevelText.text = UIKit.Spaced(rt.Data.Name.ToUpperInvariant());
                _levelNumber.text = UIKit.Spaced($"LEVEL {rt.LevelNumber:00}");
            }
        }

        public float HealthTarget => Health.target;
        public float ResonanceTarget => ResonanceBar.target;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var player = LevelRuntime.Current != null ? LevelRuntime.Current.Player : null;
            Resonance res = null;
            PlayerHealth hp = null;
            if (player != null)
            {
                hp = player.GetComponent<PlayerHealth>();
                res = player.GetComponent<Resonance>();
                if (hp != null) Health.target = hp.Normalized;
                if (res != null) ResonanceBar.target = res.Normalized;
            }
            Health.Tick(dt);
            ResonanceBar.Tick(dt);
            TickChips(dt, res);
            TickCinematic(dt);
            // Only rebuild the timer string when the shown value changes (no per-frame garbage when paused).
            float lt = Mathf.Floor(GameSession.LevelTime * 100f);
            // Lightkeeper arena: its own HUD (countdown, deaths, energy, lamp cards) replaces the campaign HUD.
            if (_gameplayGroup != null && LightkeeperMode.Active) _gameplayGroup.alpha = 0f;
            if (lt != _lastTimer) { _lastTimer = lt; TimerText.text = UIKit.FormatTime(GameSession.LevelTime); }
            if (ShardsText != null && (MelodyShards.Collected != _lastShards || MelodyShards.InLevel != _lastShardTotal))
            {
                _lastShards = MelodyShards.Collected; _lastShardTotal = MelodyShards.InLevel;
                ShardsText.text = _lastShardTotal > 0 ? "SHARDS  " + _lastShards + " / " + _lastShardTotal : "";
            }
            if (string.IsNullOrEmpty(LevelText.text)) RefreshLevelName();

            _legendRefresh -= dt;
            if (_legendRefresh <= 0f) { _legendRefresh = 0.5f; RefreshLegend(); if (_rawMessage != null) SetMessageText(_rawMessage); }

            TickBeat(dt);

            // Message fade (slight rise as it appears).
            float targetA = Time.unscaledTime < _messageUntil ? 1f : 0f;
            _messageGroup.alpha = Mathf.MoveTowards(_messageGroup.alpha, targetA, dt * 3f);
            _messageRoot.anchoredPosition = new Vector2(0f, 188f - 14f * (1f - _messageGroup.alpha));

            // Banner fade.
            float bA = Time.unscaledTime < _bannerUntil ? 1f : 0f;
            _bannerGroup.alpha = Mathf.MoveTowards(_bannerGroup.alpha, bA, dt * 2.5f);
            if (_bannerWave != null) _bannerWave.enabled = _bannerGroup.alpha > 0.01f;

            // Burn level from the light receiver (works in god mode too, so captures show it).
            var rec = LightReceiver.Current;
            float dps = 0f;
            if (rec != null && player != null && !(hp != null && hp.IsDead) && !Cinematic)
                dps = rec.LastEffect.lethal ? 120f : rec.LastEffect.damagePerSecond;
            float burnTarget = Mathf.Clamp01(dps / 60f);
            BurnLevel = Mathf.MoveTowards(BurnLevel, burnTarget, dt * (burnTarget > BurnLevel ? 5f : 1.6f));
            _burnPhase += dt * (4f + 7f * BurnLevel) * Mathf.PI * 2f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_burnPhase);
            if (BurnLevel > 0.02f)
            {
                Health.flash = Mathf.Max(Health.flash, BurnLevel * (0.25f + 0.45f * pulse));
                Health.shake = Mathf.Max(Health.shake, 0.25f + 0.5f * BurnLevel);
            }
            // A lethal beam stopped by the shield: a hard red flash at the edges (near miss).
            float shieldHit = Mathf.Clamp01(1f - (Time.unscaledTime - ShieldDome.LethalContactTime) * 3f);
            TickNearLethal(dt, hp, shieldHit > 0.9f);

            // Damage vignette: hits, low health, and a pulsing burn edge (stronger = redder and faster).
            _damagePulse = Mathf.MoveTowards(_damagePulse, 0f, dt * 2f);
            float low = Health.target < 0.3f ? (0.3f - Health.target) / 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)) * 0.5f : 0f;
            float burnA = BurnLevel * (0.22f + 0.2f * pulse);
            var vc = Color.Lerp(new Color(1f, 0.38f, 0.08f), new Color(0.95f, 0.02f, 0.02f), BurnLevel);
            if (shieldHit > 0f) vc = Color.Lerp(vc, new Color(1f, 0.05f, 0.05f), shieldHit);
            // Lethal light raises a damage event every frame (god mode / the frame before death): don't stack hit pulses on it.
            float hitA = rec != null && rec.LastEffect.lethal ? 0f : _damagePulse * 0.6f;
            _damageVignette.color = new Color(vc.r, vc.g, vc.b, Mathf.Clamp01(hitA + low + burnA + shieldHit * 0.45f));
            _burnFlash.color = new Color(vc.r, vc.g * 0.5f, vc.b, BurnLevel * 0.035f * pulse + shieldHit * 0.04f);

            // Death overlay.
            bool dead = hp != null && hp.IsDead;
            _death = Mathf.MoveTowards(_death, dead ? 1f : 0f, dt * (dead ? 2f : 3f));
            _deathVignette.color = new Color(0, 0, 0, _death * 0.95f);
            _deathText.color = new Color(1f, 1f, 1f, _death);
        }

        void SetMessageText(string raw)
        {
            MessageText.text = PromptFormatter.FormatRich(raw);
            float w = Mathf.Clamp(MessageText.preferredWidth + 260f, 520f, 1500f);
            _messageBacking.sizeDelta = new Vector2(w, 124f);
        }

        public void ShowMessage(string raw, float seconds = 5f)
        {
            _rawMessage = raw;
            SetMessageText(raw);
            _messageUntil = Time.unscaledTime + seconds;
        }

        public void ShowBanner(string title, string sub, float seconds = 2.5f)
        {
            _banner.text = title;
            _bannerSub.text = sub;
            if (_bannerWave != null) _bannerWave.gameObject.SetActive(!string.IsNullOrEmpty(title));
            _bannerUntil = Time.unscaledTime + seconds;
        }

        void OnMessage(string m) => ShowMessage(m, Mathf.Clamp(2.5f + m.Length * 0.06f, 3.5f, 8f));
        void OnDamaged(float amount, LightColor c) { Health.flash = 1f; Health.shake = Mathf.Max(Health.shake, Mathf.Clamp01(amount / 30f)); _damagePulse = Mathf.Clamp01(_damagePulse + amount / 25f); }
        void OnPowerFailed(PowerType p)
        {
            ResonanceBar.shake = 1f;
            foreach (var c in _chips) if (c != null && c.power == p) c.fail = 1f;
        }
        void OnPowerUsed(PowerType p)
        {
            foreach (var c in _chips) if (c != null && c.power == p) c.flash = 1f;
        }
        void OnLevelCompleted(int level, float time)
        {
            if (Cinematic) { _finaleCompleted = true; _completedAt = CinematicTime; return; }   // the ending speaks for itself
            ShowBanner(UIKit.Spaced("LEVEL COMPLETE", 1), UIKit.FormatTime(time), 3f);
        }
        void OnDied() { SlowMo.Cancel(); }

        /// <summary>
        /// Near-lethal moments (a big hit that leaves Umbra low, or a lethal beam stopped by the shield):
        /// brief slow motion + heartbeat. Health is sampled every 0.25 s (DoT arrives in small chunks).
        /// </summary>
        void TickNearLethal(float dt, PlayerHealth hp, bool shieldSave)
        {
            _nearLethalCooldown -= dt;
            bool trigger = shieldSave;
            if (hp != null && !hp.IsDead)
            {
                _hpSampleT -= dt;
                if (_hpSampleT <= 0f)
                {
                    _hpSampleT = 0.25f;
                    float cur = hp.Normalized;
                    float before = _hpPrev2 >= 0f ? _hpPrev2 : cur;
                    if (before - cur >= 0.18f && cur <= 0.35f && cur > 0f) trigger = true;
                    _hpPrev2 = _hpPrev; _hpPrev = cur;
                }
            }
            else { _hpPrev = _hpPrev2 = -1f; }
            if (!trigger || _nearLethalCooldown > 0f) return;
            _nearLethalCooldown = 4f;
            _damagePulse = 1f;
            Health.flash = 1f; Health.shake = 1f;
            if (DangerAudio.Instance != null) DangerAudio.Instance.PlayHeartbeat(1f);
            SlowMo.Trigger(this);
        }
        void OnRespawned() { Health.flash = 0.6f; }
        void OnCheckpoint(int i) => ShowBanner("", UIKit.Spaced("CHECKPOINT"), 1.5f);
        void OnLevelStarted(int level) { LevelText.text = ""; RefreshLevelName(); }
    }
}
