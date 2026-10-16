using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Builds Umbra's hero assets the standard Unity way from the CC0 Quaternius Universal Animation Library
    /// (UAL1 mannequin + UAL1/UAL2 Humanoid clips):
    ///  - Resources/Hero/UmbraAnimator.controller: a parameter-driven Animator Controller.
    ///      Base layer: Idle, Locomotion (1D blend on Speed: walk / jog / sprint), Jump, Fall, Land, Dash,
    ///      Death, Sing. Upper-body override layer "Actions" (arms + spine + head mask): Empty, Cast, Shield, Hurt.
    ///      Parameters: Speed, VerticalSpeed, Grounded, Dash, Cast (trigger), Shield, Hurt (trigger), Dead, Sing,
    ///      plus LocoRate (playback-rate multiplier for the locomotion state).
    ///  - Resources/Hero/UmbraModel.prefab: mannequin + Animator (avatar, controller, no root motion) +
    ///      <see cref="LumenWave.UmbraAnimEvents"/> (receives the footstep / land animation events that
    ///      ArtImport adds to the clips).
    /// Idempotent: rebuilt only when missing or when <see cref="Version"/> changes. Called from ProjectSetup.Run.
    /// </summary>
    public static class HeroSetup
    {
        public const string Ual1 = "Assets/LumenWave/ThirdParty/QuaterniusUAL/UAL1_Standard.fbx";
        public const string Ual2 = "Assets/LumenWave/ThirdParty/QuaterniusUAL/UAL2_Standard.fbx";
        public const string Dir = "Assets/LumenWave/Resources/Hero";
        public const string ControllerPath = Dir + "/UmbraAnimator.controller";
        public const string PrefabPath = Dir + "/UmbraModel.prefab";
        const int Version = 10;
        static string VersionParam => "v" + Version;

        [MenuItem("LumenWave/Build Hero")]
        public static void Ensure() => Ensure(false);

        public static void Ensure(bool force)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Ual1) == null) { Debug.LogError("[LumenWave] Hero model missing: " + Ual1); return; }
            System.IO.Directory.CreateDirectory(Dir);
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            bool rebuild = force || ctrl == null || !ctrl.parameters.Any(p => p.name == VersionParam)
                           || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null;
            if (!rebuild) { Debug.Log("[LumenWave] Hero assets up to date (" + VersionParam + ")"); return; }

            // Re-import so ArtImport's clip settings (loops, root baking, animation events) are current.
            AssetDatabase.ImportAsset(Ual1, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(Ual2, ImportAssetOptions.ForceUpdate);
            if (ctrl != null) AssetDatabase.DeleteAsset(ControllerPath);
            ctrl = BuildController();
            BuildPrefab(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[LumenWave] Hero assets built (UAL): " + ControllerPath + ", " + PrefabPath);
        }

        static AnimationClip Clip(string name)
        {
            foreach (var path in new[] { Ual1, Ual2 })
            {
                var c = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(x => x.name.EndsWith("|" + name));
                if (c != null) return c;
            }
            Debug.LogError("[LumenWave] Hero clip missing: " + name);
            return null;
        }

        static AnimatorStateTransition To(AnimatorState from, AnimatorState to, float duration, bool exit = false, float exitTime = 0f)
        {
            var t = from.AddTransition(to);
            t.duration = duration; t.hasExitTime = exit; t.exitTime = exitTime; t.hasFixedDuration = true;
            return t;
        }

        static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState to, float duration)
        {
            var t = sm.AddAnyStateTransition(to);
            t.duration = duration; t.hasExitTime = false; t.hasFixedDuration = true; t.canTransitionToSelf = false;
            return t;
        }

        static AnimatorController BuildController()
        {
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(VersionParam, AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Dash", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Shield", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Sing", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("LocoRate", AnimatorControllerParameterType.Float);
            var gp = ctrl.parameters; foreach (var p in gp) if (p.name == "Grounded") p.defaultBool = true; if (gp.Any(p => p.name == "LocoRate")) gp.First(p => p.name == "LocoRate").defaultFloat = 1f; ctrl.parameters = gp;

            var layer = ctrl.layers[0];
            layer.name = "Base";
            var sm = layer.stateMachine;

            var idle = sm.AddState("Idle", new Vector3(300, 0)); idle.motion = Clip("Idle_Loop"); idle.iKOnFeet = true;
            var tree = new BlendTree { name = "WalkJogSprint", blendParameter = "Speed", useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(Clip("Walk_Loop"), 0.12f);
            tree.AddChild(Clip("Jog_Fwd_Loop"), 0.55f);
            tree.AddChild(Clip("Sprint_Loop"), 1.0f);
            var loco = sm.AddState("Locomotion", new Vector3(300, 90)); loco.motion = tree; loco.iKOnFeet = true;
            loco.speedParameterActive = true; loco.speedParameter = "LocoRate";
            var jump = sm.AddState("Jump", new Vector3(600, 0)); jump.motion = Clip("Jump_Start"); jump.cycleOffset = 0.1f; jump.speed = 1.3f;
            var fall = sm.AddState("Fall", new Vector3(600, 90)); fall.motion = Clip("Jump_Loop");
            var land = sm.AddState("Land", new Vector3(600, 180)); land.motion = Clip("Jump_Land"); land.speed = 1.6f; land.iKOnFeet = true;
            var dash = sm.AddState("Dash", new Vector3(300, 270)); dash.motion = Clip("Sword_Dash_RM"); dash.speed = 1.8f;
            var death = sm.AddState("Death", new Vector3(600, 270)); death.motion = Clip("Death01");
            var sing = sm.AddState("Sing", new Vector3(0, 180)); sing.motion = Clip("Spell_Simple_Idle_Loop"); sing.iKOnFeet = true;
            sm.defaultState = idle;

            // Death / respawn (respawn simply returns to Idle).
            Any(sm, death, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Dead");
            To(death, idle, 0f).AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            // Dash interrupts anything alive.
            var td = Any(sm, dash, 0.05f); td.AddCondition(AnimatorConditionMode.If, 0, "Dash"); td.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var dl = To(dash, loco, 0.12f); dl.AddCondition(AnimatorConditionMode.IfNot, 0, "Dash"); dl.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); dl.AddCondition(AnimatorConditionMode.Greater, 0.06f, "Speed");
            var di = To(dash, idle, 0.15f); di.AddCondition(AnimatorConditionMode.IfNot, 0, "Dash"); di.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var df = To(dash, fall, 0.15f); df.AddCondition(AnimatorConditionMode.IfNot, 0, "Dash"); df.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            // Singing (finale).
            var ts = Any(sm, sing, 0.4f); ts.AddCondition(AnimatorConditionMode.If, 0, "Sing"); ts.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); ts.AddCondition(AnimatorConditionMode.IfNot, 0, "Dash");
            To(sing, idle, 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0, "Sing");
            // Ground locomotion.
            To(idle, loco, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.06f, "Speed");
            To(loco, idle, 0.15f).AddCondition(AnimatorConditionMode.Less, 0.06f, "Speed");
            foreach (var g in new[] { idle, loco, land })
            {
                var j = To(g, jump, 0.06f); j.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); j.AddCondition(AnimatorConditionMode.Greater, 0.5f, "VerticalSpeed");
                var f = To(g, fall, 0.2f); f.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded"); f.AddCondition(AnimatorConditionMode.Less, 0.5f, "VerticalSpeed");
            }
            // Air.
            To(jump, fall, 0.3f).AddCondition(AnimatorConditionMode.Less, -1f, "VerticalSpeed");
            foreach (var a in new[] { jump, fall })
            {
                var r = To(a, loco, 0.1f); r.AddCondition(AnimatorConditionMode.If, 0, "Grounded"); r.AddCondition(AnimatorConditionMode.Greater, 0.35f, "Speed");
                To(a, land, 0.05f).AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }
            To(land, loco, 0.12f).AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            To(land, idle, 0.25f, true, 0.55f);

            // Upper-body actions (override layer, arms + spine + head).
            var mask = new AvatarMask { name = "UmbraUpperBody" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                    || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            AssetDatabase.AddObjectToAsset(mask, ctrl);
            var asm = new AnimatorStateMachine { name = "Actions", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(asm, ctrl);
            var empty = asm.AddState("Empty", new Vector3(300, 0));
            var cast = asm.AddState("Cast", new Vector3(600, 0)); cast.motion = Clip("OverhandThrow"); cast.speed = 2.2f;
            var shield = asm.AddState("Shield", new Vector3(600, 90)); shield.motion = Clip("Idle_Shield_Loop");
            var hurt = asm.AddState("Hurt", new Vector3(600, 180)); hurt.motion = Clip("Hit_Chest"); hurt.speed = 1.2f;
            asm.defaultState = empty;
            var tc = Any(asm, cast, 0.05f); tc.AddCondition(AnimatorConditionMode.If, 0, "Cast"); tc.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            To(cast, empty, 0.2f, true, 0.85f);
            var sh = To(empty, shield, 0.1f); sh.AddCondition(AnimatorConditionMode.If, 0, "Shield"); sh.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            To(cast, shield, 0.15f, true, 0.85f).AddCondition(AnimatorConditionMode.If, 0, "Shield");
            To(shield, empty, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0, "Shield");
            To(shield, empty, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var th = Any(asm, hurt, 0.04f); th.AddCondition(AnimatorConditionMode.If, 0, "Hurt"); th.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); th.AddCondition(AnimatorConditionMode.IfNot, 0, "Shield");
            To(hurt, empty, 0.15f, true, 0.9f);
            ctrl.AddLayer(new AnimatorControllerLayer
            {
                name = "Actions", stateMachine = asm, avatarMask = mask, defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
            });
            var layers = ctrl.layers; layers[0] = layer; ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void BuildPrefab(AnimatorController ctrl)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Ual1);
            var go = Object.Instantiate(model);
            go.name = "UmbraModel";
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.avatar = AssetDatabase.LoadAllAssetsAtPath(Ual1).OfType<Avatar>().FirstOrDefault();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (go.GetComponent<LumenWave.UmbraAnimEvents>() == null) go.AddComponent<LumenWave.UmbraAnimEvents>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LumenWave/Resources/Materials/Umbra.mat");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (mat != null) smr.sharedMaterials = Enumerable.Repeat(mat, smr.sharedMesh.subMeshCount).ToArray();
                smr.updateWhenOffscreen = true;
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
    }
}
