using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Generates the base material assets in Resources/Materials. Materials in Resources are always
    /// shipped, which guarantees their shaders (and keyword variants) are included in the player build.
    /// Runtime code gets them via <see cref="MaterialLibrary"/>. Add new entries to <see cref="Specs"/>.
    /// </summary>
    public static class MaterialAssets
    {
        public const string Dir = "Assets/LumenWave/Resources/Materials";

        public struct Spec
        {
            public string name, shader;
            public System.Action<Material> setup;
            public Spec(string name, string shader, System.Action<Material> setup = null) { this.name = name; this.shader = shader; this.setup = setup; }
        }

        public static readonly System.Collections.Generic.List<Spec> Specs = new System.Collections.Generic.List<Spec>
        {
            new Spec("Lit", "Universal Render Pipeline/Lit", m => { m.SetFloat("_Smoothness", 0.2f); }),
            new Spec("LitEmissive", "Universal Render Pipeline/Lit", m => {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", Color.white);
            }),
            new Spec("Unlit", "Universal Render Pipeline/Unlit"),
            new Spec("UnlitTransparent", "Universal Render Pipeline/Unlit", m => {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }),
            new Spec("ParticleAdditive", "Universal Render Pipeline/Particles/Unlit", m => {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f); // additive
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }),
            new Spec("ParticleAlpha", "Universal Render Pipeline/Particles/Unlit", m => {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }),
        };

        /// <summary>Custom shaders (LumenWave/*) get one material each, named after the shader file.</summary>
        public static void EnsureAll()
        {
            Directory.CreateDirectory(Dir);
            foreach (var s in Specs) Ensure(s.name, s.shader, s.setup);
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/LumenWave/Shaders" }))
            {
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (sh == null || !sh.name.StartsWith("LumenWave/")) continue;
                Ensure(sh.name.Substring("LumenWave/".Length), sh.name, null);
            }
            AssetDatabase.SaveAssets();
        }

        static void Ensure(string name, string shaderName, System.Action<Material> setup)
        {
            var path = Dir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError("[LumenWave] Shader not found: " + shaderName); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
        }
    }
}
