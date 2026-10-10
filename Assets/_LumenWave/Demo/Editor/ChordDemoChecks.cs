using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LumenWave.Demo.Editor
{
    [InitializeOnLoad]
    public static class ChordDemoChecks
    {
        const string KeyName = "LumenWave.ChordDemoChecks";
        static ChordMotor player;
        static DemoSession session;
        static Keyboard keyboard;
        static Key[] heldKeys = Array.Empty<Key>();
        static int stage;
        static double next;
        static float damagedHealth;
        static string report;
        static readonly Vector2[] route = { new Vector2(-1.5f,1.4f), new Vector2(1.8f,2.8f), new Vector2(4.3f,4.2f),
            new Vector2(1.2f,5.6f), new Vector2(-2,7), new Vector2(-4.2f,8.4f), new Vector2(-1.1f,9.8f),
            new Vector2(2.1f,11.2f), new Vector2(4.3f,12.6f), new Vector2(.9f,14), new Vector2(-2.5f,15.4f) };
        static int landing;
        static bool jumping;
        static float routeDeadline, pauseUntil, jumpUntil;
        static ChordDemoChecks() { EditorApplication.playModeStateChanged += ModeChanged; }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor for these checks.");
            EditorSceneManager.OpenScene(ChordDemoBuilder.TowerPath);
            SessionState.SetBool(KeyName, true);
            EditorApplication.EnterPlaymode();
        }
        static void ModeChanged(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(KeyName,false)) return;
            player = UnityEngine.Object.FindFirstObjectByType<ChordMotor>();
            session = UnityEngine.Object.FindFirstObjectByType<DemoSession>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.onBeforeUpdate += FeedKeyboard;
            stage = 0; report = ""; next = Time.time + 1;
            EditorApplication.update += Tick;
        }
        static void Keys(params Key[] keys) { heldKeys = keys; }
        static void FeedKeyboard()
        {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
        }
        static void Require(bool condition,string text)
        {
            if (!condition) throw new Exception("FAIL: "+text);
            report += "PASS: "+text+"\n"; Debug.Log("PASS: "+text);
        }
        static void Tick()
        {
            if (Time.time < next) return;
            double delay = .3;
            try
            {
                switch(stage++)
                {
                    case 0:
                        Capture("Logs/ChordDemoEntrance.png");
                        Require(player.Grounded,"Player lands on Cainos platform");
                        foreach (var sprite in player.GetComponentsInChildren<SpriteRenderer>())
                            Require(sprite.sprite != null,"Player sprite is imported: "+sprite.name);
                        Require(ChordInput.Direction(1)==0 && ChordInput.Direction(3)==-1 && ChordInput.Direction(12)==1 && ChordInput.Direction(15)==0,"Chord direction and opposing-pair cancellation");
                        Keys(Key.Q); break;
                    case 1:
                        Require(Mathf.Abs(player.Body.linearVelocity.x)<.01f,"Single note does not move player");
                        Require(player.GetComponents<AudioSource>().Length==5,"Five note voices are initialized");
                        Keys(Key.O,Key.P); break;
                    case 2:
                        Require(player.Body.linearVelocity.x>2,"O+P moves right via keyboard input (held="+player.GetComponent<ChordInput>().Held+", vx="+player.Body.linearVelocity.x+")");
                        Require(session.Lights[1].Remaining>2,"Movement pair triggers blue timer");
                        Keys(Key.O,Key.P,Key.Space); delay=.2; break;
                    case 3:
                        Require(player.Body.linearVelocity.y>2,"Adding Space performs a physics jump");
                        Require(session.Lights[3].Remaining>2,"Major chord triggers red timer");
                        Keys(); player.Teleport(new Vector2(-4.8f,.55f)); delay=3.4; break;
                    case 4:
                        Require(session.Lights[1].Remaining==0 && session.Lights[3].Remaining==0,"Effects expire without repeated activation");
                        player.Teleport(new Vector2(0,2.05f)); delay=.4; break;
                    case 5:
                        Require(session.Health<99 && session.Health>0,"Blue field damages without instant death");
                        damagedHealth=session.Health;
                        player.Teleport(new Vector2(-4.8f,.55f)); delay=.6; break;
                    case 6:
                        Require(session.Health>damagedHealth,"Green field heals");
                        player.Teleport(new Vector2(0,7.6f)); delay=.12; break;
                    case 7:
                        Require(session.Recovering && session.Health==0 && session.Deaths==1,"Red field kills immediately");
                        delay=.8; break;
                    case 8:
                        Require(!session.Recovering && session.Health==100 && player.transform.position.x<-4,"Death respawns safely");
                        player.Teleport(new Vector2(4.3f,4.75f)); break;
                    case 9:
                        player.Teleport(new Vector2(8,-8)); delay=1; break;
                    case 10:
                        Capture("Logs/ChordDemoCheckpoint.png");
                        Require(Mathf.Abs(player.transform.position.x-4.3f)<.1f && player.transform.position.y>4,"Checkpoint restores player after falling");
                        player.Teleport(new Vector2(-2.5f,16)); break;
                    case 11:
                        Require(session.Completed && player.Frozen,"Goal completes and freezes player");
                        Require(Camera.main.transform.position.y>6,"Camera follows ascent");
                        Keys(Key.R); break;
                    case 12:
                        Require(!session.Completed && player.transform.position.x<-4,"R restarts completed demo");
                        Keys(); landing=0; jumping=false; routeDeadline=Time.time+45; pauseUntil=Time.time+.2f; break;
                    case 13:
                        if (!DriveRoute()) { stage=13; delay=0; break; }
                        Require(session.Completed,"Full tower completed using chord keyboard input without teleports");
                        Capture("Logs/ChordDemoGoal.png");
                        Keys(); Directory.CreateDirectory("Logs");
                        File.WriteAllText("Logs/ChordDemoChecks.txt",report);
                        SessionState.SetBool(KeyName,false); EditorApplication.update-=Tick; EditorApplication.Exit(0); return;
                }
                next=Time.time+delay;
            }
            catch(Exception exception)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/ChordDemoChecks.txt",report+exception);
                Debug.LogException(exception); SessionState.SetBool(KeyName,false); EditorApplication.update-=Tick; EditorApplication.Exit(1);
            }
        }
        static bool DriveRoute()
        {
            if (session.Completed) return true;
            if (Time.time>routeDeadline || session.Recovering)
                throw new Exception("Tower traversal failed at landing "+(landing+1)+" / position "+player.transform.position+" / "+session.Message);
            if (Time.time<pauseUntil) { Keys(); return false; }
            var target = route[landing];
            Vector2 position=player.transform.position;
            float dx=target.x-position.x;
            if (player.Grounded && Mathf.Abs(position.y-(target.y+.335f))<.15f && Mathf.Abs(dx)<.15f)
            {
                landing++; jumping=false; pauseUntil=Time.time+.12f; Keys();
                if (landing>=route.Length) throw new Exception("Exit trigger was not reached.");
                return false;
            }
            bool right=dx>0;
            if (Mathf.Abs(dx)<.08f) { Keys(); return false; }
            if (player.Grounded && !jumping)
            { jumping=true; jumpUntil=Time.time+.10f; }
            if (Time.time<jumpUntil) Keys(right?Key.O:Key.Q,right?Key.P:Key.W,Key.Space);
            else Keys(right?Key.O:Key.Q,right?Key.P:Key.W);
            return false;
        }
        static void Capture(string path)
        {
            var camera = Camera.main;
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            var target = new RenderTexture(1280,720,24);
            var texture = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            var pp = camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pp.enabled = false; camera.rect = new Rect(0,0,1,1); camera.ResetAspect();
            int previousOrder=canvas.sortingOrder; canvas.sortingOrder=100;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(path,texture.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            canvas.sortingOrder=previousOrder;
            pp.enabled = true;
            UnityEngine.Object.DestroyImmediate(texture); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
