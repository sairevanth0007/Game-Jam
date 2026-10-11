using System.Collections;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Avoids first-use hitches (GPU shader compilation when an effect appears for the first time, e.g. the
    /// finale rings): at level load, every material in Resources/Materials is drawn once on a tiny quad just in
    /// front of the camera for a few frames, while the screen is still fading in from black.
    /// </summary>
    public static class ShaderWarmup
    {
        static bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            _done = false;
            LevelRuntime.Loaded += rt => { if (!_done && rt != null) rt.StartCoroutine(Run()); };
        }

        static IEnumerator Run()
        {
            yield return null;   // camera rig exists after the first frame
            var cam = SceneRig.Instance != null ? SceneRig.Instance.Camera : Camera.main;
            if (cam == null) yield break;
            _done = true;
            var root = new GameObject("ShaderWarmup");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, cam.nearClipPlane + 0.05f);
            int i = 0;
            foreach (var m in Resources.LoadAll<Material>("Materials"))
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(q.GetComponent<Collider>());
                q.name = "Warm_" + m.name;
                q.transform.SetParent(root.transform, false);
                q.transform.localPosition = new Vector3((i % 8) * 0.0005f, (i / 8) * 0.0005f, 0f);
                q.transform.localScale = Vector3.one * 0.0004f;
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                i++;
            }
            for (int f = 0; f < 3; f++) yield return null;
            Object.Destroy(root);
        }
    }
}
