using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>Batch probe for the UAL hero clips: logs names/lengths and samples key bone heights.</summary>
    public static class HeroProbe
    {
        public const string Ual1 = "Assets/LumenWave/ThirdParty/QuaterniusUAL/UAL1_Standard.fbx";
        public const string Ual2 = "Assets/LumenWave/ThirdParty/QuaterniusUAL/UAL2_Standard.fbx";

        static readonly string[] Interesting =
        {
            "Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Jump_Start", "Jump_Loop", "Jump_Land", "Roll",
            "Spell_Simple_Shoot", "Spell_Simple_Idle_Loop", "Hit_Chest", "Death01", "NinjaJump_Start", "NinjaJump_Idle_Loop",
            "NinjaJump_Land", "Slide_Start", "Slide_Loop", "Slide_Exit", "Hit_Knockback", "Idle_Shield_Loop", "Sword_Block",
            "OverhandThrow", "Crouch_Idle_Loop", "Sword_Idle", "Dance_Loop",
        };

        public static void ProbeBatch()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Ual1);
            var go = Object.Instantiate(model);
            var anim = go.GetComponent<Animator>();
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            Debug.Log($"[LumenWave] UAL anim={anim != null} avatar={(anim != null && anim.avatar != null ? anim.avatar.name : "none")} human={anim != null && anim.isHuman} smr={smr?.name} bounds={smr?.sharedMesh.bounds} lossy={smr?.transform.lossyScale} rootChildren={string.Join(",", go.transform.Cast<Transform>().Select(t => t.name + t.localScale))}");
            foreach (var path in new[] { Ual1, Ual2 })
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                {
                    string shortName = c.name.Substring(c.name.IndexOf('|') + 1);
                    if (!Interesting.Contains(shortName)) continue;
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"[LumenWave] CLIP {c.name} len={c.length:0.00} loop={c.isLooping} ev={c.events.Length} |");
                    for (int i = 0; i <= 12; i++)
                    {
                        float t = c.length * i / 12f;
                        AnimationMode.StartAnimationMode();
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, c, t);
                        AnimationMode.EndSampling();
                        var h = anim.GetBoneTransform(HumanBodyBones.Hips).position;
                        var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot).position;
                        var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand).position;
                        var rh = anim.GetBoneTransform(HumanBodyBones.RightHand).position;
                        var hd = anim.GetBoneTransform(HumanBodyBones.Head).position;
                        sb.Append($" {i / 12f:0.00}:hy{h.y:0.00} hz{h.z:0.00} hd{hd.y:0.00} lf{lf.y:0.00}/{lf.z:0.00} rf{rf.y:0.00}/{rf.z:0.00} lh{lh.y:0.00} rh{rh.y:0.00};");
                    }
                    Debug.Log(sb.ToString());
                }
            AnimationMode.StopAnimationMode();
            EditorApplication.Exit(0);
        }
    }
}
