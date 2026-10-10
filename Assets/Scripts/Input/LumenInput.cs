using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// The one place that enables and disables the LumenControls actions.
    /// Put exactly one in the first scene. Other scripts only subscribe/unsubscribe callbacks;
    /// they never call Enable/Disable on shared actions. To block the player, use
    /// PlayerMovement.SetControlEnabled(false) instead.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LumenInput : MonoBehaviour
    {
        public static LumenInput Instance { get; private set; }

        [Tooltip("The LumenControls asset.")]
        [SerializeField] InputActionAsset actions;
        [Tooltip("Keep this object (and the enabled actions) alive across scene loads.")]
        [SerializeField] bool persistAcrossScenes = true;

        public InputActionAsset Actions => actions;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A persistent LumenInput already exists (e.g. after reloading the first scene).
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            if (actions == null)
                Debug.LogWarning("[LumenInput] No InputActionAsset assigned; drag LumenControls into Actions.", this);
        }

        void OnEnable()
        {
            if (Instance == this && actions != null)
                actions.Enable();
        }

        void OnDisable()
        {
            if (Instance == this && actions != null)
                actions.Disable();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
