using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    public class VisualsPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (LevelRuntime.Current != null) Object.Destroy(LevelRuntime.Current.gameObject);
            if (SceneRig.Instance != null) Object.Destroy(SceneRig.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LevelBoot_CreatesCameraVolumeBackdrop()
        {
            var rt = SceneBootstrap.BootLevel(1);
            yield return null;
            yield return null;
            Assert.IsNotNull(SceneRig.Instance, "SceneRig");
            Assert.IsNotNull(Camera.main, "Main camera");
            Assert.IsNotNull(Camera.main.GetComponent<CameraRig>(), "CameraRig");
            Assert.IsNotNull(GameObject.Find("PostFX"), "Volume");
            Assert.IsTrue(SceneRig.Instance.HasBloom, "Bloom");
            Assert.IsNotNull(GameObject.Find("Backdrop"), "Backdrop");
            Assert.IsTrue(RenderSettings.fog, "Fog");
            Assert.IsNotNull(rt.transform.Find("Level_" + rt.Data.id + "/Geometry/EdgeGlow") ?? FindEdge(rt.transform), "Edge glow strips");
        }

        static Transform FindEdge(Transform t)
        {
            foreach (Transform c in t.GetComponentsInChildren<Transform>()) if (c.name == "EdgeGlow") return c;
            return null;
        }

        [UnityTest]
        public IEnumerator Camera_FollowsPlayer_AndStaysInBounds()
        {
            var rt = SceneBootstrap.BootLevel(1);
            yield return null;
            if (rt.Player == null) { Assert.Ignore("No player entity yet"); yield break; }
            var cam = Camera.main.transform;
            var body = rt.Player.GetComponent<IPlayerBody>();
            var target = new Vector3(rt.Data.Width * 0.5f, rt.Player.transform.position.y + 0.5f, 0f);
            if (body != null) body.Teleport(target); else rt.Player.transform.position = target;
            for (int i = 0; i < 90; i++) yield return null;
            Assert.Less(Mathf.Abs(cam.position.x - rt.Player.transform.position.x), 6f, "camera x near player");
            Assert.Less(cam.position.z, -5f, "camera behind play plane");
        }

        [UnityTest]
        public IEnumerator Shake_DecaysToRest()
        {
            SceneBootstrap.BootLevel(1);
            yield return null;
            Settings.ScreenShake = true;
            CameraRig.Shake(1f);
            yield return null;
            yield return new WaitForSecondsRealtime(1.2f);
            yield return null;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(0f, Camera.main.transform.eulerAngles.z)), 0.05f);
        }
    }
}
