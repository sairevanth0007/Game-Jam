using System.Collections.Generic;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Runtime access to material assets generated into Resources/Materials (see EditorTools.MaterialAssets).
    /// Base materials: Lit, LitEmissive, Unlit, UnlitTransparent, ParticleAdditive, ParticleAlpha,
    /// plus one per custom shader in Assets/LumenWave/Shaders (named after the shader, e.g. "Umbra").
    /// </summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> _shared = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> _tinted = new Dictionary<string, Material>();

        /// <summary>Shared material asset (do not modify).</summary>
        public static Material Get(string name)
        {
            if (_shared.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogWarning("[LumenWave] Missing material Resources/Materials/" + name + ", falling back");
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader");
                m = new Material(sh) { name = name + "_fallback" };
            }
            _shared[name] = m;
            return m;
        }

        /// <summary>A new instance (caller owns it).</summary>
        public static Material Create(string name) => new Material(Get(name));

        /// <summary>Cached tinted copy: same (name,color) returns the same material (batching-friendly).</summary>
        public static Material Tinted(string name, Color color, Color? emission = null)
        {
            string key = name + ColorUtility.ToHtmlStringRGBA(color) + (emission.HasValue ? ColorUtility.ToHtmlStringRGBA(emission.Value) + emission.Value.maxColorComponent : "");
            if (_tinted.TryGetValue(key, out var m) && m != null) return m;
            m = Create(name);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (emission.HasValue && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); }
            _tinted[key] = m;
            return m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _shared.Clear(); _tinted.Clear(); }
    }
}
