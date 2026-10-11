using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumenWave
{
    /// <summary>
    /// Debug capture of every power and light rule in the real build:
    /// <c>LumenWave.exe -lw-showcase &lt;dir&gt;</c>. Each shot boots a tiny purpose-built level (via
    /// <see cref="SceneBootstrap.PendingLevelData"/>), drives Umbra with scripted commands and saves PNGs.
    /// </summary>
    public class ShowcaseDirector : MonoBehaviour
    {
        public string outDir;

        /// <summary>Scripted input: edge flags are consumed by the first Poll each frame.</summary>
        class Script : ICommandSource
        {
            public float moveX;
            public bool jumpHeld, shieldHeld;
            public bool jump, dash, echo, pulse;
            public PlayerCommands Poll()
            {
                var c = new PlayerCommands
                {
                    moveX = moveX, jumpHeld = jumpHeld || jump, shieldHeld = shieldHeld,
                    jumpPressed = jump, dashPressed = dash, echoPressed = echo, pulsePressed = pulse,
                };
                jump = dash = echo = pulse = false;
                return c;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-lw-showcase")
                {
                    var go = new GameObject("ShowcaseDirector");
                    DontDestroyOnLoad(go);
                    go.AddComponent<ShowcaseDirector>().outDir = args[i + 1];
                }
        }

        Script _cmd;
        LevelRuntime _rt;
        UmbraController _body;

        /// <summary>Flat ground (2 rows) of the given width with optional gaps "[from,to)" in columns.</summary>
        static string Level(string mood, int width, int height, string entities, params (int from, int to)[] gaps)
        {
            var sb = new StringBuilder();
            sb.Append("name: Showcase\nmood: ").Append(mood).Append("\nfog: 0.03\n---\n");
            for (int row = 0; row < height; row++)
            {
                bool ground = row >= height - 2;
                for (int x = 0; x < width; x++)
                {
                    bool gap = false;
                    foreach (var g in gaps) if (x >= g.from && x < g.to) gap = true;
                    sb.Append(ground && !gap ? '#' : ' ');
                }
                sb.Append('\n');
            }
            sb.Append("---\n").Append(entities).Append('\n');
            return sb.ToString();
        }

        IEnumerator Boot(string levelText, float settle = 1.2f)
        {
            _rt = null; _body = null;
            SceneBootstrap.PendingLevelData = LevelData.Parse(levelText, "showcase");
            var prev = LevelRuntime.Current;
            SceneManager.LoadScene(SceneFlow.LevelScene(1));
            yield return null;   // the load happens at the end of this frame
            float w = 0f;
            while ((LevelRuntime.Current == null || LevelRuntime.Current == prev || LevelRuntime.Current.Player == null) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
            _rt = LevelRuntime.Current;
            _body = _rt != null && _rt.Player != null ? _rt.Player.GetComponent<UmbraController>() : null;
            _cmd = new Script();
            if (_body != null) _body.Commands = _cmd;
            var res = _rt?.Player?.GetComponent<Resonance>();
            if (res != null) res.infinite = true;
            yield return new WaitForSeconds(settle);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[LumenWave] Showcase " + path);
            yield return null;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            GameSession.NewRun();
            GameSession.BotMode = true;

            // 1. Echo Path forming over a gap, then Umbra standing on the finished bridge.
            yield return Boot(Level("violet", 40, 12, "player 9.5 2", (12, 19)));
            _cmd.moveX = 1f; yield return new WaitForSeconds(0.25f); _cmd.moveX = 0f;
            yield return new WaitForSeconds(0.4f);
            _cmd.echo = true;
            yield return new WaitForSeconds(0.16f);
            yield return Shot("echo_forming");
            yield return new WaitForSeconds(0.5f);
            _cmd.moveX = 1f; yield return new WaitForSeconds(0.55f); _cmd.moveX = 0f;
            yield return new WaitForSeconds(0.35f);
            yield return Shot("echo_bridge");

            // 2. Shield held inside a red beam.
            yield return Boot(Level("blue", 40, 12, "player 8.5 2\nbeam 14 9 color=red dir=-90 range=12 intensity=1.2 width=0.9"));
            _cmd.shieldHeld = true;
            _cmd.moveX = 1f; yield return new WaitForSeconds(0.75f); _cmd.moveX = 0f;
            yield return new WaitForSeconds(0.6f);
            yield return Shot("shield");
            _cmd.shieldHeld = false;

            // 3. Pulse: expanding ring stuns a sentinel and shatters a crystal.
            yield return Boot(Level("blue", 40, 12, "player 10.5 2\ncrystal 15.5 2\ncrystal 15.5 3\nsentinel 18 8 range=8 sweep=40 speed=0.6"));
            _cmd.pulse = true;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("pulse_ring");
            yield return new WaitForSeconds(0.35f);
            yield return Shot("pulse_shatter");

            // 4. Dash afterimages mid-air.
            yield return Boot(Level("gold", 50, 12, "player 8.5 2"));
            _cmd.moveX = 1f; yield return new WaitForSeconds(0.3f);
            _cmd.jump = true; _cmd.jumpHeld = true; yield return new WaitForSeconds(0.22f);
            _cmd.dash = true; yield return new WaitForSeconds(0.12f);
            yield return Shot("dash");
            _cmd.jumpHeld = false; _cmd.moveX = 0f;

            // 5. Blue light: low-gravity leap.
            yield return Boot(Level("green", 40, 14, "player 9.5 2\nlamp 6 7.5 color=blue radius=9 intensity=1.6"));
            _cmd.moveX = 0.6f; _cmd.jump = true; _cmd.jumpHeld = true;
            yield return new WaitForSeconds(0.55f);
            yield return Shot("blue_lift");
            _cmd.moveX = 0f; _cmd.jumpHeld = false;

            // 6. Green light heals (start hurt).
            yield return Boot(Level("blue", 40, 12, "player 10.5 2\nlamp 11 6 color=green radius=6 intensity=1.4"));
            var hp = _rt?.Player?.GetComponent<PlayerHealth>();
            if (hp != null) hp.current = hp.max * 0.35f;
            yield return new WaitForSeconds(0.8f);
            yield return Shot("green_heal");

            // 7. Yellow beat laser, off then on.
            yield return Boot(Level("gold", 40, 12, "player 6.5 2\nbeam 11 3.4 color=yellow dir=0 range=26 intensity=1"));
            yield return WaitBeat(false);
            yield return Shot("beat_off");
            yield return WaitBeat(true);
            yield return Shot("beat_laser");

            // 8. Death on thorns and respawn.
            yield return Boot(Level("red", 40, 12, "player 8.5 2\nthorns 12.5 2"));
            _cmd.moveX = 1f;
            float t = 0f;
            var health = _rt?.Player?.GetComponent<PlayerHealth>();
            while (health != null && !health.IsDead && t < 3f) { t += Time.deltaTime; yield return null; }
            _cmd.moveX = 0f;
            yield return new WaitForSeconds(0.35f);
            yield return Shot("death");
            yield return new WaitForSeconds(1.4f);
            yield return Shot("respawn");

            // 9. Level start card (normal runs show it automatically; capture runs skip fades).
            yield return Boot(Level("violet", 40, 12, "player 9.5 2"), 0.2f);
            ScreenFader.Instance?.ShowCard(8, "Prism and Mirror");
            yield return new WaitForSeconds(1.3f);
            yield return Shot("level_card");

            Debug.Log("[LumenWave] Showcase done");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(0);
        }

        IEnumerator WaitBeat(bool hot)
        {
            float t = 0f;
            // Wait for the right half of the beat (well inside the window) so the frame is unambiguous.
            while (t < 4f)
            {
                var c = BeatClock.Instance;
                if (c != null && (hot ? c.Phase > 0.08f && c.Phase < 0.2f : c.Phase > 0.6f && c.Phase < 0.75f)) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
