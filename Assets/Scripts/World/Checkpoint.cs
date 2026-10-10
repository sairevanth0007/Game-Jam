using UnityEngine;
using UnityEngine.Events;

namespace LumenWave
{
    /// <summary>
    /// Shadow rune: trigger that sets the respawn point the first time the player touches it.
    /// Needs a Collider2D with Is Trigger on (set automatically). The player is recognised by its
    /// PlayerMovement component, optionally narrowed down with Player Layers.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Layers that may count as the player (the object must also have PlayerMovement). Everything by default.")]
        [SerializeField] LayerMask playerLayers = ~0;
        [Tooltip("Where the player respawns. If empty, this checkpoint's position is used.")]
        [SerializeField] Transform respawnPoint;
        [Tooltip("Leave empty to use CheckpointManager.Instance.")]
        [SerializeField] CheckpointManager manager;

        [Header("Optional visuals")]
        [SerializeField] GameObject inactiveVisual;
        [SerializeField] GameObject activeVisual;

        public UnityEvent OnCheckpointReached = new UnityEvent();

        public bool IsActivated { get; private set; }
        public Vector2 RespawnPosition => respawnPoint != null ? (Vector2)respawnPoint.position : (Vector2)transform.position;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            UpdateVisuals();
        }

        void Start()
        {
            if (manager == null) manager = CheckpointManager.Instance;
            if (manager != null) manager.Register(this);
            else Debug.LogWarning("[Checkpoint] No CheckpointManager in the scene.", this);
        }

        void OnDestroy()
        {
            if (manager != null) manager.Unregister(this);
        }

        /// <summary>Visuals for code-built checkpoints (LevelLoader).</summary>
        public void SetVisuals(GameObject inactive, GameObject active)
        {
            inactiveVisual = inactive;
            activeVisual = active;
            UpdateVisuals();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (IsActivated || (playerLayers.value & (1 << other.gameObject.layer)) == 0 || !PlayerMovement.IsPlayer(other))
                return;

            IsActivated = true;
            UpdateVisuals();
            if (manager != null) manager.ReachCheckpoint(this);
            OnCheckpointReached.Invoke();
            GameEvents.RaiseCheckpointReached(RespawnPosition);
        }

        /// <summary>Re-arm this checkpoint (called by CheckpointManager.ResetAll on restart).</summary>
        public void ResetCheckpoint()
        {
            IsActivated = false;
            UpdateVisuals();
        }

        void UpdateVisuals()
        {
            if (inactiveVisual != null) inactiveVisual.SetActive(!IsActivated);
            if (activeVisual != null) activeVisual.SetActive(IsActivated);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = IsActivated ? Color.green : new Color(1f, 0.6f, 0f);
            Gizmos.DrawWireSphere(RespawnPosition, 0.25f);
        }
    }
}
