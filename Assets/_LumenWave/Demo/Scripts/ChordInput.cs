using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-200)]
    public sealed class ChordInput : MonoBehaviour
    {
        public const int LeftFirst = 1, LeftSecond = 2, RightFirst = 4, RightSecond = 8;
        public const int C = 1, E = 2, G = 4;
        public const string MapName = "ChordDemo";
        static readonly string[] noteNames = { "NoteC", "NoteE", "NoteG" };
        static readonly string[] movementNames = { "LeftFirst", "LeftSecond", "RightFirst", "RightSecond" };
        [SerializeField] InputActionAsset inputActions;
        readonly InputAction[] notes = new InputAction[3];
        readonly InputAction[] movement = new InputAction[4];
        InputAction jump;
        int previousMovement;
        bool previousJump;
        InputActionMap map;
        InputAction restart;
        public InputActionAsset RuntimeActions { get; private set; }
        public event Action BindingsChanged;
        public int Held { get; private set; }
        public int Pressed { get; private set; }
        public float Horizontal { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool ChordPressed { get; private set; }
        public bool MajorPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        
        public int MovementHeld { get; private set; }
        public int MovementPressed { get; private set; }
        public static int Direction(int mask)
        {
            bool left = (mask & (LeftFirst | LeftSecond)) == (LeftFirst | LeftSecond);
            bool right = (mask & (RightFirst | RightSecond)) == (RightFirst | RightSecond);
            return left == right ? 0 : left ? -1 : 1;
        }
        public void Sample(int mask, int movementMask = 0, bool jumpHeld = false, bool restart = false)
        {
            int old = Held;
            Pressed = mask & ~old;
            Held = mask;
            Horizontal = Direction(movementMask);
            bool jumpChord = Horizontal != 0 && jumpHeld;
            JumpPressed = jumpChord && !(Direction(previousMovement) != 0 && previousJump);
            MovementPressed = movementMask & ~previousMovement;
            MovementHeld = movementMask;
            previousMovement = movementMask;
            previousJump = jumpHeld;
            ChordPressed = (mask & (C | E)) == (C | E) && (old & (C | E)) != (C | E);
            MajorPressed = mask == (C | E | G) && old != (C | E | G);
            RestartPressed = restart;
        }
        public void Configure(InputActionAsset asset) { inputActions = asset; }
        void Awake()
        {
            if (inputActions == null)
            { Debug.LogError("Assign an Input Actions asset to ChordInput.",this); enabled=false; return; }
            RuntimeActions = Instantiate(inputActions);
            map = RuntimeActions.FindActionMap(MapName);
            if (map == null)
            { Debug.LogError("Input Actions asset needs the ChordDemo action map.",this); enabled=false; return; }
            for (int i=0; i<notes.Length; i++) notes[i]=map.FindAction(noteNames[i],true);
            for (int i=0; i<movement.Length; i++) movement[i]=map.FindAction(movementNames[i],true);
            jump=map.FindAction("Jump",true);
            restart=map.FindAction("Restart",true);
        }
        void OnEnable()
        {
            if (map == null) return;
            InputSystem.onActionChange += ActionChanged;
            map.Enable();
        }
        void ActionChanged(object changed,InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged) return;
            if (changed == map || changed == RuntimeActions || (changed is InputAction action && action.actionMap == map))
                BindingsChanged?.Invoke();
        }
        public string ControlsHint
        {
            get
            {
                var actions=RuntimeActions!=null ? RuntimeActions : inputActions;
                var bindings=actions!=null ? actions.FindActionMap(MapName) : null;
                if (bindings==null) return "Assign the ChordDemo Input Actions map.";
                string left=bindings.FindAction("LeftFirst").GetBindingDisplayString()+" + "+bindings.FindAction("LeftSecond").GetBindingDisplayString();
                string right=bindings.FindAction("RightFirst").GetBindingDisplayString()+" + "+bindings.FindAction("RightSecond").GetBindingDisplayString();
                return left+" LEFT / "+right+" RIGHT / + "+bindings.FindAction("Jump").GetBindingDisplayString()+" JUMP     "+
                    bindings.FindAction("NoteC").GetBindingDisplayString()+" C / "+bindings.FindAction("NoteE").GetBindingDisplayString()+" E / "+bindings.FindAction("NoteG").GetBindingDisplayString()+" G / "+bindings.FindAction("Restart").GetBindingDisplayString()+" RETRY";
            }
        }
        void Update()
        {
            int mask = 0;
            int movementMask = 0;
            bool focused=Application.isFocused || Application.isBatchMode;
            if (focused)
                for (int i=0; i<notes.Length; i++) if (notes[i].IsPressed()) mask|=1<<i;
            if (focused)
                for (int i=0; i<movement.Length; i++) if (movement[i].IsPressed()) movementMask|=1<<i;
            Sample(mask,movementMask,focused && jump.IsPressed(),focused && restart.WasPressedThisFrame());
        }
        void OnDisable()
        {
            InputSystem.onActionChange -= ActionChanged;
            map?.Disable();
            Sample(0);
        }
        void OnDestroy() { if (RuntimeActions!=null) Destroy(RuntimeActions); }
    }
}
