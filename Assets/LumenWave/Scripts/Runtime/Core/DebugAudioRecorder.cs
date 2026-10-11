using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// QA tool: <c>-lw-record-audio &lt;file.wav&gt;</c> taps the final mix on the active AudioListener
    /// (OnAudioFilterRead) and writes a 16-bit PCM WAV when the application quits. Used with -lw-autoplay
    /// to verify the procedural audio in the real build (level, clipping, beat, pitch).
    /// </summary>
    public class DebugAudioRecorder : MonoBehaviour
    {
        static string _path;
        static readonly List<float> _samples = new List<float>(48000 * 2 * 120);
        static readonly object _lock = new object();
        static int _channels = 2;
        static bool _written;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-lw-record-audio") _path = args[i + 1];
            if (_path == null) return;
            var go = new GameObject("DebugAudioRecorder");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugAudioRecorder>();
            Application.quitting += Write;
            Debug.Log("[LumenWave] Recording audio to " + _path);
        }

        void Update()
        {
            // Keep a tap on whichever AudioListener is active (cameras are rebuilt per scene).
            var listener = FindAnyObjectByType<AudioListener>();
            if (listener != null && listener.GetComponent<Tap>() == null) listener.gameObject.AddComponent<Tap>();
        }

        class Tap : MonoBehaviour
        {
            void OnAudioFilterRead(float[] data, int channels)
            {
                lock (_lock)
                {
                    _channels = channels;
                    _samples.AddRange(data);
                }
            }
        }

        static void Write()
        {
            if (_written || _path == null) return;
            _written = true;
            float[] s;
            int ch;
            lock (_lock) { s = _samples.ToArray(); ch = _channels; }
            int rate = AudioSettings.outputSampleRate;
            try
            {
                using (var w = new BinaryWriter(File.Create(_path)))
                {
                    int dataBytes = s.Length * 2;
                    w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + dataBytes);
                    w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                    w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }); w.Write(16);
                    w.Write((short)1); w.Write((short)ch); w.Write(rate); w.Write(rate * ch * 2); w.Write((short)(ch * 2)); w.Write((short)16);
                    w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(dataBytes);
                    foreach (var f in s)
                    {
                        float v = float.IsNaN(f) ? 0f : Mathf.Clamp(f, -1f, 1f);
                        w.Write((short)Mathf.RoundToInt(v * 32767f));
                    }
                }
                Debug.Log($"[LumenWave] Wrote {s.Length / Math.Max(1, ch) / (float)rate:F1}s audio ({ch}ch {rate}Hz) to {_path}");
            }
            catch (Exception e) { Debug.LogError("[LumenWave] Audio write failed: " + e.Message); }
        }
    }
}
