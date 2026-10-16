using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Proves the Lightkeeper keyboard controls through the real Input System (virtual keyboard).</summary>
    public class LightkeeperKeyboardTests : InputTestFixture
    {
        Keyboard _kb;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            LightkeeperInput.Reset();
        }

        public override void TearDown()
        {
            LevelTestUtil.ClearScene();
            LightkeeperInput.Reset();
            base.TearDown();
        }

        IEnumerator Tap(UnityEngine.InputSystem.Controls.ButtonControl key, int holdFrames = 2)
        {
            Press(key, queueEventOnly: true);
            for (int i = 0; i < holdFrames; i++) yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
            yield return null;
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator Keys_1_W_D_Q_R_DriveLamp1()
        {
            LevelTestUtil.ClearScene();
            yield return null;
            SceneBootstrap.BootLevel(LightkeeperMode.LoadByName(LightkeeperMode.ArenaName), 1);
            yield return null;
            yield return null;
            var c = LightkeeperController.Instance;
            Assert.IsNotNull(c, "arena boots in Lightkeeper mode");
            Assert.IsTrue(LightkeeperInput.Map.enabled, "Lightkeeper action map enabled");
            Assert.IsFalse(LumenInput.Move.enabled, "player gameplay map disabled");
            var lamp = c.Lamps[0];
            float x0 = lamp.X, a0 = lamp.angle;
            var col0 = lamp.beam.color;

            yield return Tap(_kb.digit1Key);
            Assert.AreEqual(0, c.Selected, "1 selects lamp 1");
            yield return Tap(_kb.wKey);
            Assert.IsTrue(lamp.On, "W turns lamp 1 on");
            yield return Tap(_kb.dKey, 20);
            Assert.Greater(lamp.X, x0 + 0.5f, "D moves lamp 1 right");
            yield return Tap(_kb.qKey, 20);
            Assert.Less(lamp.angle, a0 - 2f, "Q rotates lamp 1");
            yield return Tap(_kb.rKey);
            Assert.AreNotEqual(col0, lamp.beam.color, "R changes lamp 1's colour");
        }
    }
}
