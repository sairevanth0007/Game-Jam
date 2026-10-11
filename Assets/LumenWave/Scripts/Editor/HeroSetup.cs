using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Builds Umbra's skinned hero assets from the CC0 Quaternius "Animated Human":
    ///  - Resources/Hero/UmbraAnimator.controller (base layer: Idle, Locomotion walk→run blend, Jump, Fall,
    ///    Land, Dash, Death, Respawn; override layer "Cast" with an arms+head mask playing Punch)
    ///  - Resources/Hero/UmbraModel.prefab (model + Animator + controller) loaded at runtime by UmbraRig.
    /// Idempotent: assets are only rebuilt when missing or when <see cref="Version"/> changes.
    /// Called from ProjectSetup.Run (scripts/compile.ps1 -Setup) and menu LumenWave/Build Hero.
    /// </summary>
    public static class HeroSetup
    {
        public const string ModelPath = "Assets/LumenWave/ThirdParty/QuaterniusHuman/Resources/Art/QuaterniusHuman/AnimatedHuman.fbx";
        public const string Dir = "Assets/LumenWave/Resources/Hero";
        public const string ControllerPath = Dir + "/UmbraAnimator.controller";
        public const string PrefabPath = Dir + "/UmbraModel.prefab";
        const int Version = 3;
        static string VersionParam => "v" + Version;

        [MenuItem("LumenWave/Build Hero")]
        public static void Ensure() => Ensure(false);

        public static void Ensure(bool force)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogWarning("[LumenWave] Hero model missing: " + ModelPath + " (runtime falls back to the primitive rig)"); return; }
            System.IO.Directory.CreateDirectory(Dir);

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            bool rebuild = force || ctrl == null || !ctrl.parameters.Any(p => p.name == VersionParam)
                           || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            if (!rebuild) { Debug.Log("[LumenWave] Hero assets up to date (" + VersionParam + ")"); return; }

            // Re-import the model so ArtImport's per-clip root settings are current before we reference clips.
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (ctrl != null) AssetDatabase.DeleteAsset(ControllerPath);
            ctrl = BuildController();
            BuildPrefab(model, ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[LumenWave] Hero assets built: " + ControllerPath + ", " + PrefabPath);
        }

        static AnimationClip Clip(string shortName)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == "Human Armature|" + shortName);
            if (clip == null) Debug.LogWarning("[LumenWave] Hero clip missing: " + shortName);
            return clip;
        }

        static AnimatorController BuildController()
        {
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(VersionParam, AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);     // |vx| / maxSpeed (0..1+)
            ctrl.AddParameter("LocoRate", AnimatorControllerParameterType.Float);  // playback rate of locomotion
            ctrl.AddParameter("AirTime", AnimatorControllerParameterType.Float);   // normalized time in the jump clip
            ctrl.AddParameter("CastTime", AnimatorControllerParameterType.Float);  // normalized time in the punch clip

            var idle = Clip("Idle"); var walk = Clip("Walk"); var run = Clip("Run");
            var jump = Clip("Jump"); var punch = Clip("Punch"); var death = Clip("Death");

            var layer = ctrl.layers[0];
            layer.name = "Base";
            layer.iKPass = true;
            var sm = layer.stateMachine;

            var sIdle = sm.AddState("Idle", new Vector3(300, 0));
            sIdle.motion = idle;

            var tree = new BlendTree { name = "WalkRun", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(walk, 0.15f);
            tree.AddChild(run, 0.8f);
            var sLoco = sm.AddState("Locomotion", new Vector3(300, 80));
            sLoco.motion = tree;
            sLoco.speedParameterActive = true; sLoco.speedParameter = "LocoRate";

            var sJump = sm.AddState("Jump", new Vector3(560, 0));
            sJump.motion = jump; sJump.timeParameterActive = true; sJump.timeParameter = "AirTime";
            var sFall = sm.AddState("Fall", new Vector3(560, 80));
            sFall.motion = jump; sFall.timeParameterActive = true; sFall.timeParameter = "AirTime";
            var sLand = sm.AddState("Land", new Vector3(560, 160));
            sLand.motion = jump; sLand.timeParameterActive = true; sLand.timeParameter = "AirTime";

            var sDash = sm.AddState("Dash", new Vector3(300, 160));
            sDash.motion = run; sDash.speed = 1.7f;

            var sDeath = sm.AddState("Death", new Vector3(300, 240));
            sDeath.motion = death;
            var sRespawn = sm.AddState("Respawn", new Vector3(560, 240));
            sRespawn.motion = idle;
            var back = sRespawn.AddTransition(sIdle);
            back.hasExitTime = true; back.exitTime = 0.05f; back.duration = 0.2f;

            sm.defaultState = sIdle;

            // Cast: arms + head play the punch, driven by CastTime; layer weight set from code.
            var mask = new AvatarMask { name = "UmbraUpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool on = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                       || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                       || part == AvatarMaskBodyPart.Head || part == AvatarMaskBodyPart.Body;
                mask.SetHumanoidBodyPartActive(part, on);
            }
            AssetDatabase.AddObjectToAsset(mask, ctrl);
            var castSm = new AnimatorStateMachine { name = "Cast", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(castSm, ctrl);
            var sCast = castSm.AddState("Cast", new Vector3(300, 0));
            sCast.motion = punch; sCast.timeParameterActive = true; sCast.timeParameter = "CastTime";
            castSm.defaultState = sCast;
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Cast", stateMachine = castSm, avatarMask = mask, defaultWeight = 0f,
                blendingMode = AnimatorLayerBlendingMode.Override, iKPass = true,
            });

            var layers = ctrl.layers; layers[0] = layer; ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void BuildPrefab(GameObject model, AnimatorController ctrl)
        {
            var go = Object.Instantiate(model);
            go.name = "UmbraModel";
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LumenWave/Resources/Materials/Umbra.mat");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (mat != null) smr.sharedMaterial = mat;
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = false;
                smr.skinnedMotionVectors = false;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 9; // Layers.Player
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
        }

        public static void EnsureBatch()
        {
            try { Ensure(true); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[LumenWave] Hero setup failed: " + e); EditorApplication.Exit(1); }
        }

        /// <summary>Batch probe: samples each clip and logs key bone heights (root space, unscaled model).</summary>
        public static void ProbeClipsBatch()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var go = Object.Instantiate(model);
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            Debug.Log($"[LumenWave] humanScale={anim.humanScale} isHuman={anim.isHuman} meshH={smr.sharedMesh.bounds.size.y * smr.transform.lossyScale.y}");
            Transform B(HumanBodyBones b) => anim.GetBoneTransform(b);
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
            {
                var graph = PlayableGraph.Create("probe");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", anim);
                var cp = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(cp);
                for (int i = 0; i <= 10; i++)
                {
                    float t = clip.length * i / 10f;
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, t);
                    AnimationMode.EndSampling();
                    var hips = B(HumanBodyBones.Hips).position; var head = B(HumanBodyBones.Head).position;
                    var lf = B(HumanBodyBones.LeftFoot).position; var rf = B(HumanBodyBones.RightFoot).position;
                    var lh = B(HumanBodyBones.LeftHand).position; var rh = B(HumanBodyBones.RightHand).position;
                    var fwd = B(HumanBodyBones.Hips).forward;
                    Debug.Log($"[LumenWave] {clip.name} t={i / 10f:0.0} hips={hips:F2} head={head:F2} lf={lf:F2} rf={rf:F2} lh={lh:F2} rh={rh:F2} hipFwd={fwd:F2}");
                }
                graph.Destroy();
            }
            EditorApplication.Exit(0);
        }
    }
}
