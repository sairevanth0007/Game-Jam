using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Runtime key rebinding. Saves overrides to PlayerPrefs and loads them on Awake.
    ///
    /// No-code settings menu: put one InputRebinder per row, set Target Action + Target Binding Index,
    /// point a Button's OnClick at StartRebind(), and wire On Rebind Finished (string) to a text label.
    /// From code, call StartRebind(action, bindingIndex) directly.
    /// </summary>
    public class InputRebinder : MonoBehaviour
    {
        [Tooltip("The LumenControls asset. If empty, LumenInput's asset or Target Action's asset is used.")]
        [SerializeField] InputActionAsset actions;
        [SerializeField] string playerPrefsKey = "LumenWave.BindingOverrides";

        [Header("Used by the no-argument StartRebind()")]
        [SerializeField] InputActionReference targetAction;
        [Tooltip("Index into the action's binding list. Composite parts count, e.g. Move: 0 = WASD, 1 = W, 2 = S, 3 = A, 4 = D.")]
        [SerializeField] int targetBindingIndex;

        [Header("Rebind options")]
        [SerializeField] string cancelControlPath = "<Keyboard>/escape";
        [SerializeField] bool excludeMouse = true;

        [Header("Events")]
        [Tooltip("Passes the name of the action being rebound.")]
        public UnityEvent<string> onRebindStarted = new UnityEvent<string>();
        [Tooltip("Passes the new binding as display text, e.g. \"Space\".")]
        public UnityEvent<string> onRebindFinished = new UnityEvent<string>();
        [Tooltip("Passes the unchanged binding as display text.")]
        public UnityEvent<string> onRebindCanceled = new UnityEvent<string>();
        public UnityEvent onBindingsReset = new UnityEvent();

        InputActionRebindingExtensions.RebindingOperation rebindOperation;

        public bool IsRebinding => rebindOperation != null;

        InputActionAsset Asset
        {
            get
            {
                if (actions != null) return actions;
                if (LumenInput.Instance != null && LumenInput.Instance.Actions != null) return LumenInput.Instance.Actions;
                if (targetAction != null && targetAction.action != null) return targetAction.action.actionMap?.asset;
                return null;
            }
        }

        void Awake()
        {
            LoadOverrides();
        }

        void OnDisable()
        {
            CancelRebind();
        }

        /// <summary>Rebinds Target Action / Target Binding Index set in the inspector.</summary>
        public void StartRebind()
        {
            StartRebind(targetAction, targetBindingIndex);
        }

        /// <summary>Waits for the player to press a control and assigns it to the given binding.</summary>
        public void StartRebind(InputActionReference actionReference, int bindingIndex)
        {
            if (actionReference == null || actionReference.action == null)
            {
                Debug.LogWarning("[InputRebinder] No action to rebind.", this);
                return;
            }

            InputAction action = actionReference.action;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                Debug.LogWarning($"[InputRebinder] {action.name} has no binding at index {bindingIndex}.", this);
                return;
            }
            if (action.bindings[bindingIndex].isComposite)
            {
                Debug.LogWarning($"[InputRebinder] Binding {bindingIndex} of {action.name} is a composite. Use the index of one of its parts.", this);
                return;
            }

            CancelRebind();

            // The Input System refuses to rebind an enabled action, so this is the one place besides
            // LumenInput that disables an action. Only for the rebind; the previous state is restored after.
            bool wasEnabled = action.enabled;
            action.Disable();

            rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    FinishRebind(action, wasEnabled);
                    SaveOverrides();
                    onRebindFinished.Invoke(action.GetBindingDisplayString(bindingIndex));
                })
                .OnCancel(operation =>
                {
                    FinishRebind(action, wasEnabled);
                    onRebindCanceled.Invoke(action.GetBindingDisplayString(bindingIndex));
                });

            if (!string.IsNullOrEmpty(cancelControlPath))
                rebindOperation.WithCancelingThrough(cancelControlPath);
            if (excludeMouse)
                rebindOperation.WithControlsExcluding("<Mouse>");

            onRebindStarted.Invoke(action.name);
            rebindOperation.Start();
        }

        public void CancelRebind()
        {
            rebindOperation?.Cancel();
        }

        void FinishRebind(InputAction action, bool reenable)
        {
            rebindOperation?.Dispose();
            rebindOperation = null;
            if (reenable)
                action.Enable();
        }

        public void SaveOverrides()
        {
            InputActionAsset asset = Asset;
            if (asset == null) return;

            PlayerPrefs.SetString(playerPrefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        public void LoadOverrides()
        {
            InputActionAsset asset = Asset;
            if (asset == null || !PlayerPrefs.HasKey(playerPrefsKey)) return;

            asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(playerPrefsKey));
        }

        /// <summary>Back to the bindings stored in LumenControls.inputactions, and forget the saved ones.</summary>
        public void ResetAll()
        {
            CancelRebind();

            InputActionAsset asset = Asset;
            if (asset != null)
                asset.RemoveAllBindingOverrides();

            PlayerPrefs.DeleteKey(playerPrefsKey);
            PlayerPrefs.Save();
            onBindingsReset.Invoke();
        }

        /// <summary>Display text for a binding, e.g. for a settings menu label.</summary>
        public static string GetBindingDisplayString(InputActionReference actionReference, int bindingIndex)
        {
            if (actionReference == null || actionReference.action == null) return string.Empty;
            InputAction action = actionReference.action;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return string.Empty;
            return action.GetBindingDisplayString(bindingIndex);
        }
    }
}
