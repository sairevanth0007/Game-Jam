using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Import settings for third-party CC0 art under Assets/LumenWave/ThirdParty:
    /// the hero (Quaternius Animated Human) is a Humanoid rig with looping locomotion clips;
    /// Kenney props are static meshes (no animation, cameras or lights, read/write off).
    /// Textures: ambientCG normal maps are flagged as normal maps; HDRIs import as 2D lat-long textures.
    /// </summary>
    public class ArtImport : AssetPostprocessor
    {
        const string Root = "Assets/LumenWave/ThirdParty/";

        static bool IsHero(string p) => p.StartsWith(Root + "QuaterniusHuman/");
        /// <summary>Quaternius Universal Animation Library 1+2 (mannequin + 250 humanoid clips), set up per the pack's Unity guide.</summary>
        static bool IsUal(string p) => p.StartsWith(Root + "QuaterniusUAL/");

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            if (IsUal(assetPath))
            {
                mi.bakeAxisConversion = true;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.optimizeGameObjects = false;
                mi.importBlendShapes = false;
                mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            }
            else if (IsHero(assetPath))
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.optimizeGameObjects = false;
                mi.importBlendShapes = false;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                mi.importBlendShapes = false;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.addCollider = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (IsUal(assetPath))
            {
                var ui = (ModelImporter)assetImporter;
                var all = ui.defaultClipAnimations;
                if (all == null || all.Length == 0) return;
                foreach (var c in all)
                {
                    // Pack guide: loop the clips whose name ends in _Loop. Root motion stays off in the Animator;
                    // bake root rotation/height/XZ into the pose so the gameplay controller alone moves Umbra.
                    bool loop = c.name.EndsWith("_Loop");
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = false;
                    c.keepOriginalPositionY = true;
                    c.keepOriginalPositionXZ = false;
                    // *_RM clips travel: let that travel become (discarded) root motion so the hero stays on its collider.
                    if (c.name.EndsWith("_RM")) c.lockRootPositionXZ = false;
                    // Hero animation events (receiver: LumenWave.UmbraAnimEvents on the model). Times are the
                    // normalized foot-contact frames measured with HeroProbe (lowest foot, leading).
                    string shortName = c.name.Substring(c.name.IndexOf('|') + 1);
                    float[] steps = shortName == "Walk_Loop" ? new[] { 0.02f, 0.52f }
                                  : shortName == "Jog_Fwd_Loop" ? new[] { 0.08f, 0.58f }
                                  : shortName == "Sprint_Loop" ? new[] { 0.04f, 0.54f } : null;
                    if (steps != null)
                        c.events = steps.Select((t, k) => new AnimationEvent { functionName = "OnFootstep", time = t, intParameter = k }).ToArray();
                    else if (shortName == "Jump_Land")
                        c.events = new[] { new AnimationEvent { functionName = "OnLand", time = 0.06f } };
                    else if (shortName == "Idle_Loop")
                        c.loopTime = c.loopPose = true;
                }
                ui.clipAnimations = all;
                return;
            }
            if (!IsHero(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            foreach (var c in clips)
            {
                string n = c.name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("run") || n.Contains("walk") || n.Contains("work");
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                // Hero runtime fixes (gameplay moves the body, the clip must not):
                // Idle/Punch are authored ~45° off forward → align to body orientation.
                if (n.EndsWith("idle") || n.EndsWith("punch")) c.keepOriginalOrientation = false;
                // Jump: the clip's own vertical travel becomes (discarded) root motion; the controller does the rise.
                if (n.EndsWith("jump")) c.lockRootHeightY = false;
                // Death: keep the fall to the floor but not the forward slide out of the collider.
                if (n.EndsWith("death")) c.lockRootPositionXZ = false;
            }
            mi.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_Normal.jpg"))
            {
                ti.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.EndsWith(".hdr"))
            {
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.sRGBTexture = false;
                ti.mipmapEnabled = false;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            if (assetPath.EndsWith("_Color.jpg"))
            {
                // Tiling surface textures (triplanar rock): plain 2D textures with mipmaps, not sprites.
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = true;
                ti.alphaSource = TextureImporterAlphaSource.None;
            }
            if (assetPath.EndsWith(".jpg"))
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 1024;
                ti.anisoLevel = 4;
            }
        }

        /// <summary>Batch probe: lists the Universal Animation Library clips and avatar validity.</summary>
        public static void ProbeUalBatch()
        {
            foreach (var p in new[] { Root + "QuaterniusUAL/UAL1_Standard.fbx", Root + "QuaterniusUAL/UAL2_Standard.fbx" })
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(p);
                var av = all.OfType<Avatar>().FirstOrDefault();
                Debug.Log($"[LumenWave] {p} avatar valid={av != null && av.isValid} human={av != null && av.isHuman}");
                Debug.Log("[LumenWave] Clips: " + string.Join(" | ", all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).Select(c => $"{c.name}({c.length:0.0}{(c.isLooping ? "L" : "")})")));
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    Debug.Log($"[LumenWave] SMR {smr.name} verts={smr.sharedMesh.vertexCount} bounds={smr.bounds}");
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Batch probe: logs the hero's clips, avatar validity and bone mapping.</summary>
        public static void ProbeBatch()
        {
            const string p = Root + "QuaterniusHuman/Resources/Art/QuaterniusHuman/AnimatedHuman.fbx";
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            var all = AssetDatabase.LoadAllAssetsAtPath(p);
            foreach (var a in all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
                Debug.Log($"[LumenWave] Clip '{a.name}' len={a.length:0.00}s loop={a.isLooping} human={a.humanMotion}");
            var av = all.OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[LumenWave] Avatar {(av != null ? av.name : "none")} valid={av != null && av.isValid} human={av != null && av.isHuman}");
            var mi = (ModelImporter)AssetImporter.GetAtPath(p);
            foreach (var hb in mi.humanDescription.human) Debug.Log($"[LumenWave] Bone {hb.humanName} -> {hb.boneName}");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                Debug.Log($"[LumenWave] SMR {smr.name} verts={smr.sharedMesh.vertexCount} bounds={smr.sharedMesh.bounds} mats={smr.sharedMaterials.Length}");
            foreach (var t in go.GetComponentsInChildren<Transform>()) Debug.Log($"[LumenWave] T {t.name} pos={t.localPosition} scale={t.localScale} rot={t.localEulerAngles}");
            EditorApplication.Exit(0);
        }
    }
}
