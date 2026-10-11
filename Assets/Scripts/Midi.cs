using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace LumenWave.Demo
{
    [DefaultExecutionOrder(-250)] // CHANGED: runs before ChordInput (-200), so no one-frame delay
    [RequireComponent(typeof(ChordInput))]
    public sealed class Midi : MonoBehaviour
    {
        [Header("MIDI Device")]
        [SerializeField] string deviceNameContains = "iRig";

        [Header("MIDI Note Mapping (movement)")]
        [SerializeField] int leftFirstNote = 60;  // C4
        [SerializeField] int leftSecondNote = 62; // D4
        [SerializeField] int rightFirstNote = 64; // E4
        [SerializeField] int rightSecondNote = 65;// F4
        [SerializeField] int jumpNote = 67;       // G4

        // CHANGED: new section. Lower octave so chords don't clash with movement.
        [Header("MIDI Chord Notes (these bend the lights)")]
        [SerializeField] int chordNoteC = 48; // C3
        [SerializeField] int chordNoteE = 52; // E3
        [SerializeField] int chordNoteG = 55; // G3

        [Header("Debug")]
        [SerializeField] bool logNotes = true; // CHANGED: prints each note number in the Console

        InputDevice midiDevice;
        ChordInput chordInput;

        readonly ConcurrentQueue<(int note, bool pressed)> messages =
            new ConcurrentQueue<(int, bool)>();

        readonly HashSet<int> heldNotes = new HashSet<int>();

        void Awake()
        {
            chordInput = GetComponent<ChordInput>();
        }

        void Start()
        {
            try
            {
                // CHANGED: ToList() so disposing devices can't disturb the loop
                var devices = InputDevice.GetAll().ToList();

                foreach (var device in devices)
                {
                    Debug.Log("MIDI device: " + device.Name);

                    if (midiDevice == null &&
                        device.Name.IndexOf(
                            deviceNameContains,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        midiDevice = device;
                    }
                    else
                    {
                        device.Dispose();
                    }
                }

                if (midiDevice == null)
                {
                    Debug.LogError(
                        "iRig MIDI device not found. Check the Console.");
                    enabled = false;
                    return;
                }

                midiDevice.EventReceived += OnMidiEvent;
                midiDevice.StartEventsListening();

                Debug.Log("MIDI connected: " + midiDevice.Name);
            }
            catch (Exception e)
            {
                Debug.LogError("MIDI error: " + e);
            }
        }

        void OnMidiEvent(
            object sender, MidiEventReceivedEventArgs e)
        {
            if (e.Event is NoteOnEvent noteOn)
            {
                int note = (int)noteOn.NoteNumber;
                bool pressed = (int)noteOn.Velocity > 0;

                messages.Enqueue((note, pressed));
            }
            else if (e.Event is NoteOffEvent noteOff)
            {
                messages.Enqueue(((int)noteOff.NoteNumber, false));
            }
        }

        void Update()
        {
            while (messages.TryDequeue(out var message))
            {
                if (logNotes) // CHANGED
                    Debug.Log("MIDI note " + message.note + (message.pressed ? " down" : " up"));

                if (message.pressed)
                    heldNotes.Add(message.note);
                else
                    heldNotes.Remove(message.note);
            }

            int movementMask = 0;

            if (heldNotes.Contains(leftFirstNote))
                movementMask |= ChordInput.LeftFirst;

            if (heldNotes.Contains(leftSecondNote))
                movementMask |= ChordInput.LeftSecond;

            if (heldNotes.Contains(rightFirstNote))
                movementMask |= ChordInput.RightFirst;

            if (heldNotes.Contains(rightSecondNote))
                movementMask |= ChordInput.RightSecond;

            bool jumpHeld = heldNotes.Contains(jumpNote);

            // CHANGED: the chord notes that bend the lights
            int noteMask = 0;
            if (heldNotes.Contains(chordNoteC)) noteMask |= ChordInput.C;
            if (heldNotes.Contains(chordNoteE)) noteMask |= ChordInput.E;
            if (heldNotes.Contains(chordNoteG)) noteMask |= ChordInput.G;

            chordInput.SetMidiInput(movementMask, jumpHeld);
            chordInput.SetMidiNotes(noteMask); // CHANGED
        }

        void OnDisable()
        {
            chordInput?.SetMidiInput(0, false);
            chordInput?.SetMidiNotes(0); // CHANGED
            heldNotes.Clear();

            while (messages.TryDequeue(out _)) { }

            if (midiDevice != null)
            {
                midiDevice.EventReceived -= OnMidiEvent;
                midiDevice.Dispose();
                midiDevice = null;
            }
        }

        void OnDestroy()
        {
            chordInput?.SetMidiInput(0, false);
            chordInput?.SetMidiNotes(0); // CHANGED
        }
    }
}