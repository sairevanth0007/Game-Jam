using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Single entry point for notes. Listens to the three note actions, and exposes
    /// PressNote / ReleaseNote so other sources (mic, MIDI, OSC, ...) can drive notes too.
    /// Presses are counted per note, so a note stays held until every source has let go.
    /// Gameplay scripts subscribe to NotePressed / NoteReleased or poll IsHeld.
    /// </summary>
    public class NoteInput : MonoBehaviour
    {
        public static NoteInput Instance { get; private set; }

        [Header("Input (drag from LumenControls > Notes)")]
        [SerializeField] InputActionReference noteLowAction;
        [SerializeField] InputActionReference noteMidAction;
        [SerializeField] InputActionReference noteHighAction;

        [SerializeField] bool logNotes;

        /// <summary>Fires when a note goes from silent to held.</summary>
        public event Action<Note> NotePressed;

        /// <summary>Fires when the last source holding a note releases it.</summary>
        public event Action<Note> NoteReleased;

        public const int NoteCount = 3;

        readonly int[] holdCounts = new int[NoteCount];
        readonly bool[] heldByActions = new bool[NoteCount];

        public bool IsHeld(Note note) => holdCounts[(int)note] > 0;

        public bool AnyHeld => holdCounts[0] > 0 || holdCounts[1] > 0 || holdCounts[2] > 0;

        void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning($"[NoteInput] More than one NoteInput in the scene ({Instance.name}, {name}). Using the newest.", this);
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            Bind(noteLowAction, OnLowPerformed, OnLowCanceled);
            Bind(noteMidAction, OnMidPerformed, OnMidCanceled);
            Bind(noteHighAction, OnHighPerformed, OnHighCanceled);
        }

        void OnDisable()
        {
            Unbind(noteLowAction, OnLowPerformed, OnLowCanceled);
            Unbind(noteMidAction, OnMidPerformed, OnMidCanceled);
            Unbind(noteHighAction, OnHighPerformed, OnHighCanceled);

            // Disabling an action normally fires canceled, but make sure nothing stays stuck.
            for (int i = 0; i < NoteCount; i++)
                SetHeldByActions((Note)i, false);
        }

        /// <summary>Start holding a note. Every call must be matched by one ReleaseNote call.</summary>
        public void PressNote(Note note)
        {
            int i = (int)note;
            holdCounts[i]++;
            if (holdCounts[i] == 1)
            {
                if (logNotes) Debug.Log($"[NoteInput] {note} pressed", this);
                NotePressed?.Invoke(note);
            }
        }

        /// <summary>Stop holding a note. The note only goes silent once all sources have released it.</summary>
        public void ReleaseNote(Note note)
        {
            int i = (int)note;
            if (holdCounts[i] == 0)
            {
                Debug.LogWarning($"[NoteInput] ReleaseNote({note}) called without a matching PressNote.", this);
                return;
            }

            holdCounts[i]--;
            if (holdCounts[i] == 0)
            {
                if (logNotes) Debug.Log($"[NoteInput] {note} released", this);
                NoteReleased?.Invoke(note);
            }
        }

        // The Input System counts as one source, however many bindings hold the action.
        void SetHeldByActions(Note note, bool held)
        {
            int i = (int)note;
            if (heldByActions[i] == held)
                return;

            heldByActions[i] = held;
            if (held) PressNote(note);
            else ReleaseNote(note);
        }

        void OnLowPerformed(InputAction.CallbackContext _) => SetHeldByActions(Note.Low, true);
        void OnLowCanceled(InputAction.CallbackContext _) => SetHeldByActions(Note.Low, false);
        void OnMidPerformed(InputAction.CallbackContext _) => SetHeldByActions(Note.Mid, true);
        void OnMidCanceled(InputAction.CallbackContext _) => SetHeldByActions(Note.Mid, false);
        void OnHighPerformed(InputAction.CallbackContext _) => SetHeldByActions(Note.High, true);
        void OnHighCanceled(InputAction.CallbackContext _) => SetHeldByActions(Note.High, false);

        static void Bind(InputActionReference reference, Action<InputAction.CallbackContext> performed, Action<InputAction.CallbackContext> canceled)
        {
            if (reference == null || reference.action == null)
                return;

            reference.action.performed += performed;
            reference.action.canceled += canceled;
            reference.action.Enable();
        }

        static void Unbind(InputActionReference reference, Action<InputAction.CallbackContext> performed, Action<InputAction.CallbackContext> canceled)
        {
            if (reference == null || reference.action == null)
                return;

            // Disable first so a held note still reaches our canceled handler.
            reference.action.Disable();
            reference.action.performed -= performed;
            reference.action.canceled -= canceled;
        }
    }
}
