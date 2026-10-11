using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>Replaces {Move},{Jump},{Dash},{Echo},{Shield},{Pulse},{Pause} with live binding prompts.</summary>
    public static class PromptFormatter
    {
        public static string MovePrompt => LumenInput.UsingGamepad ? "Left Stick" : "A/D";

        public static string Format(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('{') < 0) return s;
            LumenInput.Ensure();
            s = s.Replace("{Move}", MovePrompt);
            s = s.Replace("{Jump}", LumenInput.Prompt(LumenInput.Jump));
            s = s.Replace("{Dash}", LumenInput.Prompt(LumenInput.Dash));
            s = s.Replace("{Echo}", LumenInput.Prompt(LumenInput.Echo));
            s = s.Replace("{Shield}", LumenInput.Prompt(LumenInput.Shield));
            s = s.Replace("{Pulse}", LumenInput.Prompt(LumenInput.Pulse));
            s = s.Replace("{Pause}", LumenInput.Prompt(LumenInput.Pause));
            return s;
        }
    }

    /// <summary>Interactive rebinding state (menus ignore Back while a rebind listens).</summary>
    public static class UIRebind
    {
        static InputActionRebindingExtensions.RebindingOperation _op;
        static int _busyUntilFrame;
        public static bool Busy => _op != null || Time.frameCount <= _busyUntilFrame;

        public static void Start(InputAction action, int bindingIndex, bool gamepad, Action done)
        {
            if (_op != null || action == null || bindingIndex < 0) return;
            bool wasEnabled = action.enabled;
            action.Disable();
            var op = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Pointer>/position")
                .OnMatchWaitForAnother(0.08f);
            if (gamepad) op.WithControlsHavingToMatchPath("<Gamepad>");
            else { op.WithControlsExcluding("<Gamepad>"); }
            op.OnComplete(o => Finish(action, wasEnabled, done, true))
              .OnCancel(o => Finish(action, wasEnabled, done, false));
            _op = op;
            op.Start();
        }

        static void Finish(InputAction action, bool wasEnabled, Action done, bool save)
        {
            _op?.Dispose();
            _op = null;
            _busyUntilFrame = Time.frameCount + 2;
            if (wasEnabled) action.Enable();
            if (save) LumenInput.SaveOverrides();
            done?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _op = null; _busyUntilFrame = 0; }
    }

    /// <summary>Settings (volume, shake, fullscreen) + Controls (rebinding) + Credits panels, shared by title and pause.</summary>
    public static class UIPanels
    {
        public static GameObject BuildSettings(Transform parent, MenuStack stack, out GameObject first)
        {
            var p = UIKit.Panel(parent, "SettingsPanel", new Vector2(760, 790));
            var title = UIKit.Label(p, UIKit.Spaced("SETTINGS", 2), 40, TextAnchor.MiddleCenter, UIStyle.Text, "Title");
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(700, 60), new Vector2(0.5f, 0.5f));
            var list = UIKit.Rect(p, "List");
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(1, 1);
            list.offsetMin = new Vector2(60, 40); list.offsetMax = new Vector2(-60, -110);
            UIKit.VList(list, 10);

            var master = UIKit.Slider(list, "Master volume", Settings.MasterVolume, v => Settings.MasterVolume = v);
            UIKit.Slider(list, "Music volume", Settings.MusicVolume, v => Settings.MusicVolume = v);
            UIKit.Slider(list, "Effects volume", Settings.SfxVolume, v => Settings.SfxVolume = v);
            UIKit.Toggle(list, "Screen shake", Settings.ScreenShake, v => Settings.ScreenShake = v);
            UIKit.Toggle(list, "Fullscreen", Settings.Fullscreen, v => Settings.Fullscreen = v);
            UIKit.Toggle(list, "Assist mode", Settings.AssistMode, v => Settings.AssistMode = v);
            UIKit.Toggle(list, "Show beat", Settings.ShowBeat, v => Settings.ShowBeat = v);
            GameObject controlsFirst = null;
            GameObject controls = null;
            UIKit.Button(list, "Controls", () => stack.Push(controls, controlsFirst), 56, 26);
            UIKit.Button(list, "Back", () => { PlayerPrefs.Save(); stack.Pop(); }, 56, 26);
            controls = BuildControls(parent, stack, out controlsFirst);
            controls.SetActive(false);
            first = master.gameObject;
            p.gameObject.SetActive(false);
            return p.gameObject;
        }

        public static GameObject BuildControls(Transform parent, MenuStack stack, out GameObject first)
        {
            LumenInput.Ensure();
            var p = UIKit.Panel(parent, "ControlsPanel", new Vector2(1000, 760));
            var title = UIKit.Label(p, UIKit.Spaced("CONTROLS", 2), 40, TextAnchor.MiddleCenter, UIStyle.Text, "Title");
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(900, 60), new Vector2(0.5f, 0.5f));
            var hint = UIKit.Label(p, "Select a binding and press a new key / button.  Esc cancels.  Move: A/D, arrows, left stick", 20, TextAnchor.MiddleCenter, UIStyle.TextDim);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(900, 30), new Vector2(0.5f, 0.5f));

            var header = UIKit.Rect(p, "Header");
            UIKit.Place(header, new Vector2(0.5f, 1f), new Vector2(0, -145), new Vector2(880, 30), new Vector2(0.5f, 0.5f));
            var h1 = UIKit.Label(header, "ACTION", 20, TextAnchor.MiddleLeft, UIStyle.Accent); h1.rectTransform.anchorMin = new Vector2(0, 0); h1.rectTransform.anchorMax = new Vector2(0.33f, 1); h1.rectTransform.offsetMin = new Vector2(16, 0); h1.rectTransform.offsetMax = Vector2.zero;
            var h2 = UIKit.Label(header, "KEYBOARD / MOUSE", 20, TextAnchor.MiddleCenter, UIStyle.Accent); h2.rectTransform.anchorMin = new Vector2(0.33f, 0); h2.rectTransform.anchorMax = new Vector2(0.66f, 1); h2.rectTransform.offsetMin = h2.rectTransform.offsetMax = Vector2.zero;
            var h3 = UIKit.Label(header, "GAMEPAD", 20, TextAnchor.MiddleCenter, UIStyle.Accent); h3.rectTransform.anchorMin = new Vector2(0.66f, 0); h3.rectTransform.anchorMax = new Vector2(1f, 1); h3.rectTransform.offsetMin = h3.rectTransform.offsetMax = Vector2.zero;

            var list = UIKit.Rect(p, "List");
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(1, 1);
            list.offsetMin = new Vector2(60, 40); list.offsetMax = new Vector2(-60, -170);
            UIKit.VList(list, 8);

            var refreshers = new List<Action>();
            first = null;
            foreach (var action in LumenInput.Rebindable)
            {
                var row = UIKit.Rect(list, action.name);
                var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 54; le.minHeight = 54;
                var name = UIKit.Label(row, UIKit.Spaced(action.name.ToUpperInvariant()), 22, TextAnchor.MiddleLeft, UIStyle.Text);
                name.rectTransform.anchorMin = new Vector2(0, 0); name.rectTransform.anchorMax = new Vector2(0.33f, 1);
                name.rectTransform.offsetMin = new Vector2(16, 0); name.rectTransform.offsetMax = Vector2.zero;
                var kb = BindButton(row, action, false, 0.34f, 0.65f, refreshers);
                BindButton(row, action, true, 0.67f, 0.98f, refreshers);
                if (first == null) first = kb;
            }
            var buttons = UIKit.Rect(list, "Buttons");
            var ble = buttons.gameObject.AddComponent<LayoutElement>(); ble.preferredHeight = 56; ble.minHeight = 56; ble.flexibleHeight = 0f;
            var hl = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 16; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = false;
            UIKit.Button(buttons, "Reset defaults", () => { LumenInput.ResetOverrides(); foreach (var r in refreshers) r(); }, 56, 24);
            UIKit.Button(buttons, "Back", () => stack.Pop(), 56, 24);
            p.gameObject.SetActive(false);
            return p.gameObject;
        }

        static GameObject BindButton(RectTransform row, InputAction action, bool gamepad, float x0, float x1, List<Action> refreshers)
        {
            int idx = LumenInput.BindingIndex(action, gamepad ? LumenInput.GamepadGroup : LumenInput.KeyboardGroup);
            Text label = null;
            Button b = null;
            Action refresh = () =>
            {
                if (label == null) return;
                label.text = idx < 0 ? "-" : action.GetBindingDisplayString(idx);
            };
            b = UIKit.Button(row, "bind", () =>
            {
                if (idx < 0) return;
                label.text = "press...";
                UIRebind.Start(action, idx, gamepad, refresh);
            }, 50, 22, gamepad ? "Pad" : "Key");
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(x0, 0.08f); rt.anchorMax = new Vector2(x1, 0.92f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var le = b.GetComponent<LayoutElement>(); if (le != null) le.ignoreLayout = true;
            label = b.GetComponentInChildren<Text>();
            refresh();
            refreshers.Add(refresh);
            return b.gameObject;
        }

        public const string CreditsText =
            "LUMENWAVE\n\n" +
            "A light & sound platformer made for a game jam.\n\n" +
            "Design, code, shaders, procedural audio & levels\nthe LumenWave team\n\n" +
            "Built with Unity 6 · Universal Render Pipeline · Input System\n" +
            "All visuals and sounds are generated in code.\n\n" +
            "Inspired by Limbo, Inside, Ori, Journey, Celeste, Sound Shapes\n" +
            "and by real physics: inverse-square light, Beer–Lambert fog,\nNewton's prism, reflection, standing waves and just intonation.\n\n" +
            "Full asset list and licences: see CREDITS.md";

        public static GameObject BuildCredits(Transform parent, MenuStack stack, out GameObject first)
        {
            var p = UIKit.Panel(parent, "CreditsPanel", new Vector2(980, 760));
            var t = UIKit.Label(p, CreditsText, 26, TextAnchor.UpperCenter, UIStyle.Text, "Text");
            t.lineSpacing = 1.1f;
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.offsetMin = new Vector2(50, 110); t.rectTransform.offsetMax = new Vector2(-50, -50);
            var back = UIKit.Button(p, "Back", () => stack.Pop(), 60);
            UIKit.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(360, 60), new Vector2(0.5f, 0f));
            first = back.gameObject;
            p.gameObject.SetActive(false);
            return p.gameObject;
        }
    }
}
