using UnityEngine;

namespace LumenWave
{
    /// <summary>
    /// Example adapter: copy this file to plug in a device the Input System can't bind directly
    /// (mic pitch detection, MIDI library, OSC from a phone, a custom Arduino controller, ...).
    ///
    /// The rule: an adapter never touches gameplay scripts' internals. It only calls
    ///   NoteInput.PressNote / ReleaseNote      for notes
    ///   PlayerMovement.SetExternalMove / ClearExternalMove / ExternalJump   for movement
    ///
    /// If your device IS supported by the Input System (gamepad, joystick, generic HID, ...),
    /// you don't need an adapter: just add a binding in LumenControls.inputactions.
    ///
    /// To try this template as-is: add it to any GameObject, enable "Demo Keys Via Inspector",
    /// and tick the demo checkboxes in Play mode. They act like a fake device.
    /// </summary>
    public class InputSourceTemplate : MonoBehaviour
    {
        [Tooltip("Leave empty to use NoteInput.Instance.")]
        [SerializeField] NoteInput noteInput;
        [Tooltip("Leave empty to find the PlayerMovement in the scene.")]
        [SerializeField] PlayerMovement player;

        [Header("Demo: a fake device driven from the inspector")]
        [SerializeField] bool demoKeysViaInspector;
        [SerializeField] bool demoHoldLow;
        [SerializeField] bool demoHoldMid;
        [SerializeField] bool demoHoldHigh;
        [SerializeField, Range(-1f, 1f)] float demoMoveX;

        // Notes this source is currently holding. NoteInput counts presses per source,
        // so we must release exactly what we pressed, including when we get disabled.
        readonly bool[] held = new bool[NoteInput.NoteCount];
        bool jumpHeld;
        bool drivingMovement;

        void Start()
        {
            if (noteInput == null) noteInput = NoteInput.Instance;
            if (player == null) player = FindFirstObjectByType<PlayerMovement>();
        }

        void Update()
        {
            if (!demoKeysViaInspector)
                return;

            // In a real adapter this is where you'd poll your device, or do this work
            // in its callbacks (MIDI note-on/off, OSC message received, pitch detected ...).
            SetNote(Note.Low, demoHoldLow);
            SetNote(Note.Mid, demoHoldMid);
            SetNote(Note.High, demoHoldHigh);
            if (demoMoveX != 0f) OnDeviceMove(new Vector2(demoMoveX, 0f));
            else ReleaseMovement();
        }

        void OnDisable()
        {
            // Never leave a note or the jump button stuck down when this source goes away.
            for (int i = 0; i < held.Length; i++)
                SetNote((Note)i, false);
            OnDeviceJump(false);
            ReleaseMovement();
        }

        // ---------- Call these from your device code ----------

        /// <summary>Device says a note started (e.g. MIDI note-on, or mic pitch entered a note's range).</summary>
        public void OnDeviceNoteOn(Note note) => SetNote(note, true);

        /// <summary>Device says a note stopped.</summary>
        public void OnDeviceNoteOff(Note note) => SetNote(note, false);

        /// <summary>Device stick / tilt / slider. x = -1 (left) to 1 (right).</summary>
        public void OnDeviceMove(Vector2 move)
        {
            if (player == null) return;
            player.SetExternalMove(move);
            drivingMovement = true;
        }

        /// <summary>Device jump button down (true) or up (false).</summary>
        public void OnDeviceJump(bool pressed)
        {
            if (player == null || pressed == jumpHeld) return;
            jumpHeld = pressed;
            player.ExternalJump(pressed);
        }

        /// <summary>Hand movement back to the normal Move action (keyboard / gamepad).</summary>
        public void ReleaseMovement()
        {
            if (player == null || !drivingMovement) return;
            player.ClearExternalMove();
            drivingMovement = false;
        }

        // Example of mapping a continuous value to notes, e.g. detected pitch in Hz from a mic:
        //
        //   Note? NoteFromPitch(float hz)
        //   {
        //       if (hz > 100f && hz < 200f) return Note.Low;
        //       if (hz >= 200f && hz < 400f) return Note.Mid;
        //       if (hz >= 400f && hz < 800f) return Note.High;
        //       return null;
        //   }
        //
        // Each frame: release the notes that no longer match, press the one that does (SetNote handles both).

        void SetNote(Note note, bool down)
        {
            int i = (int)note;
            if (held[i] == down) return;

            if (noteInput == null) noteInput = NoteInput.Instance;
            if (noteInput == null) return;

            held[i] = down;
            if (down) noteInput.PressNote(note);
            else noteInput.ReleaseNote(note);
        }
    }
}
