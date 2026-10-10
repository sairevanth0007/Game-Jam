using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Umbra's three sound powers. Added to the player on <see cref="GameEvents.PlayerSpawned"/>.
    /// Each power plays a just-intonation note, shows a travelling sound wave and costs Resonance.
    /// </summary>
    public class SoundPowers : MonoBehaviour
    {
        [Header("Costs")]
        public float echoCost = 25f;
        public float pulseCost = 20f;
        public float shieldDrainPerSecond = 22f;
        public float shieldMinToStart = 8f;

        [Header("Tuning")]
        public float echoCooldown = 0.35f;
        public float pulseCooldown = 0.5f;
        public int maxBridges = 2;
        /// <summary>Height of Umbra's centre above the transform pivot (feet).</summary>
        public float centerHeight = 0.9f;

        public IPlayerBody Body { get; private set; }
        public Resonance Resonance { get; private set; }
        public ShieldDome Shield { get; private set; }
        public bool ShieldActive => Shield != null && Shield.IsActive;

        /// <summary>Optional command override (tests). Otherwise reads the body's current commands.</summary>
        public ICommandSource commandOverride;

        readonly List<EchoBridge> _bridges = new List<EchoBridge>();
        float _nextEcho, _nextPulse;
        bool _shieldHeldPrev;
        PlayerHealth _health;
        Func<PlayerCommands> _currentCommands;

        void Awake()
        {
            Body = GetComponent<IPlayerBody>();
            Resonance = GetComponent<Resonance>();
            if (Resonance == null) Resonance = gameObject.AddComponent<Resonance>();
            _health = GetComponent<PlayerHealth>();
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Shield, true);
            var go = new GameObject("ResonanceShield");
            go.transform.SetParent(transform, false);
            Shield = go.AddComponent<ShieldDome>();
            Shield.Init(this);
        }

        void Start()
        {
            if (Body == null) Body = GetComponent<IPlayerBody>();
            FindCommandProvider();
        }

        /// <summary>
        /// The player controller polls its command source once per frame; if it exposes that frame's
        /// <see cref="PlayerCommands"/> as a public property we reuse it (so edge-triggered presses from a bot
        /// aren't consumed twice). Otherwise we poll the source ourselves.
        /// </summary>
        void FindCommandProvider()
        {
            foreach (var c in GetComponents<MonoBehaviour>())
            {
                if (c == null || c == this) continue;
                foreach (var p in c.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.PropertyType == typeof(PlayerCommands) && p.CanRead && p.GetIndexParameters().Length == 0)
                    {
                        var comp = c; var prop = p;
                        _currentCommands = () => (PlayerCommands)prop.GetValue(comp);
                        return;
                    }
                }
            }
        }

        PlayerCommands ReadCommands()
        {
            if (commandOverride != null) return commandOverride.Poll();
            if (_currentCommands != null) return _currentCommands();
            if (Body?.Commands != null) return Body.Commands.Poll();
            return default;
        }

        int Facing => Body != null && Body.Facing != 0 ? Body.Facing : 1;
        public Vector3 Center => transform.position + Vector3.up * centerHeight;

        void Update()
        {
            if (GameSession.Paused) return;
            bool dead = _health != null && _health.IsDead;
            var cmd = dead ? default : ReadCommands();

            if (cmd.echoPressed) TryEcho();
            if (cmd.pulsePressed) TryPulse();
            UpdateShield(cmd.shieldHeld && !dead);
            _bridges.RemoveAll(b => b == null);
        }

        // -------------------------------------------------------- Echo Path

        public bool TryEcho()
        {
            if (Time.time < _nextEcho) return false;
            if (!Resonance.TrySpend(echoCost)) { GameEvents.RaisePowerFailed(PowerType.Echo); return false; }
            _nextEcho = Time.time + echoCooldown;
            while (_bridges.Count >= maxBridges) { var old = _bridges[0]; _bridges.RemoveAt(0); if (old != null) old.Dissolve(); }
            var b = EchoBridge.Spawn(transform.position, Facing, Harmony.EchoNote);
            _bridges.Add(b);
            GameEvents.RaisePowerUsed(PowerType.Echo);
            return true;
        }

        // -------------------------------------------------------- Pulse

        public bool TryPulse()
        {
            if (Time.time < _nextPulse) return false;
            if (!Resonance.TrySpend(pulseCost)) { GameEvents.RaisePowerFailed(PowerType.Pulse); return false; }
            _nextPulse = Time.time + pulseCooldown;
            PulseWave.Spawn(Center, gameObject);
            GameEvents.RaisePowerUsed(PowerType.Pulse);
            return true;
        }

        // -------------------------------------------------------- Shield

        void UpdateShield(bool held)
        {
            bool active = Shield.IsActive;
            if (held && !active)
            {
                if (Resonance.Has(shieldMinToStart))
                {
                    Shield.SetActive(true);
                    GameEvents.RaisePowerUsed(PowerType.Shield);
                }
                else if (!_shieldHeldPrev) GameEvents.RaisePowerFailed(PowerType.Shield);
            }
            else if (!held && active) Shield.SetActive(false);

            if (Shield.IsActive)
            {
                if (!Resonance.Drain(shieldDrainPerSecond * Time.deltaTime)) Shield.SetActive(false);
                AudioDirector.Instance?.StartSustain("shield", Harmony.ShieldNote, 0.32f);
            }
            else AudioDirector.Instance?.StopSustain("shield");
            _shieldHeldPrev = held;
        }

        void OnDisable()
        {
            if (Shield != null) Shield.SetActive(false);
        }

        /// <summary>Parent for spawned power objects so respawn resets them.</summary>
        public static Transform SpawnParent => LevelRuntime.Current != null ? LevelRuntime.Current.transform : null;
    }
}
