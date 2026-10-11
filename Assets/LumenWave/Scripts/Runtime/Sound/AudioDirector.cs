using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Persistent procedural audio: an ambient drone in D (just intonation) that gains layers with
    /// progress, beat-locked pulse + arpeggio at 100 BPM, gameplay SFX from <see cref="GameEvents"/>,
    /// sustained notes (shield) and the finale chorus. Lives on the GameSystems root.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        const int PoolSize = 14;
        readonly List<AudioSource> _pool = new List<AudioSource>();
        int _poolNext;

        // Drone voices: (frequency, harmonic weights, base volume, from-level)
        struct Voice { public AudioSource src; public float baseVol; public int fromLevel; public float lfoHz, lfoPhase; }
        readonly List<Voice> _drone = new List<Voice>();
        readonly Dictionary<string, AudioSource> _sustains = new Dictionary<string, AudioSource>();
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<int, AudioClip> _noteCache = new Dictionary<int, AudioClip>();
        readonly List<AudioSource> _finale = new List<AudioSource>();

        float _music = 0.7f, _sfx = 0.8f, _settingsTimer;
        float _lastDamageSfx = -10f, _lastHealSfx = -10f;
        float _finaleStart = -1f;
        int _layerLevel = 1;
        float _droneMaster;   // fades in at start
        AudioListener _ownListener;

        // Arpeggio pattern (scale degrees) for later levels; just intonation from Harmony.
        static readonly int[] Arp = { 0, 4, 7, 9, 7, 4, 2, 4 };

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < PoolSize; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                _pool.Add(s);
            }
            BuildClips();
            BuildDrone();
            ReadSettings();
            Subscribe(true);
        }

        void OnDestroy()
        {
            Subscribe(false);
            if (Instance == this) Instance = null;
        }

        void Subscribe(bool on)
        {
            if (on)
            {
                GameEvents.PlayerJumped += OnJump;
                GameEvents.PlayerLanded += OnLand;
                GameEvents.PowerUsed += OnPower;
                GameEvents.PowerFailed += OnPowerFailed;
                GameEvents.PlayerDamaged += OnDamaged;
                GameEvents.PlayerHealed += OnHealed;
                GameEvents.CheckpointReached += OnCheckpoint;
                GameEvents.CrystalShattered += OnShatter;
                GameEvents.EnemyStunned += OnStun;
                GameEvents.PlayerDied += OnDied;
                GameEvents.PlayerRespawned += OnRespawn;
                GameEvents.LevelStarted += OnLevelStarted;
                GameEvents.LevelCompleted += OnLevelCompleted;
                GameEvents.GameWon += OnWon;
                GameEvents.FinaleStarted += OnFinale;
                BeatClock.OnBeat += OnBeat;
            }
            else
            {
                GameEvents.PlayerJumped -= OnJump;
                GameEvents.PlayerLanded -= OnLand;
                GameEvents.PowerUsed -= OnPower;
                GameEvents.PowerFailed -= OnPowerFailed;
                GameEvents.PlayerDamaged -= OnDamaged;
                GameEvents.PlayerHealed -= OnHealed;
                GameEvents.CheckpointReached -= OnCheckpoint;
                GameEvents.CrystalShattered -= OnShatter;
                GameEvents.EnemyStunned -= OnStun;
                GameEvents.PlayerDied -= OnDied;
                GameEvents.PlayerRespawned -= OnRespawn;
                GameEvents.LevelStarted -= OnLevelStarted;
                GameEvents.LevelCompleted -= OnLevelCompleted;
                GameEvents.GameWon -= OnWon;
                GameEvents.FinaleStarted -= OnFinale;
                BeatClock.OnBeat -= OnBeat;
            }
        }

        // ---------------------------------------------------------------- clips

        void BuildClips()
        {
            float R = Harmony.Root;
            Add("jump", Synth.Render(Harmony.Degree(7), 0.06f, Synth.Soft, new Envelope(0.003f, 0.08f, 0f, 0.05f), 0.35f, 0.1f, 1.5f));
            Add("land", Synth.Render(R * 0.5f, 0.04f, Synth.Sine, Envelope.Hit, 0.55f, 0.6f, 0.6f));
            Add("dash", Synth.Render(R * 2f, 0.16f, Synth.Sine, new Envelope(0.01f, 0.15f, 0f, 0.08f), 0.45f, 0.9f, 0.4f));
            // Red damage: minor second (16/15) beating against the root + a hollow buzz.
            var red = Synth.Render(R * 2f, 0.15f, Synth.Hollow, Envelope.Hit, 0.35f);
            red = Synth.Mix(red, Synth.Render(R * 2f * 16f / 15f, 0.15f, Synth.Hollow, Envelope.Hit, 0.35f));
            Add("hurt_red", red);
            // Yellow (beat) damage: tritone 45/32 sting.
            var yel = Synth.Render(R * 4f, 0.1f, Synth.Bright, Envelope.Hit, 0.3f);
            yel = Synth.Mix(yel, Synth.Render(R * 4f * 45f / 32f, 0.1f, Synth.Bright, Envelope.Hit, 0.3f));
            Add("hurt_yellow", yel);
            Add("hurt", Synth.Render(R, 0.12f, Synth.Bright, Envelope.Hit, 0.4f, 0.4f, 0.7f));
            // Heal shimmer: quick rising major triad, high register.
            var heal = Synth.Render(Harmony.Degree(14), 0.05f, Synth.Sine, Envelope.Pluck, 0.18f);
            heal = Synth.Mix(heal, Synth.Render(Harmony.Degree(16), 0.05f, Synth.Sine, Envelope.Pluck, 0.18f), Synth.SampleRate / 25);
            heal = Synth.Mix(heal, Synth.Render(Harmony.Degree(18), 0.05f, Synth.Sine, Envelope.Pluck, 0.18f), Synth.SampleRate / 12);
            Add("heal", heal);
            // Checkpoint chime: bell triad 4:5:6.
            var chime = Synth.Render(Harmony.Degree(7), 0.1f, Synth.Soft, Envelope.Bell, 0.25f);
            chime = Synth.Mix(chime, Synth.Render(Harmony.Degree(9), 0.1f, Synth.Soft, Envelope.Bell, 0.22f), Synth.SampleRate / 10);
            chime = Synth.Mix(chime, Synth.Render(Harmony.Degree(11), 0.1f, Synth.Soft, Envelope.Bell, 0.22f), Synth.SampleRate / 5);
            chime = Synth.Mix(chime, Synth.Render(Harmony.Degree(14), 0.2f, Synth.Sine, Envelope.Bell, 0.2f), Synth.SampleRate * 3 / 10);
            Add("checkpoint", chime);
            // Crystal shatter: inharmonic partials (glass) + noise burst.
            var glass = Synth.Render(R * 8.3f, 0.02f, new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, new Envelope(0.001f, 0.5f, 0f, 0.2f), 0.25f, 0.5f);
            glass = Synth.Mix(glass, Synth.Render(R * 11.7f, 0.02f, Synth.Sine, new Envelope(0.001f, 0.4f, 0f, 0.2f), 0.2f), 200);
            glass = Synth.Mix(glass, Synth.Render(R * 15.1f, 0.02f, Synth.Sine, new Envelope(0.001f, 0.3f, 0f, 0.2f), 0.15f), 500);
            Add("shatter", glass);
            Add("stun", Synth.Render(Harmony.Degree(11), 0.2f, Synth.Hollow, new Envelope(0.005f, 0.3f, 0f, 0.1f), 0.3f, 0f, 0.5f, 18f, 0.04f));
            Add("death", Synth.Render(R, 0.6f, Synth.Bright, new Envelope(0.01f, 0.6f, 0.3f, 0.5f), 0.4f, 0.2f, 0.5f));
            Add("respawn", Synth.Render(Harmony.Degree(7), 0.3f, Synth.Voice, Envelope.Note, 0.25f, 0f, 1.0f, 5f, 0.01f));
            Add("fail", Synth.Render(R * 0.75f, 0.06f, Synth.Hollow, Envelope.Hit, 0.3f));
            Add("pulse_beat", Synth.Render(R * 0.5f, 0.05f, Synth.Sine, new Envelope(0.002f, 0.25f, 0f, 0.1f), 0.5f, 0.05f, 0.7f));
            Add("tick", Synth.Render(Harmony.Degree(21), 0.005f, Synth.Sine, new Envelope(0.001f, 0.04f, 0f, 0.02f), 0.12f));
            // Win: rising arpeggio into a held major chord.
            var win = new float[1];
            int[] degs = { 0, 2, 4, 7, 9, 11, 14 };
            for (int i = 0; i < degs.Length; i++)
                win = Synth.Mix(win, Synth.Render(Harmony.Degree(degs[i] + 7), 0.8f - i * 0.05f, Synth.Soft, Envelope.Note, 0.16f), i * Synth.SampleRate / 9);
            Add("win", win);
        }

        void Add(string name, float[] data) { _clips[name] = Synth.ToClip(name, data); }

        /// <summary>A sung/played note at a frequency (cached). Used by powers.</summary>
        public AudioClip NoteClip(float freq, float held = 0.35f, float[] timbre = null)
        {
            int key = Mathf.RoundToInt(freq * 10f) * 31 + Mathf.RoundToInt(held * 100f) + (timbre == null ? 0 : timbre.Length * 7);
            if (_noteCache.TryGetValue(key, out var c)) return c;
            c = Synth.ToClip("note" + freq, Synth.Render(freq, held, timbre ?? Synth.Voice, Envelope.Note, 0.32f, 0.04f, 1f, 5.5f, 0.008f));
            _noteCache[key] = c;
            return c;
        }

        // ---------------------------------------------------------------- drone

        void BuildDrone()
        {
            float R = Harmony.Root;
            AddVoice(R * 0.5f, Synth.Soft, 0.30f, 1, 0.07f);           // low D
            AddVoice(R * 0.75f, Synth.Soft, 0.16f, 1, 0.05f);          // fifth below (3/2 of low D)
            AddVoice(R, Synth.Sine, 0.12f, 2, 0.09f);                  // octave
            AddVoice(R * 1.25f, Synth.Sine, 0.07f, 3, 0.11f);          // major third (5th harmonic family)
            AddVoice(R * 1.5f, Synth.Sine, 0.07f, 4, 0.06f);           // fifth
            AddVoice(R * 3f, Synth.Sine, 0.035f, 5, 0.13f);            // 3rd harmonic shimmer
            AddVoice(R * 4f, Synth.Sine, 0.025f, 6, 0.17f);            // 4th harmonic
            AddVoice(R * 5f, Synth.Sine, 0.02f, 7, 0.21f);             // 5th harmonic
        }

        void AddVoice(float f, float[] timbre, float vol, int fromLevel, float lfoHz)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Synth.ToClip("drone" + f, Synth.RenderLoop(f, 2f, timbre, 0.9f));
            s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
            s.Play();
            _drone.Add(new Voice { src = s, baseVol = vol, fromLevel = fromLevel, lfoHz = lfoHz, lfoPhase = Random.value * 6.28f });
        }

        // ---------------------------------------------------------------- update

        /// <summary>
        /// Overall make-up gain. Measured in the real build (-lw-record-audio): mean RMS was ≈ −25 dBFS with peaks
        /// ≤ 0.37, i.e. too quiet with lots of headroom; ×1.5 lands around −21 dBFS with peaks ≈ 0.55 (no clipping).
        /// </summary>
        const float MixGain = 1.5f;

        void ReadSettings()
        {
            _music = Settings.MusicVolume * MixGain;
            _sfx = Settings.SfxVolume * MixGain;
        }

        void Update()
        {
            _settingsTimer -= Time.unscaledDeltaTime;
            if (_settingsTimer <= 0f) { _settingsTimer = 0.5f; ReadSettings(); EnsureListener(); }

            _droneMaster = Mathf.MoveTowards(_droneMaster, 1f, Time.unscaledDeltaTime * 0.3f);
            float t = Time.unscaledTime;
            float finaleDuck = _finaleStart >= 0 ? 0.5f : 1f;
            foreach (var v in _drone)
            {
                bool active = _layerLevel >= v.fromLevel;
                float lfo = 0.75f + 0.25f * Mathf.Sin(t * v.lfoHz * 6.283f + v.lfoPhase);
                float target = active ? v.baseVol * lfo * _music * _droneMaster * finaleDuck : 0f;
                v.src.volume = Mathf.MoveTowards(v.src.volume, target, Time.unscaledDeltaTime * 0.25f);
            }

            // Finale: voices swell in one by one (harmonic series 1:2:3:4:5:6).
            if (_finaleStart >= 0f)
            {
                float ft = Time.unscaledTime - _finaleStart;
                for (int i = 0; i < _finale.Count; i++)
                {
                    float target = ft > i * 1.0f ? 0.22f / (1f + i * 0.35f) * _music : 0f;
                    _finale[i].volume = Mathf.MoveTowards(_finale[i].volume, target, Time.unscaledDeltaTime * 0.12f);
                }
            }
        }

        /// <summary>Makes sure something is listening (the camera rig normally carries the listener).</summary>
        void EnsureListener()
        {
            var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            if (listeners.Length == 0 && _ownListener == null) _ownListener = gameObject.AddComponent<AudioListener>();
            else if (listeners.Length > 1 && _ownListener != null) { Destroy(_ownListener); _ownListener = null; }
        }

        // ---------------------------------------------------------------- API

        public void Play(string clip, float volume = 1f, float pitch = 1f)
        {
            if (!_clips.TryGetValue(clip, out var c)) return;
            PlayClip(c, volume, pitch);
        }

        public void PlayClip(AudioClip c, float volume = 1f, float pitch = 1f, double dspTime = 0)
        {
            if (c == null) return;
            var s = _pool[_poolNext];
            _poolNext = (_poolNext + 1) % _pool.Count;
            s.Stop();
            s.clip = c; s.pitch = pitch; s.volume = volume * _sfx;
            if (dspTime > 0) s.PlayScheduled(dspTime); else s.Play();
        }

        public void PlayNote(float freq, float held = 0.35f, float volume = 1f, float[] timbre = null)
        {
            PlayClip(NoteClip(freq, held, timbre), volume);
        }

        /// <summary>Starts (or keeps) a looping sustained tone, e.g. the shield's tonic hum.</summary>
        public void StartSustain(string key, float freq, float volume = 0.35f)
        {
            if (!_sustains.TryGetValue(key, out var s) || s == null)
            {
                s = gameObject.AddComponent<AudioSource>();
                s.clip = Synth.ToClip("sustain_" + key, Synth.RenderLoop(freq, 1f, Synth.Voice, 0.8f));
                s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f;
                _sustains[key] = s;
            }
            if (!s.isPlaying) s.Play();
            s.volume = Mathf.MoveTowards(s.volume, volume * _sfx, Time.unscaledDeltaTime * 3f);
        }

        public void StopSustain(string key)
        {
            if (_sustains.TryGetValue(key, out var s) && s != null && s.isPlaying)
            {
                s.volume = Mathf.MoveTowards(s.volume, 0f, Time.unscaledDeltaTime * 4f);
                if (s.volume <= 0.001f) s.Stop();
            }
        }

        /// <summary>Stops a sustained tone immediately (owner destroyed / scene change).</summary>
        public void StopSustainNow(string key)
        {
            if (_sustains.TryGetValue(key, out var s) && s != null) { s.volume = 0f; s.Stop(); }
        }

        public bool IsSustaining(string key) => _sustains.TryGetValue(key, out var s) && s != null && s.isPlaying;

        public static bool HasClip(string name) => Instance != null && Instance._clips.ContainsKey(name);

        // ---------------------------------------------------------------- events

        void OnJump() => Play("jump", 0.5f, Random.Range(0.97f, 1.04f));
        void OnLand() => Play("land", 0.6f, Random.Range(0.9f, 1.1f));

        void OnPower(PowerType p)
        {
            switch (p)
            {
                case PowerType.Dash: Play("dash", 0.7f); break;
                case PowerType.Echo: PlayNote(Harmony.EchoNote, 0.45f, 0.9f); PlayNote(Harmony.EchoNote * 0.5f, 0.5f, 0.4f, Synth.Soft); break;
                case PowerType.Pulse: PlayNote(Harmony.PulseNote, 0.25f, 0.9f, Synth.Soft); PlayNote(Harmony.PulseNote * 0.5f, 0.3f, 0.6f, Synth.Bright); break;
                case PowerType.Shield: PlayNote(Harmony.ShieldNote * 2f, 0.2f, 0.6f, Synth.Soft); break;
            }
        }

        void OnPowerFailed(PowerType p) => Play("fail", 0.6f);

        void OnDamaged(float amount, LightColor c)
        {
            if (Time.unscaledTime - _lastDamageSfx < 0.22f) return;
            _lastDamageSfx = Time.unscaledTime;
            string clip = c == LightColor.Red ? "hurt_red" : c == LightColor.Yellow ? "hurt_yellow" : "hurt";
            Play(clip, Mathf.Clamp(0.4f + amount * 0.03f, 0.4f, 0.9f), Random.Range(0.95f, 1.05f));
        }

        void OnHealed(float amount)
        {
            if (Time.unscaledTime - _lastHealSfx < 0.9f) return;
            _lastHealSfx = Time.unscaledTime;
            Play("heal", 0.5f);
        }

        void OnCheckpoint(int i) => Play("checkpoint", 0.8f);
        void OnShatter(Vector3 p) => Play("shatter", 0.8f, Random.Range(0.9f, 1.1f));
        void OnStun(Vector3 p) => Play("stun", 0.7f);
        void OnDied() => Play("death", 0.8f);
        void OnRespawn() => Play("respawn", 0.5f);
        void OnLevelCompleted(int n, float t) => Play("win", 0.8f);
        void OnWon() => Play("win", 0.9f);

        void OnLevelStarted(int n)
        {
            _layerLevel = Mathf.Max(1, n);
            StopFinale();
        }

        void OnBeat(int beat)
        {
            if (BeatClock.Instance == null) return;
            double next = BeatClock.Instance.NextBeatDsp();
            // Beat pulse from level 4, arpeggio (8th notes feel via every beat) from level 5.
            if (_layerLevel >= 4) PlayClip(_clips["pulse_beat"], 0.35f * _music / Mathf.Max(0.01f, _sfx), 1f, next);
            if (_layerLevel >= 5)
            {
                int deg = Arp[(beat + 1) & 7];
                PlayClip(NoteClip(Harmony.Degree(deg + 7), 0.12f, Synth.Soft), 0.22f * _music / Mathf.Max(0.01f, _sfx), 1f, next);
            }
            else if (_layerLevel >= 2 && ((beat + 1) % 4) == 0)
            {
                PlayClip(_clips["tick"], 0.25f * _music / Mathf.Max(0.01f, _sfx), 1f, next);
            }
        }

        void OnFinale()
        {
            if (_finaleStart >= 0f) return;
            _finaleStart = Time.unscaledTime;
            // Umbra sings: harmonic series 1:2:3:4:5:6 over the root, each voice with gentle vibrato.
            float baseF = Harmony.Root;
            for (int n = 1; n <= 6; n++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.clip = FinaleClip(n);
                s.loop = true; s.spatialBlend = 0f; s.volume = 0f; s.playOnAwake = false;
                s.Play();
                _finale.Add(s);
            }
            PlayNote(baseF * 2f, 1.2f, 0.8f);
        }

        static readonly AudioClip[] _finaleClips = new AudioClip[7];

        /// <summary>Finale voice n (harmonic n of the root). Rendering 6 loops takes ~80 ms, so levels with an
        /// ending pre-render them at load time (see <see cref="PrepareFinale"/>) to avoid a hitch.</summary>
        static AudioClip FinaleClip(int n)
        {
            if (_finaleClips[n] == null)
                _finaleClips[n] = Synth.ToClip("finale" + n, Synth.RenderLoop(Harmony.Root * n, 2f, Synth.Voice, 0.8f));
            return _finaleClips[n];
        }

        public static void PrepareFinale()
        {
            for (int n = 1; n <= 6; n++) FinaleClip(n);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void InstallFinalePrep()
        {
            LevelRuntime.Loaded += rt =>
            {
                if (rt != null && rt.Data != null && rt.Data.entities.Exists(e => e.type == "ending")) PrepareFinale();
            };
        }

        void StopFinale()
        {
            foreach (var s in _finale) if (s != null) Destroy(s);
            _finale.Clear();
            _finaleStart = -1f;
        }
    }
}
