using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LumenWave
{
    /// <summary>
    /// Holds the initial spawn point and the current respawn point. Checkpoints report here.
    /// One per scene.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        [Tooltip("Where the run starts. If empty, this object's position is used.")]
        [SerializeField] Transform initialSpawnPoint;

        [Tooltip("Fires when a checkpoint is reached for the first time.")]
        public UnityEvent<Checkpoint> onCheckpointReached = new UnityEvent<Checkpoint>();

        public event Action<Checkpoint> CheckpointReached;

        public Vector2 InitialSpawnPosition
        {
            get
            {
                if (spawnOverride.HasValue) return spawnOverride.Value;
                return initialSpawnPoint != null ? (Vector2)initialSpawnPoint.position : (Vector2)transform.position;
            }
        }
        public Vector2 RespawnPosition { get; private set; }
        public Checkpoint CurrentCheckpoint { get; private set; }

        readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        Vector2? spawnOverride;

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[CheckpointManager] More than one in the scene ({Instance.name}, {name}). Using the newest.", this);
            Instance = this;
            RespawnPosition = InitialSpawnPosition;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Register(Checkpoint checkpoint)
        {
            if (!checkpoints.Contains(checkpoint))
                checkpoints.Add(checkpoint);
        }

        public void Unregister(Checkpoint checkpoint) => checkpoints.Remove(checkpoint);

        /// <summary>Called by a Checkpoint the first time the player touches it.</summary>
        public void ReachCheckpoint(Checkpoint checkpoint)
        {
            CurrentCheckpoint = checkpoint;
            RespawnPosition = checkpoint.RespawnPosition;
            CheckpointReached?.Invoke(checkpoint);
            onCheckpointReached.Invoke(checkpoint);
        }

        /// <summary>New level: its start position becomes the initial spawn, and the respawn point is reset to it.</summary>
        public void SetInitialSpawn(Vector2 position)
        {
            spawnOverride = position;
            CurrentCheckpoint = null;
            RespawnPosition = position;
        }

        /// <summary>Back to the initial spawn, with every checkpoint re-armed (used on restart).</summary>
        public void ResetAll()
        {
            CurrentCheckpoint = null;
            RespawnPosition = InitialSpawnPosition;
            foreach (Checkpoint checkpoint in checkpoints)
                checkpoint.ResetCheckpoint();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(InitialSpawnPosition, 0.3f);
        }
    }
}
