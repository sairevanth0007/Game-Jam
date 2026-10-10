using System;
using System.Collections;
using UnityEngine;

namespace LumenWave
{
    /// <summary>Things that reset when the player respawns at a checkpoint.</summary>
    public interface IResettable
    {
        void ResetState();
    }

    /// <summary>
    /// The only component placed in generated scenes. Decides what the scene is (title, end, level N)
    /// and lets subsystems build the rest at runtime from code + data.
    /// </summary>
    public class SceneBootstrap : MonoBehaviour
    {
        public string kind = "level";   // "title", "end", "level"
        public int levelNumber = 1;

        /// <summary>Raised for every scene before content is built: (kind, level). Visual rigs, UI, audio hook in here.</summary>
        public static event Action<string, int> Booting;

        void Awake()
        {
            var _ = GameSystems.Root;
            Booting?.Invoke(kind, levelNumber);
            if (kind == "level")
            {
                // A test or menu may have chosen a different level to run in this shell scene.
                var go = new GameObject("LevelRuntime");
                var rt = go.AddComponent<LevelRuntime>();
                rt.Load(levelNumber);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Booting = null; }
    }

    /// <summary>Owns a running level: builds it, spawns Umbra, tracks time, checkpoints and respawns.</summary>
    public class LevelRuntime : MonoBehaviour
    {
        public static LevelRuntime Current { get; private set; }

        public LevelData Data { get; private set; }
        public LevelContext Context { get; private set; }
        public int LevelNumber { get; private set; }
        public GameObject Player { get; set; }
        public Vector3 RespawnPoint { get; private set; }
        public int CheckpointIndex { get; private set; } = -1;
        public bool Completed { get; private set; }
        public float respawnDelay = 1.1f;

        /// <summary>Raised after the level is built and the player spawned.</summary>
        public static event Action<LevelRuntime> Loaded;

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        public void Load(int levelNumber)
        {
            LevelNumber = levelNumber;
            GameSession.CurrentLevel = levelNumber;
            GameSession.LevelTime = 0f;
            Data = LevelDatabase.Load(levelNumber);
            Context = LevelBuilder.Build(Data, transform, levelNumber);
            RespawnPoint = Data.PlayerStart;
            if (Player == null && Context.byType.TryGetValue("player", out var ps) && ps.Count > 0) Player = ps[0];
            if (Player != null)
            {
                var h = Player.GetComponent<PlayerHealth>();
                if (h != null) h.Died += OnPlayerDied;
            }
            else Debug.LogWarning("[LumenWave] Level has no player entity");
            Debug.Log($"[LumenWave] Loaded level {levelNumber} '{Data.Name}' ({Data.Width}x{Data.Height}, {Data.entities.Count} entities)");
            GameEvents.RaiseLevelStarted(levelNumber);
            Loaded?.Invoke(this);
            var msg = Data.Get("intro", "");
            if (msg.Length > 0) GameEvents.RaiseMessage(msg);
        }

        void Update()
        {
            if (!Completed && !GameSession.Paused)
            {
                GameSession.LevelTime += Time.deltaTime;
                GameSession.TotalTime += Time.deltaTime;
            }
            // Safety net: falling out of the world kills.
            if (Player != null && Player.transform.position.y < -12f)
            {
                var h = Player.GetComponent<PlayerHealth>();
                if (h != null && !h.IsDead) h.Kill();
            }
        }

        public void SetCheckpoint(Vector3 position, int index)
        {
            if (index <= CheckpointIndex) return;
            CheckpointIndex = index;
            RespawnPoint = position;
            GameEvents.RaiseCheckpoint(index);
        }

        void OnPlayerDied() { StartCoroutine(RespawnRoutine()); }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(respawnDelay);
            Respawn();
        }

        public void Respawn()
        {
            if (Player == null) return;
            foreach (var r in GetComponentsInChildren<MonoBehaviour>(true))
                if (r is IResettable rs) rs.ResetState();
            var mover = Player.GetComponent<IPlayerBody>();
            if (mover != null) mover.Teleport(RespawnPoint);
            else Player.transform.position = RespawnPoint;
            var res = Player.GetComponent<Resonance>();
            if (res != null) res.current = Mathf.Max(res.current, res.max * 0.5f);
            Player.GetComponent<PlayerHealth>()?.Revive();
        }

        /// <summary>Called by the exit / win zone.</summary>
        public void Complete()
        {
            if (Completed) return;
            Completed = true;
            Debug.Log($"[LumenWave] Level {LevelNumber} complete in {GameSession.LevelTime:F1}s");
            GameEvents.RaiseLevelCompleted(LevelNumber, GameSession.LevelTime);
            if (!GameSession.BotMode || autoAdvanceInBotMode) StartCoroutine(AdvanceRoutine());
        }

        public static bool autoAdvanceInBotMode = false;
        public float advanceDelay = 2.0f;

        IEnumerator AdvanceRoutine()
        {
            yield return new WaitForSeconds(advanceDelay);
            SceneFlow.NextLevel();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Loaded = null; Current = null; }
    }

    /// <summary>Movement body of the player (implemented by the player controller).</summary>
    public interface IPlayerBody
    {
        void Teleport(Vector3 position);
        Vector2 Velocity { get; set; }
        int Facing { get; }
        bool Grounded { get; }
        /// <summary>Multiplier on gravity set by blue light each frame (1 = normal).</summary>
        float GravityScale { get; set; }
        ICommandSource Commands { get; set; }
    }
}
