using UnityEngine;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(ChordInput))]
    public sealed class ChordAudio : MonoBehaviour
    {
        [Range(0, .3f)] [SerializeField] float volume = .09f;
        readonly AudioSource[] voices = new AudioSource[5];
        readonly float[] frequencies = { 130.8128f, 164.8138f, 130.8128f, 164.8138f, 195.9977f };
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
                bool held = (input.Held & (1 << i)) != 0;
                if ((input.Pressed & (1 << i)) != 0) { voices[i].Stop(); voices[i].PlayScheduled(onset); }
                voices[i].volume = Mathf.MoveTowards(voices[i].volume, held ? volume : 0, Time.deltaTime * (held ? 1.5f : .5f));
            }
        }
        void OnDisable() { foreach (var voice in voices) if (voice != null) voice.Stop(); }
        void OnDestroy() { foreach (var voice in voices) if (voice != null && voice.clip != null) Destroy(voice.clip); }
    }
}
