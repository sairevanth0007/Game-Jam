using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave.Tests
{
    public class LightMathTests
    {
        [Test] public void InverseSquare_HalvesDistance_QuadruplesIntensity()
        {
            float a = LightMath.InverseSquare(10f, 4f);
            float b = LightMath.InverseSquare(10f, 2f);
            Assert.AreEqual(4f, b / a, 1e-4f);
        }

        [Test] public void InverseSquare_ClampsNearZero()
        {
            Assert.AreEqual(10f, LightMath.InverseSquare(10f, 0f, 1f), 1e-4f);
            Assert.IsFalse(float.IsInfinity(LightMath.InverseSquare(10f, 0f, 0.01f)));
        }

        [Test] public void BeerLambert_FollowsExponential()
        {
            Assert.AreEqual(1f, LightMath.BeerLambert(1f, 0.5f, 0f), 1e-5f);
            Assert.AreEqual(Mathf.Exp(-1f), LightMath.BeerLambert(1f, 0.5f, 2f), 1e-5f);
            Assert.Less(LightMath.BeerLambert(1f, 0.1f, 10f), LightMath.BeerLambert(1f, 0.1f, 5f));
        }

        [Test] public void Reflection_AngleInEqualsAngleOut()
        {
            var d = new Vector2(1f, -1f).normalized;
            var r = LightMath.Reflect(d, Vector2.up);
            Assert.AreEqual(d.x, r.x, 1e-5f);
            Assert.AreEqual(-d.y, r.y, 1e-5f);
            Assert.AreEqual(Vector2.Angle(-d, Vector2.up), Vector2.Angle(r, Vector2.up), 1e-3f);
        }

        [Test] public void Schlick_GrazingIsFullyReflective()
        {
            Assert.AreEqual(0.04f, LightMath.Schlick(1f, 0.04f), 1e-5f);
            Assert.AreEqual(1f, LightMath.Schlick(0f, 0.04f), 1e-5f);
        }

        [Test] public void AdditiveMixing()
        {
            Assert.AreEqual(LightColor.Yellow, LightMath.Mix(LightColor.Red, LightColor.Green));
            Assert.AreEqual(LightColor.White, LightMath.Mix(LightColor.Yellow, LightColor.Blue));
        }

        [Test] public void Prism_BlueDeviatesMoreThanRed()
        {
            Assert.Greater(LightMath.PrismDeviation(LightColor.Blue), LightMath.PrismDeviation(LightColor.Red));
        }
    }

    public class LightRulesTests
    {
        [Test] public void Red_Burns()
        {
            var fx = LightRules.Evaluate(new LightExposure(1f, 0, 0), false);
            Assert.Greater(fx.damagePerSecond, 0f);
            Assert.IsFalse(fx.lethal);
            Assert.IsTrue(LightRules.Evaluate(new LightExposure(LightRules.LethalRed, 0, 0), false).lethal);
        }

        [Test] public void Green_HealsAndRefills()
        {
            var fx = LightRules.Evaluate(new LightExposure(0, 1f, 0), false);
            Assert.Greater(fx.healPerSecond, 0f);
            Assert.Greater(fx.resonancePerSecond, 0f);
            Assert.AreEqual(0f, fx.damagePerSecond);
        }

        [Test] public void Blue_LowersGravity()
        {
            var fx = LightRules.Evaluate(new LightExposure(0, 0, 1f), false);
            Assert.Less(fx.gravityScale, 1f);
            Assert.AreEqual(1f, LightRules.Evaluate(new LightExposure(), false).gravityScale);
        }

        [Test] public void RedPlusGreen_BehavesYellow_OnlyHurtsOnBeat()
        {
            var off = LightRules.Evaluate(new LightExposure(1f, 1f, 0), false);
            var on = LightRules.Evaluate(new LightExposure(1f, 1f, 0), true);
            Assert.AreEqual(0f, off.damagePerSecond, 1e-4f);
            Assert.Greater(on.damagePerSecond, 0f);
            Assert.AreEqual(0f, on.healPerSecond, 1e-4f);
        }

        [Test] public void White_IsNeutral()
        {
            var fx = LightRules.Evaluate(new LightExposure(1f, 1f, 1f), true);
            Assert.AreEqual(0f, fx.damagePerSecond, 1e-4f);
            Assert.AreEqual(1f, fx.gravityScale, 1e-4f);
        }

        [Test] public void Darkness_RefillsResonance()
        {
            var fx = LightRules.Evaluate(new LightExposure(), false);
            Assert.IsTrue(fx.inDarkness);
            Assert.Greater(fx.resonancePerSecond, 0f);
        }
    }

    public class HarmonyTests
    {
        [Test] public void JustIntonation_Ratios()
        {
            Assert.AreEqual(1.5f, Harmony.Degree(4) / Harmony.Degree(0), 1e-5f);
            Assert.AreEqual(2f, Harmony.Degree(7) / Harmony.Degree(0), 1e-5f);
            Assert.AreEqual(1.25f, Harmony.Degree(2) / Harmony.Degree(0), 1e-5f);
        }

        [Test] public void HarmonicSeries()
        {
            Assert.AreEqual(Harmony.Root * 3f, Harmony.Harmonic(3), 1e-3f);
        }
    }

    public class LevelDataTests
    {
        [Test] public void Parse_GridAndEntities()
        {
            var d = LevelData.Parse("name: Test\n---\n P  E\n#####\n---\nbeam 3 4 color=red dir=-90\n", "t");
            Assert.AreEqual("Test", d.Name);
            Assert.AreEqual(2, d.Height);
            Assert.AreEqual('#', d.Tile(0, 0));
            Assert.AreEqual('P', d.Tile(1, 1));
            Assert.IsTrue(d.entities.Exists(e => e.type == "beam" && e.Color() == LightColor.Red));
            Assert.IsTrue(d.entities.Exists(e => e.type == "player"));
            Assert.IsTrue(d.entities.Exists(e => e.type == "exit"));
            var beam = d.entities.Find(e => e.type == "beam");
            Assert.AreEqual(-1f, beam.Dir().y, 1e-4f);
        }

        [Test] public void AllShippedLevelsParse()
        {
            Assert.Greater(LevelDatabase.Count, 0);
            for (int i = 1; i <= LevelDatabase.Count; i++)
            {
                var d = LevelDatabase.Load(i);
                Assert.Greater(d.Width, 0, d.id);
                Assert.IsTrue(d.entities.Exists(e => e.type == "player"), d.id + " has no player");
                Assert.IsTrue(d.entities.Exists(e => e.type == "exit"), d.id + " has no exit");
            }
        }
    }

    public class InputTests
    {
        [Test] public void AllActionsBound_KeyboardAndGamepad()
        {
            var asset = LumenInput.BuildAsset();
            foreach (var name in new[] { "Move", "Jump", "Dash", "Echo", "Shield", "Pulse", "Pause" })
            {
                var a = asset.FindAction("Gameplay/" + name, true);
                bool kb = false, pad = false;
                foreach (var b in a.bindings)
                {
                    if (b.path != null && b.path.StartsWith("<Keyboard>")) kb = true;
                    if (b.path != null && b.path.StartsWith("<Gamepad>")) pad = true;
                }
                Assert.IsTrue(kb, name + " keyboard");
                Assert.IsTrue(pad, name + " gamepad");
            }
        }

        [Test] public void Overrides_RoundTrip()
        {
            var asset = LumenInput.BuildAsset();
            var jump = asset.FindAction("Gameplay/Jump", true);
            int idx = LumenInput.BindingIndex(jump, LumenInput.KeyboardGroup);
            jump.ApplyBindingOverride(idx, "<Keyboard>/w");
            var json = LumenInput.SerializeOverrides(asset);
            var asset2 = LumenInput.BuildAsset();
            LumenInput.ApplyOverrides(asset2, json);
            Assert.AreEqual("<Keyboard>/w", asset2.FindAction("Gameplay/Jump").bindings[idx].effectivePath);
        }
    }
}
