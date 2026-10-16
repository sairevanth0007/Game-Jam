using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Musical system: everything is in one key (D) using just intonation, i.e. simple whole-number
    /// frequency ratios taken from the harmonic series, so every note the game plays is consonant.
    /// </summary>
    public static class Harmony
    {
        /// <summary>Root of the key: D3.</summary>
        public const float Root = 146.83f;
        public const float Bpm = 100f;
        public const float SecondsPerBeat = 60f / Bpm;

        /// <summary>Just-intonation major scale ratios (1, 9/8, 5/4, 4/3, 3/2, 5/3, 15/8).</summary>
        public static readonly float[] ScaleRatios = { 1f, 9f / 8f, 5f / 4f, 4f / 3f, 3f / 2f, 5f / 3f, 15f / 8f };

        /// <summary>Frequency of a scale degree (0-based, can exceed 6 to go up octaves).</summary>
        public static float Degree(int degree, float root = Root)
        {
            int octave = Mathf.FloorToInt(degree / 7f);
            int idx = degree - octave * 7;
            return root * ScaleRatios[idx] * Mathf.Pow(2f, octave);
        }

        /// <summary>n-th harmonic of the root (n = 1 is the fundamental).</summary>
        public static float Harmonic(int n, float root = Root) => root * n;

        // Signature notes of each power.
        public static float EchoNote => Degree(4 + 7);   // the fifth (3/2), an octave up
        public static float ShieldNote => Degree(0);     // the tonic
        public static float PulseNote => Degree(2 + 7);  // the major third (5/4), an octave up

        /// <summary>Wavelength in world units for a given frequency at the game's (slowed) wave speed.</summary>
        public static float Wavelength(float frequency, float waveSpeed) => waveSpeed / Mathf.Max(1f, frequency);
    }
}
