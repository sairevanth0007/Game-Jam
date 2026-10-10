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

        /// <summary>
        /// Creates the LumenInput object from code (used by GameBootstrap). If one already exists
        /// (e.g. it persisted from an earlier scene), that one is returned instead.
        /// </summary>
        public static LumenInput Create(InputActionAsset asset)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("LumenInput");
            go.SetActive(false); // so Awake/OnEnable run after the asset is assigned
            var input = go.AddComponent<LumenInput>();
            input.actions = asset;
            go.SetActive(true);
            return input;
        }

        /// <summary>
        /// Finds an action by "Map/Action" path in the active LumenControls, e.g. FindAction("Player/Move").
        /// Scripts use this as the fallback when their InputActionReference field is empty (code-built scenes).
        /// </summary>
        public static InputAction FindAction(string path)
        {
            if (Instance == null || Instance.actions == null)
                return null;
            return Instance.actions.FindAction(path, false);
        }

        /// <summary>The reference's action if set, otherwise the action at the given path.</summary>
        public static InputAction Resolve(InputActionReference reference, string fallbackPath)
        {
            if (reference != null && reference.action != null)
                return reference.action;
            return FindAction(fallbackPath);
        }

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
