using System;
using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Procedural sound effects (no audio files): every clip is synthesised once at startup.
    /// Listens to GameEvents. Echo pickups play the next note of the level's melody, so collecting
    /// echoes in order plays the level's tune.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [Tooltip("Master volume of all sound effects.")]
        [SerializeField, Range(0f, 1f)] float volume = 0.7f;
        [Tooltip("How many sounds can overlap.")]
        [SerializeField] int voices = 10;

        const int SampleRate = 44100;

        AudioSource[] sources;
        int nextSource;
        AudioClip jump, land, checkpoint, death, respawn, levelComplete;
        readonly Dictionary<int, AudioClip> bells = new Dictionary<int, AudioClip>();
        string[] melody = Array.Empty<string>();

        void Awake()
        {
            Instance = this;
            sources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }

            jump = Synth("jump", 0.14f, (t, d) =>
            {
                float f = Mathf.Lerp(280f, 620f, t / d);
                return Sine(f, t) * 0.5f * Env(t, d, 0.005f) + Noise() * 0.06f * Env(t, 0.05f, 0.002f);
            });
            land = Synth("land", 0.16f, (t, d) =>
                (Sine(Mathf.Lerp(120f, 55f, t / d), t) * 0.7f + Noise() * 0.25f * Mathf.Exp(-t * 40f)) * Env(t, d, 0.002f));
            checkpoint = Synth("checkpoint", 1.4f, (t, d) =>
                Arp(t, new[] { 392f, 493.88f, 587.33f, 783.99f }, 0.09f, 1.1f) * 0.5f);
            levelComplete = Synth("complete", 2.2f, (t, d) =>
                (Bell(261.63f, t, 2f) + Bell(329.63f, t, 2f) + Bell(392f, t, 2f) + Bell(523.25f, Mathf.Max(0f, t - 0.25f), 1.8f) * (t > 0.25f ? 1f : 0f)) * 0.22f);
            death = Synth("death", 0.7f, (t, d) =>
            {
                float f = Mathf.Lerp(520f, 90f, Mathf.Sqrt(t / d));
                return (Sine(f, t) * 0.35f + Noise() * 0.3f * (1f - t / d)) * Env(t, d, 0.01f);
            });
            respawn = Synth("respawn", 0.5f, (t, d) =>
            {
                float f = Mathf.Lerp(200f, 700f, t / d);
                return Sine(f, t) * 0.3f * Mathf.Sin(Mathf.PI * t / d);
            });
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            GameEvents.PlayerJumped += OnJumped;
            GameEvents.PlayerLanded += OnLanded;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.EchoCollected += OnEcho;
            GameEvents.PlayerDied += OnDied;
            GameEvents.PlayerRespawned += OnRespawned;
            GameEvents.LevelStarted += OnLevelStarted;
            GameEvents.LevelCompleted += OnLevelCompleted;
        }

        void OnDisable()
        {
            GameEvents.PlayerJumped -= OnJumped;
            GameEvents.PlayerLanded -= OnLanded;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.EchoCollected -= OnEcho;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.PlayerRespawned -= OnRespawned;
            GameEvents.LevelStarted -= OnLevelStarted;
            GameEvents.LevelCompleted -= OnLevelCompleted;
        }

        void OnJumped() => Play(jump, 0.35f, UnityEngine.Random.Range(0.95f, 1.08f));
        void OnLanded(float impact)
        {
            if (impact < 4f) return;
            Play(land, Mathf.Clamp01(impact / 20f) * 0.7f, UnityEngine.Random.Range(0.9f, 1.1f));
        }
        void OnCheckpoint(Vector2 _) => Play(checkpoint, 0.6f);
        void OnDied(Vector2 _, DeathCause cause) => Play(death, 0.6f);
        void OnRespawned(Vector2 _) => Play(respawn, 0.35f);
        void OnLevelStarted(LevelData level, int index) => melody = level.Melody ?? Array.Empty<string>();
        void OnLevelCompleted(LevelResult _) => Play(levelComplete, 0.7f);

        void OnEcho(Vector2 position, int collected, int total)
        {
            string note = melody.Length > 0 ? melody[(collected - 1) % melody.Length] : "C5";
            PlayNote(NoteFrequency(note) * 2f, 0.55f); // an octave up: bright, bell-like
        }

        // ---------- Public ----------

        public void Play(AudioClip clip, float clipVolume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.pitch = pitch;
            source.PlayOneShot(clip, clipVolume * volume);
        }

        /// <summary>A short bell tone at any frequency (cached per frequency).</summary>
        public void PlayNote(float frequency, float noteVolume = 0.5f)
        {
            int key = Mathf.RoundToInt(frequency * 10f);
            if (!bells.TryGetValue(key, out AudioClip clip))
            {
                clip = Synth("bell " + frequency.ToString("0"), 1.4f, (t, d) => Bell(frequency, t, 1.3f) * 0.6f);
                bells[key] = clip;
            }
            Play(clip, noteVolume);
        }

        /// <summary>"C4", "F#3", "Bb5" → Hz (A4 = 440). Unknown names give C4.</summary>
        public static float NoteFrequency(string name)
        {
            if (string.IsNullOrEmpty(name)) return 261.63f;
            int[] semis = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
            char letter = char.ToUpperInvariant(name[0]);
            if (letter < 'A' || letter > 'G') return 261.63f;
            int semitone = semis[letter - 'A'];
            int i = 1;
            if (i < name.Length && (name[i] == '#' || name[i] == 's')) { semitone++; i++; }
            else if (i < name.Length && name[i] == 'b') { semitone--; i++; }
            int octave = 4;
            if (i < name.Length && int.TryParse(name.Substring(i), out int parsed)) octave = parsed;
            int midi = (octave + 1) * 12 + semitone;
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        // ---------- Synthesis ----------

        static readonly System.Random noiseRng = new System.Random(7);

        static AudioClip Synth(string name, float duration, Func<float, float, float> sample)
        {
            int length = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[length];
            for (int i = 0; i < length; i++)
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate, duration), -1f, 1f);
            AudioClip clip = AudioClip.Create("LW " + name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float frequency, float t) => Mathf.Sin(2f * Mathf.PI * frequency * t);

        static float Noise() => (float)(noiseRng.NextDouble() * 2.0 - 1.0);

        // Linear attack, exponential-ish release to the end.
        static float Env(float t, float duration, float attack)
        {
            if (t < attack) return t / attack;
            float r = Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, duration - attack));
            return r * r;
        }

        static float Bell(float frequency, float t, float decay)
        {
            if (t <= 0f) return 0f;
            float env = Mathf.Exp(-t * 3.2f / decay) * Mathf.Min(1f, t / 0.004f);
            return (Sine(frequency, t) + 0.45f * Sine(frequency * 2.01f, t) * Mathf.Exp(-t * 4f) + 0.2f * Sine(frequency * 3.02f, t) * Mathf.Exp(-t * 7f)) * env * 0.6f;
        }

        static float Arp(float t, float[] notes, float step, float decay)
        {
            float s = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * step;
                if (t >= start) s += Bell(notes[n], t - start, decay);
            }
            return s * 0.5f;
        }
    }
}
