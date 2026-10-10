using System;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Health of Umbra. Damage comes from light (by colour), enemies and hazards.</summary>
    public class PlayerHealth : MonoBehaviour
    {
        public float max = 100f;
        public float current = 100f;
        public float invulnerableAfterRespawn = 1.0f;
        public bool godMode;

        public bool IsDead { get; private set; }
        public float Normalized => max > 0 ? current / max : 0f;
        float _invulUntil;
        float _damageEventAccum;
        LightColor _damageEventColor;

        public event Action Died;

        public void Damage(float amount, LightColor source = LightColor.None)
        {
            if (IsDead || amount <= 0f || godMode || Time.time < _invulUntil) return;
            current = Mathf.Max(0f, current - amount);
            // Throttle the event for continuous (per-frame) damage so listeners aren't spammed.
            _damageEventAccum += amount;
            _damageEventColor = source;
            if (_damageEventAccum >= 4f || current <= 0f)
            {
                GameEvents.RaisePlayerDamaged(_damageEventAccum, _damageEventColor);
                _damageEventAccum = 0f;
            }
            if (current <= 0f) Kill();
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f || current >= max) return;
            current = Mathf.Min(max, current + amount);
            GameEvents.RaisePlayerHealed(amount);
        }

        public void Kill()
        {
            if (IsDead || godMode) return;
            current = 0f;
            IsDead = true;
            GameSession.Deaths++;
            Died?.Invoke();
            GameEvents.RaisePlayerDied();
        }

        public void Revive()
        {
            IsDead = false;
            current = max;
            _invulUntil = Time.time + invulnerableAfterRespawn;
            GameEvents.RaisePlayerRespawned();
        }
    }

    /// <summary>
    /// Resonance: the energy that powers sound abilities. Refills in darkness and in green light
    /// (that refill is applied by the light receiver via <see cref="Add"/>).
    /// </summary>
    public class Resonance : MonoBehaviour
    {
        public float max = 100f;
        public float current = 100f;
        public bool infinite;

        public float Normalized => max > 0 ? current / max : 0f;

        public bool Has(float amount) => infinite || current >= amount;

        public bool TrySpend(float amount)
        {
            if (infinite) return true;
            if (current < amount) return false;
            current -= amount;
            return true;
        }

        /// <summary>Continuous drain (e.g. shield). Returns false when empty.</summary>
        public bool Drain(float amount)
        {
            if (infinite) return true;
            current = Mathf.Max(0f, current - amount);
            return current > 0f;
        }

        public void Add(float amount)
        {
            current = Mathf.Min(max, current + Mathf.Max(0f, amount));
        }

        public void Fill() => current = max;
    }
}
