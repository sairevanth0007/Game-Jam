using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using LumenWave.MA;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Optional OSC remote control of the Lightkeeper lamps (off by default). Enable with
    /// <c>-lk-osc &lt;port&gt;</c> or PlayerPrefs int <c>lw.lk.oscport</c> &gt; 0. Messages (N = 1..4):
    /// <c>/lamp/N/x f</c> (world x), <c>/lamp/N/angle f</c> (deg, 0 = straight down, positive tilts toward +x),
    /// <c>/lamp/N/on i|f</c>, <c>/lamp/N/color i|s</c> (0 red,1 green,2 blue,3 yellow,4 white or the name),
    /// <c>/lamp/N/select</c>. Received on a background thread, applied on the main thread in Update.
    /// When MALink is enabled, lamp states are mirrored out (same addresses) to the MALink ip:port, plus
    /// grandMA3 commands "Go+ Exec 23N" / "Off Exec 23N" when lamp N turns on / off.
    /// </summary>
    public class LightkeeperOsc : MonoBehaviour
    {
        public const string PrefsKey = "lw.lk.oscport";
        public int Port { get; private set; }
        public int Received { get; private set; }

        UdpClient _udp;
        Thread _thread;
        volatile bool _run;
        readonly ConcurrentQueue<(string address, List<object> args)> _queue = new ConcurrentQueue<(string, List<object>)>();
        LightkeeperController _c;
        UdpClient _out;
        readonly Dictionary<int, float> _lastMirror = new Dictionary<int, float>();
        readonly Dictionary<int, bool> _lastOn = new Dictionary<int, bool>();

        /// <summary>Port from -lk-osc or PlayerPrefs (0 = disabled).</summary>
        public static int ConfiguredPort()
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-lk-osc" && int.TryParse(a[i + 1], out var p)) return p;
            return PlayerPrefs.GetInt(PrefsKey, 0);
        }

        public static LightkeeperOsc Attach(LightkeeperController c, int port)
        {
            var o = c.gameObject.AddComponent<LightkeeperOsc>();
            o._c = c;
            o.Listen(port);
            c.LampChanged += o.Mirror;
            return o;
        }

        void Listen(int port)
        {
            try
            {
                _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                Port = ((IPEndPoint)_udp.Client.LocalEndPoint).Port;
                _run = true;
                _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "LightkeeperOsc" };
                _thread.Start();
                Debug.Log("[LumenWave] Lightkeeper OSC listening on UDP " + Port);
            }
            catch (Exception e) { Debug.LogWarning("[LumenWave] Lightkeeper OSC: cannot listen on " + port + ": " + e.Message); }
        }

        void ReceiveLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_run)
            {
                try
                {
                    var data = _udp.Receive(ref ep);
                    Unpack(data);
                }
                catch (Exception) { if (!_run) return; }
            }
        }

        /// <summary>Messages and (simple, non-nested-time-tag-aware) bundles.</summary>
        void Unpack(byte[] data)
        {
            if (data.Length >= 16 && data[0] == (byte)'#')
            {
                int p = 16;   // "#bundle\0" + 8-byte time tag
                while (p + 4 <= data.Length)
                {
                    int n = (data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3];
                    p += 4;
                    if (n <= 0 || p + n > data.Length) break;
                    var part = new byte[n];
                    Buffer.BlockCopy(data, p, part, 0, n);
                    Unpack(part);
                    p += n;
                }
                return;
            }
            if (Osc.TryDecode(data, out var address, out var args)) _queue.Enqueue((address, args));
        }

        void Update()
        {
            while (_queue.TryDequeue(out var m)) { Received++; Apply(_c, m.address, m.args); }
        }

        void OnDestroy()
        {
            _run = false;
            try { _udp?.Close(); } catch { }
            try { _out?.Close(); } catch { }
        }

        static float F(object o)
        {
            switch (o)
            {
                case float f: return f;
                case int i: return i;
                case string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v): return v;
                default: return 0f;
            }
        }

        /// <summary>Applies one decoded message. Returns false if it was not a lamp message.</summary>
        public static bool Apply(LightkeeperController c, string address, List<object> args)
        {
            if (c == null || string.IsNullOrEmpty(address)) return false;
            var parts = address.Trim('/').Split('/');
            if (parts.Length < 3 || parts[0] != "lamp" || !int.TryParse(parts[1], out int n)) return false;
            int i = n - 1;
            if (i < 0 || i >= c.Lamps.Count) return false;
            object a0 = args != null && args.Count > 0 ? args[0] : null;
            switch (parts[2])
            {
                case "x": c.SetLampX(i, F(a0)); return true;
                case "angle": c.SetLampAngle(i, -90f + F(a0)); return true;
                case "on": c.SetLampOn(i, a0 == null || F(a0) >= 0.5f); return true;
                case "select": c.Select(i); return true;
                case "color":
                    if (a0 is string s)
                    {
                        foreach (var col in LightkeeperController.Palette)
                            if (string.Equals(col.ToString(), s, StringComparison.OrdinalIgnoreCase)) { c.SetLampColor(i, col); return true; }
                        if (int.TryParse(s, out var k)) a0 = k; else return false;
                    }
                    int idx = Mathf.Clamp(Mathf.RoundToInt(F(a0)), 0, LightkeeperController.Palette.Length - 1);
                    c.SetLampColor(i, LightkeeperController.Palette[idx]);
                    return true;
            }
            return false;
        }

        void Mirror(int i)
        {
            var ma = MALink.Instance;
            if (ma == null || !ma.config.enabled || _c == null || i >= _c.Lamps.Count) return;
            var l = _c.Lamps[i];
            int n = i + 1;
            if (!_lastOn.TryGetValue(i, out var was) || was != l.On)
            {
                _lastOn[i] = l.On;
                ma.SendCommand((l.On ? "Go+ Exec " : "Off Exec ") + (230 + n), "lamp " + n);
            }
            if (_lastMirror.TryGetValue(i, out var t) && Time.unscaledTime - t < 0.1f) return;
            _lastMirror[i] = Time.unscaledTime;
            try
            {
                if (_out == null) _out = new UdpClient();
                Send($"/lamp/{n}/x", l.X, ma);
                Send($"/lamp/{n}/angle", l.angle + 90f, ma);
                var b = Osc.Encode($"/lamp/{n}/on", l.On ? 1 : 0); _out.Send(b, b.Length, ma.config.ip, ma.config.port);
                b = Osc.Encode($"/lamp/{n}/color", l.beam.color.ToString().ToLowerInvariant()); _out.Send(b, b.Length, ma.config.ip, ma.config.port);
            }
            catch (Exception) { }
        }

        void Send(string address, float v, MALink ma)
        {
            var b = Osc.Encode(address, v);
            _out.Send(b, b.Length, ma.config.ip, ma.config.port);
        }
    }
}
