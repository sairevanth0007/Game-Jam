using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using LumenWave.UI;

namespace LumenWave
{
    /// <summary>
    /// Persistent full-screen fader: fades in from black on every scene load, fades out before player-initiated
    /// scene changes (<see cref="FadeThen"/>), dips to black between death and respawn, and shows a level card
    /// (number + name) at the start of each level. Debug/capture runs (-lw-* args) skip the fades.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        CanvasGroup _black;
        CanvasGroup _card;
        Text _cardNumber, _cardName;
        Coroutine _fade, _cardRoutine, _dip;
        static bool _captureRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            foreach (var a in Environment.GetCommandLineArgs()) if (a.StartsWith("-lw-")) _captureRun = true;
            GameSystems.Register(root => { if (root.GetComponent<ScreenFader>() == null) root.AddComponent<ScreenFader>(); });
        }

        /// <summary>Fade to black, then run the action (scene load). Immediate when no fader / capture run.</summary>
        public static void FadeThen(Action action, float seconds = 0.35f)
        {
            if (Instance == null || _captureRun || GameSession.BotMode) { action(); return; }
            Instance.StartFade(1f, seconds, action);
        }

        void Awake()
        {
            Instance = this;
            var canvas = UIKit.CreateCanvas("FaderCanvas", 900, transform);
            _black = UIKit.Image(canvas.transform, "Black", Color.black).gameObject.AddComponent<CanvasGroup>();
            UIKit.Stretch((RectTransform)_black.transform);
            _black.blocksRaycasts = false;
            _black.interactable = false;
            _black.alpha = 0f;

            var card = UIKit.Rect(canvas.transform, "LevelCard");
            UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 200));
            _card = card.gameObject.AddComponent<CanvasGroup>();
            _card.blocksRaycasts = false;
            _card.alpha = 0f;
            var cardBack = UIKit.Image(card, "Back", new Color(0f, 0f, 0.01f, 0.55f), UIKit.Glow);
            UIKit.Place(cardBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1300, 300));
            _cardNumber = UIKit.Heading(card, "", 28, TextAnchor.MiddleCenter, UIStyle.Accent, "Number", 0);
            UIKit.Place(_cardNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 62), new Vector2(1400, 44));
            _cardName = UIKit.Heading(card, "", 70, TextAnchor.MiddleCenter, UIStyle.Text, "Name", 0);
            UIKit.Place(_cardName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(1600, 100));
            var g = _cardName.gameObject.AddComponent<UIGradient>();
            g.top = Color.white; g.bottom = new Color(0.6f, 0.92f, 1f);
            UIKit.GlowText(_cardName, UIStyle.Accent, 2f);
            var wave = UIKit.Rect(card, "Wave").gameObject.AddComponent<UIWaveLine>();
            wave.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.8f);
            wave.thickness = 3f; wave.glowWidth = 3f; wave.amplitude = 0.55f; wave.raycastTarget = false;
            UIKit.Place(wave.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -72), new Vector2(700, 34));

            SceneManager.sceneLoaded += OnSceneLoaded;
            LevelRuntime.Loaded += OnLevelLoaded;
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerRespawned += OnRespawned;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LevelRuntime.Loaded -= OnLevelLoaded;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerRespawned -= OnRespawned;
            if (Instance == this) Instance = null;
        }

        void OnSceneLoaded(Scene s, LoadSceneMode m)
        {
            if (_captureRun) { _black.alpha = 0f; return; }
            _black.alpha = 1f;
            StartFade(0f, 0.7f, null);
        }

        void OnLevelLoaded(LevelRuntime rt)
        {
            if (_captureRun || rt == null || rt.Data == null) return;
            if (_cardRoutine != null) StopCoroutine(_cardRoutine);
            _cardRoutine = StartCoroutine(Card(rt.LevelNumber, rt.Data.Name));
        }

        /// <summary>Shows the level card now (also used by the showcase capture).</summary>
        public void ShowCard(int n, string name)
        {
            if (_cardRoutine != null) StopCoroutine(_cardRoutine);
            _cardRoutine = StartCoroutine(Card(n, name));
        }

        IEnumerator Card(int n, string name)
        {
            _cardNumber.text = UIKit.Spaced("LEVEL " + n.ToString("00"), 1);
            _cardName.text = UIKit.Spaced(name.ToUpperInvariant(), 1);
            yield return new WaitForSecondsRealtime(0.35f);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.6f) { _card.alpha = t; yield return null; }
            _card.alpha = 1f;
            yield return new WaitForSecondsRealtime(1.6f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { _card.alpha = t; yield return null; }
            _card.alpha = 0f;
        }

        void OnDied()
        {
            if (_captureRun || GameSession.BotMode) return;
            if (_dip != null) StopCoroutine(_dip);
            _dip = StartCoroutine(DeathDip());
        }

        IEnumerator DeathDip()
        {
            yield return new WaitForSeconds(0.55f);
            if (_fadeHasAction) yield break;
            StartFade(0.85f, 0.4f, null);
            // Safety: never stay dark if the respawn event doesn't come.
            yield return new WaitForSecondsRealtime(2.5f);
            if (_black.alpha > 0.5f && !_fadeHasAction) StartFade(0f, 0.4f, null);
            _dip = null;
        }

        void OnRespawned()
        {
            if (_dip != null) { StopCoroutine(_dip); _dip = null; }
            if (_captureRun || _fadeHasAction) return;   // never cancel a scene-change fade
            if (_black.alpha <= 0.01f && _fade == null) return;
            StartFade(0f, 0.45f, null);
        }

        bool _fadeHasAction;

        void StartFade(float target, float seconds, Action then)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fadeHasAction = then != null;
            _fade = StartCoroutine(Fade(target, seconds, then));
        }

        IEnumerator Fade(float target, float seconds, Action then)
        {
            float start = _black.alpha;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
            {
                _black.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            _black.alpha = target;
            _fade = null;
            _fadeHasAction = false;
            then?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; _captureRun = false; }
    }
}
