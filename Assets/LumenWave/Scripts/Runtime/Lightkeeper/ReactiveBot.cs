using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Rule-based Umbra for Lightkeeper mode (no hint markers). Each frame:
    /// walk toward the exit; a dangerous red beam in the next ~3 m → hold Shield if resonance allows, otherwise wait
    /// in the dark until it moves / turns off (timeout, then run through); no ground ahead → Echo bridge (or jump+dash
    /// without resonance); a wall step ahead → jump; not progressing → hop. Deaths are handled by LevelRuntime respawns.
    /// </summary>
    public class ReactiveBot : ICommandSource
    {
        public float lookAhead = 3f;
        public float waitTimeout = 4f;

        readonly Transform _player;
        readonly IPlayerBody _body;
        readonly Resonance _res;
        readonly SoundPowers _powers;
        float _targetX;

        int _lastFrame = -1;
        PlayerCommands _cached;
        float _jumpHoldUntil, _shieldUntil, _pauseUntil, _lastEcho = -10f, _waitStarted = -1f, _dashAt = -1f;
        bool _jump, _echo, _dash;
        float _stuckTimer, _lastProgressX;

        public string State { get; private set; } = "walk";

        public ReactiveBot(Transform player, float targetX)
        {
            _player = player;
            _targetX = targetX;
            _body = player.GetComponent<IPlayerBody>();
            _res = player.GetComponent<Resonance>();
            _powers = player.GetComponent<SoundPowers>();
            _lastProgressX = player.position.x;
            GameEvents.PlayerRespawned += OnRespawn;
        }

        public void Dispose() { GameEvents.PlayerRespawned -= OnRespawn; }

        void OnRespawn()
        {
            _jumpHoldUntil = _shieldUntil = _pauseUntil = 0f;
            _waitStarted = -1f; _dashAt = -1f;
            _jump = _echo = _dash = false;
            if (_player != null) _lastProgressX = _player.position.x;
        }

        static int GroundMask => Layers.Mask(Layers.Ground, Layers.Bridge, Layers.Crystal);

        /// <summary>Walking pace (fraction of max run speed) on the ground: a steady, readable walk.</summary>
        public float pace = 0.35f;
        /// <summary>While true (3-2-1 countdown, round over) Umbra stands still.</summary>
        public bool Hold;

        public PlayerCommands Poll()
        {
            if (Hold) return _cached = default;
            if (_lastFrame == Time.frameCount) return _cached;
            _lastFrame = Time.frameCount;
            if (_player == null) return _cached = default;
            float now = Time.time;
            Vector3 pos = _player.position;
            float dir = _targetX >= pos.x ? 1f : -1f;
            bool grounded = _body == null || _body.Grounded;
            float move = dir;
            State = "walk";

            // ---- Danger: red beams ahead.
            var danger = DangerAhead(pos, dir, out bool inside);
            float shieldNeed = _powers != null ? _powers.shieldMinToStart + 6f : 14f;
            bool shieldOn = _powers != null && _powers.ShieldActive;
            if (danger != LightBeam.BeamDanger.Safe)
            {
                bool canShield = _res != null && (_res.Has(shieldNeed) || (shieldOn && _res.current > 1f));
                if (canShield) { _shieldUntil = now + 0.25f; _waitStarted = -1f; State = "shield"; }
                else if (danger == LightBeam.BeamDanger.Lethal && !inside)
                {
                    if (_waitStarted < 0f) _waitStarted = now;
                    if (now - _waitStarted < waitTimeout) { move = 0f; State = "wait"; }
                    else State = "dash-through";
                    if (State == "dash-through" && grounded) _dash = true;
                }
                else State = "run-through";
            }
            else _waitStarted = -1f;

            // ---- Gaps: no ground ahead.
            if (move != 0f && grounded)
            {
                bool groundAhead = Physics.Raycast(new Vector3(pos.x + dir * 0.9f, pos.y + 0.5f, 0f), Vector3.down, 3f, GroundMask, QueryTriggerInteraction.Ignore);
                if (!groundAhead)
                {
                    bool canEcho = _powers != null && _res != null && _res.Has(_powers.echoCost) && now - _lastEcho > 1.6f;
                    if (canEcho) { _echo = true; _lastEcho = now; _pauseUntil = now + 0.45f; State = "echo"; }
                    else if (now - _lastEcho > 0.6f) { _jump = true; _jumpHoldUntil = now + 0.45f; _dashAt = now + 0.22f; State = "leap"; }
                }
                // ---- Step / wall ahead: jump.
                else if (Physics.Raycast(new Vector3(pos.x, pos.y + 0.4f, 0f), new Vector3(dir, 0f, 0f), 0.9f, Layers.Mask(Layers.Ground, Layers.Crystal), QueryTriggerInteraction.Ignore))
                { _jump = true; _jumpHoldUntil = now + 0.45f; State = "jump"; }
            }
            if (_dashAt > 0f && now >= _dashAt) { _dash = true; _dashAt = -1f; }
            if (now < _pauseUntil) move = 0f;

            // ---- Unstick.
            if (Mathf.Abs(move) > 0.1f)
            {
                if (Mathf.Abs(pos.x - _lastProgressX) > 0.15f) { _lastProgressX = pos.x; _stuckTimer = 0f; }
                else if ((_stuckTimer += Time.deltaTime) > 0.7f) { _jump = true; _jumpHoldUntil = now + 0.45f; _stuckTimer = 0f; State = "unstick"; }
            }
            else { _stuckTimer = 0f; _lastProgressX = pos.x; }

            _cached = new PlayerCommands
            {
                moveX = move * (grounded && State != "dash-through" ? pace : 1f),
                jumpPressed = _jump,
                jumpHeld = _jump || now < _jumpHoldUntil,
                dashPressed = _dash,
                echoPressed = _echo,
                shieldHeld = now < _shieldUntil,
            };
            _jump = _echo = _dash = false;
            return _cached;
        }

        /// <summary>Worst red beam crossing Umbra's path box (behind 0.6 m .. ahead lookAhead, feet .. head).</summary>
        public LightBeam.BeamDanger DangerAhead(Vector3 pos, float dir, out bool inside)
        {
            inside = false;
            var worst = LightBeam.BeamDanger.Safe;
            float x0 = dir > 0 ? pos.x - 0.6f : pos.x - lookAhead, x1 = dir > 0 ? pos.x + lookAhead : pos.x + 0.6f;
            float y0 = pos.y - 0.2f, y1 = pos.y + 2.2f;
            foreach (var b in LightField.Beams)
            {
                if (b == null || !b.isActiveAndEnabled || !b.on) continue;
                if (!b.IsLit && !b.IsBeatGated) continue;
                var d = b.Danger;
                if (d == LightBeam.BeamDanger.Safe) continue;
                float half = b.width * 0.5f + 0.3f;
                foreach (var s in b.Segments)
                {
                    if ((s.color & LightColor.Red) == 0) continue;
                    float len = s.Length;
                    int n = Mathf.Max(1, Mathf.CeilToInt(len / 0.3f));
                    for (int i = 0; i <= n; i++)
                    {
                        var p = Vector3.Lerp(s.start, s.end, i / (float)n);
                        if (p.y < y0 - half || p.y > y1 + half || p.x < x0 - half || p.x > x1 + half) continue;
                        if (d > worst) worst = d;
                        if (Mathf.Abs(p.x - pos.x) < 0.3f + half) inside = true;
                        break;
                    }
                }
            }
            return worst;
        }
    }
}
