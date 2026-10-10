using System;
using UnityEngine;

namespace LumenWave
{
    public enum PowerType { Echo, Shield, Pulse, Dash }

    /// <summary>
    /// Global event hub. Gameplay raises; audio, HUD, camera shake and MALink listen.
    /// Keeps systems decoupled so they can be built in parallel.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<float, LightColor> PlayerDamaged;   // amount, colour source
        public static event Action<float> PlayerHealed;
        public static event Action PlayerDied;
        public static event Action PlayerRespawned;
        public static event Action<PowerType> PowerUsed;
        public static event Action<PowerType> PowerFailed;              // not enough resonance
        public static event Action<int> CheckpointReached;              // checkpoint index
        public static event Action<int> LevelStarted;                   // level number
        public static event Action<int, float> LevelCompleted;          // level number, time
        public static event Action GameWon;
        public static event Action PlayerJumped;
        public static event Action PlayerLanded;
        public static event Action<Vector3> CrystalShattered;
        public static event Action<Vector3> EnemyStunned;
        public static event Action<string> Message;                     // tutorial / info text

        public static void RaisePlayerDamaged(float amount, LightColor c) => PlayerDamaged?.Invoke(amount, c);
        public static void RaisePlayerHealed(float amount) => PlayerHealed?.Invoke(amount);
        public static void RaisePlayerDied() => PlayerDied?.Invoke();
        public static void RaisePlayerRespawned() => PlayerRespawned?.Invoke();
        public static void RaisePowerUsed(PowerType p) => PowerUsed?.Invoke(p);
        public static void RaisePowerFailed(PowerType p) => PowerFailed?.Invoke(p);
        public static void RaiseCheckpoint(int i) => CheckpointReached?.Invoke(i);
        public static void RaiseLevelStarted(int n) => LevelStarted?.Invoke(n);
        public static void RaiseLevelCompleted(int n, float t) => LevelCompleted?.Invoke(n, t);
        public static void RaiseGameWon() => GameWon?.Invoke();
        public static void RaisePlayerJumped() => PlayerJumped?.Invoke();
        public static void RaisePlayerLanded() => PlayerLanded?.Invoke();
        public static void RaiseCrystalShattered(Vector3 p) => CrystalShattered?.Invoke(p);
        public static void RaiseEnemyStunned(Vector3 p) => EnemyStunned?.Invoke(p);
        public static void RaiseMessage(string m) => Message?.Invoke(m);

        /// <summary>Clears all listeners (tests, domain reload off).</summary>
        public static void ResetAll()
        {
            PlayerDamaged = null; PlayerHealed = null; PlayerDied = null; PlayerRespawned = null;
            PowerUsed = null; PowerFailed = null; CheckpointReached = null; LevelStarted = null;
            LevelCompleted = null; GameWon = null; PlayerJumped = null; PlayerLanded = null;
            CrystalShattered = null; EnemyStunned = null; Message = null;
        }
    }

    /// <summary>Anything that reacts when a Pulse wavefront passes over it.</summary>
    public interface IPulseReceiver
    {
        void OnPulse(Vector3 origin, float strength);
    }

    /// <summary>Physics layers (set in ProjectSettings/TagManager.asset).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int Ground = 8;
        public const int Player = 9;
        public const int Shield = 10;
        public const int Mirror = 11;
        public const int Prism = 12;
        public const int Enemy = 13;
        public const int Bridge = 14;
        public const int Trigger = 15;
        public const int Hazard = 16;
        public const int Crystal = 17;

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (var l in layers) m |= 1 << l;
            return m;
        }

        /// <summary>What the player stands on / collides with.</summary>
        public static int Solid => Mask(Ground, Bridge, Crystal, Default);
        /// <summary>What blocks / redirects light beams.</summary>
        public static int LightBlockers => Mask(Ground, Bridge, Crystal, Shield, Mirror, Prism, Default);
    }
}
