using NUnit.Framework;
using UnityEngine;

namespace LumenWave.Tests
{
    public class LightSystemTests
    {
        [Test] public void Beam_ReferenceDistance_GivesUnitIntensity()
        {
            Assert.AreEqual(1f, LightField.BeamIntensityAt(1f, LightField.ReferenceDistance, 0.04f), 1e-3f);
        }

        [Test] public void Beam_InverseSquareTimesFog()
        {
            float s = 0.05f;
            float near = LightField.BeamIntensityAt(1f, 4f, s);
            float far = LightField.BeamIntensityAt(1f, 8f, s);
            // Double the path: 1/4 from inverse square, × e^(−σ·4) from Beer–Lambert.
            Assert.AreEqual(0.25f * Mathf.Exp(-s * 4f), far / near, 1e-4f);
        }

        [Test] public void Beam_ClampedNearSource()
        {
            float a = LightField.BeamIntensityAt(1f, 0f, 0.04f);
            Assert.IsFalse(float.IsInfinity(a));
            // Only the 1/d² term is clamped; at d = 0 there is no fog absorption yet.
            float expected = LightField.BeamSourceStrength(0.04f) / (LightField.MinDistance * LightField.MinDistance);
            Assert.AreEqual(expected, a, 1e-2f);
        }

        [Test] public void Lamp_ZeroOutsideRadius_AndFallsOff()
        {
            Assert.AreEqual(0f, LightField.LampIntensityAt(1f, 6f, 5f, 0.04f));
            Assert.Greater(LightField.LampIntensityAt(1f, 1.5f, 5f, 0.04f), LightField.LampIntensityAt(1f, 3f, 5f, 0.04f));
            Assert.AreEqual(1f, LightField.LampIntensityAt(1f, LightField.LampReferenceDistance, 5f, 0.04f), 1e-3f);
        }

        [Test] public void Rotate_And_SegmentDistance()
        {
            var r = LightField.Rotate(Vector2.right, 90f);
            Assert.AreEqual(0f, r.x, 1e-5f);
            Assert.AreEqual(1f, r.y, 1e-5f);
            float d = LightField.DistanceToSegment(new Vector2(1f, 1f), Vector2.zero, new Vector2(2f, 0f), out float t);
            Assert.AreEqual(1f, d, 1e-5f);
            Assert.AreEqual(0.5f, t, 1e-5f);
        }

        [Test] public void Prism_Mesh_IsClosedTriangle()
        {
            var m = Prism.TriangularPrism(0.75f, 1f);
            Assert.AreEqual(8 * 3, m.triangles.Length);
            // Front face normal points to the camera (−Z).
            Assert.Less(m.normals[0].z, 0f);
        }
    }
}
