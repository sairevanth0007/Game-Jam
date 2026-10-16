using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Danger and light-identity sounds, all synthesised (D just intonation) and pre-rendered at load:
    ///  - every Burn beam hums low and soft, every Lethal beam buzzes and crackles; both are spatial
    ///    (a <see cref="BeamHum"/> on the beam keeps its source at the point of the beam closest to the listener);
    ///  - a sizzle on Umbra rises in volume and brightness with damage per second;
    ///  - a heartbeat for near-lethal moments (<see cref="PlayHeartbeat"/>);
    ///  - first sight of green / blue / yellow light in a level gets a cue (shimmer / whoosh / charge), and yellow
    ///    beat beams near Umbra click on the beat.
    /// Respects the effects volume and fades out while paused.
    /// </summary>
    public class DangerAudio : MonoBehaviour
    {
        public static DangerAudio Instance { get; private set; }
        public AudioClip HumClip { get; private set; }
        public AudioClip BuzzClip { get; private set; }
        AudioClip _sizzle, _heartbeat, _shimmer, _whoosh, _charge, _click;
        AudioSource _sizzleSrc;
        AudioLowPassFilter _sizzleLp;
        float _dps, _scan, _cueScan, _levelTime;
        Transform _listener;
        float _listenerScan;
        readonly HashSet<LightColor> _cued = new HashSet<LightColor>();
        bool _yellowNear;
        float _lastHeartbeat = -10f;

        const float MixGain = 1.5f;
        public static float Sfx => Settings.SfxVolume * MixGain;

        void Awake()
        {
            Instance = this;
            BuildClips();
            _sizzleSrc = gameObject.AddComponent<AudioSource>();
            _sizzleSrc.clip = _sizzle; _sizzleSrc.loop = true; _sizzleSrc.playOnAwake = false; _sizzleSrc.spatialBlend = 0f; _sizzleSrc.volume = 0f;
            _sizzleLp = gameObject.AddComponent<AudioLowPassFilter>();
            _sizzleLp.cutoffFrequency = 2000f;
            GameEvents.LevelStarted += OnLevelStarted;
            BeatClock.OnBeat += OnBeat;
        }

        void OnDestroy()
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            BeatClock.OnBeat -= OnBeat;
            if (Instance == this) Instance = null;
        }

        void OnLevelStarted(int n) { _cued.Clear(); _levelTime = 0f; }

        // ------------------------------------------------------------------ clips (pre-rendered once)

        void BuildClips()
        {
            const int sr = Synth.SampleRate;
            float R = Harmony.Root;

            // Burn hum: D2 + A2 (3:2), gentle 2 Hz tremolo; 2 s loop (whole cycles, seamless).
            var hum = Synth.RenderLoop(R * 0.5f, 2f, Synth.Soft, 0.5f);
            var fifth = Synth.RenderLoop(R * 0.75f, 2f, Synth.Sine, 0.22f);
            for (int i = 0; i < hum.Length; i++)
            {
                float t = i / (float)sr;
                hum[i] = (hum[i] + fifth[i]) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 2f * t));
            }
            HumClip = Synth.ToClip("danger_hum", hum);

            // Lethal buzz: D3 against a minor second (16:15) in bright/hollow timbres (rough beating) + crackle.
            var buzz = Synth.RenderLoop(R, 2f, Synth.Bright, 0.32f);
            var rub = Synth.RenderLoop(R * 16f / 15f, 2f, Synth.Hollow, 0.26f);
            var crackle = Synth.CrackleLoop(2f, 55f, 0.5f, 7);
            for (int i = 0; i < buzz.Length; i++)
                buzz[i] = Synth.SoftClip((buzz[i] + rub[i]) * 1.4f + crackle[i]) * 0.6f;
            BuzzClip = Synth.ToClip("danger_buzz", buzz);

            // Sizzle: bright hiss with crackle (brightness via a low-pass on the source).
            var hiss = Synth.NoiseLoop(1.5f, 0.85f, 0.32f, 11);
            var sc = Synth.CrackleLoop(1.5f, 90f, 0.35f, 13);
            for (int i = 0; i < hiss.Length; i++) hiss[i] += sc[i];
            _sizzle = Synth.ToClip("danger_sizzle", hiss);

            // Heartbeat: lub-dub, low sine thumps gliding down.
            var lub = Synth.Render(R * 0.375f, 0.05f, new[] { 1f, 0.3f }, new Envelope(0.004f, 0.14f, 0f, 0.06f), 0.9f, 0.08f, 0.7f);
            var dub = Synth.Render(R * 0.33f, 0.05f, new[] { 1f, 0.25f }, new Envelope(0.004f, 0.16f, 0f, 0.08f), 0.65f, 0.06f, 0.7f);
            _heartbeat = Synth.ToClip("heartbeat", Synth.Mix(lub, dub, (int)(0.2f * sr)));

            // Green: soft shimmer, a quick sparkle of high just-intonation partials.
            var sh = new float[1];
            int[] degs = { 14, 18, 16, 21, 18, 23 };
            for (int i = 0; i < degs.Length; i++)
                sh = Synth.Mix(sh, Synth.Render(Harmony.Degree(degs[i]), 0.03f, Synth.Sine, new Envelope(0.004f, 0.5f, 0f, 0.3f), 0.12f), i * sr / 18);
            _shimmer = Synth.ToClip("cue_green", sh);

            // Blue: airy whoosh, noise swelling and falling with a sweeping low-pass.
            _whoosh = Synth.ToClip("cue_blue", Synth.Whoosh(0.9f, 0.4f, 17));

            // Yellow: a rising charge that snaps into a click.
            var ch = Synth.Render(Harmony.Degree(7), 0.28f, Synth.Bright, new Envelope(0.25f, 0.02f, 0.2f, 0.03f), 0.16f, 0.1f, 2f);
            var click = Synth.Render(Harmony.Degree(21), 0.004f, Synth.Soft, new Envelope(0.001f, 0.05f, 0f, 0.02f), 0.3f, 0.3f);
            _charge = Synth.ToClip("cue_yellow", Synth.Mix(ch, click, (int)(0.28f * sr)));
            _click = Synth.ToClip("beat_click", click);
        }

        // ------------------------------------------------------------------ runtime

        public Transform Listener
        {
            get
            {
                if (_listener == null || Time.unscaledTime > _listenerScan)
                {
                    _listenerScan = Time.unscaledTime + 1f;
                    var l = FindAnyObjectByType<AudioListener>();
                    _listener = l != null ? l.transform : null;
                }
                return _listener;
            }
        }

        public void PlayHeartbeat(float volume = 1f)
        {
            if (Time.unscaledTime - _lastHeartbeat < 0.5f) return;
            _lastHeartbeat = Time.unscaledTime;
            var ad = AudioDirector.Instance;
            if (ad != null) ad.PlayClip(_heartbeat, volume);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _levelTime += dt;
            bool paused = GameSession.Paused;

            // Sizzle follows damage per second (smoothed).
            var rec = LightReceiver.Current;
            float dps = 0f;
            if (rec != null && LevelRuntime.Current != null)
            {
                var hp = rec.GetComponent<PlayerHealth>();
                if (hp == null || !hp.IsDead) dps = rec.LastEffect.lethal ? 120f : rec.LastEffect.damagePerSecond;
            }
            _dps = Mathf.MoveTowards(_dps, dps, dt * (dps > _dps ? 400f : 160f));
            float k = Mathf.Clamp01(_dps / 70f);
            float vol = paused ? 0f : (k > 0.01f ? (0.12f + 0.45f * k) * Sfx : 0f);
            _sizzleSrc.volume = Mathf.MoveTowards(_sizzleSrc.volume, vol, dt * 3f);
            _sizzleLp.cutoffFrequency = Mathf.Lerp(1800f, 11000f, k);
            if (_sizzleSrc.volume > 0.001f) { if (!_sizzleSrc.isPlaying) _sizzleSrc.Play(); }
            else if (_sizzleSrc.isPlaying) _sizzleSrc.Pause();

            // Attach hums to dangerous beams as they register (cheap scan, 4×/s).
            _scan -= dt;
            if (_scan <= 0f)
            {
                _scan = 0.25f;
                var beams = LightField.Beams;
                for (int i = 0; i < beams.Count; i++)
                {
                    var b = beams[i];
                    if (b == null || b.GetComponent<BeamHum>() != null) continue;
                    if (b.Danger != LightBeam.BeamDanger.Safe) b.gameObject.AddComponent<BeamHum>();
                }
            }

            // First-sight cues for green / blue / yellow light, and "is a yellow beam near?" for the beat click.
            _cueScan -= dt;
            if (_cueScan <= 0f && LevelRuntime.Current != null)
            {
                _cueScan = 0.3f;
                ScanCues();
            }
        }

        void ScanCues()
        {
            var l = Listener;
            if (l == null) return;
            Vector2 c = l.position;
            _yellowNear = false;
            foreach (var b in LightField.Beams)
            {
                if (b == null || !b.on) continue;
                float d = BeamHum.DistanceToBeam(b, c, out _);
                if (d > 13f) continue;
                if (b.color == LightColor.Yellow || b.IsBeatGated) { _yellowNear |= d < 9f; Cue(LightColor.Yellow); }
                else if (b.color == LightColor.Green || b.color == LightColor.Cyan) Cue(LightColor.Green);
                else if (b.color == LightColor.Blue) Cue(LightColor.Blue);
            }
            foreach (var lamp in LightField.Lamps)
            {
                if (lamp == null || !lamp.on) continue;
                if (Vector2.Distance(lamp.transform.position, c) > 13f) continue;
                if (lamp.color == LightColor.Green || lamp.color == LightColor.Cyan) Cue(LightColor.Green);
                else if (lamp.color == LightColor.Blue) Cue(LightColor.Blue);
                else if (lamp.color == LightColor.Yellow || lamp.beatGated) Cue(LightColor.Yellow);
            }
        }

        void Cue(LightColor c)
        {
            if (_levelTime < 1.5f || _cued.Contains(c) || GameSession.Paused) return;
            _cued.Add(c);
            var ad = AudioDirector.Instance;
            if (ad == null) return;
            if (c == LightColor.Green) ad.PlayClip(_shimmer, 0.7f);
            else if (c == LightColor.Blue) ad.PlayClip(_whoosh, 0.75f);
            else if (c == LightColor.Yellow) ad.PlayClip(_charge, 0.6f);
        }

        void OnBeat(int beat)
        {
            if (!_yellowNear || GameSession.Paused || BeatClock.Instance == null) return;
            var ad = AudioDirector.Instance;
            if (ad != null) ad.PlayClip(_click, 0.35f, 1f, BeatClock.Instance.NextBeatDsp());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }

    /// <summary>
    /// Spatial danger sound on a beam: soft hum (Burn) or harsh buzz (Lethal). The source sits at the beam point
    /// nearest the listener, at the listener's depth, so falloff follows the distance to the beam line on screen.
    /// </summary>
    public class BeamHum : MonoBehaviour
    {
        LightBeam _beam;
        AudioSource _src;
        LightBeam.BeamDanger _danger;
        float _dangerCheck;

        void Awake()
        {
            _beam = GetComponent<LightBeam>();
            var go = new GameObject("DangerSound");
            go.transform.SetParent(transform, false);
            _src = go.AddComponent<AudioSource>();
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 1.5f;
            _src.maxDistance = 16f;
            _src.dopplerLevel = 0f;
            _src.volume = 0f;
            _dangerCheck = (Mathf.Abs(GetInstanceID()) % 50) / 100f;   // stagger checks (no UnityEngine.Random: keeps bot runs deterministic)
        }

        public static float DistanceToBeam(LightBeam b, Vector2 p, out Vector2 closest)
        {
            closest = b.transform.position;
            float best = float.MaxValue;
            var segs = b.Segments;
            for (int i = 0; i < segs.Count; i++)
            {
                Vector2 a = segs[i].start, e = segs[i].end;
                var ab = e - a;
                float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
                var q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d < best) { best = d; closest = q; }
            }
            return best < float.MaxValue ? Mathf.Sqrt(best) : Vector2.Distance(p, closest);
        }

        void Update()
        {
            var da = DangerAudio.Instance;
            if (_beam == null || da == null) return;
            _dangerCheck -= Time.unscaledDeltaTime;
            if (_dangerCheck <= 0f)
            {
                _dangerCheck = 0.5f;
                var d = _beam.Danger;
                if (d != _danger)
                {
                    _danger = d;
                    _src.Stop();
                    _src.clip = d == LightBeam.BeamDanger.Lethal ? da.BuzzClip : d == LightBeam.BeamDanger.Burn ? da.HumClip : null;
                    _src.timeSamples = 0;
                }
            }
            var l = da.Listener;
            float target = 0f;
            if (l != null && _src.clip != null && _beam.on && !GameSession.Paused)
            {
                Vector2 lp = l.position;
                DistanceToBeam(_beam, lp, out var q);
                _src.transform.position = new Vector3(q.x, q.y, l.position.z);
                target = (_danger == LightBeam.BeamDanger.Lethal ? 0.55f : 0.4f) * DangerAudio.Sfx * Mathf.Max(0.15f, _beam.VisualLevel);
            }
            _src.volume = Mathf.MoveTowards(_src.volume, target, Time.unscaledDeltaTime * 2f);
            bool audible = _src.volume > 0.001f;
            if (audible && !_src.isPlaying && _src.clip != null)
            {
                _src.timeSamples = (int)((Mathf.Abs(GetInstanceID()) * 7919L) % _src.clip.samples);   // desync identical loops
                _src.Play();
            }
            else if (!audible && _src.isPlaying) _src.Pause();
        }
    }
}
