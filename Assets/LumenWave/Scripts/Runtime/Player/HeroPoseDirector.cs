using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumenWave
{
    /// <summary>
    /// Debug capture of Umbra's animation states in the real build:
    /// <c>LumenWave.exe -lw-heroposes &lt;dir&gt;</c>. Boots a flat test level, drives Umbra with scripted
    /// commands and saves one PNG per state (idle, run, jump, fall, land, dash, cast, shield, hurt, sing,
    /// death, respawn). Logs Umbra's screen rectangle per shot ("[LumenWave] HeroPose name x y h") for cropping.
    /// </summary>
    public class HeroPoseDirector : MonoBehaviour
    {
        public string outDir;
        /// <summary>True for <c>-lw-enemyposes</c>: close-ups of every enemy (calm, hunting, stunned).</summary>
        public bool enemies;

        class Script : ICommandSource
        {
            public float moveX;
            public bool jumpHeld, shieldHeld, jump, dash, echo, pulse;
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
                if (args[i] == "-lw-heroposes" || args[i] == "-lw-enemyposes")
                {
                    var go = new GameObject("HeroPoseDirector");
                    DontDestroyOnLoad(go);
                    var d = go.AddComponent<HeroPoseDirector>();
                    d.outDir = args[i + 1];
                    d.enemies = args[i] == "-lw-enemyposes";
                }
        }

        Script _cmd;
        UmbraController _body;
        PlayerHealth _health;
        UmbraRig _rig;

        static string Level(string mood, string entities = "player 8.5 2\n")
        {
            var sb = new StringBuilder();
            sb.Append("name: Hero\nmood: ").Append(mood).Append("\nfog: 0.02\n---\n");
            for (int row = 0; row < 12; row++)
            {
                for (int x = 0; x < 120; x++) sb.Append(row >= 10 || (row == 9 && x > 40 && x < 44) ? '#' : ' ');
                sb.Append('\n');
            }
            sb.Append("---\n").Append(entities);
            return sb.ToString();
        }

        IEnumerator Boot(string entities = "player 8.5 2\n", string mood = "blue")
        {
            SceneBootstrap.PendingLevelData = LevelData.Parse(Level(mood, entities), "heroposes");
            var prev = LevelRuntime.Current;
            SceneManager.LoadScene(SceneFlow.LevelScene(1));
            yield return null;
            float w = 0f;
            while ((LevelRuntime.Current == null || LevelRuntime.Current == prev || LevelRuntime.Current.Player == null) && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
            var p = LevelRuntime.Current.Player;
            _body = p.GetComponent<UmbraController>();
            _health = p.GetComponent<PlayerHealth>();
            _rig = p.GetComponent<UmbraRig>();
            _cmd = new Script();
            _body.Commands = _cmd;
            var res = p.GetComponent<Resonance>();
            if (res != null) res.infinite = true;
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(outDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            var cam = Camera.main;
            if (cam != null && _body != null)
            {
                var a = cam.WorldToScreenPoint(_body.transform.position);
                var b = cam.WorldToScreenPoint(_body.transform.position + Vector3.up * _body.height);
                Debug.Log($"[LumenWave] HeroPose {name} {a.x:0} {Screen.height - a.y:0} {b.y - a.y:0} state={_rig?.Current}");
            }
            yield return null;
        }

        IEnumerator EnemyShots()
        {
            yield return Boot("player 8.5 2\nsentinel 13.5 6.5 range=6 sweep=30 speed=0.8\nflare 18.5 3.5 range=12 period=2.4\nmoth 4.5 4.5\nwisp 21.5 5.5 sight=20\n", "violet");
            _health.godMode = true;
            yield return new WaitForSeconds(1.0f);
            yield return EnemyShot("enemies_calm");
            yield return new WaitForSeconds(0.9f);
            yield return EnemyShot("enemies_calm2");
            _cmd.echo = true;
            yield return new WaitForSeconds(0.35f);
            yield return EnemyShot("enemies_hunt");
            yield return new WaitForSeconds(0.6f);
            _cmd.pulse = true;
            yield return new WaitForSeconds(0.9f);
            yield return EnemyShot("enemies_stunned");
            Debug.Log("[LumenWave] EnemyPoses done");
        }

        IEnumerator EnemyShot(string name)
        {
            yield return Shot(name);
            var cam = Camera.main;
            if (cam == null) yield break;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is Sentinel || mb is Flare || mb is EchoMoth || mb is Wisp)
                {
                    var a = cam.WorldToScreenPoint(mb.transform.position);
                    var b = cam.WorldToScreenPoint(mb.transform.position + Vector3.up);
                    Debug.Log($"[LumenWave] EnemyPose {name} {mb.GetType().Name} {a.x:0} {Screen.height - a.y:0} {b.y - a.y:0}");
                }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            GameSession.NewRun();
            GameSession.BotMode = true;
            if (enemies) { yield return EnemyShots(); Application.Quit(); yield break; }
            yield return Boot();
            _health.godMode = true;

            yield return new WaitForSeconds(1.0f);
            yield return Shot("idle");

            _cmd.moveX = 1f;
            yield return new WaitForSeconds(0.25f); yield return Shot("walk_start");
            yield return new WaitForSeconds(0.6f); yield return Shot("run_a");
            yield return new WaitForSeconds(0.16f); yield return Shot("run_b");
            _cmd.jumpHeld = true; _cmd.jump = true;
            yield return new WaitForSeconds(0.12f); yield return Shot("jump_rise");
            yield return new WaitForSeconds(0.3f); yield return Shot("jump_apex");
            _cmd.jumpHeld = false;
            yield return new WaitForSeconds(0.3f); yield return Shot("fall");
            while (!_body.Grounded) yield return null;
            _cmd.moveX = 0f;
            yield return null; yield return Shot("land");
            yield return new WaitForSeconds(0.8f);

            _cmd.dash = true;
            yield return new WaitForSeconds(0.15f); yield return Shot("dash");
            yield return new WaitForSeconds(0.8f);

            _cmd.echo = true;
            yield return new WaitForSeconds(0.17f); yield return Shot("cast");
            yield return new WaitForSeconds(0.8f);

            _cmd.shieldHeld = true;
            yield return new WaitForSeconds(0.45f); yield return Shot("shield");
            _cmd.shieldHeld = false;
            yield return new WaitForSeconds(0.8f);

            _cmd.moveX = -1f; yield return new WaitForSeconds(0.5f); yield return Shot("run_left");
            _cmd.moveX = 0f; yield return new WaitForSeconds(0.8f); yield return Shot("idle_left");
            _cmd.moveX = 1f; yield return new WaitForSeconds(0.1f); _cmd.moveX = 0f;
            yield return new WaitForSeconds(0.8f);

            _health.godMode = false;
            _health.Damage(5f);
            _health.godMode = true;
            yield return new WaitForSeconds(0.05f); yield return Shot("hurt");
            yield return new WaitForSeconds(0.8f);

            for (float t = 0f; t < 1f; t += Time.deltaTime) { _rig.Singing = t; yield return null; }
            _rig.Singing = 1f;
            yield return new WaitForSeconds(0.6f); yield return Shot("sing");
            for (float t = 1f; t > 0f; t -= Time.deltaTime * 2f) { _rig.Singing = t; yield return null; }
            _rig.Singing = 0f;
            yield return new WaitForSeconds(0.5f);

            _health.godMode = false;
            _health.Kill();
            yield return new WaitForSeconds(0.25f); yield return Shot("death_a");
            yield return new WaitForSeconds(0.35f); yield return Shot("death_b");
            while (_health.IsDead) yield return null;
            yield return new WaitForSeconds(0.15f); yield return Shot("respawn");
            yield return new WaitForSeconds(0.6f);
            Debug.Log("[LumenWave] HeroPoses done");
            Application.Quit();
        }
    }
}
