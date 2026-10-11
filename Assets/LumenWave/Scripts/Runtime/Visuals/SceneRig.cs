using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LumenWave
{
    /// <summary>
    /// Per-scene visual rig created on <see cref="SceneBootstrap.Booting"/>: camera (+ follow), moonlight,
    /// global post-processing volume, fog and ambient. Also hooks tile styling and the parallax backdrop.
    /// </summary>
    public class SceneRig : MonoBehaviour
    {
        public static SceneRig Instance { get; private set; }

        public Camera Camera { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public Light Moonlight { get; private set; }
        public Volume Volume { get; private set; }
        public Mood Mood { get; private set; }
        public string Kind { get; private set; }
        public bool HasBloom => Volume != null && Volume.sharedProfile != null && Volume.sharedProfile.Has<Bloom>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            SceneBootstrap.Booting += OnBooting;
            LevelBuilder.TileStyler = TileStyle.Style;
            LevelBuilder.PostBuild += OnPostBuild;
            LevelRuntime.Loaded += OnLevelLoaded;
            GameEvents.PlayerDamaged += (amount, c) => CameraRig.Shake(Mathf.Clamp(amount / 40f, 0.05f, 0.6f));
            GameEvents.PlayerDied += () => CameraRig.Shake(0.7f);
            GameEvents.CrystalShattered += p => CameraRig.Shake(0.25f);
        }

        static void OnBooting(string kind, int level)
        {
            Mood mood = Mood.Blue;
            if (kind == "level")
            {
                try { mood = Mood.Get((SceneBootstrap.BootingData ?? LevelDatabase.Load(level)).Get("mood", "blue")); } catch { }
            }
            else if (kind == "end") mood = Mood.Get("dawn");
            var rig = Create(kind, mood);
            if (kind != "level")
            {
                // Menu scenes: a static, slowly drifting vista behind the UI.
                Backdrop.Build(rig.transform, 48, 14, mood, kind == "end" ? 77 : 31, null, true);
                rig.CameraRig.SetBounds(new Rect(0f, -1.5f, 48f, 20f));
                rig.Camera.transform.position = new Vector3(24f, 5f, -CameraRig.DefaultDistance);
                rig.gameObject.AddComponent<MenuCameraDrift>();
            }
        }

        /// <summary>Creates (or replaces) the rig for the current scene.</summary>
        public static SceneRig Create(string kind, Mood mood)
        {
            if (Instance != null) Destroy(Instance.gameObject);
            var go = new GameObject("SceneRig");
            var rig = go.AddComponent<SceneRig>();
            Instance = rig;
            rig.Kind = kind;
            rig.Build(mood);
            return rig;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(Mood mood)
        {
            Mood = mood;
            // Camera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = new Vector3(0f, 4f, -CameraRig.DefaultDistance);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 260f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = mood.skyBottom;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.renderShadows = true;
            if (FindAnyObjectByType<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            Camera = cam;
            CameraRig = camGo.AddComponent<CameraRig>();
            MoonDisc.Create(cam, mood);

            // Moonlight: cool, dim, from upper-left behind the camera so platform tops catch it.
            var lgo = new GameObject("Moonlight");
            lgo.transform.SetParent(transform, false);
            lgo.transform.rotation = Quaternion.Euler(48f, 28f, 0f);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = mood.moonlight;
            l.intensity = 0.42f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.85f;
            Moonlight = l;

            // Fog + ambient
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = mood.fogDensity;
            RenderSettings.fogColor = mood.fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = mood.ambient;
            // No skybox reflections: the default sky cubemap makes grazing stone look like ice.
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0.2f;

            // Post-processing
            var vgo = new GameObject("PostFX");
            vgo.transform.SetParent(transform, false);
            var vol = vgo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            vol.sharedProfile = CreateProfile(mood);
            Volume = vol;
        }

        public static VolumeProfile CreateProfile(Mood mood)
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "LumenWavePostFX";
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.72f);
            bloom.highQualityFiltering.Override(true);
            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.55f);
            ca.contrast.Override(18f);
            ca.saturation.Override(8f);
            ca.colorFilter.Override(Color.Lerp(Color.white, mood.edge, 0.05f));
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium2);
            grain.intensity.Override(0.28f);
            grain.response.Override(0.75f);
            var chroma = p.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.08f);
            var smh = p.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.08f, 0f));
            // Per-mood split toning: shadows lean toward the fog hue, highlights toward the mood's edge light.
            var split = p.Add<SplitToning>(true);
            var sh = mood.fog; float shMax = Mathf.Max(0.001f, sh.maxColorComponent);
            split.shadows.Override(Color.Lerp(Color.gray, new Color(sh.r / shMax, sh.g / shMax, sh.b / shMax), 0.55f));
            var hi = mood.edge; float hiMax = Mathf.Max(0.001f, hi.maxColorComponent);
            split.highlights.Override(Color.Lerp(Color.gray, new Color(hi.r / hiMax, hi.g / hiMax, hi.b / hiMax), 0.12f));   // subtle: light colours are gameplay rules
            split.balance.Override(-15f);
            return p;
        }

        static void OnPostBuild(LevelContext ctx)
        {
            var mood = Instance != null ? Instance.Mood : Mood.Get(ctx.data.Get("mood", "blue"));
            Backdrop.Build(ctx.root, ctx.data.Width, ctx.data.Height, mood, ctx.levelNumber * 7919 + ctx.data.Width, ctx.data);
        }

        static void OnLevelLoaded(LevelRuntime rt)
        {
            if (Instance == null || Instance.CameraRig == null) return;
            var d = rt.Data;
            Instance.CameraRig.SetBounds(new Rect(0f, -1.5f, Mathf.Max(1, d.Width), Mathf.Max(1, d.Height) + 6f));
            if (rt.Player != null) Instance.CameraRig.SetTarget(rt.Player.transform);
            Instance.CameraRig.SnapToTarget();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }
}
