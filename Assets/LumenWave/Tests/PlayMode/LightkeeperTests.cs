using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Lightkeeper mode: reactive bot vs. lamps on the arena level.</summary>
    public class LightkeeperTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LevelTestUtil.ClearScene();
            yield return null;
        }

        static IEnumerator Boot(System.Action<LevelRuntime, LightkeeperController> ready)
        {
            LevelTestUtil.ClearScene();
            yield return null;
            var rt = SceneBootstrap.BootLevel(LightkeeperMode.LoadByName(LightkeeperMode.ArenaName), 1);
            yield return null;
            yield return null;
            var c = LightkeeperController.Instance;
            Assert.IsNotNull(c, "arena boots in Lightkeeper mode");
            Assert.AreEqual(4, c.Lamps.Count, "four lamps");
            Assert.IsTrue(LightkeeperMode.Active);
            c.humanInput = false;
            ready(rt, c);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ReactiveBot_ReachesDoor_WithLampsOff()
        {
            LevelRuntime rt = null; LightkeeperController c = null;
            yield return Boot((r, k) => { rt = r; c = k; });
            Time.timeScale = 2.5f;
            float t = 0f;
            while (!rt.Completed && t < 80f) { t += Time.deltaTime; yield return null; }
            Time.timeScale = 1f;
            var p = rt.Player.transform.position;
            Debug.Log($"[LumenWave] LK bot lamps off: completed={rt.Completed} t={t:F1} deaths={c.BotDeaths} pos=({p.x:F1},{p.y:F1}) state={c.Bot.State}");
            Assert.IsTrue(rt.Completed, $"bot reached the door (stuck at x={p.x:F1}, y={p.y:F1}, state {c.Bot.State})");
            yield return null;
            Assert.IsTrue(c.Over && !c.LightkeeperWon, "Umbra escapes");
            Assert.AreEqual(GameSession.CurrentLevel, 1, "no advance to the next campaign level");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator StaticLethalBeam_BlocksBot()
        {
            LevelRuntime rt = null; LightkeeperController c = null;
            yield return Boot((r, k) => { rt = r; c = k; });
            c.drainPerLamp = 0f;
            c.SetLampX(0, 14f);
            c.SetLampAngle(0, -90f);
            c.SetLampColor(0, LightColor.Red);
            Assert.IsTrue(c.SetLampOn(0, true));
            var res = rt.Player.GetComponent<Resonance>();
            Time.timeScale = 2.5f;
            float t = 0f, maxX = -1f;
            while (!rt.Completed && t < 20f)
            {
                if (res != null) res.current = 0f;   // no shield: the bot must wait or burn
                maxX = Mathf.Max(maxX, rt.Player.transform.position.x);
                t += Time.deltaTime;
                yield return null;
            }
            Time.timeScale = 1f;
            Debug.Log($"[LumenWave] LK static beam: completed={rt.Completed} danger={c.Lamps[0].beam.Danger} deaths={c.BotDeaths} maxX={maxX:F1} state={c.Bot.State}");
            Assert.AreEqual(LightBeam.BeamDanger.Lethal, c.Lamps[0].beam.Danger, "lamp is lethal at the floor");
            Assert.IsFalse(rt.Completed, "a static lethal beam keeps Umbra from the door");
            Assert.Less(maxX, 30f, "Umbra never got far past the beam");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator OscMessage_MovesLamp()
        {
            LevelRuntime rt = null; LightkeeperController c = null;
            yield return Boot((r, k) => { rt = r; c = k; });
            var osc = LightkeeperOsc.Attach(c, 0);   // 0 = any free port
            Assert.Greater(osc.Port, 0, "listener bound");
            using (var udp = new System.Net.Sockets.UdpClient())
            {
                void Send(byte[] b) => udp.Send(b, b.Length, "127.0.0.1", osc.Port);
                Send(MA.Osc.Encode("/lamp/2/x", 40.5f));
                Send(MA.Osc.Encode("/lamp/2/angle", 30f));
                Send(MA.Osc.Encode("/lamp/2/color", "green"));
                Send(MA.Osc.Encode("/lamp/2/on", 1));
            }
            float t = 0f;
            while (osc.Received < 4 && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
            var lamp = c.Lamps[1];
            Assert.AreEqual(4, osc.Received, "all OSC messages received");
            Assert.AreEqual(40.5f, lamp.X, 0.01f, "OSC /lamp/2/x moved the lamp");
            Assert.AreEqual(-60f, lamp.angle, 0.01f, "OSC angle (0 = down)");
            Assert.AreEqual(LightColor.Green, lamp.beam.color);
            Assert.IsTrue(lamp.On);
        }
    }
}
