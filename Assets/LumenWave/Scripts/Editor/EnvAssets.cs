using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace LumenWave.EditorTools
{
    /// <summary>
    /// Environment assets built from code (run by ProjectSetup / compile.ps1 -Setup):
    ///  - Resources/Timelines/Finale.playable: the level-10 finale sequence (Signal track "Steps" driving
    ///    FinaleDirector, Activation tracks "FinaleCamera" (Cinemachine blend to a wide shot) and "Motes");
    ///  - Resources/Prefabs/Props/*.prefab: every scenery model the themes use, flattened to MeshFilter/
    ///    MeshRenderer parts with the shared LumenWave/Prop material, no colliders/shadows (instantiated at
    ///    runtime by PropLibrary and re-tinted per layer).
    /// </summary>
    public static class EnvAssets
    {
        public const string TimelineDir = "Assets/LumenWave/Resources/Timelines";
        public const string PrefabDir = "Assets/LumenWave/Resources/Prefabs";

        [MenuItem("LumenWave/Build Environment Assets")]
        public static void EnsureAll()
        {
            BuildFinaleTimeline();
            BuildPropPrefabs();
            BuildLampPostPrefab();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Lamp post for open-sky lanterns (AreaLamp stretches "Post" and moves "Base" to the floor).</summary>
        public static void BuildLampPostPrefab()
        {
            Directory.CreateDirectory(PrefabDir);
            string matPath = PrefabDir + "/LampIron.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.07f));
            mat.SetFloat("_Smoothness", 0.35f);
            mat.SetFloat("_Metallic", 0.6f);
            EditorUtility.SetDirty(mat);
            var root = new GameObject("LampPost");
            void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            const float postX = -0.75f;
            Part(PrimitiveType.Cylinder, "Post", new Vector3(postX, 0.4f, 0.45f), new Vector3(0.12f, 0.5f, 0.12f));
            Part(PrimitiveType.Cube, "Arm", new Vector3(postX * 0.5f, 0.9f, 0.45f), new Vector3(0.9f, 0.08f, 0.08f));
            Part(PrimitiveType.Sphere, "Finial", new Vector3(postX, 0.98f, 0.45f), new Vector3(0.16f, 0.16f, 0.16f));
            Part(PrimitiveType.Cylinder, "Chain", new Vector3(0, 0.65f, 0.2f), new Vector3(0.04f, 0.2f, 0.04f));
            Part(PrimitiveType.Cube, "Base", new Vector3(postX, -1f, 0.45f), new Vector3(0.45f, 0.2f, 0.45f));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/LampPost.prefab");
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------ timeline
        static SignalAsset Signal(string name)
        {
            string dir = TimelineDir + "/Signals";
            Directory.CreateDirectory(dir);
            string path = dir + "/" + name + ".signal";
            var s = AssetDatabase.LoadAssetAtPath<SignalAsset>(path);
            if (s == null) { s = ScriptableObject.CreateInstance<SignalAsset>(); AssetDatabase.CreateAsset(s, path); }
            return s;
        }

        public static void BuildFinaleTimeline()
        {
            Directory.CreateDirectory(TimelineDir);
            string path = TimelineDir + "/Finale.playable";
            AssetDatabase.DeleteAsset(path);   // rebuilt from code every time (deterministic)
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            tl.name = "Finale";
            AssetDatabase.CreateAsset(tl, path);

            // Steps (FinaleDirector implements INotificationReceiver; times match its curves).
            var steps = tl.CreateTrack<SignalTrack>(null, FinaleTimeline.StepsTrack);
            void Emit(double t, string name)
            {
                var e = steps.CreateMarker<SignalEmitter>(t);
                e.asset = Signal(name);
                e.emitOnce = true;
                e.retroactive = true;
            }
            Emit(0.3, FinaleTimeline.RingsOn);
            Emit(2.85, FinaleTimeline.Harmonize);     // harmony curve crosses 0.45: lights switch to white
            Emit(8.5, FinaleTimeline.DawnPeak);
            Emit(11.5, FinaleTimeline.RingsOff);

            // Camera: the wide finale CinemachineCamera goes live at 0.5 s; the brain blends to it.
            var cam = tl.CreateTrack<ActivationTrack>(null, FinaleTimeline.CameraTrack);
            cam.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
            var cc = cam.CreateDefaultClip(); cc.start = 0.5; cc.duration = 13.5; cc.displayName = "Wide shot";

            // Motes rise from 0.2 s.
            var motes = tl.CreateTrack<ActivationTrack>(null, FinaleTimeline.MotesTrack);
            motes.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
            var mc = motes.CreateDefaultClip(); mc.start = 0.2; mc.duration = 13.8; mc.displayName = "Motes";

            EditorUtility.SetDirty(tl);
            AssetDatabase.SaveAssets();
            Debug.Log("[LumenWave] Finale timeline built: " + path);
        }

        // ------------------------------------------------------------------ prefabs
        public static void BuildPropPrefabs()
        {
            string dir = PrefabDir + "/Props";
            Directory.CreateDirectory(dir);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/LumenWave/Resources/Materials/Prop.mat");
            int n = 0;
            foreach (var path in Theme.AllModelPaths())
            {
                var src = Resources.Load<GameObject>("Art/" + path);
                if (src == null) continue;
                string file = dir + "/" + PropLibrary.PrefabName(path) + ".prefab";
                var root = new GameObject(PropLibrary.PrefabName(path));
                var model = PropLibrary.Get(path);
                if (!model.valid) { Object.DestroyImmediate(root); continue; }
                // Pivot at bottom-centre, unit height (runtime scales to the requested height).
                var pivot = new Vector3(model.bounds.center.x, model.bounds.min.y, model.bounds.center.z);
                float s = 1f / model.bounds.size.y;
                for (int i = 0; i < model.meshes.Count; i++)
                {
                    var part = new GameObject("part" + i);
                    part.transform.SetParent(root.transform, false);
                    var mtx = Matrix4x4.Scale(Vector3.one * s) * Matrix4x4.Translate(-pivot) * model.matrices[i];
                    part.transform.localPosition = mtx.GetColumn(3);
                    part.transform.localRotation = mtx.rotation;
                    part.transform.localScale = mtx.lossyScale;
                    part.AddComponent<MeshFilter>().sharedMesh = model.meshes[i];
                    var r = part.AddComponent<MeshRenderer>();
                    var mats = new Material[Mathf.Max(1, model.meshes[i].subMeshCount)];
                    for (int k = 0; k < mats.Length; k++) mats[k] = mat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
                var info = root.AddComponent<PropInfo>();
                info.aspect = Mathf.Max(model.bounds.size.x, model.bounds.size.z) / model.bounds.size.y;
                PrefabUtility.SaveAsPrefabAsset(root, file);
                Object.DestroyImmediate(root);
                n++;
            }
            Debug.Log("[LumenWave] Scenery prefabs built: " + n);
        }
    }
}
