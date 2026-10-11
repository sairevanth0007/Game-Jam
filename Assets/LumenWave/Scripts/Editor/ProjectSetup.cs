using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Idempotent project configuration: URP 3D (Universal Renderer, Forward+), player settings,
    /// material assets in Resources (so shaders get included in builds). Menu: LumenWave/Setup Project.
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/LumenWave/Settings";
        const string PipelinePath = SettingsDir + "/LumenWaveURP.asset";
        const string RendererPath = SettingsDir + "/LumenWaveRenderer.asset";

        [MenuItem("LumenWave/Setup Project")]
        public static void Run()
        {
            EnsurePipeline();
            ConfigurePlayer();
            MaterialAssets.EnsureAll();
            HeroSetup.Ensure();
            AssetDatabase.SaveAssets();
            Debug.Log("[LumenWave] Project setup complete");
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogError("[LumenWave] Setup failed: " + e); EditorApplication.Exit(1); }
        }

        public static UniversalRenderPipelineAsset EnsurePipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, RendererPath);
            }
            rd.renderingMode = RenderingMode.ForwardPlus;
            rd.postProcessData = rd.postProcessData != null ? rd.postProcessData : LoadDefaultPostProcessData();
            EditorUtility.SetDirty(rd);

            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (rp == null)
            {
                rp = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(rp, PipelinePath);
            }
            rp.supportsHDR = true;
            rp.msaaSampleCount = 4;
            rp.renderScale = 1f;
            rp.shadowDistance = 60f;
            rp.supportsCameraDepthTexture = true;
            rp.supportsCameraOpaqueTexture = false;
            var so = new SerializedObject(rp);
            SetProp(so, "m_MainLightShadowsSupported", true);
            SetProp(so, "m_AdditionalLightShadowsSupported", true);
            SetProp(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
            SetProp(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetProp(so, "m_SoftShadowsSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rp);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = rp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = rp;
            }
            QualitySettings.SetQualityLevel(current, false);
            Debug.Log("[LumenWave] URP pipeline set: " + PipelinePath + " (renderer " + rd.GetType().Name + ", " + rd.renderingMode + ", ppData=" + (rd.postProcessData != null) + ")");
            return rp;
        }

        static PostProcessData LoadDefaultPostProcessData()
        {
            // Find the URP package's default PostProcessData asset.
            var guid = AssetDatabase.FindAssets("t:PostProcessData").FirstOrDefault();
            if (guid == null) { Debug.LogWarning("[LumenWave] No PostProcessData found"); return null; }
            return AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void SetProp(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "LumenWave";
            PlayerSettings.companyName = "LumenWave Team";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
        }
    }
}
