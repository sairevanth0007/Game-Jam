using System;
using UnityEngine;

namespace LumenWave
{
    public enum DeathCause
    {
        Fell,
        Burned,
    }

    /// <summary>What the level-complete screen shows.</summary>
    public struct LevelResult
    {
        public int LevelIndex;
        public string LevelName;
        public float Time;
        public int Echoes;
        public int EchoTotal;
        public int Burns;
        public bool IsLastLevel;
    }

    /// <summary>
    /// The one event bus. Gameplay, UI, audio and (later) MALink all talk through here,
    /// so a new listener never has to know who raised an event.
    /// Raise with the Raise* methods; subscribe in OnEnable, unsubscribe in OnDisable.
    /// </summary>
    public static class GameEvents
    {
        // ---------- Notes ----------
        public static event Action<Note> NotePressed;
        public static event Action<Note> NoteReleased;

        // ---------- Player ----------
        public static event Action PlayerJumped;
        /// <summary>Impact speed in units/s (positive).</summary>
        public static event Action<float> PlayerLanded;
        public static event Action<Vector2, DeathCause> PlayerDied;
        public static event Action<Vector2> PlayerRespawned;

        // ---------- World ----------
        public static event Action<Vector2> CheckpointReached;
        /// <summary>Position, collected count in this level, total in this level.</summary>
        public static event Action<Vector2, int, int> EchoCollected;
        /// <summary>Text to show, or null to hide.</summary>
        public static event Action<string> HintChanged;

        // ---------- Flow ----------
        public static event Action<LevelData, int> LevelStarted;
        public static event Action<LevelResult> LevelCompleted;
        public static event Action<GameState> GameStateChanged;

        // ---------- Feel ----------
        /// <summary>Trauma 0..1 added to the camera shake.</summary>
        public static event Action<float> CameraShake;

        public static void RaiseNotePressed(Note note) => NotePressed?.Invoke(note);
        public static void RaiseNoteReleased(Note note) => NoteReleased?.Invoke(note);
        public static void RaisePlayerJumped() => PlayerJumped?.Invoke();
        public static void RaisePlayerLanded(float impactSpeed) => PlayerLanded?.Invoke(impactSpeed);
        public static void RaisePlayerDied(Vector2 position, DeathCause cause) => PlayerDied?.Invoke(position, cause);
        public static void RaisePlayerRespawned(Vector2 position) => PlayerRespawned?.Invoke(position);
        public static void RaiseCheckpointReached(Vector2 position) => CheckpointReached?.Invoke(position);
        public static void RaiseEchoCollected(Vector2 position, int collected, int total) => EchoCollected?.Invoke(position, collected, total);
        public static void RaiseHintChanged(string text) => HintChanged?.Invoke(text);
        public static void RaiseLevelStarted(LevelData level, int index) => LevelStarted?.Invoke(level, index);
        public static void RaiseLevelCompleted(LevelResult result) => LevelCompleted?.Invoke(result);
        public static void RaiseGameStateChanged(GameState state) => GameStateChanged?.Invoke(state);
        public static void RaiseCameraShake(float trauma) => CameraShake?.Invoke(trauma);

        // Static events survive "Enter Play Mode" without a domain reload; start every play session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            NotePressed = null;
            NoteReleased = null;
            PlayerJumped = null;
            PlayerLanded = null;
            PlayerDied = null;
            PlayerRespawned = null;
            CheckpointReached = null;
            EchoCollected = null;
            HintChanged = null;
            LevelStarted = null;
            LevelCompleted = null;
            GameStateChanged = null;
            CameraShake = null;
        }
    }
}
