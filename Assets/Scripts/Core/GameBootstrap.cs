using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LumenWave
{
    /// <summary>
    /// The only object a scene needs. On Awake it builds the whole game from code: input, notes, audio,
    /// camera (follow, sky, post-processing), ambient light, level loader, Umbra, HUD and the GameManager.
    /// Drop it into any empty scene and press Play; in the Editor its asset fields fill themselves.
    /// For builds use LumenWave > Create Game Scene, which saves a scene with the fields filled in.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameBootstrap : MonoBehaviour
    {
        public const string ControlsPath = "Assets/Scripts/Input/LumenControls.inputactions";
        public const string LevelsFolder = "Assets/Levels";
        public const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        public const string LitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

        public static GameBootstrap Instance { get; private set; }

        [Header("Assets (filled automatically in the Editor)")]
        [Tooltip("The LumenControls input actions.")]
        [SerializeField] InputActionAsset controls;
        [Tooltip("Level files in play order. In the Editor this is refreshed from Assets/Levels (sorted by name) on every Play.")]
        [SerializeField] TextAsset[] levels;
        [Tooltip("When on, the Editor keeps Levels in sync with every .txt in Assets/Levels.")]
        [SerializeField] bool autoFindLevels = true;
        [Tooltip("URP Sprite-Unlit-Default material (glowing things, UI-like sprites).")]
        [SerializeField] Material unlitSpriteMaterial;
        [Tooltip("URP Sprite-Lit-Default material (things lit by Light2D).")]
        [SerializeField] Material litSpriteMaterial;

        [Header("Start")]
        [Tooltip("Show the title screen first. Off = jump straight into the start level.")]
        [SerializeField] bool startOnTitle;
        [Tooltip("Index into Levels to start on.")]
        [SerializeField] int startLevel;

        [Header("World look")]
        [SerializeField] Color backgroundColor = new Color(0.02f, 0.015f, 0.04f);
        [Tooltip("Colour of the dim global light. The coloured lamps are the only strong light.")]
        [SerializeField] Color ambientColor = new Color(0.6f, 0.55f, 0.85f);
        [SerializeField, Range(0f, 1f)] float ambientIntensity = 0.32f;
        [Tooltip("Bloom + vignette.")]
        [SerializeField] bool postProcessing = true;
        [Tooltip("Slow floating motes in front of the camera.")]
        [SerializeField] bool ambientMotes = true;

        public InputActionAsset Controls => controls;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameBootstrap] A second GameBootstrap was found and removed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

#if UNITY_EDITOR
            AutoAssignAssets();
#endif
            if (controls == null)
                Debug.LogError("[GameBootstrap] No input actions assigned. Assign LumenControls in the Inspector (or run LumenWave > Create Game Scene).", this);
            if (levels == null || levels.Length == 0)
                Debug.LogError("[GameBootstrap] No level files assigned. Put .txt maps in Assets/Levels.", this);

            RuntimeArt.Init(unlitSpriteMaterial, litSpriteMaterial);
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            // Input first: everything else resolves its actions through LumenInput.
            LumenInput.Create(controls);

            var systems = new GameObject("Systems");
            systems.transform.SetParent(transform, false);
            systems.AddComponent<NoteInput>();
            CheckpointManager checkpoints = systems.AddComponent<CheckpointManager>();
            systems.AddComponent<InputRebinder>(); // loads saved key bindings
            systems.AddComponent<Fx>();

            var sfx = new GameObject("SFX");
            sfx.transform.SetParent(transform, false);
            sfx.AddComponent<SfxPlayer>();

            var notes = new GameObject("Note Sound");
            notes.transform.SetParent(transform, false);
            notes.AddComponent<NoteSound>();

            Camera cam = SetupCamera(out CameraController cameraController);
            SetupGlobalLight();
            if (postProcessing) SetupPostProcessing(cam);

            var loaderObject = new GameObject("Level Loader");
            loaderObject.transform.SetParent(transform, false);
            LevelLoader loader = loaderObject.AddComponent<LevelLoader>();
            loader.Configure(levels, cam);

            PlayerMovement player = UmbraBuilder.Create(Vector2.zero);
            cameraController.SetTarget(player.transform);

            Hud.Create();

            var flow = new GameObject("Game Manager");
            flow.transform.SetParent(transform, false);
            GameManager gameManager = flow.AddComponent<GameManager>();
            gameManager.Configure(player, checkpoints, loader, cameraController, startOnTitle, startLevel);
        }

        Camera SetupCamera(out CameraController controller)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            if (FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();

            controller = cam.GetComponent<CameraController>();
            if (controller == null) controller = cam.gameObject.AddComponent<CameraController>();

            // Sky gradient glued to the camera, behind everything.
            SpriteRenderer sky = RuntimeArt.CreateSprite("Sky", cam.transform, RuntimeArt.SkyGradient, Color.white, RuntimeArt.SortSky, true, new Vector3(0f, 0f, 20f));
            sky.transform.localScale = new Vector3(80f, 0.75f, 1f);

            if (ambientMotes) CreateMotes(cam.transform);
            return cam;
        }

        static void CreateMotes(Transform cameraTransform)
        {
            ParticleSystem motes = RuntimeArt.CreateParticles("Motes", cameraTransform, 120, -0.005f, RuntimeArt.SortFx - 1);
            motes.transform.localPosition = new Vector3(0f, 0f, 15f);
            var main = motes.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.45f, 1f, 0.35f), new Color(0.85f, 0.85f, 1f, 0.25f));
            var emission = motes.emission;
            emission.rateOverTime = 9f;
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 18f, 0.1f);
            var noise = motes.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;
            var size = motes.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f)));
        }

        void SetupGlobalLight()
        {
            Light2D global = null;
            foreach (Light2D existing in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (existing.lightType == Light2D.LightType.Global)
                {
                    global = existing;
                    break;
                }
            }

            if (global == null)
            {
                var go = new GameObject("Global Light 2D");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                global = go.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
                go.SetActive(true);
            }
            global.color = ambientColor;
            global.intensity = ambientIntensity;
        }

        void SetupPostProcessing(Camera cam)
        {
            var additional = cam.GetUniversalAdditionalCameraData();
            if (additional != null) additional.renderPostProcessing = true;

            var go = new GameObject("Post Processing");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.7f);
            bloom.intensity.Override(1.4f);
            bloom.scatter.Override(0.72f);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.4f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            volume.sharedProfile = profile;
        }

#if UNITY_EDITOR
        void Reset() => AutoAssignAssets();

        void OnValidate()
        {
            if (!Application.isPlaying) AutoAssignAssets();
        }

        /// <summary>Editor only: fills empty asset fields and keeps the level list in sync with Assets/Levels.</summary>
        public void AutoAssignAssets()
        {
            bool changed = false;
            if (controls == null)
            {
                controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
                changed |= controls != null;
            }
            if (unlitSpriteMaterial == null)
            {
                unlitSpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
                changed |= unlitSpriteMaterial != null;
            }
            if (litSpriteMaterial == null)
            {
                litSpriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
                changed |= litSpriteMaterial != null;
            }
            if (autoFindLevels || levels == null || levels.Length == 0)
            {
                TextAsset[] found = FindLevelFiles();
                if (!SameLevels(found, levels))
                {
                    levels = found;
                    changed = true;
                }
            }
            if (changed && !Application.isPlaying)
                EditorUtility.SetDirty(this);
        }

        /// <summary>All .txt files in Assets/Levels, sorted by file name (01_..., 02_...).</summary>
        public static TextAsset[] FindLevelFiles()
        {
            var paths = new List<string>();
            if (AssetDatabase.IsValidFolder(LevelsFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
            }
            paths.Sort(System.StringComparer.Ordinal);
            var result = new TextAsset[paths.Count];
            for (int i = 0; i < paths.Count; i++) result[i] = AssetDatabase.LoadAssetAtPath<TextAsset>(paths[i]);
            return result;
        }

        static bool SameLevels(TextAsset[] a, TextAsset[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
#endif
    }
}
