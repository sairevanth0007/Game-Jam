using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(ChordInput))]
    public sealed class ChordAudio : MonoBehaviour
    {
        [Range(0, .3f)] [SerializeField] float volume = .09f;
        [Range(0, .3f)] [SerializeField] float movementVolume = .06f; // CHANGED: movement notes, a bit quieter
        const int NoteCount = 3;                                      // CHANGED: C, E, G
        readonly AudioSource[] voices = new AudioSource[7];           // CHANGED: 3 notes + 4 movement keys
        readonly float[] frequencies =
        {
            130.8128f, 164.8138f, 195.9977f,   // C, E, G
            146.8324f, 174.6141f,              // CHANGED: LeftFirst = D, LeftSecond = F
            220.0000f, 261.6256f               // CHANGED: RightFirst = A, RightSecond = C (higher)
        };
        ChordInput input;
        void Awake()
        {
            input = GetComponent<ChordInput>();
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = true;
                voice.spatialBlend = 0;
                voice.volume = 0;
                int cycles = Mathf.RoundToInt(frequencies[i]);
                int samples = Mathf.RoundToInt(cycles * 48000f / frequencies[i]);
                var data = new float[samples];
                for (int n = 0; n < samples; n++)
                {
                    float phase = 2 * Mathf.PI * cycles * n / samples;
                    data[n] = .78f * Mathf.Sin(phase) + .16f * Mathf.Sin(phase * 2) + .06f * Mathf.Sin(phase * 3);
                }
                voice.clip = AudioClip.Create("Chord note " + i, samples, 1, 48000, false);
                voice.clip.SetData(data, 0);
                voices[i] = voice;
            }
        }
        void Update()
        {
            double onset = AudioSettings.dspTime + .015;
            for (int i = 0; i < voices.Length; i++)
            {
                // CHANGED: the first 3 voices read the note keys, the rest read the movement keys.
                bool isNote = i < NoteCount;
                int bit = 1 << (isNote ? i : i - NoteCount);
                int heldMask = isNote ? input.Held : input.MovementHeld;
                int pressedMask = isNote ? input.Pressed : input.MovementPressed;
                float level = isNote ? volume : movementVolume;

                bool held = (heldMask & bit) != 0;
                if ((pressedMask & bit) != 0) { voices[i].Stop(); voices[i].PlayScheduled(onset); }
                voices[i].volume = Mathf.MoveTowards(voices[i].volume, held ? level : 0, Time.deltaTime * (held ? 1.5f : .5f));
            }
        }
        //  task
        void OnDisable() { foreach (var voice in voices) if (voice != null) voice.Stop(); }
        void OnDestroy() { foreach (var voice in voices) if (voice != null && voice.clip != null) Destroy(voice.clip); }
    }
}