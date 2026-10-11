using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Runs a Lightkeeper round on the arena's LevelRuntime: the human drives four lamps on a rail
    /// (1–4 select, A/D or mouse-drag move, Q/E or wheel rotate, W on/off, R colour) under an energy budget,
    /// the <see cref="ReactiveBot"/> drives Umbra. Lightkeeper wins after 90 s or 3 bot deaths; Umbra wins at the door.
    /// </summary>
    public class LightkeeperController : MonoBehaviour
    {
        public static LightkeeperController Instance { get; private set; }

        public class Lamp
        {
            public LightBeam beam;
            public Transform housing;
            public MeshRenderer lens;
            public float angle = -90f;   // beam direction in degrees (-90 = straight down)
            public float X => beam.transform.position.x;
            public bool On => beam.on;
        }

        public static readonly LightColor[] Palette = { LightColor.Red, LightColor.Green, LightColor.Blue, LightColor.Yellow, LightColor.White };

        public readonly List<Lamp> Lamps = new List<Lamp>();
        public int Selected { get; private set; }
        public float Energy { get; private set; } = 100f;
        public float maxEnergy = 100f;
        public float drainPerLamp = 4f;     // per second per lit lamp (2 lit: 12 s from full)
        public float regen = 10f;           // per second while every lamp is dark
        public int maxLit = 2;
        public float moveSpeed = 12f;
        public float rotateSpeed = 70f;
        public float minAngle = -165f, maxAngle = -15f;
        public float railMin = 3f, railMax = 77f;

        public float TimeLeft { get; private set; } = LightkeeperMode.RoundSeconds;
        public int BotDeaths { get; private set; }
        public bool Over { get; private set; }
        public bool LightkeeperWon { get; private set; }
        public string Flash { get; private set; } = "";
        public float FlashUntil { get; private set; }
        /// <summary>Set false (tests) to ignore keyboard / mouse.</summary>
        public bool humanInput = true;

        public ReactiveBot Bot { get; private set; }
        public event Action<int> LampChanged;
        public event Action<bool> Finished;

        LevelRuntime _rt;
        IPlayerBody _body;
        Transform _marker;
        bool _started;

        void Awake()
        {
            Instance = this;
            GameSession.BotMode = true;   // no auto-advance to the next campaign level, no slow-mo, no run stats
            _rt = GetComponent<LevelRuntime>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameEvents.PlayerDied -= OnDied;
            GameEvents.LevelCompleted -= OnCompleted;
            Bot?.Dispose();
        }

        void Start()
        {
            if (_rt == null) _rt = LevelRuntime.Current;
            var data = _rt.Data;
            railMin = 3f; railMax = Mathf.Max(6f, data.Width - 3f);
            if (_rt.Context.byType.TryGetValue("beam", out var beams))
            {
                beams.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
                foreach (var go in beams)
                {
                    var b = go != null ? go.GetComponent<LightBeam>() : null;
                    if (b == null) continue;
                    b.sweepDegrees = 0f;
                    b.on = false;
                    var l = new Lamp { beam = b, angle = Mathf.Atan2(b.direction.y, b.direction.x) * Mathf.Rad2Deg };
                    BuildHousing(l);
                    Lamps.Add(l);
                    if (Lamps.Count == 4) break;
                }
            }
            BuildRail();

            float exitX = data.Width - 3f;
            if (_rt.Context.byType.TryGetValue("exit", out var exits) && exits.Count > 0 && exits[0] != null) exitX = exits[0].transform.position.x;
            if (_rt.Player != null)
            {
                _body = _rt.Player.GetComponent<IPlayerBody>();
                Bot = new ReactiveBot(_rt.Player.transform, exitX);
                if (_body != null) _body.Commands = Bot;
            }
            GameEvents.PlayerDied += OnDied;
            GameEvents.LevelCompleted += OnCompleted;
            for (int i = 0; i < Lamps.Count; i++) Refresh(i);
            LightkeeperUI.Build(this);
            int oscPort = LightkeeperOsc.ConfiguredPort();
            if (oscPort > 0) LightkeeperOsc.Attach(this, oscPort);
            _started = true;
            Debug.Log($"[LumenWave] Lightkeeper round: {Lamps.Count} lamps, door at x={exitX:F1}");
        }

        // ------------------------------------------------------------------ public lamp API (keys, OSC, tests)

        public void Select(int i) { if (i >= 0 && i < Lamps.Count) { Selected = i; UpdateMarker(); } }

        public void SetLampX(int i, float x)
        {
            if (!Valid(i)) return;
            var t = Lamps[i].beam.transform;
            t.position = new Vector3(Mathf.Clamp(x, railMin, railMax), t.position.y, t.position.z);
            Refresh(i);
        }

        public void SetLampAngle(int i, float deg)
        {
            if (!Valid(i)) return;
            Lamps[i].angle = Mathf.Clamp(deg, minAngle, maxAngle);
            Refresh(i);
        }

        /// <summary>Turns a lamp on/off. Refused (returns false) when the lit limit or the energy would be exceeded.</summary>
        public bool SetLampOn(int i, bool on)
        {
            if (!Valid(i) || Over && on) return false;
            if (on && !Lamps[i].On)
            {
                if (LitCount >= maxLit) { Say($"MAX {maxLit} LAMPS LIT"); return false; }
                if (Energy < 8f) { Say("NO ENERGY"); return false; }
            }
            Lamps[i].beam.on = on;
            Refresh(i);
            return true;
        }

        public void SetLampColor(int i, LightColor c)
        {
            if (!Valid(i)) return;
            Lamps[i].beam.color = c;
            Refresh(i);
        }

        public void CycleColor(int i)
        {
            if (!Valid(i)) return;
            int k = Array.IndexOf(Palette, Lamps[i].beam.color);
            SetLampColor(i, Palette[(k + 1) % Palette.Length]);
        }

        public int LitCount { get { int n = 0; foreach (var l in Lamps) if (l.On) n++; return n; } }

        bool Valid(int i) => i >= 0 && i < Lamps.Count && Lamps[i].beam != null;

        void Say(string s) { Flash = s; FlashUntil = Time.unscaledTime + 1.5f; }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!_started || GameSession.Paused) return;
            float dt = Time.deltaTime;
            if (!Over && humanInput) HandleInput(dt);

            // Energy budget.
            int lit = LitCount;
            if (lit > 0) Energy -= drainPerLamp * lit * dt;
            else Energy += regen * dt;
            Energy = Mathf.Clamp(Energy, 0f, maxEnergy);
            if (Energy <= 0f && lit > 0)
            {
                for (int i = 0; i < Lamps.Count; i++) if (Lamps[i].On) { Lamps[i].beam.on = false; Refresh(i); }
                Say("OUT OF ENERGY");
            }

            if (Over) return;
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
            if (_rt != null && _rt.Completed) Finish(false);
            else if (BotDeaths >= LightkeeperMode.DeathsToWin || TimeLeft <= 0f) Finish(true);
        }

        void HandleInput(float dt)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) Select(0);
                if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) Select(1);
                if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) Select(2);
                if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) Select(3);
                float mx = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
                if (mx != 0f && Valid(Selected)) SetLampX(Selected, Lamps[Selected].X + mx * moveSpeed * dt);
                float rot = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
                if (rot != 0f && Valid(Selected)) SetLampAngle(Selected, Lamps[Selected].angle + rot * rotateSpeed * dt);
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) { if (Valid(Selected)) SetLampOn(Selected, !Lamps[Selected].On); }
                if (kb.rKey.wasPressedThisFrame) CycleColor(Selected);
            }
            var mouse = Mouse.current;
            if (mouse != null && Valid(Selected))
            {
                if (mouse.leftButton.isPressed)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                        var plane = new Plane(Vector3.forward, Vector3.zero);
                        if (plane.Raycast(ray, out float d)) SetLampX(Selected, ray.GetPoint(d).x);
                    }
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) SetLampAngle(Selected, Lamps[Selected].angle + Mathf.Sign(wheel) * 6f);
            }
        }

        void OnDied()
        {
            if (Over) return;
            BotDeaths++;
            Say(BotDeaths >= LightkeeperMode.DeathsToWin ? "UMBRA FALLS" : $"UMBRA BURNED  {BotDeaths}/{LightkeeperMode.DeathsToWin}");
        }

        void OnCompleted(int level, float time) { if (!Over) Finish(false); }

        void Finish(bool lightkeeperWins)
        {
            if (Over) return;
            Over = true;
            LightkeeperWon = lightkeeperWins;
            if (_body != null) _body.Commands = new IdleSource();
            Debug.Log($"[LumenWave] Lightkeeper round over: {(lightkeeperWins ? "LIGHTKEEPER WINS" : "UMBRA ESCAPES")} (time left {TimeLeft:F1}s, deaths {BotDeaths})");
            Finished?.Invoke(lightkeeperWins);
        }

        public void Retry() => LightkeeperMode.Start();
        public void ToTitle() => LightkeeperMode.ExitToTitle();

        class IdleSource : ICommandSource { public PlayerCommands Poll() => default; }

        // ------------------------------------------------------------------ visuals

        void Refresh(int i)
        {
            var l = Lamps[i];
            var b = l.beam;
            var dir = new Vector2(Mathf.Cos(l.angle * Mathf.Deg2Rad), Mathf.Sin(l.angle * Mathf.Deg2Rad));
            b.SetBaseDirection(dir);
            if (l.housing != null) l.housing.localRotation = Quaternion.Euler(0, 0, l.angle + 90f);
            if (l.lens != null)
            {
                var c = LightMath.DisplayColor(b.color);
                l.lens.sharedMaterial = b.on ? MaterialLibrary.Tinted("LitEmissive", c * 0.4f, c * 5f) : MaterialLibrary.Tinted("LitEmissive", c * 0.15f, c * 0.35f);
            }
            if (i == Selected) UpdateMarker();
            LampChanged?.Invoke(i);
        }

        void BuildHousing(Lamp l)
        {
            var dark = MaterialLibrary.Tinted("Lit", new Color(0.06f, 0.06f, 0.08f));
            var root = new GameObject("LampHousing").transform;
            root.SetParent(l.beam.transform, false);
            // Local +Y points opposite the beam; the lens faces along the beam.
            LightVisuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.3f, 0), new Vector3(0.75f, 0.6f, 0.75f), dark, "Housing");
            LightVisuals.Part(PrimitiveType.Cube, root, new Vector3(0, 0.66f, 0), new Vector3(0.95f, 0.12f, 0.95f), dark, "Cap");
            var lens = LightVisuals.Part(PrimitiveType.Cylinder, root, new Vector3(0, 0.0f, 0), new Vector3(0.55f, 0.05f, 0.55f), dark, "Lens");
            l.housing = root;
            l.lens = lens.GetComponent<MeshRenderer>();
            // Trolley on the rail.
            LightVisuals.Part(PrimitiveType.Cube, l.beam.transform, new Vector3(0, 0.95f, 0), new Vector3(0.5f, 0.3f, 0.5f), dark, "Trolley");
        }

        void BuildRail()
        {
            if (Lamps.Count == 0) return;
            float y = Lamps[0].beam.transform.position.y + 1.15f;
            var mat = MaterialLibrary.Tinted("Lit", new Color(0.1f, 0.1f, 0.13f));
            var rail = LightVisuals.Part(PrimitiveType.Cube, transform, new Vector3((railMin + railMax) * 0.5f, y, 0f), new Vector3(railMax - railMin + 1.5f, 0.12f, 0.25f), mat, "LampRail");
            rail.transform.SetParent(transform, true);
            var glow = MaterialLibrary.Tinted("LitEmissive", new Color(1f, 0.85f, 0.4f), new Color(4f, 3f, 1.2f));
            _marker = LightVisuals.Part(PrimitiveType.Cube, transform, Vector3.zero, new Vector3(0.6f, 0.08f, 0.3f), glow, "SelectedMarker").transform;
            UpdateMarker();
        }

        void UpdateMarker()
        {
            if (_marker == null || !Valid(Selected)) return;
            var p = Lamps[Selected].beam.transform.position;
            _marker.position = new Vector3(p.x, p.y + 1.45f, p.z);
        }
    }
}
