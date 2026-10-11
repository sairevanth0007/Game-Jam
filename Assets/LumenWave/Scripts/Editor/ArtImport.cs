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

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            if (IsHero(assetPath))
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
            if (assetPath.EndsWith(".jpg"))
            {
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.maxTextureSize = 1024;
                ti.anisoLevel = 4;
            }
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
