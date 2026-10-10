using UnityEngine;
using UnityEngine.Events;

namespace LumenWave
{
    /// <summary>Trigger at the top of the tower. Tells GameManager the player won.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class WinZone : MonoBehaviour
    {
        [Tooltip("Layers that count as the player.")]
        [SerializeField] LayerMask playerLayers = 1 << 0;
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
            if ((playerLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            if (gameManager == null) gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogWarning("[WinZone] No GameManager in the scene.", this);
                return;
            }

            if (gameManager.State != GameState.Playing)
                return;

            gameManager.Win();
            onPlayerEntered.Invoke();
        }
    }
}
