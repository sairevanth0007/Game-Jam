using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// TextMeshPro setup, idempotent (called from ProjectSetup.Run):
    /// 1. imports TMP Essential Resources (TMP Settings, shaders, default font) from the ugui package into Assets/TextMesh Pro/;
    /// 2. creates SDF font assets for the GGBotNet CC0 fonts under Resources/Fonts/ (dynamic, ASCII pre-baked into the atlas),
    ///    using the full "TextMeshPro/Distance Field" shader, plus two preset materials per font ("Shadow" = underlay,
    ///    "Glow" = glow + underlay) saved as assets so the shader keyword variants are included in builds.
    /// </summary>
    public static class TmpSetup
    {
        const string FontDir = "Assets/LumenWave/Resources/Fonts";
        const string SourceDir = "Assets/LumenWave/ThirdParty/GGBotNetFonts/Resources/Art/GGBotNetFonts";
        const string Chars =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·–—…’‘“”×";

        static readonly (string src, string asset)[] Fonts =
        {
            ("OSerif-Regular", "OSerif SDF"),
            ("Jupiteroid-Regular", "Jupiteroid SDF"),
            ("Jupiteroid-Bold", "Jupiteroid Bold SDF"),
            ("Jupiteroid-Light", "Jupiteroid Light SDF"),
        };

        [MenuItem("LumenWave/Setup TextMeshPro")]
        public static void Ensure()
        {
            EnsureEssentials();
            EnsureFontAssets();
        }

        public static void EnsureEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset")) return;
            string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg)) { Debug.LogError("[LumenWave] TMP Essential Resources package not found: " + pkg); return; }
            // AssetDatabase.ImportPackage is asynchronous and never completes before a batch-mode exit, so the
            // .unitypackage (a gzipped tar of <guid>/asset, asset.meta, pathname) is unpacked directly, keeping GUIDs.
            int n = ExtractUnityPackage(pkg);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log($"[LumenWave] Imported TMP Essential Resources ({n} files)");
        }

        static int ExtractUnityPackage(string pkg)
        {
            var entries = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, byte[]>>();
            using (var fs = File.OpenRead(pkg))
            using (var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress))
            {
                var header = new byte[512];
                string longName = null;
                while (true)
                {
                    if (ReadFull(gz, header, 512) < 512) break;
                    bool empty = true;
                    for (int i = 0; i < 512; i++) if (header[i] != 0) { empty = false; break; }
                    if (empty) break;
                    string name = System.Text.Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    string prefix = System.Text.Encoding.UTF8.GetString(header, 345, 155).TrimEnd('\0');
                    if (prefix.Length > 0) name = prefix + "/" + name;
                    string sizeStr = System.Text.Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
                    long size = sizeStr.Length > 0 ? System.Convert.ToInt64(sizeStr, 8) : 0;
                    char type = (char)header[156];
                    var data = new byte[size];
                    ReadFull(gz, data, (int)size);
                    long pad = (512 - size % 512) % 512;
                    if (pad > 0) ReadFull(gz, new byte[pad], (int)pad);
                    if (type == 'L') { longName = System.Text.Encoding.UTF8.GetString(data).TrimEnd('\0'); continue; }
                    if (longName != null) { name = longName; longName = null; }
                    if (type != '0' && type != '\0') continue;
                    name = name.TrimStart('.', '/');
                    int slash = name.IndexOf('/');
                    if (slash < 0) continue;
                    string guid = name.Substring(0, slash), file = name.Substring(slash + 1);
                    if (!entries.TryGetValue(guid, out var d)) entries[guid] = d = new System.Collections.Generic.Dictionary<string, byte[]>();
                    d[file] = data;
                }
            }
            int count = 0;
            foreach (var e in entries.Values)
            {
                if (!e.TryGetValue("pathname", out var pn)) continue;
                string path = System.Text.Encoding.UTF8.GetString(pn).Split('\n')[0].Trim();
                if (!path.StartsWith("Assets/")) continue;
                if (e.TryGetValue("asset", out var asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, asset);
                    count++;
                }
                else Directory.CreateDirectory(path);
                if (e.TryGetValue("asset.meta", out var meta)) File.WriteAllBytes(path + ".meta", meta);
            }
            return count;
        }

        static int ReadFull(Stream s, byte[] buf, int n)
        {
            int read = 0;
            while (read < n) { int r = s.Read(buf, read, n - read); if (r <= 0) break; read += r; }
            return read;
        }

        public static void EnsureFontAssets()
        {
            var sdf = Shader.Find("TextMeshPro/Distance Field");
            if (sdf == null) { Debug.LogWarning("[LumenWave] TMP shaders not imported yet; run setup again"); return; }
            if (!AssetDatabase.IsValidFolder(FontDir)) AssetDatabase.CreateFolder("Assets/LumenWave/Resources", "Fonts");
            foreach (var (src, name) in Fonts)
            {
                string path = FontDir + "/" + name + ".asset";
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fa == null)
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>(SourceDir + "/" + src + ".ttf");
                    if (font == null) { Debug.LogError("[LumenWave] Missing font " + src); continue; }
                    fa = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                    if (fa == null) { Debug.LogError("[LumenWave] Could not create font asset for " + src); continue; }
                    fa.name = name;
                    AssetDatabase.CreateAsset(fa, path);
                    var so = new SerializedObject(fa);
                    var clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                    if (clear != null) { clear.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                    fa.atlasTexture.name = name + " Atlas";
                    AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
                    var mat = fa.material;
                    mat.name = name + " Material";
                    mat.shader = sdf;
                    AssetDatabase.AddObjectToAsset(mat, fa);
                    fa.TryAddCharacters(Chars, out string missing);
                    if (!string.IsNullOrEmpty(missing)) Debug.Log($"[LumenWave] {name}: glyphs not in font: {missing}");
                    EditorUtility.SetDirty(fa);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[LumenWave] Created TMP font asset " + path);
                }
                EnsurePreset(fa, name, "Shadow", m =>
                {
                    m.EnableKeyword("UNDERLAY_ON");
                    m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.85f));
                    m.SetFloat("_UnderlayOffsetX", 0.35f);
                    m.SetFloat("_UnderlayOffsetY", -0.45f);
                    m.SetFloat("_UnderlayDilate", 0.25f);
                    m.SetFloat("_UnderlaySoftness", 0.45f);
                });
                EnsurePreset(fa, name, "Glow", m =>
                {
                    m.EnableKeyword("UNDERLAY_ON");
                    m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.7f));
                    m.SetFloat("_UnderlayOffsetX", 0.3f);
                    m.SetFloat("_UnderlayOffsetY", -0.4f);
                    m.SetFloat("_UnderlayDilate", 0.2f);
                    m.SetFloat("_UnderlaySoftness", 0.5f);
                    m.EnableKeyword("GLOW_ON");
                    m.SetColor("_GlowColor", new Color(0.45f, 0.95f, 1f, 0.55f));
                    m.SetFloat("_GlowOffset", 0.1f);
                    m.SetFloat("_GlowInner", 0.05f);
                    m.SetFloat("_GlowOuter", 0.55f);
                    m.SetFloat("_GlowPower", 0.6f);
                });
            }
            AssetDatabase.SaveAssets();
        }

        static void EnsurePreset(TMP_FontAsset fa, string fontName, string kind, System.Action<Material> setup)
        {
            string path = FontDir + "/" + fontName + " " + kind + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(fa.material) { name = fontName + " " + kind };
            m.shader = fa.material.shader;
            m.CopyPropertiesFromMaterial(fa.material);
            setup(m);
            m.SetTexture("_MainTex", fa.atlasTexture);
            if (create) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
        }
    }
}
