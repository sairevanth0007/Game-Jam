using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LumenWave
{
    /// <summary>
    /// Automated screenshot mode for the built player (visual verification without a human):
    ///   LumenWave.exe -lw-screenshots &lt;dir&gt; [-lw-shots "level:x:y;title;end;..."]
    /// Visits each shot, places Umbra (god mode) and the camera, waits for things to settle, writes
    /// &lt;dir&gt;/L{level}_{x}_{y}.png (or Title.png / End.png) and quits. Without -lw-shots it captures the
    /// title, the end screen and three points per level.
    /// </summary>
    public class ScreenshotDirector : MonoBehaviour
    {
        public static bool Active { get; private set; }
        public float settleSeconds = 1.6f;

        string _dir;
        readonly List<string> _shots = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Active = false;
            var args = Environment.GetCommandLineArgs();
            string dir = null, shots = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-lw-screenshots") dir = args[i + 1];
                if (args[i] == "-lw-shots") shots = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir)) return;
            Active = true;
            var go = new GameObject("ScreenshotDirector");
            DontDestroyOnLoad(go);
            var d = go.AddComponent<ScreenshotDirector>();
            d._dir = dir;
            if (!string.IsNullOrEmpty(shots)) d._shots.AddRange(shots.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(_dir);
            Debug.Log("[LumenWave] Screenshot mode -> " + _dir);
            if (_shots.Count == 0) BuildDefaultShots();
            yield return null;
            foreach (var shot in _shots)
            {
                yield return Capture(shot.Trim());
            }
            Debug.Log("[LumenWave] Screenshot mode done (" + _shots.Count + " shots)");
            yield return new WaitForSecondsRealtime(0.3f);
            Application.Quit();
        }

        void BuildDefaultShots()
        {
            _shots.Add("title");
            for (int lvl = 1; lvl <= LevelDatabase.Count; lvl++)
            {
                LevelData d;
                try { d = LevelDatabase.Load(lvl); } catch { continue; }
                var start = d.PlayerStart;
                _shots.Add(lvl + ":" + F(start.x) + ":" + F(start.y));
                foreach (var f in new[] { 0.35f, 0.65f, 0.92f })
                {
                    int x = Mathf.Clamp(Mathf.RoundToInt(d.Width * f), 1, d.Width - 2);
                    int top = GroundTop(d, ref x);
                    if (top >= 0) _shots.Add(lvl + ":" + (x + 0.5f).ToString(CultureInfo.InvariantCulture) + ":" + top);
                }
            }
            _shots.Add("end");
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Finds the lowest walkable surface near x (searching sideways), i.e. usually the floor. Returns −1 if none.</summary>
        static int GroundTop(LevelData d, ref int x)
        {
            for (int off = 0; off < 12; off++)
            {
                foreach (int xx in new[] { x + off, x - off })
                {
                    if (xx < 0 || xx >= d.Width) continue;
                    for (int y = 0; y < d.Height - 1; y++)
                        if (LevelData.IsSolid(d.Tile(xx, y)) && !LevelData.IsSolid(d.Tile(xx, y + 1)) && !LevelData.IsSolid(d.Tile(xx, y + 2)))
                        { x = xx; return y + 1; }
                }
            }
            return -1;
        }

        IEnumerator Capture(string shot)
        {
            string file;
            if (shot == "title" || shot == "end")
            {
                SceneManager.LoadScene(shot == "title" ? SceneFlow.TitleScene : SceneFlow.EndScene);
                file = shot == "title" ? "Title.png" : "End.png";
                yield return new WaitForSecondsRealtime(settleSeconds + 0.5f);
            }
            else
            {
                var p = shot.Split(':');
                if (p.Length < 3) { Debug.LogWarning("[LumenWave] Bad shot spec " + shot); yield break; }
                int level = int.Parse(p[0], CultureInfo.InvariantCulture);
                float x = float.Parse(p[1], CultureInfo.InvariantCulture), y = float.Parse(p[2], CultureInfo.InvariantCulture);
                SceneFlow.LoadLevel(level);
                float t = 0f;
                while ((LevelRuntime.Current == null || LevelRuntime.Current.LevelNumber != level) && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
                yield return null;
                var rt = LevelRuntime.Current;
                if (rt != null && rt.Player != null)
                {
                    var h = rt.Player.GetComponent<PlayerHealth>();
                    if (h != null) h.godMode = true;
                    var body = rt.Player.GetComponent<IPlayerBody>();
                    var pos = new Vector3(x, y + 0.05f, 0f);
                    if (body != null) body.Teleport(pos); else rt.Player.transform.position = pos;
                }
                if (CameraRig.Instance != null)
                {
                    if (rt != null && rt.Player == null)
                    {
                        // No player yet: frame the requested point directly.
                        CameraRig.Instance.FramePoint(new Vector3(x, y, 0f));
                    }
                    else CameraRig.Instance.SnapToTarget();
                }
                yield return new WaitForSecondsRealtime(settleSeconds);
                file = "L" + level + "_" + F(x) + "_" + F(y) + ".png";
            }
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var path = Path.Combine(_dir, file);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[LumenWave] Screenshot " + path);
        }
    }
}
