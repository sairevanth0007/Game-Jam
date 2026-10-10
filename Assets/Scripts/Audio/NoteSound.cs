using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Procedural sustained tone per note: sine + soft 2nd harmonic + light vibrato,
    /// with attack/release envelopes. Plays while NoteInput holds the note.
    /// Needs an AudioSource on the same GameObject (added automatically, no clip needed).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class NoteSound : MonoBehaviour
    {
        [Tooltip("Leave empty to use NoteInput.Instance.")]
        [SerializeField] NoteInput noteInput;

        [Header("Pitch (Hz)")]
        [SerializeField] float lowFrequency = 130.81f;   // C3
        [SerializeField] float midFrequency = 196.00f;   // G3
        [SerializeField] float highFrequency = 329.63f;  // E4

        [Header("Tone")]
        [SerializeField, Range(0f, 1f)] float volume = 0.5f;
        [SerializeField, Range(0f, 1f)] float secondHarmonic = 0.25f;
        [SerializeField] float vibratoRate = 5f;
        [Tooltip("Vibrato depth in cents (100 cents = 1 semitone).")]
        [SerializeField] float vibratoCents = 8f;

        [Header("Envelope (seconds)")]
        [SerializeField] float attack = 0.05f;
        [SerializeField] float release = 0.4f;

        const int VoiceCount = NoteInput.NoteCount;
        const float VoiceGain = 0.25f;

        // Written on the main thread, read on the audio thread. Single floats/bools are safe enough here.
        readonly bool[] gates = new bool[VoiceCount];
        readonly float[] frequencies = new float[VoiceCount];
        float masterVolume, attackSeconds, releaseSeconds, harmonicLevel, vibratoHz, vibratoDepth;

        // Audio-thread state.
        readonly double[] phases = new double[VoiceCount];
        readonly double[] vibratoPhases = new double[VoiceCount];
        readonly float[] envelopes = new float[VoiceCount];
        int sampleRate;

        bool subscribed;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            CacheSettings();

            AudioSource source = GetComponent<AudioSource>();
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            if (!source.isPlaying)
                source.Play();
        }

        void OnValidate() => CacheSettings();

        void Start() => Subscribe();

        void OnEnable() => Subscribe();

        void OnDisable()
        {
            if (subscribed && noteInput != null)
            {
                noteInput.NotePressed -= OnNotePressed;
                noteInput.NoteReleased -= OnNoteReleased;
            }
            subscribed = false;

            for (int i = 0; i < VoiceCount; i++)
                gates[i] = false;
        }

        void Subscribe()
        {
            if (subscribed) return;
            if (noteInput == null) noteInput = NoteInput.Instance;
            if (noteInput == null) return; // Start will try again

            noteInput.NotePressed += OnNotePressed;
            noteInput.NoteReleased += OnNoteReleased;
            subscribed = true;

            // Pick up notes already held when we subscribed.
            for (int i = 0; i < VoiceCount; i++)
                gates[i] = noteInput.IsHeld((Note)i);
        }

        void OnNotePressed(Note note) => gates[(int)note] = true;

        void OnNoteReleased(Note note) => gates[(int)note] = false;

        void CacheSettings()
        {
            frequencies[0] = lowFrequency;
            frequencies[1] = midFrequency;
            frequencies[2] = highFrequency;
            masterVolume = volume;
            attackSeconds = Mathf.Max(0.001f, attack);
            releaseSeconds = Mathf.Max(0.001f, release);
            harmonicLevel = secondHarmonic;
            vibratoHz = vibratoRate;
            // Frequency ratio for the vibrato peak: 2^(cents/1200) - 1
            vibratoDepth = Mathf.Pow(2f, vibratoCents / 1200f) - 1f;
        }

        // Audio thread. No Unity API calls in here.
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate <= 0) return;

            float dt = 1f / sampleRate;
            float attackStep = dt / attackSeconds;
            float releaseStep = dt / releaseSeconds;
            double twoPi = 2.0 * System.Math.PI;
            float normalize = 1f / (1f + harmonicLevel);

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float sample = 0f;

                for (int v = 0; v < VoiceCount; v++)
                {
                    float env = envelopes[v];
                    env = gates[v] ? Mathf.Min(1f, env + attackStep) : Mathf.Max(0f, env - releaseStep);
                    envelopes[v] = env;
                    if (env <= 0f)
                    {
                        phases[v] = 0.0;
                        continue;
                    }

                    vibratoPhases[v] += twoPi * vibratoHz * dt;
                    if (vibratoPhases[v] > twoPi) vibratoPhases[v] -= twoPi;

                    double freq = frequencies[v] * (1.0 + vibratoDepth * System.Math.Sin(vibratoPhases[v]));
                    phases[v] += twoPi * freq * dt;
                    if (phases[v] > twoPi) phases[v] -= twoPi;

                    double p = phases[v];
                    float tone = (float)(System.Math.Sin(p) + harmonicLevel * System.Math.Sin(2.0 * p)) * normalize;
                    sample += tone * env * env * VoiceGain; // squared envelope sounds smoother
                }

                sample *= masterVolume;
                for (int c = 0; c < channels; c++)
                    data[frame + c] += sample;
            }
        }
    }
}
