using UnityEngine;
using UnityEngine.UI;

namespace LumenWave
{
    /// <summary>
    /// In-game HUD built from code: level name + time, echo counter, hint line, level-start banner,
    /// pause overlay and the level-complete / game-complete screen. Driven entirely by GameEvents.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        [Tooltip("Seconds the level name banner stays up at level start.")]
        [SerializeField] float bannerDuration = 2.6f;

        Text levelName, timer, echoCount, hint, bannerTitle, bannerSubtitle;
        Text completeTitle, completeBody, completePrompt, pausePrompt;
        CanvasGroup hintGroup, bannerGroup, completeGroup, pauseGroup;
        RectTransform echoIcon;

        float echoPop;
        float bannerTimer;
        bool hintVisible;

        public static Hud Create()
        {
            Canvas canvas = UiFactory.CreateCanvas("HUD", 10);
            var hud = canvas.gameObject.AddComponent<Hud>();
            hud.Build(canvas.transform);
            return hud;
        }

        void Build(Transform root)
        {
            Vector2 topLeft = new Vector2(0f, 1f), topRight = new Vector2(1f, 1f), center = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f);

            levelName = UiFactory.CreateText("Level Name", root, "", 34, TextAnchor.UpperLeft, topLeft, topLeft, new Vector2(40f, -30f), new Vector2(900f, 50f));
            timer = UiFactory.CreateText("Timer", root, "", 26, TextAnchor.UpperLeft, topLeft, topLeft, new Vector2(40f, -76f), new Vector2(400f, 40f), UiFactory.DimTextColor);

            echoCount = UiFactory.CreateText("Echo Count", root, "0 / 0", 40, TextAnchor.UpperRight, topRight, topRight, new Vector2(-40f, -28f), new Vector2(300f, 60f));
            echoIcon = UiFactory.CreateImage("Echo Icon", root, RuntimeArt.Diamond, Color.white, topRight, new Vector2(0.5f, 0.5f), new Vector2(-250f, -52f), new Vector2(56f, 56f)).rectTransform;

            // Hint line
            RectTransform hintRect = UiFactory.CreateRect("Hint", root, bottom, bottom, new Vector2(0f, 60f), new Vector2(1400f, 90f));
            hintGroup = hintRect.gameObject.AddComponent<CanvasGroup>();
            var hintBack = hintRect.gameObject.AddComponent<Image>();
            hintBack.color = new Color(0.03f, 0.02f, 0.06f, 0.75f);
            hintBack.raycastTarget = false;
            hint = UiFactory.CreateText("Text", hintRect, "", 32, TextAnchor.MiddleCenter, center, center, Vector2.zero, new Vector2(1360f, 90f));
            hintGroup.alpha = 0f;

            // Level banner
            RectTransform banner = UiFactory.CreateRect("Banner", root, center, center, new Vector2(0f, 180f), new Vector2(1600f, 200f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerTitle = UiFactory.CreateText("Title", banner, "", 78, TextAnchor.MiddleCenter, center, center, new Vector2(0f, 30f), new Vector2(1600f, 100f));
            bannerSubtitle = UiFactory.CreateText("Subtitle", banner, "", 34, TextAnchor.MiddleCenter, center, center, new Vector2(0f, -45f), new Vector2(1600f, 50f), UiFactory.DimTextColor);
            bannerGroup.alpha = 0f;

            // Pause overlay
            Image pauseDim = UiFactory.CreateFullscreen("Pause", root, new Color(0.01f, 0.005f, 0.03f, 0.7f));
            pauseGroup = pauseDim.gameObject.AddComponent<CanvasGroup>();
            UiFactory.CreateText("Title", pauseDim.transform, "PAUSED", 80, TextAnchor.MiddleCenter, center, center, new Vector2(0f, 40f), new Vector2(1200f, 100f));
            pausePrompt = UiFactory.CreateText("Prompt", pauseDim.transform, "", 32, TextAnchor.MiddleCenter, center, center, new Vector2(0f, -50f), new Vector2(1400f, 50f), UiFactory.DimTextColor);
            pauseGroup.alpha = 0f;

            // Level complete
            Image completeDim = UiFactory.CreateFullscreen("Level Complete", root, new Color(0.01f, 0.005f, 0.03f, 0.78f));
            completeGroup = completeDim.gameObject.AddComponent<CanvasGroup>();
            completeTitle = UiFactory.CreateText("Title", completeDim.transform, "", 76, TextAnchor.MiddleCenter, center, center, new Vector2(0f, 190f), new Vector2(1600f, 100f), UiFactory.Accent);
            completeBody = UiFactory.CreateText("Body", completeDim.transform, "", 40, TextAnchor.MiddleCenter, center, center, new Vector2(0f, 0f), new Vector2(1400f, 260f));
            completePrompt = UiFactory.CreateText("Prompt", completeDim.transform, "", 30, TextAnchor.MiddleCenter, center, center, new Vector2(0f, -200f), new Vector2(1400f, 50f), UiFactory.DimTextColor);
            completeGroup.alpha = 0f;
        }

        void OnEnable()
        {
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.EchoCollected += OnEchoCollected;
            GameEvents.HintChanged += OnHintChanged;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.GameStateChanged += OnStateChanged;
        }

        void OnDisable()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.EchoCollected -= OnEchoCollected;
            GameEvents.HintChanged -= OnHintChanged;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.GameStateChanged -= OnStateChanged;
        }

        void OnLevelStarted(LevelData level, int index)
        {
            levelName.text = level.Name;
            bannerTitle.text = level.Name;
            bannerSubtitle.text = level.Subtitle;
            bannerTimer = bannerDuration;
            int total = GameManager.Instance != null ? GameManager.Instance.EchoTotal : 0;
            echoCount.text = $"0 / {total}";
            hintVisible = false;
        }

        void OnEchoCollected(Vector2 position, int collected, int total)
        {
            echoCount.text = $"{collected} / {total}";
            echoPop = 1f;
        }

        void OnHintChanged(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                hintVisible = false;
                return;
            }
            hint.text = text;
            hintVisible = true;
        }

        void OnLevelCompleted(LevelResult result)
        {
            completeTitle.text = "LEVEL COMPLETE";
            string echoLine = result.Echoes == result.EchoTotal && result.EchoTotal > 0
                ? $"Echoes   {result.Echoes} / {result.EchoTotal}   (all found!)"
                : $"Echoes   {result.Echoes} / {result.EchoTotal}";
            completeBody.text = $"{result.LevelName}\n\nTime   {GameManager.FormatTime(result.Time)}\n{echoLine}\nBurns   {result.Burns}";
            string next = result.IsLastLevel ? "finish" : "next level";
            completePrompt.text = BindingText.Format($"{{Confirm}} / {{Jump}}  —  {next}");
        }

        void OnStateChanged(GameState state)
        {
            if (state == GameState.Won)
            {
                GameManager gm = GameManager.Instance;
                completeTitle.text = "THE WORLD IS QUIET";
                completeBody.text = gm != null
                    ? $"Every level done.\n\nTotal time   {gm.RunTimeText}\nTotal burns   {gm.BurnCount}"
                    : "Every level done.";
                completePrompt.text = BindingText.Format("{Confirm} / {Jump}  —  play again");
            }
            if (state == GameState.Paused)
                pausePrompt.text = BindingText.Format("{Pause}  —  resume        {Respawn}  —  back to rune");
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            GameManager gm = GameManager.Instance;
            GameState state = gm != null ? gm.State : GameState.Playing;

            if (gm != null)
                timer.text = GameManager.FormatTime(gm.LevelTime);

            // Echo icon pop
            echoPop = Mathf.MoveTowards(echoPop, 0f, dt * 3f);
            float pop = 1f + 0.45f * echoPop * echoPop;
            echoIcon.localScale = new Vector3(pop, pop, 1f);
            echoIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 1.5f) * 8f);

            // Banner: fade in fast, hold, fade out
            if (bannerTimer > 0f) bannerTimer -= dt;
            float bannerAlpha = bannerTimer <= 0f ? 0f : Mathf.Clamp01(Mathf.Min((bannerDuration - bannerTimer) * 4f, bannerTimer * 1.5f));
            bannerGroup.alpha = bannerAlpha;

            hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, hintVisible && state == GameState.Playing ? 1f : 0f, dt * 5f);
            pauseGroup.alpha = Mathf.MoveTowards(pauseGroup.alpha, state == GameState.Paused ? 1f : 0f, dt * 8f);
            bool showComplete = state == GameState.LevelComplete || state == GameState.Won;
            completeGroup.alpha = Mathf.MoveTowards(completeGroup.alpha, showComplete ? 1f : 0f, dt * 3f);
        }
    }
}
