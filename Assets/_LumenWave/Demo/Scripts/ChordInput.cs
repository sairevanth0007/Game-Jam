using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-200)]
    public sealed class ChordInput : MonoBehaviour
    {
        public const int Q = 1, W = 2, O = 4, P = 8, Space = 16;
        public int Held { get; private set; }
        public int Pressed { get; private set; }
        public float Horizontal { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool PairPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public static int Direction(int mask)
        {
            bool left = (mask & (Q | W)) == (Q | W);
            bool right = (mask & (O | P)) == (O | P);
            return left == right ? 0 : left ? -1 : 1;
        }
        public void Sample(int mask, bool restart = false)
        {
            int old = Held;
            Pressed = mask & ~old;
            Held = mask;
            Horizontal = Direction(mask);
            PairPressed = Horizontal != 0 && Direction(old) != Horizontal;
            bool jumpChord = Horizontal != 0 && (mask & Space) != 0;
            bool oldJumpChord = Direction(old) != 0 && (old & Space) != 0;
            JumpPressed = jumpChord && !oldJumpChord;
            RestartPressed = restart;
        }
        void Update()
        {
            var k = Keyboard.current;
            int mask = 0;
            if (k != null && (Application.isFocused || Application.isBatchMode))
            {
                if (k.qKey.isPressed) mask |= Q;
                if (k.wKey.isPressed) mask |= W;
                if (k.oKey.isPressed) mask |= O;
                if (k.pKey.isPressed) mask |= P;
                if (k.spaceKey.isPressed) mask |= Space;
            }
            Sample(mask, k != null && k.rKey.wasPressedThisFrame);
        }
        void OnDisable() { Sample(0); }
    }
}
