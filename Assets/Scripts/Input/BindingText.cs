using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LumenWave
{
    /// <summary>
    /// Turns "{Jump}" style placeholders into the current binding names (after rebinding too),
    /// for whichever device the player used last (keyboard or gamepad).
    /// Placeholders: {Move} {Jump} {Down} {NoteLow} {NoteMid} {NoteHigh} {Pause} {Respawn} {Confirm}.
    /// </summary>
    public static class BindingText
    {
        public const string KeyboardGroup = "Keyboard";
        public const string GamepadGroup = "Gamepad";

        /// <summary>"Gamepad" if a gamepad was touched more recently than the keyboard, else "Keyboard".</summary>
        public static string ActiveGroup
        {
            get
            {
                Gamepad pad = Gamepad.current;
                Keyboard keyboard = Keyboard.current;
                if (pad == null) return KeyboardGroup;
                if (keyboard == null) return GamepadGroup;
                return pad.lastUpdateTime > keyboard.lastUpdateTime ? GamepadGroup : KeyboardGroup;
            }
        }

        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;

            var sb = new StringBuilder(text.Length + 16);
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf('{', i);
                if (open < 0) { sb.Append(text, i, text.Length - i); break; }
                int close = text.IndexOf('}', open + 1);
                if (close < 0) { sb.Append(text, i, text.Length - i); break; }
                sb.Append(text, i, open - i);
                string key = text.Substring(open + 1, close - open - 1);
                sb.Append(Resolve(key) ?? "{" + key + "}");
                i = close + 1;
            }
            return sb.ToString();
        }

        static string Resolve(string key)
        {
            switch (key)
            {
                case "Move": return MoveText(ActiveGroup);
                case "Down": return CompositePart("Player/Move", "down", ActiveGroup) ?? "Down";
                case "Jump": return ForAction("Player/Jump", ActiveGroup);
                case "NoteLow": return ForAction("Notes/NoteLow", ActiveGroup);
                case "NoteMid": return ForAction("Notes/NoteMid", ActiveGroup);
                case "NoteHigh": return ForAction("Notes/NoteHigh", ActiveGroup);
                case "Pause": return ForAction("Game/Pause", ActiveGroup);
                case "Respawn": return ForAction("Game/Respawn", ActiveGroup);
                case "Confirm": return ForAction("Game/Confirm", ActiveGroup);
                default: return null;
            }
        }

        /// <summary>First binding of the action in the group, e.g. "Space". Falls back to any binding.</summary>
        public static string ForAction(string actionPath, string group)
        {
            InputAction action = LumenInput.FindAction(actionPath);
            if (action == null) return actionPath;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                if (InGroup(b, group)) return action.GetBindingDisplayString(i);
            }
            string any = action.GetBindingDisplayString();
            return string.IsNullOrEmpty(any) ? actionPath : any;
        }

        static string MoveText(string group)
        {
            if (group == GamepadGroup) return "Left Stick";
            string left = CompositePart("Player/Move", "left", group);
            string right = CompositePart("Player/Move", "right", group);
            return left != null && right != null ? left + " / " + right : "A / D";
        }

        static string CompositePart(string actionPath, string part, string group)
        {
            InputAction action = LumenInput.FindAction(actionPath);
            if (action == null) return null;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding b = action.bindings[i];
                if (b.isPartOfComposite && b.name == part && InGroup(b, group))
                    return action.GetBindingDisplayString(i);
            }
            return group == GamepadGroup ? (part == "down" ? "Stick Down" : null) : null;
        }

        static bool InGroup(InputBinding binding, string group)
        {
            return !string.IsNullOrEmpty(binding.groups) && binding.groups.Contains(group);
        }
    }
}
