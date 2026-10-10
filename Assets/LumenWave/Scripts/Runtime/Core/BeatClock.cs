using System;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// 100 BPM clock that drives yellow lasers and the music. Uses the audio DSP clock when available
    /// so visuals and synthesised audio stay locked together.
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        public static BeatClock Instance { get; private set; }

        public float bpm = Harmony.Bpm;
        /// <summary>Fraction of a beat (from the downbeat) during which beat-gated lights are "hot".</summary>
        public float hotWindow = 0.35f;

        public static event Action<int> OnBeat;

        double _start;
        int _lastBeat = -1;

        public double Time => AudioSettings.dspTime - _start;
        public float SecondsPerBeat => 60f / bpm;
        public int Beat => (int)Math.Floor(Time / SecondsPerBeat);
        /// <summary>0..1 position within the current beat.</summary>
        public float Phase => (float)(Time / SecondsPerBeat - Math.Floor(Time / SecondsPerBeat));
        /// <summary>True during the first part of each beat (yellow lights fire).</summary>
        public bool IsHot => Phase < hotWindow;

        /// <summary>Static helpers that work even if no clock exists (tests).</summary>
        public static bool Hot => Instance != null ? Instance.IsHot : false;
        public static float CurrentPhase => Instance != null ? Instance.Phase : 0.5f;

        void Awake()
        {
            Instance = this;
            _start = AudioSettings.dspTime;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            int b = Beat;
            if (b != _lastBeat)
            {
                _lastBeat = b;
                OnBeat?.Invoke(b);
            }
        }

        /// <summary>DSP time of the next beat (for sample-accurate scheduling).</summary>
        public double NextBeatDsp()
        {
            return _start + (Beat + 1) * (double)SecondsPerBeat;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { OnBeat = null; Instance = null; }
    }
}
