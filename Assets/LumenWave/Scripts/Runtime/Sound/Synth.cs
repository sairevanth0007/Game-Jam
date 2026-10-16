using System;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Attack / decay / sustain / release amplitude envelope (seconds, sustain level 0..1).</summary>
    [Serializable]
    public struct Envelope
    {
        public float attack, decay, sustain, release;

        public Envelope(float attack, float decay, float sustain, float release)
        {
            this.attack = attack; this.decay = decay; this.sustain = sustain; this.release = release;
        }

        /// <summary>Amplitude at time t for a note held for <paramref name="held"/> seconds, then released.</summary>
        public float Evaluate(float t, float held)
        {
            if (t < 0f) return 0f;
            float level = LevelWhileHeld(Mathf.Min(t, held));
            if (t <= held) return level;
            if (release <= 0f) return 0f;
            return level * Mathf.Clamp01(1f - (t - held) / release);
        }

        float LevelWhileHeld(float t)
        {
            if (attack > 0f && t < attack) return t / attack;
            t -= attack;
            if (decay > 0f && t < decay) return Mathf.Lerp(1f, sustain, t / decay);
            return sustain;
        }

        public static readonly Envelope Pluck = new Envelope(0.005f, 0.25f, 0.0f, 0.1f);
        public static readonly Envelope Bell = new Envelope(0.004f, 0.9f, 0.0f, 0.4f);
        public static readonly Envelope Pad = new Envelope(0.3f, 0.4f, 0.8f, 0.8f);
        public static readonly Envelope Note = new Envelope(0.02f, 0.2f, 0.6f, 0.35f);
        public static readonly Envelope Hit = new Envelope(0.002f, 0.12f, 0.0f, 0.05f);
    }

    /// <summary>
    /// Tiny additive synthesiser. Everything the game hears is computed here from sine partials
    /// (harmonic series), filtered noise and envelopes — no audio files.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;

        /// <summary>Partial amplitude weights: 1 = fundamental, then 2f, 3f... (harmonic series).</summary>
        public static readonly float[] Sine = { 1f };
        public static readonly float[] Soft = { 1f, 0.35f, 0.12f, 0.05f };
        public static readonly float[] Voice = { 1f, 0.6f, 0.45f, 0.25f, 0.18f, 0.08f, 0.05f };   // "ah"-like
        public static readonly float[] Bright = { 1f, 0.5f, 0.33f, 0.25f, 0.2f, 0.16f };              // saw-ish
        public static readonly float[] Hollow = { 1f, 0f, 0.33f, 0f, 0.2f, 0f, 0.14f };                // square-ish

        /// <summary>
        /// Renders a note: Σ h_k·sin(2π·k·f·t) shaped by an envelope. Optional vibrato (Hz, depth ratio),
        /// noise mix and a linear pitch glide to <paramref name="glideTo"/>·f.
        /// </summary>
        public static float[] Render(float freq, float held, float[] harmonics, Envelope env,
            float gain = 0.5f, float noise = 0f, float glideTo = 1f, float vibratoHz = 0f, float vibratoDepth = 0f,
            int sampleRate = SampleRate, int seed = 1)
        {
            float dur = held + Mathf.Max(0f, env.release);
            int n = Mathf.Max(1, Mathf.CeilToInt(dur * sampleRate));
            var data = new float[n];
            var rng = new System.Random(seed);
            double phase = 0;
            float norm = 0f;
            foreach (var h in harmonics) norm += Mathf.Abs(h);
            norm = norm > 0 ? 1f / norm : 1f;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sampleRate;
                float glide = Mathf.Lerp(1f, glideTo, Mathf.Clamp01(t / Mathf.Max(0.0001f, dur)));
                float vib = vibratoHz > 0 ? 1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * t) : 1f;
                phase += 2.0 * Math.PI * freq * glide * vib / sampleRate;
                float s = 0f;
                for (int k = 0; k < harmonics.Length; k++)
                    if (harmonics[k] != 0f) s += harmonics[k] * (float)Math.Sin(phase * (k + 1));
                s *= norm;
                if (noise > 0f)
                {
                    // One-pole low-passed white noise (softer "breath").
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (w - lp) * 0.35f;
                    s = s * (1f - noise) + lp * noise * 1.6f;
                }
                data[i] = s * env.Evaluate(t, held) * gain;
            }
            return data;
        }

        /// <summary>Sums b into a (offset in samples), growing a if needed.</summary>
        public static float[] Mix(float[] a, float[] b, int offset = 0, float gainB = 1f)
        {
            int len = Mathf.Max(a.Length, offset + b.Length);
            var r = new float[len];
            Array.Copy(a, r, a.Length);
            for (int i = 0; i < b.Length; i++) r[i + offset] += b[i] * gainB;
            return r;
        }

        /// <summary>
        /// A seamless loop of a steady tone: the frequency is nudged so a whole number of cycles fits,
        /// which avoids clicks at the loop point.
        /// </summary>
        public static float[] RenderLoop(float freq, float seconds, float[] harmonics, float gain = 0.5f, int sampleRate = SampleRate)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(seconds * sampleRate));
            int cycles = Mathf.Max(1, Mathf.RoundToInt(freq * n / sampleRate));
            double f = cycles * (double)sampleRate / n;
            var data = new float[n];
            float norm = 0f;
            foreach (var h in harmonics) norm += Mathf.Abs(h);
            norm = norm > 0 ? 1f / norm : 1f;
            for (int i = 0; i < n; i++)
            {
                double ph = 2.0 * Math.PI * f * i / sampleRate;
                float s = 0f;
                for (int k = 0; k < harmonics.Length; k++)
                    if (harmonics[k] != 0f) s += harmonics[k] * (float)Math.Sin(ph * (k + 1));
                data[i] = s * norm * gain;
            }
            return data;
        }

        /// <summary>Gentle saturation (tanh-like), keeps summed layers below full scale.</summary>
        public static float SoftClip(float x) => x / (1f + Mathf.Abs(x));

        /// <summary>
        /// Loop of white noise through a one-pole high-pass (brightness 0..1 = how much low end is removed).
        /// Start and end are cross-faded over 20 ms so the loop point doesn't click.
        /// </summary>
        public static float[] NoiseLoop(float seconds, float brightness, float gain, int seed, int sampleRate = SampleRate)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(seconds * sampleRate));
            int fade = Mathf.Min(n / 4, sampleRate / 50);
            var raw = new float[n + fade];
            var rng = new System.Random(seed);
            float lp = 0f, a = Mathf.Lerp(0.6f, 0.05f, Mathf.Clamp01(brightness));
            for (int i = 0; i < raw.Length; i++)
            {
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (w - lp) * a;
                raw[i] = (w - lp) * gain;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[i] = raw[i] * t + raw[n + i] * (1f - t);
            }
            return data;
        }

        /// <summary>Sparse random crackle: short decaying impulses (about <paramref name="perSecond"/> of them per second).</summary>
        public static float[] CrackleLoop(float seconds, float perSecond, float gain, int seed, int sampleRate = SampleRate)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[n];
            var rng = new System.Random(seed);
            int count = Mathf.RoundToInt(perSecond * seconds);
            int len = sampleRate / 250;   // 4 ms
            for (int c = 0; c < count; c++)
            {
                int at = rng.Next(0, n - len);
                float amp = (float)(0.3 + 0.7 * rng.NextDouble()) * gain * (rng.Next(2) == 0 ? -1f : 1f);
                for (int i = 0; i < len; i++)
                {
                    float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                    data[at + i] += amp * w * Mathf.Exp(-i / (len * 0.25f));
                }
            }
            return data;
        }

        /// <summary>Airy whoosh: noise swelling and falling while a low-pass sweeps up and back down.</summary>
        public static float[] Whoosh(float seconds, float gain, int seed, int sampleRate = SampleRate)
        {
            int n = Mathf.Max(64, Mathf.RoundToInt(seconds * sampleRate));
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.7f));
                float a = Mathf.Lerp(0.02f, 0.25f, env);
                float w = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += (w - lp1) * a;
                lp2 += (lp1 - lp2) * a;
                data[i] = lp2 * env * env * gain * 3f;
            }
            return data;
        }

        /// <summary>Counts sign changes — used by tests to measure frequency (crossings/2 per second).</summary>
        public static int ZeroCrossings(float[] data, int start = 0, int count = -1)
        {
            if (count < 0) count = data.Length - start;
            int z = 0;
            for (int i = start + 1; i < start + count && i < data.Length; i++)
                if ((data[i - 1] < 0f) != (data[i] < 0f)) z++;
            return z;
        }

        public static AudioClip ToClip(string name, float[] data, int sampleRate = SampleRate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
