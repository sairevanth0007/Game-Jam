using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Danger readability: L4's teaching beams are classified (and therefore drawn) correctly.</summary>
    public class BeamDangerTests
    {
        LevelRuntime _rt;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_rt != null) Object.Destroy(_rt.gameObject);
            if (SceneRig.Instance != null) Object.Destroy(SceneRig.Instance.gameObject);
            _rt = null;
            yield return null;
        }

        static LightBeam BeamAt(float x)
        {
            foreach (var b in Object.FindObjectsByType<LightBeam>(FindObjectsSortMode.None))
                if (Mathf.Abs(b.transform.position.x - x) < 0.1f) return b;
            return null;
        }

        [UnityTest]
        public IEnumerator Level4_BurnAndLethalBeams_AreClassified()
        {
            _rt = SceneBootstrap.BootLevel(4);
            yield return null;
            yield return null;
            var burn = BeamAt(16.5f);
            var lethal = BeamAt(28.5f);
            var hall = BeamAt(139.5f);
            Assert.IsNotNull(burn, "beam at 16.5");
            Assert.IsNotNull(lethal, "beam at 28.5");
            Assert.IsNotNull(hall, "beam at 139.5");
            Assert.AreEqual(LightBeam.BeamDanger.Burn, burn.Danger, "first beam only burns");
            Assert.AreEqual(LightBeam.BeamDanger.Lethal, lethal.Danger, "second beam kills");
            Assert.AreEqual(LightBeam.BeamDanger.Burn, hall.Danger, "burning hall only burns");
            // The look follows the danger: lethal is drawn thicker and hotter than burn.
            var lb = burn.LookFor(LightColor.Red);
            var ll = lethal.LookFor(LightColor.Red);
            Assert.Greater(ll.coreW, lb.coreW);
            Assert.Greater(ll.hot, lb.hot);
        }
    }
}
