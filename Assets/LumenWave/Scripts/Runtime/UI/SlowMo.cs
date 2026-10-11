using System;
using System.Collections;
using UnityEngine;

namespace LumenWave.UI
{
    /// <summary>
    /// Brief slow motion for near-lethal moments: timeScale 0.35 for 0.3 s (real time), eased back to 1 over 0.25 s.
    /// Never runs while paused, during the finale, in bot runs (tests, autoplay) or in capture runs (-lw-*), and it
    /// gives up immediately (without touching timeScale) if anything else changes timeScale meanwhile (pause).
    /// </summary>
    public static class SlowMo
    {
        public const float Scale = 0.35f, Hold = 0.3f, EaseBack = 0.25f;
        static Coroutine _running;
        static MonoBehaviour _host;
        static bool? _captureRun;

        public static bool Active => _running != null;

        static bool CaptureRun
        {
            get
            {
                if (_captureRun == null)
                {
                    _captureRun = false;
                    foreach (var a in Environment.GetCommandLineArgs()) if (a.StartsWith("-lw-")) _captureRun = true;
                }
                return _captureRun.Value;
            }
        }

        public static bool Allowed =>
            !GameSession.Paused && !GameSession.BotMode && !FinaleTrigger.Started && !CaptureRun &&
            Mathf.Approximately(Time.timeScale, 1f) && !Application.isBatchMode;

        public static void Trigger(MonoBehaviour host)
        {
            if (host == null || _running != null || !Allowed) return;
            _host = host;
            _running = host.StartCoroutine(Run());
        }

        public static void Cancel()
        {
            if (_running == null) return;
            if (_host != null) _host.StopCoroutine(_running);
            _running = null;
            if (!GameSession.Paused && Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = 1f;
        }

        static IEnumerator Run()
        {
            Time.timeScale = Scale;
            float t = 0f;
            while (t < Hold + EaseBack)
            {
                yield return null;
                // Someone else took over timeScale (pause menu, finale): leave it alone.
                if (GameSession.Paused || FinaleTrigger.Started || Time.timeScale <= 0f) { _running = null; yield break; }
                t += Time.unscaledDeltaTime;
                float k = t <= Hold ? 0f : Mathf.SmoothStep(0f, 1f, (t - Hold) / EaseBack);
                Time.timeScale = Mathf.Lerp(Scale, 1f, k);
            }
            Time.timeScale = 1f;
            _running = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _running = null; _host = null; _captureRun = null; }
    }
}
