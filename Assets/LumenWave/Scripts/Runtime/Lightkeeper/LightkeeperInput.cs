using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// The dedicated "Lightkeeper" action map (built in code like <see cref="LumenInput"/>). Enabled only during a
    /// Lightkeeper round, while the player's gameplay map is disabled (the bot drives Umbra), so the shared keys
    /// (A/D, W, Q/E, R, 1–4) never conflict. Mouse select/drag/wheel is read directly in the controller.
    /// </summary>
    public static class LightkeeperInput
    {
        static InputActionMap _map;
        public static InputAction[] Select { get; private set; }
        public static InputAction Move { get; private set; }
        public static InputAction Rotate { get; private set; }
        public static InputAction Toggle { get; private set; }
        public static InputAction Colour { get; private set; }

        public static InputActionMap Map { get { Ensure(); return _map; } }

        static void Ensure()
        {
            if (_map != null) return;
            _map = new InputActionMap("Lightkeeper");
            Select = new InputAction[4];
            for (int i = 0; i < 4; i++)
            {
                Select[i] = _map.AddAction("Select" + (i + 1), InputActionType.Button);
                Select[i].AddBinding("<Keyboard>/" + (i + 1));
                Select[i].AddBinding("<Keyboard>/numpad" + (i + 1));
            }
            Move = _map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            Move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            Rotate = _map.AddAction("Rotate", InputActionType.Value);
            Rotate.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");
            Toggle = _map.AddAction("Toggle", InputActionType.Button);
            Toggle.AddBinding("<Keyboard>/w");
            Toggle.AddBinding("<Keyboard>/upArrow");
            Colour = _map.AddAction("Colour", InputActionType.Button);
            Colour.AddBinding("<Keyboard>/r");
        }

        /// <summary>Drops the map so it is rebuilt against the current input devices (tests reset the Input System).</summary>
        public static void Reset() { if (_map != null) { _map.Disable(); _map.Dispose(); } _map = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();
    }
}
