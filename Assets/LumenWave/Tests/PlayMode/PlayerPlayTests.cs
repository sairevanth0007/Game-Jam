using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LumenWave.Tests
{
    /// <summary>Scripted command source: tests set fields; press flags are consumed on poll.</summary>
    public class PlayerTestCommands : ICommandSource
    {
        public float moveX;
        public bool jumpHeld, shieldHeld;
        public bool jumpPressed, dashPressed, echoPressed, pulsePressed;

        public PlayerCommands Poll()
        {
            var c = new PlayerCommands
            {
                moveX = moveX, jumpHeld = jumpHeld, shieldHeld = shieldHeld,
                jumpPressed = jumpPressed, dashPressed = dashPressed, echoPressed = echoPressed, pulsePressed = pulsePressed,
            };
            jumpPressed = dashPressed = echoPressed = pulsePressed = false;
            return c;
        }
    }

    public class PlayerPlayTests
    {
        const string Flat =
            "name: PlayerFlat\n---\n" +
            "                                                            \n" +
            "                                                            \n" +
            "                                                            \n" +
            "                                                            \n" +
            "                                                            \n" +
            "                                                            \n" +
            "   P                                                        \n" +
            "############################################################\n" +
            "############################################################\n";

        // Raised ledge (top at y=5) ending at x=10, then a low floor (top at y=1).
        const string Ledge =
            "name: PlayerLedge\n---\n" +
            "                                        \n" +
            "                                        \n" +
            "                                        \n" +
            "                                        \n" +
            "   P                                    \n" +
            "##########                              \n" +
            "##########                              \n" +
            "##########                              \n" +
            "##########                              \n" +
            "########################################\n";

        LevelRuntime _rt;
        UmbraController _ctl;
        PlayerTestCommands _cmd;

        IEnumerator Boot(string level)
        {
            _rt = SceneBootstrap.BootLevel(LevelData.Parse(level, "test"), 1);
            Assert.IsNotNull(_rt.Player, "player spawned");
            _ctl = _rt.Player.GetComponent<UmbraController>();
            Assert.IsNotNull(_ctl);
            // Isolate the controller from other systems (light receiver may rewrite GravityScale etc).
            foreach (var mb in _rt.Player.GetComponents<MonoBehaviour>())
                if (!(mb is UmbraController || mb is PlayerHealth || mb is Resonance || mb is UmbraRig)) mb.enabled = false;
            _rt.Player.GetComponent<PlayerHealth>().godMode = true;
            _cmd = new PlayerTestCommands();
            _ctl.Commands = _cmd;
            yield return WaitSeconds(0.4f); // settle on the ground
        }

        static IEnumerator WaitSeconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_rt != null) Object.Destroy(_rt.gameObject);
            foreach (var g in Object.FindObjectsByType<UmbraGhost>(FindObjectsSortMode.None)) Object.Destroy(g.gameObject);
            _rt = null;
        }

        [UnityTest]
        public IEnumerator SpawnsAndLandsOnGround()
        {
            yield return Boot(Flat);
            Assert.IsTrue(_ctl.Grounded, "grounded after settle");
            Assert.AreEqual(2f, _ctl.transform.position.y, 0.06f, "feet on top of floor (y=2)");
        }

        [UnityTest]
        public IEnumerator MovesRight()
        {
            yield return Boot(Flat);
            float x0 = _ctl.transform.position.x;
            _cmd.moveX = 1f;
            yield return WaitSeconds(1f);
            float dx = _ctl.transform.position.x - x0;
            Assert.Greater(dx, 5f, "ran right at ~7 u/s");
            Assert.AreEqual(1, _ctl.Facing);
            Assert.AreEqual(0f, _ctl.transform.position.z, 1e-4f, "Z locked");
        }

        IEnumerator MeasureJump(bool hold, System.Action<float> result, float gravityScale = 1f)
        {
            float y0 = _ctl.transform.position.y, peak = y0;
            _cmd.jumpPressed = true;
            _cmd.jumpHeld = true;
            float t = 0f;
            bool released = false;
            while (t < 2.5f)
            {
                _ctl.GravityScale = gravityScale;
                yield return null;
                t += Time.deltaTime;
                if (!hold && !released && t > 0.06f) { _cmd.jumpHeld = false; released = true; }
                peak = Mathf.Max(peak, _ctl.transform.position.y);
                if (t > 0.2f && _ctl.Grounded) break;
            }
            _cmd.jumpHeld = false;
            result(peak - y0);
        }

        [UnityTest]
        public IEnumerator HeldJumpIsHigherThanTap()
        {
            yield return Boot(Flat);
            float held = 0, tap = 0;
            yield return MeasureJump(true, h => held = h);
            yield return WaitSeconds(0.3f);
            yield return MeasureJump(false, h => tap = h);
            Assert.Greater(held, 2.6f, "full jump ~3.2");
            Assert.Greater(tap, 0.3f, "tap still jumps");
            Assert.Greater(held, tap * 1.5f, $"held {held} vs tap {tap}");
        }

        [UnityTest]
        public IEnumerator LowGravityJumpsHigher()
        {
            yield return Boot(Flat);
            float normal = 0, blue = 0;
            yield return MeasureJump(true, h => normal = h);
            yield return WaitSeconds(0.3f);
            yield return MeasureJump(true, h => blue = h, 0.35f);
            _ctl.GravityScale = 1f;
            Assert.Greater(blue, normal * 1.8f, $"blue {blue} vs normal {normal}");
        }

        [UnityTest]
        public IEnumerator CoyoteJumpAfterLeavingLedge()
        {
            yield return Boot(Ledge);
            Assert.IsTrue(_ctl.Grounded);
            _cmd.moveX = 1f;
            float guard = 0f;
            while (_ctl.Grounded && guard < 3f) { yield return null; guard += Time.deltaTime; }
            Assert.IsFalse(_ctl.Grounded, "ran off the ledge");
            yield return WaitSeconds(0.05f);
            Assert.IsFalse(_ctl.Grounded);
            _cmd.jumpPressed = true; _cmd.jumpHeld = true;
            float y = _ctl.transform.position.y;
            yield return WaitSeconds(0.15f);
            Assert.Greater(_ctl.transform.position.y, y + 0.3f, "coyote jump went up");
            _cmd.jumpHeld = false;
        }

        [UnityTest]
        public IEnumerator NoJumpLongAfterLeavingLedge()
        {
            yield return Boot(Ledge);
            _cmd.moveX = 1f;
            float guard = 0f;
            while (_ctl.Grounded && guard < 3f) { yield return null; guard += Time.deltaTime; }
            yield return WaitSeconds(0.25f);
            float vy0 = _ctl.Velocity.y;
            _cmd.jumpPressed = true; _cmd.jumpHeld = true;
            yield return null; yield return null;
            Assert.LessOrEqual(_ctl.Velocity.y, vy0 + 0.01f, "no jump after coyote window");
        }

        [UnityTest]
        public IEnumerator DashCoversDistance()
        {
            yield return Boot(Flat);
            float x0 = _ctl.transform.position.x;
            _cmd.dashPressed = true;
            yield return WaitSeconds(0.2f);
            float dx = _ctl.transform.position.x - x0;
            Assert.Greater(dx, 2.3f, "dash ~3 units in 0.18 s");
        }

        [UnityTest]
        public IEnumerator OnlyOneAirDash()
        {
            yield return Boot(Flat);
            _ctl.GravityScale = 0.3f; // long airtime so the dash cooldown expires mid-air
            _cmd.jumpPressed = true; _cmd.jumpHeld = true;
            yield return WaitSeconds(0.1f);
            float x0 = _ctl.transform.position.x;
            _cmd.dashPressed = true;
            yield return WaitSeconds(0.7f);
            Assert.Greater(_ctl.transform.position.x - x0, 2.3f, "first air dash");
            Assert.IsFalse(_ctl.Grounded, "still airborne");
            float x1 = _ctl.transform.position.x;
            _cmd.dashPressed = true;
            yield return WaitSeconds(0.15f);
            Assert.Less(_ctl.transform.position.x - x1, 1.0f, "second air dash refused");
            _cmd.jumpHeld = false;
            _ctl.GravityScale = 1f;
        }

        [UnityTest]
        public IEnumerator BlockedByWall()
        {
            const string wall =
                "name: Wall\n---\n" +
                "            #       \n" +
                "            #       \n" +
                "            #       \n" +
                "   P        #       \n" +
                "####################\n";
            yield return Boot(wall);
            _cmd.moveX = 1f;
            yield return WaitSeconds(1.5f);
            Assert.LessOrEqual(_ctl.transform.position.x + _ctl.width * 0.5f, 12.01f, "stopped at the wall");
            Assert.Greater(_ctl.transform.position.x, 10.5f);
        }
    }
}
