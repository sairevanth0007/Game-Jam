using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>
    /// The ending (GameEvents.FinaleStarted, ~10 s): Umbra sings. Harmonic rings (radii 1:2:3:4:5:6, coloured
    /// through the spectrum) pulse out on every beat; every beam and lamp fades from its hazard colour into
    /// warm white and breathes with the BeatClock; enemies fall quiet; the night turns to dawn; the camera
    /// slowly pulls back. Light damage is switched off for the whole sequence.
    /// Sequenced by a Timeline (Resources/Timelines/Finale, built by EditorTools.EnvAssets): a Signal track fires the
    /// discrete steps (rings on/off, harmonize lights, dawn peak) into this component, Activation tracks bring in the
    /// rising motes and a wide CinemachineCamera that the brain blends to. Continuous curves stay in Update.
    /// Without the asset it falls back to the same timings in code.
    /// </summary>
    public class FinaleDirector : MonoBehaviour, INotificationReceiver
    {
        public static FinaleDirector Instance { get; private set; }
        public static bool Active => Instance != null;
        public float Elapsed { get; private set; }

        static readonly Color Gold = new Color(1f, 0.84f, 0.6f);

        GameObject _player;
        UmbraRig _rig;
        UmbraScarf _scarf;
        float _scarfGravity, _scarfWind;

        struct BeamState { public LightBeam beam; public float intensity; public bool switched; }
        struct LampState { public AreaLamp lamp; public float intensity; public bool switched; }
        struct LightState { public Light light; public Color color; public float intensity; }
        struct RendState { public Renderer r; public string prop; public Color color; }
        readonly List<BeamState> _beams = new List<BeamState>();
        readonly List<LampState> _lamps = new List<LampState>();
        readonly List<LightState> _lights = new List<LightState>();
        readonly List<RendState> _glows = new List<RendState>();

        // Environment start values.
        Color _fog0, _amb0, _bg0, _moon0;
        float _fogD0, _moonI0;
        Material _sky;
        Color _skyTop0, _skyHor0, _skyBot0;
        Bloom _bloom; ColorAdjustments _color; Vignette _vig;
        float _bloom0, _exp0, _vig0, _sat0;
        ParticleSystem _motes;
        int _lastBeat = -1;
        PlayableDirector _director;
        CinemachineCamera _wideCam;
        bool _ringsOn, _harmonized;
        CinemachineBlendDefinition _blend0;
        public bool TimelineDriven => _director != null;
        public PlayableDirector Director => _director;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            GameEvents.FinaleStarted -= Begin;
            GameEvents.FinaleStarted += Begin;
        }

        /// <summary>Starts the finale (also callable directly, e.g. from tests).</summary>
        public static void Begin()
        {
            if (Instance != null) return;
            var go = new GameObject("FinaleDirector");
            if (LevelRuntime.Current != null) go.transform.SetParent(LevelRuntime.Current.transform, false);
            go.AddComponent<FinaleDirector>();
        }

        void Awake()
        {
            Instance = this;
            CapturePlayer();
            CaptureLights();
            CalmEnemies();
            CaptureEnvironment();
            _motes = MakeMotes();
            SetupTimeline();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            var rig = SceneRig.Instance;
            if (rig != null && rig.CameraRig != null && rig.CameraRig.Brain != null && _wideCam != null) rig.CameraRig.Brain.DefaultBlend = _blend0;
            if (_wideCam != null) Destroy(_wideCam.gameObject);
        }

        void SetupTimeline()
        {
            var tl = Resources.Load<TimelineAsset>(FinaleTimeline.Resource);
            if (tl == null) return;
            // Wide shot: same follow as the gameplay camera, pulled back and raised (blended by the brain).
            var rig = SceneRig.Instance;
            if (rig != null && rig.CameraRig != null && rig.CameraRig.VirtualCamera != null && _player != null)
            {
                var src = rig.CameraRig.VirtualCamera;
                var go = new GameObject("CM FinaleWide");
                go.transform.SetParent(src.transform.parent, false);
                go.transform.rotation = src.transform.rotation;
                go.SetActive(false);
                _wideCam = go.AddComponent<CinemachineCamera>();
                _wideCam.Lens = src.Lens;
                _wideCam.Priority = 50;
                _wideCam.Follow = _player.transform;
                var comp = go.AddComponent<CinemachinePositionComposer>();
                var srcComp = src.GetComponent<CinemachinePositionComposer>();
                comp.CameraDistance = (srcComp != null ? srcComp.CameraDistance : CameraRig.DefaultDistance) + 3.5f;
                comp.Composition = srcComp != null ? srcComp.Composition : ScreenComposerSettings.Default;
                comp.Damping = new Vector3(1f, 1f, 1f);
                comp.TargetOffset = new Vector3(0f, 1.2f, 0f);
                var conf = src.GetComponent<CinemachineConfiner3D>();
                if (conf != null) go.AddComponent<CinemachineConfiner3D>().BoundingVolume = conf.BoundingVolume;
                go.AddComponent<CinemachineImpulseListener>().UseCameraSpace = true;
                if (rig.CameraRig.Brain != null)
                {
                    _blend0 = rig.CameraRig.Brain.DefaultBlend;
                    rig.CameraRig.Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 9f);
                }
            }
            _director = gameObject.AddComponent<PlayableDirector>();
            _director.playOnAwake = false;
            _director.timeUpdateMode = DirectorUpdateMode.GameTime;
            _director.extrapolationMode = DirectorWrapMode.Hold;
            _director.playableAsset = tl;
            foreach (var output in tl.outputs)
            {
                if (output.sourceObject is not TrackAsset track) continue;
                if (track.name == FinaleTimeline.StepsTrack) _director.SetGenericBinding(track, gameObject);
                else if (track.name == FinaleTimeline.MotesTrack && _motes != null) _director.SetGenericBinding(track, _motes.gameObject);
                else if (track.name == FinaleTimeline.CameraTrack && _wideCam != null) _director.SetGenericBinding(track, _wideCam.gameObject);
            }
            _director.Play();
        }

        /// <summary>Timeline signals (Signal track "Steps").</summary>
        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (notification is not SignalEmitter e || e.asset == null) return;
            switch (e.asset.name)
            {
                case FinaleTimeline.RingsOn: _ringsOn = true; break;
                case FinaleTimeline.RingsOff: _ringsOn = false; break;
                case FinaleTimeline.Harmonize: _harmonized = true; break;
                case FinaleTimeline.DawnPeak: CameraRig.Shake(0.15f); break;
            }
        }

        // ------------------------------------------------------------------ setup

        void CapturePlayer()
        {
            _player = LevelRuntime.Current != null ? LevelRuntime.Current.Player : null;
            if (_player == null) return;
            // Umbra stops and sings; the light can no longer hurt them.
            var ctl = _player.GetComponent<UmbraController>();
            if (ctl != null) ctl.InputLocked = true;
            var rec = _player.GetComponent<LightReceiver>();
            if (rec != null) rec.enabled = false;
            var body = _player.GetComponent<IPlayerBody>();
            if (body != null) { body.GravityScale = 1f; body.Velocity = new Vector2(0f, Mathf.Min(0f, body.Velocity.y)); }
            var h = _player.GetComponent<PlayerHealth>();
            if (h != null) { h.godMode = true; h.Heal(h.max); }
            _rig = _player.GetComponentInChildren<UmbraRig>();
            _scarf = FindAnyObjectByType<UmbraScarf>();
            if (_scarf != null) { _scarfGravity = _scarf.gravity; _scarfWind = _scarf.wind; }
        }

        void CaptureLights()
        {
            foreach (var b in FindObjectsByType<LightBeam>(FindObjectsSortMode.None))
                _beams.Add(new BeamState { beam = b, intensity = b.intensity });
            foreach (var l in FindObjectsByType<AreaLamp>(FindObjectsSortMode.None))
            {
                _lamps.Add(new LampState { lamp = l, intensity = l.intensity });
                foreach (var r in l.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer) continue;
                    var m = r.sharedMaterial;
                    if (m == null) continue;
                    string prop = m.HasProperty("_Color") ? "_Color" : m.HasProperty("_BaseColor") ? "_BaseColor" : null;
                    if (prop == null) continue;
                    var inst = r.material;   // per-renderer instance; the finale owns these now
                    _glows.Add(new RendState { r = r, prop = prop, color = inst.GetColor(prop) });
                }
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) continue;
                _lights.Add(new LightState { light = l, color = l.color, intensity = l.intensity });
            }
        }

        void CalmEnemies()
        {
            foreach (var s in FindObjectsByType<Sentinel>(FindObjectsSortMode.None))
            {
                s.enabled = false;   // stops sweeping/stun logic; its beam is harmonised with the others
                if (s.Beam != null) { s.Beam.on = true; s.Beam.sweepDegrees = 0f; }
                s.gameObject.AddComponent<FinaleCalm>().Init(false);
            }
            foreach (var w in FindObjectsByType<Wisp>(FindObjectsSortMode.None)) { w.enabled = false; w.gameObject.AddComponent<FinaleCalm>().Init(true); }
            foreach (var m in FindObjectsByType<EchoMoth>(FindObjectsSortMode.None)) { m.enabled = false; m.gameObject.AddComponent<FinaleCalm>().Init(true); }
            foreach (var f in FindObjectsByType<Flare>(FindObjectsSortMode.None)) { f.ClearBolts(); f.enabled = false; f.gameObject.AddComponent<FinaleCalm>().Init(false); }
        }

        void CaptureEnvironment()
        {
            _fog0 = RenderSettings.fogColor; _fogD0 = RenderSettings.fogDensity; _amb0 = RenderSettings.ambientLight;
            var rig = SceneRig.Instance;
            if (rig != null)
            {
                if (rig.Camera != null) _bg0 = rig.Camera.backgroundColor;
                if (rig.Moonlight != null) { _moon0 = rig.Moonlight.color; _moonI0 = rig.Moonlight.intensity; }
                var prof = rig.Volume != null ? rig.Volume.sharedProfile : null;
                if (prof != null)
                {
                    if (prof.TryGet(out _bloom)) _bloom0 = _bloom.intensity.value;
                    if (prof.TryGet(out _color)) { _exp0 = _color.postExposure.value; _sat0 = _color.saturation.value; }
                    if (prof.TryGet(out _vig)) _vig0 = _vig.intensity.value;
                }
            }
            var skyGo = GameObject.Find("Sky");
            var sr = skyGo != null ? skyGo.GetComponent<Renderer>() : null;
            if (sr != null && sr.sharedMaterial != null && sr.sharedMaterial.HasProperty("_TopColor"))
            {
                _sky = sr.material;
                _skyTop0 = _sky.GetColor("_TopColor"); _skyHor0 = _sky.GetColor("_HorizonColor"); _skyBot0 = _sky.GetColor("_BottomColor");
            }
        }

        ParticleSystem MakeMotes()
        {
            var cam = SceneRig.Instance != null ? SceneRig.Instance.Camera : Camera.main;
            Vector3 c = _player != null ? _player.transform.position : (cam != null ? cam.transform.position : Vector3.zero);
            var go = new GameObject("FinaleMotes");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(c.x, c.y - 1.5f, 1.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), new Color(0.7f, 0.9f, 1f));
            main.gravityModifier = -0.04f;
            main.maxParticles = 800;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 0.5f, 8f);
            shape.rotation = new Vector3(-90f, 0f, 0f);   // emit upward
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f; noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MaterialLibrary.Get("GlowParticle");
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ timeline

        void Update()
        {
            float dt = Time.deltaTime;
            Elapsed += dt;
            float t = Elapsed;
            float sing = Smooth(0.1f, 1.4f, t);
            float harmony = Smooth(1.0f, 5.0f, t);     // hazard colours → warm white
            float dawn = Smooth(0.8f, 8.5f, t);        // night → dawn
            float phase = BeatClock.CurrentPhase;
            float beatPulse = Mathf.Exp(-phase * 5f);   // 1 on the beat, decays through it

            // Umbra
            if (_rig != null) _rig.Singing = sing;
            if (_scarf != null)
            {
                _scarf.gravity = Mathf.Lerp(_scarfGravity, -2.2f, sing);   // scarf lifts and flows upward
                _scarf.wind = Mathf.Lerp(_scarfWind, 3.2f, sing);
            }
            if (_player != null)
            {
                var body = _player.GetComponent<IPlayerBody>();
                if (body != null) body.GravityScale = 1f;
            }

            // Beams: dim out of their hazard colour, switch to neutral white, swell back gently on the beat.
            for (int i = 0; i < _beams.Count; i++)
            {
                var s = _beams[i];
                if (s.beam == null) continue;
                float baseI = Mathf.Clamp(s.intensity, 0.6f, 1.6f);
                if (!s.switched)
                {
                    s.beam.intensity = s.intensity * Mathf.Clamp01(1f - harmony * 2.2f);
                    if (harmony >= 0.45f || _harmonized)
                    {
                        s.switched = true;
                        s.beam.color = LightColor.White;   // white is neutral; through a prism it becomes a spectrum
                        s.beam.beatGated = false;
                        s.beam.sweepDegrees *= 0.25f;
                        s.beam.on = true;
                    }
                }
                else
                {
                    float up = Smooth(0.45f, 1f, harmony);
                    s.beam.intensity = baseI * up * (0.75f + 0.4f * beatPulse);
                }
                _beams[i] = s;
            }

            for (int i = 0; i < _lamps.Count; i++)
            {
                var s = _lamps[i];
                if (s.lamp == null) continue;
                if (!s.switched && (harmony >= 0.45f || _harmonized))
                {
                    s.switched = true;
                    s.lamp.color = LightColor.White;
                    s.lamp.beatGated = false;
                    s.lamp.on = true;
                }
                s.lamp.intensity = Mathf.Max(0.4f, s.intensity) * (0.8f + 0.3f * beatPulse);
                _lamps[i] = s;
            }

            foreach (var g in _glows)
            {
                if (g.r == null) continue;
                float mag = Mathf.Max(1f, g.color.maxColorComponent);
                var target = Gold * mag * (0.85f + 0.35f * beatPulse);
                target.a = g.color.a;
                g.r.material.SetColor(g.prop, Color.Lerp(g.color, target, harmony));
            }

            foreach (var l in _lights)
            {
                if (l.light == null) continue;
                l.light.color = Color.Lerp(l.color, Gold, harmony);
                l.light.intensity = Mathf.Lerp(l.intensity, Mathf.Max(l.intensity, 1.5f) * (0.85f + 0.3f * beatPulse), harmony);
            }

            // Harmonic rings on every beat.
            var clock = BeatClock.Instance;
            int beat = clock != null ? clock.Beat : Mathf.FloorToInt(t / Harmony.SecondsPerBeat);
            if (beat != _lastBeat)
            {
                _lastBeat = beat;
                if (_director != null ? _ringsOn : (t > 0.3f && t < 11.5f)) EmitRings();
            }

            // Environment → dawn.
            var dawnMood = Mood.Get("dawn");
            RenderSettings.fogColor = Color.Lerp(_fog0, dawnMood.fog * 1.6f, dawn);
            RenderSettings.fogDensity = Mathf.Lerp(_fogD0, dawnMood.fogDensity, dawn);
            RenderSettings.ambientLight = Color.Lerp(_amb0, dawnMood.ambient * 1.6f, dawn);
            var rig = SceneRig.Instance;
            if (rig != null)
            {
                if (rig.Camera != null) rig.Camera.backgroundColor = Color.Lerp(_bg0, dawnMood.skyBottom, dawn);
                if (rig.Moonlight != null)
                {
                    rig.Moonlight.color = Color.Lerp(_moon0, new Color(1f, 0.82f, 0.62f), dawn);
                    rig.Moonlight.intensity = Mathf.Lerp(_moonI0, 1.1f, dawn);
                }
                if (rig.CameraRig != null && _wideCam == null)
                {
                    float pull = Smooth(0.5f, 10f, t);
                    rig.CameraRig.extraDistance = 3.5f * pull;
                    rig.CameraRig.extraOffset = new Vector2(0f, 1.2f * pull);
                }
            }
            if (_bloom != null) _bloom.intensity.Override(Mathf.Lerp(_bloom0, 1.9f, dawn) + 0.25f * beatPulse * sing);
            if (_color != null)
            {
                _color.postExposure.Override(Mathf.Lerp(_exp0, _exp0 + 0.4f, dawn));
                _color.saturation.Override(Mathf.Lerp(_sat0, _sat0 + 10f, dawn));
            }
            if (_vig != null) _vig.intensity.Override(Mathf.Lerp(_vig0, 0.24f, dawn));
            if (_sky != null)
            {
                _sky.SetColor("_TopColor", Color.Lerp(_skyTop0, dawnMood.skyTop * 1.4f, dawn));
                _sky.SetColor("_HorizonColor", Color.Lerp(_skyHor0, dawnMood.skyHorizon * 1.5f, dawn));
                _sky.SetColor("_BottomColor", Color.Lerp(_skyBot0, dawnMood.skyBottom * 1.3f, dawn));
            }
            if (_motes != null)
            {
                var em = _motes.emission;
                em.rateOverTime = Mathf.Lerp(0f, 90f, Smooth(0.3f, 4f, t));
                if (_player != null)
                {
                    var p = _player.transform.position;
                    _motes.transform.position = new Vector3(p.x, p.y - 1.5f, 1.5f);
                }
            }
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // Harmonic series spectrum: fundamental red → 6th harmonic violet.
        static readonly Color[] RingColors =
        {
            new Color(1f, 0.25f, 0.2f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.92f, 0.35f),
            new Color(0.35f, 1f, 0.5f), new Color(0.3f, 0.65f, 1f), new Color(0.75f, 0.4f, 1f),
        };

        void EmitRings()
        {
            Vector3 c = _player != null ? _player.transform.position + Vector3.up * 1.2f : transform.position;
            for (int n = 1; n <= 6; n++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "HarmonicRing" + n;
                Backdrop.RemoveCollider(q);
                Backdrop.Shadowless(q);
                q.transform.SetParent(transform, false);
                q.transform.position = new Vector3(c.x, c.y, 0.7f);   // just behind Umbra, so the silhouette stays crisp
                q.AddComponent<HarmonicRing>().Init(n, RingColors[n - 1]);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }

    /// <summary>One ring of a harmonic group: radius n·b(t), so the six rings keep the ratios 1:2:3:4:5:6 as they grow.</summary>
    public class HarmonicRing : MonoBehaviour
    {
        const float Half = 10f;       // quad half-size in world units
        const float Life = 2.6f;
        int _n; Color _c; float _t;
        Renderer _r; MaterialPropertyBlock _mpb;

        public void Init(int n, Color c)
        {
            _n = n; _c = c;
            transform.localScale = new Vector3(Half * 2f, Half * 2f, 1f);
            _r = GetComponent<Renderer>();
            _r.sharedMaterial = MaterialLibrary.Get("Wave");
            _mpb = new MaterialPropertyBlock();
            Apply();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= Life) { Destroy(gameObject); return; }
            Apply();
        }

        void Apply()
        {
            float k = _t / Life;
            float b = 0.55f + 1.0f * (1f - Mathf.Pow(1f - k, 2f));   // fundamental radius 0.55 → 1.55 u (6th → 9.3 u)
            float radius = _n * b / Half;
            float fade = (1f - k) * (1f - k) * Mathf.Clamp01(_t * 8f);
            _r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", _c * (0.75f + 0.08f * _n));   // inner rings dimmer: Umbra stays a black silhouette
            _mpb.SetFloat("_Radius", radius);
            _mpb.SetFloat("_Thickness", 0.012f + 0.004f * _n);
            _mpb.SetFloat("_Alpha", fade);
            _r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Calms an enemy during the finale: it rises gently and fades away (or simply settles, for sentinels).</summary>
    public class FinaleCalm : MonoBehaviour
    {
        bool _vanish; float _t; Vector3 _scale0, _pos0;

        public void Init(bool vanish) { _vanish = vanish; _scale0 = transform.localScale; _pos0 = transform.position; }

        void Update()
        {
            _t += Time.deltaTime;
            if (_vanish)
            {
                float k = Mathf.Clamp01(_t / 3f);
                transform.position = _pos0 + Vector3.up * (1.5f * k);
                transform.localScale = _scale0 * (1f - k);
                if (k >= 1f) gameObject.SetActive(false);
            }
            else
            {
                // Sentinels sway softly instead of sweeping.
                transform.position = _pos0 + Vector3.up * (0.2f * Mathf.Sin(_t * 1.3f));
            }
        }
    }
}
