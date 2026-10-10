using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Scripted "player" used by PlayMode playthrough tests (and attract mode). Holds right by default
    /// and performs the actions of `bot` markers in the level file when Umbra crosses their x position.
    /// Actions: jump [t=hold], tap, dash, jumpdash, echo, pulse, shield [t], wait [t], waitbeat,
    /// right, left [t], stop. Markers may set delay= (seconds) and any=1 (ignore height).
    /// </summary>
    public class BotCommandSource : ICommandSource
    {
        readonly Transform _player;
        readonly List<BotMarker> _markers;
        readonly bool[] _fired;
        readonly List<(float time, string action, float t)> _scheduled = new List<(float, string, float)>();

        float _prevX;
        float _dir = 1f, _dirRevertAt = -1f, _dirRevertTo = 1f;
        float _jumpHoldUntil, _shieldUntil, _waitUntil;
        bool _jump, _dash, _echo, _pulse;
        int _beatWait; // 0 none, 1 waiting to see hot, 2 waiting for hot to end
        float _beatWaitStarted;
        float _stuckTimer, _lastProgressX;
        int _lastFrame = -1;
        PlayerCommands _cached;

        public int ActionsFired { get; private set; }
        public string LastAction { get; private set; } = "";

        public BotCommandSource(Transform player, IEnumerable<BotMarker> markers)
        {
            _player = player;
            _markers = markers.Where(m => m != null).OrderBy(m => m.order).ToList();
            _fired = new bool[_markers.Count];
            _prevX = player.position.x;
            _lastProgressX = _prevX;
            GameEvents.PlayerRespawned += OnRespawn;
        }

        /// <summary>Collects all bot markers of the current level.</summary>
        public static BotCommandSource ForCurrentLevel()
        {
            var rt = LevelRuntime.Current;
            if (rt == null || rt.Player == null) return null;
            return new BotCommandSource(rt.Player.transform, rt.GetComponentsInChildren<BotMarker>(true));
        }

        public void Dispose() { GameEvents.PlayerRespawned -= OnRespawn; }

        void OnRespawn()
        {
            for (int i = 0; i < _fired.Length; i++) _fired[i] = false;
            _scheduled.Clear();
            _prevX = _player != null ? _player.position.x : 0f;
            _lastProgressX = _prevX;
            _jumpHoldUntil = _shieldUntil = _waitUntil = 0f;
            _beatWait = 0;
            _dir = 1f; _dirRevertAt = -1f;
            _jump = _dash = _echo = _pulse = false;
        }

        public PlayerCommands Poll()
        {
            // Idempotent within a frame (several components may poll).
            if (_lastFrame == Time.frameCount) return _cached;
            _lastFrame = Time.frameCount;
            if (_player == null) return _cached = default;
            float now = Time.time;
            Vector3 pos = _player.position;

            // Fire markers crossed since last frame.
            for (int i = 0; i < _markers.Count; i++)
            {
                if (_fired[i] || _markers[i] == null) continue;
                var m = _markers[i];
                float mx = m.transform.position.x;
                bool crossed = (_prevX < mx && pos.x >= mx) || (_prevX > mx && pos.x <= mx) || Mathf.Abs(pos.x - mx) < 0.02f;
                bool heightOk = m.anyHeight || Mathf.Abs(pos.y - m.transform.position.y) < 2.5f;
                if (!crossed || !heightOk) continue;
                _fired[i] = true;
                if (m.delay > 0f) _scheduled.Add((now + m.delay, m.action, m.duration));
                else Do(m.action, m.duration, now);
            }
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                if (now < _scheduled[i].time) continue;
                Do(_scheduled[i].action, _scheduled[i].t, now);
                _scheduled.RemoveAt(i);
            }
            _prevX = pos.x;

            if (_dirRevertAt > 0f && now >= _dirRevertAt) { _dir = _dirRevertTo; _dirRevertAt = -1f; }

            // Beat waiting: hold still until a yellow pulse has just finished.
            bool beatHold = false;
            if (_beatWait != 0)
            {
                bool hot = BeatClock.Instance != null && BeatClock.Instance.IsHot;
                if (BeatClock.Instance == null || now - _beatWaitStarted > 3f) _beatWait = 0;
                else if (_beatWait == 1) { if (hot) _beatWait = 2; beatHold = true; }
                else if (_beatWait == 2) { if (!hot) _beatWait = 0; else beatHold = true; }
            }

            bool waiting = now < _waitUntil || beatHold;
            float move = waiting ? 0f : _dir;

            // Unstick: if trying to move but not progressing, hop.
            if (Mathf.Abs(move) > 0.1f)
            {
                if (Mathf.Abs(pos.x - _lastProgressX) > 0.15f) { _lastProgressX = pos.x; _stuckTimer = 0f; }
                else
                {
                    _stuckTimer += Time.deltaTime;
                    if (_stuckTimer > 0.7f) { _jump = true; _jumpHoldUntil = now + 0.45f; _stuckTimer = 0f; LastAction = "unstick"; }
                }
            }
            else { _stuckTimer = 0f; _lastProgressX = pos.x; }

            _cached = new PlayerCommands
            {
                moveX = move,
                jumpPressed = _jump,
                jumpHeld = _jump || now < _jumpHoldUntil,
                dashPressed = _dash,
                echoPressed = _echo,
                pulsePressed = _pulse,
                shieldHeld = now < _shieldUntil,
            };
            _jump = _dash = _echo = _pulse = false;
            return _cached;
        }

        void Do(string action, float t, float now)
        {
            ActionsFired++;
            LastAction = action;
            switch (action)
            {
                case "jump": _jump = true; _jumpHoldUntil = now + (t > 0f ? t : 0.45f); break;
                case "tap": _jump = true; _jumpHoldUntil = now + (t > 0f ? t : 0.08f); break;
                case "dash": _dash = true; break;
                case "jumpdash":
                    _jump = true; _jumpHoldUntil = now + 0.45f;
                    _scheduled.Add((now + (t > 0f ? t : 0.18f), "dash", 0f));
                    break;
                case "echo": _echo = true; break;
                case "pulse": _pulse = true; break;
                case "shield": _shieldUntil = now + (t > 0f ? t : 1.5f); break;
                case "wait": _waitUntil = now + (t > 0f ? t : 0.5f); break;
                case "waitbeat": _beatWait = 1; _beatWaitStarted = now; break;
                case "right": SetDir(1f, t, now); break;
                case "left": SetDir(-1f, t, now); break;
                case "stop": _dir = 0f; _dirRevertAt = t > 0f ? now + t : -1f; _dirRevertTo = 1f; break;
                default: Debug.LogWarning("[LumenWave] Unknown bot action " + action); break;
            }
        }

        void SetDir(float d, float t, float now)
        {
            if (t > 0f) { _dirRevertTo = _dir == 0f ? 1f : _dir; _dirRevertAt = now + t; }
            else _dirRevertAt = -1f;
            _dir = d;
        }
    }
}
