using UnityEngine;
using UnityEngine.Events;

namespace LumenWave
{
    /// <summary>The level-end door. Tells GameManager the level is complete when the player walks in.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class WinZone : MonoBehaviour
    {
        [Tooltip("Layers that may count as the player (the object must also have PlayerMovement). Everything by default.")]
        [SerializeField] LayerMask playerLayers = ~0;
        [Tooltip("Leave empty to use GameManager.Instance.")]
        [SerializeField] GameManager gameManager;

        public UnityEvent onPlayerEntered = new UnityEvent();

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0 || !PlayerMovement.IsPlayer(other))
                return;

            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning("[WinZone] No GameManager in the scene.", this);
                return;
            }

            if (gameManager.State != GameState.Playing)
                return;

            gameManager.CompleteLevel();
            onPlayerEntered.Invoke();
        }
    }
}
