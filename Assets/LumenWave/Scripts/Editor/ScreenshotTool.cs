using System.IO;
using UnityEditor;
using UnityEngine;

namespace LumenWave.EditorTools
{
    /// <summary>Editor helper: saves the Game view to Screenshots/ (enter Play mode first).</summary>
    public static class ScreenshotTool
    {
        [MenuItem("LumenWave/Capture Game View Screenshot")]
        public static void Capture()
        {
            Directory.CreateDirectory("Screenshots");
            var path = Path.Combine("Screenshots", "editor_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[LumenWave] Screenshot queued: " + path + (Application.isPlaying ? "" : " (Game view must be visible; works best in Play mode)"));
        }
    }
}
