using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace LumenWave.MA
{
    /// <summary>MALink settings. Stored as JSON next to the executable (malink.json) or in persistentDataPath.</summary>
    [Serializable]
    public class MALinkConfig
    {
        public bool enabled = false;
        public string ip = "127.0.0.1";
        public int port = 8000;
        /// <summary>OSC prefix configured in grandMA3 (Menu → In &amp; Out → OSC → Prefix). Address = "/" + prefix + "/cmd".</summary>
        public string prefix = "gma3";
        public bool showOverlay = false;
        /// <summary>Minimum seconds between two messages of the same event (continuous damage etc.).</summary>
        public float throttle = 0.25f;
        // Executor numbers per event ("Go+ Exec N").
        public int execRedDamage = 201;
        public int execYellowDamage = 202;
        public int execGreenHeal = 203;
        public int execEcho = 211;
        public int execShield = 212;
        public int execPulse = 213;
        public int execDash = 214;
        public int execCheckpoint = 221;
        public int execDeath = 222;
        public int execLevelComplete = 223;
        public int execWin = 224;
        public int execFinale = 225;
    }

    /// <summary>
    /// Sends grandMA3 OSC commands ("/gma3/cmd ,s 'Go+ Exec 201'") over UDP on gameplay events, so a real
    /// lighting rig mirrors the game. Off by default; completely safe offline (UDP is fire-and-forget,
    /// every send is wrapped). Toggle overlay with F9 (in-game debug list of sent messages).
    /// </summary>
    public class MALink : MonoBehaviour
    {
        public static MALink Instance { get; private set; }
        public MALinkConfig config = new MALinkConfig();
        public readonly List<string> log = new List<string>();
        public int SentCount { get; private set; }
        public string LastError { get; private set; }

        UdpClient _udp;
        readonly Dictionary<string, float> _lastSent = new Dictionary<string, float>();

        public string Address => "/" + config.prefix.Trim('/') + "/cmd";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install()
        {
            GameSystems.Register(root => { if (root.GetComponent<MALink>() == null) root.AddComponent<MALink>(); });
        }

        public static string ConfigPath
        {
            get
            {
                var nextToExe = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "malink.json");
                if (File.Exists(nextToExe)) return nextToExe;
                return Path.Combine(Application.persistentDataPath, "malink.json");
            }
        }

        void Awake()
        {
            Instance = this;
            LoadConfig();
            // Command line overrides: -malink ip:port[:prefix]
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-malink") ApplyEndpoint(args[i + 1]);
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
            _udp?.Close();
            _udp = null;
            if (Instance == this) Instance = null;
        }

        public void ApplyEndpoint(string spec)
        {
            var p = spec.Split(':');
            if (p.Length >= 1 && p[0].Length > 0) config.ip = p[0];
            if (p.Length >= 2 && int.TryParse(p[1], out var port)) config.port = port;
            if (p.Length >= 3) config.prefix = p[2];
            config.enabled = true;
            ResetSocket();
        }

        public void LoadConfig()
        {
            try
            {
                var path = ConfigPath;
                if (File.Exists(path)) JsonUtility.FromJsonOverwrite(File.ReadAllText(path), config);
            }
            catch (Exception e) { LastError = e.Message; }
        }

        public void SaveConfig()
        {
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "malink.json"), JsonUtility.ToJson(config, true)); }
            catch (Exception e) { LastError = e.Message; }
        }

        void ResetSocket() { _udp?.Close(); _udp = null; }

        /// <summary>Send "Go+ Exec N". Returns true if a packet left the socket.</summary>
        public bool GoExec(int exec, string reason) => SendCommand("Go+ Exec " + exec, reason);

        public bool SendCommand(string command, string reason = "")
        {
            if (!config.enabled) return false;
            float now = Time.unscaledTime;
            if (_lastSent.TryGetValue(command, out var t) && now - t < config.throttle) return false;
            _lastSent[command] = now;
            try
            {
                if (_udp == null) _udp = new UdpClient();
                var bytes = Osc.Encode(Address, command);
                _udp.Send(bytes, bytes.Length, config.ip, config.port);
                SentCount++;
                Log($"{Address} \"{command}\"  ({reason})");
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Log("ERROR " + e.Message);
                return false;
            }
        }

        void Log(string line)
        {
            log.Add($"{DateTime.Now:HH:mm:ss} {line}");
            if (log.Count > 14) log.RemoveAt(0);
        }

        // ---- Event mapping ----
        void Subscribe()
        {
            GameEvents.PlayerDamaged += OnDamaged;
            GameEvents.PlayerHealed += OnHealed;
            GameEvents.PowerUsed += OnPower;
            GameEvents.CheckpointReached += OnCheckpoint;
            GameEvents.PlayerDied += OnDied;
            GameEvents.LevelCompleted += OnLevelCompleted;
            GameEvents.GameWon += OnWon;
            GameEvents.FinaleStarted += OnFinale;
        }

        void Unsubscribe()
        {
            GameEvents.PlayerDamaged -= OnDamaged;
            GameEvents.PlayerHealed -= OnHealed;
            GameEvents.PowerUsed -= OnPower;
            GameEvents.CheckpointReached -= OnCheckpoint;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.LevelCompleted -= OnLevelCompleted;
            GameEvents.GameWon -= OnWon;
            GameEvents.FinaleStarted -= OnFinale;
        }

        void OnDamaged(float amount, LightColor c)
        {
            if (c == LightColor.Yellow) GoExec(config.execYellowDamage, "yellow damage");
            else if (c == LightColor.Red) GoExec(config.execRedDamage, "red damage");
        }
        void OnHealed(float amount) => GoExec(config.execGreenHeal, "green heal");
        void OnPower(PowerType p)
        {
            switch (p)
            {
                case PowerType.Echo: GoExec(config.execEcho, "echo path"); break;
                case PowerType.Shield: GoExec(config.execShield, "shield"); break;
                case PowerType.Pulse: GoExec(config.execPulse, "pulse"); break;
                case PowerType.Dash: GoExec(config.execDash, "dash"); break;
            }
        }
        void OnCheckpoint(int i) => GoExec(config.execCheckpoint, "checkpoint " + i);
        void OnDied() => GoExec(config.execDeath, "death");
        void OnLevelCompleted(int n, float t) => GoExec(config.execLevelComplete, "level " + n + " complete");
        void OnWon() => GoExec(config.execWin, "win");
        void OnFinale() => GoExec(config.execFinale, "finale");

        // ---- Debug overlay (F9) ----
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) config.showOverlay = !config.showOverlay;
        }

        void OnGUI()
        {
            if (!config.showOverlay) return;
            const int w = 520;
            GUILayout.BeginArea(new Rect(Screen.width - w - 12, 12, w, 330), GUI.skin.box);
            GUILayout.Label($"<b>MALink</b>  {(config.enabled ? "ON" : "OFF")}  →  {config.ip}:{config.port}  {Address}   sent: {SentCount}", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(config.enabled ? "Disable" : "Enable")) { config.enabled = !config.enabled; ResetSocket(); }
            if (GUILayout.Button("Test (Go+ Exec " + config.execWin + ")")) GoExec(config.execWin, "manual test");
            if (GUILayout.Button("Save config")) SaveConfig();
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(LastError)) GUILayout.Label("Last error: " + LastError);
            foreach (var l in log) GUILayout.Label(l);
            GUILayout.EndArea();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
    }
}
