using NUnit.Framework;
using UnityEngine;

namespace LumenWave.Tests
{
    public class VisualsTests
    {
        static readonly string[] ContractMaterials = { "Umbra", "Beam", "Shield", "EchoBridge", "Wave", "Glow", "GlowParticle", "Sky", "Silhouette", "Lit", "LitEmissive" };

        [Test] public void ContractMaterialsExistAndShadersSupported()
        {
            foreach (var name in ContractMaterials)
            {
                var m = Resources.Load<Material>("Materials/" + name);
                Assert.IsNotNull(m, "Missing Resources/Materials/" + name + " (run compile.ps1 -Setup)");
                Assert.IsTrue(m.shader.isSupported, name + " shader not supported / failed to compile");
            }
        }

        [Test] public void ShaderContractProperties()
        {
            AssertProps("Umbra", "_BaseColor", "_RimColor", "_RimPower", "_RimF0");
            AssertProps("Beam", "_Color", "_Intensity", "_FogSigma", "_Length", "_Flicker");
            AssertProps("Shield", "_Color", "_ModeN", "_ModeM", "_Phase", "_Alpha");
            AssertProps("EchoBridge", "_Color", "_Fade", "_Frequency");
            AssertProps("Wave", "_Color", "_Radius", "_Thickness", "_Alpha");
            AssertProps("Glow", "_Color");
        }

        static void AssertProps(string mat, params string[] props)
        {
            var m = Resources.Load<Material>("Materials/" + mat);
            Assert.IsNotNull(m, mat);
            foreach (var p in props) Assert.IsTrue(m.HasProperty(p), mat + " lacks " + p);
        }

        [Test] public void MoodsAreDistinctAndDark()
        {
            foreach (var n in new[] { "blue", "green", "red", "gold", "violet", "dawn" })
            {
                var m = Mood.Get(n);
                Assert.Greater(m.fogDensity, 0f, n);
                Assert.Less(m.fog.maxColorComponent, 0.3f, n + " fog should stay moody");
            }
            Assert.AreNotEqual(Mood.Get("red").edge, Mood.Get("blue").edge);
        }
    }
}
