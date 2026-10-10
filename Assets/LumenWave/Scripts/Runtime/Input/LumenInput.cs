using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Owns the game's Input System actions, built in code (keyboard + mouse + gamepad).
    /// Rebinding overrides persist in PlayerPrefs. Single source of truth for prompts.
    /// </summary>
    public static class LumenInput
    {
        public const string PrefsKey = "lw.bindings";
        public const string KeyboardGroup = "Keyboard";
        public const string GamepadGroup = "Gamepad";

        static InputActionAsset _asset;
        public static InputActionAsset Asset { get { Ensure(); return _asset; } }
        public static InputActionMap Gameplay { get; private set; }
        public static InputAction Move { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Dash { get; private set; }
        public static InputAction Echo { get; private set; }
        public static InputAction Shield { get; private set; }
        public static InputAction Pulse { get; private set; }
        public static InputAction Pause { get; private set; }

        /// <summary>Actions shown in the rebinding menu, in order.</summary>
        public static InputAction[] Rebindable => new[] { Jump, Dash, Echo, Shield, Pulse, Pause };

        /// <summary>True when the last device used was a gamepad (prompts switch accordingly).</summary>
        public static bool UsingGamepad { get; private set; }

        public static void Ensure()
        {
            if (_asset != null) return;
            _asset = BuildAsset();
            Gameplay = _asset.FindActionMap("Gameplay", true);
            Move = Gameplay.FindAction("Move", true);
            Jump = Gameplay.FindAction("Jump", true);
            Dash = Gameplay.FindAction("Dash", true);
            Echo = Gameplay.FindAction("Echo", true);
            Shield = Gameplay.FindAction("Shield", true);
            Pulse = Gameplay.FindAction("Pulse", true);
            Pause = Gameplay.FindAction("Pause", true);
            LoadOverrides();
            _asset.Enable();
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
        }

        static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            if (obj is InputAction a && a.activeControl != null)
                UsingGamepad = a.activeControl.device is Gamepad;
        }

        public static InputActionAsset BuildAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "LumenWaveInput";
            asset.AddControlScheme(KeyboardGroup).WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            asset.AddControlScheme(GamepadGroup).WithRequiredDevice("<Gamepad>");

            var map = asset.AddActionMap("Gameplay");

            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Axis");
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a", KeyboardGroup).With("Positive", "<Keyboard>/d", KeyboardGroup);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow", KeyboardGroup).With("Positive", "<Keyboard>/rightArrow", KeyboardGroup);
            move.AddBinding("<Gamepad>/leftStick/x").WithGroup(GamepadGroup);
            move.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/dpad/left", GamepadGroup).With("Positive", "<Gamepad>/dpad/right", GamepadGroup);

            AddButton(map, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(map, "Dash", "<Keyboard>/leftShift", "<Gamepad>/buttonEast");
            var echo = AddButton(map, "Echo", "<Keyboard>/j", "<Gamepad>/buttonWest");
            echo.AddBinding("<Mouse>/leftButton").WithGroup(KeyboardGroup);
            var shield = AddButton(map, "Shield", "<Keyboard>/k", "<Gamepad>/leftTrigger");
            shield.AddBinding("<Mouse>/rightButton").WithGroup(KeyboardGroup);
            var pulse = AddButton(map, "Pulse", "<Keyboard>/l", "<Gamepad>/buttonNorth");
            pulse.AddBinding("<Mouse>/middleButton").WithGroup(KeyboardGroup);
            AddButton(map, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            return asset;
        }

        static InputAction AddButton(InputActionMap map, string name, string key, string pad)
        {
            var a = map.AddAction(name, InputActionType.Button);
            a.AddBinding(key).WithGroup(KeyboardGroup);
            a.AddBinding(pad).WithGroup(GamepadGroup);
            return a;
        }

        public static void SaveOverrides()
        {
            PlayerPrefs.SetString(PrefsKey, SerializeOverrides(Asset));
            PlayerPrefs.Save();
        }

        public static void LoadOverrides()
        {
            var data = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(data)) ApplyOverrides(_asset, data);
        }

        /// <summary>
        /// Overrides serialised as "Map/Action|bindingIndex|path" lines. Actions are built in code, so their
        /// GUIDs change every run; names + indices are stable, which makes this survive restarts.
        /// </summary>
        public static string SerializeOverrides(InputActionAsset asset)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var map in asset.actionMaps)
                foreach (var a in map.actions)
                    for (int i = 0; i < a.bindings.Count; i++)
                    {
                        var b = a.bindings[i];
                        if (!string.IsNullOrEmpty(b.overridePath))
                            sb.Append(map.name).Append('/').Append(a.name).Append('|').Append(i).Append('|').Append(b.overridePath).Append('\n');
                    }
            return sb.ToString();
        }

        public static void ApplyOverrides(InputActionAsset asset, string data)
        {
            foreach (var line in data.Split('\n'))
            {
                var parts = line.Split('|');
                if (parts.Length != 3) continue;
                var a = asset.FindAction(parts[0], false);
                if (a == null || !int.TryParse(parts[1], out int idx) || idx < 0 || idx >= a.bindings.Count) continue;
                a.ApplyBindingOverride(idx, parts[2]);
            }
        }

        public static void ResetOverrides()
        {
            Asset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        /// <summary>Human readable binding for on-screen prompts (current device family).</summary>
        public static string Prompt(InputAction action)
        {
            if (action == null) return "?";
            var group = UsingGamepad ? GamepadGroup : KeyboardGroup;
            var s = action.GetBindingDisplayString(InputBinding.MaskByGroup(group));
            if (string.IsNullOrEmpty(s)) s = action.GetBindingDisplayString();
            return s;
        }

        /// <summary>Index of the first binding of the action in the given group (for rebinding).</summary>
        public static int BindingIndex(InputAction action, string group)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite &&
                    action.bindings[i].groups != null && action.bindings[i].groups.Contains(group))
                    return i;
            return -1;
        }

        public static void SetGameplayEnabled(bool on)
        {
            Ensure();
            // Pause stays on so the menu can be toggled.
            foreach (var a in Gameplay.actions)
            {
                if (a == Pause) continue;
                if (on) a.Enable(); else a.Disable();
            }
        }
    }

    /// <summary>One frame of player intent. Produced by a human (LumenInput) or a bot.</summary>
    public struct PlayerCommands
    {
        public float moveX;
        public bool jumpPressed, jumpHeld, dashPressed;
        public bool echoPressed, shieldHeld, pulsePressed;
    }

    public interface ICommandSource
    {
        PlayerCommands Poll();
    }

    public sealed class HumanCommandSource : ICommandSource
    {
        public PlayerCommands Poll()
        {
            LumenInput.Ensure();
            if (!LumenInput.Jump.enabled) return default;
            return new PlayerCommands
            {
                moveX = Mathf.Clamp(LumenInput.Move.ReadValue<float>(), -1f, 1f),
                jumpPressed = LumenInput.Jump.WasPressedThisFrame(),
                jumpHeld = LumenInput.Jump.IsPressed(),
                dashPressed = LumenInput.Dash.WasPressedThisFrame(),
                echoPressed = LumenInput.Echo.WasPressedThisFrame(),
                shieldHeld = LumenInput.Shield.IsPressed(),
                pulsePressed = LumenInput.Pulse.WasPressedThisFrame(),
            };
        }
    }
}
