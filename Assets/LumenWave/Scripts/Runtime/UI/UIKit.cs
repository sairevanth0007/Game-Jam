using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>Colours and sizes for LumenWave's minimal UI style.</summary>
    public static class UIStyle
    {
        public static readonly Color Panel = new Color(0.02f, 0.03f, 0.06f, 0.86f);
        public static readonly Color PanelLine = new Color(0.45f, 0.95f, 1f, 0.55f);
        public static readonly Color Accent = new Color(0.45f, 0.95f, 1f, 1f);
        public static readonly Color AccentDim = new Color(0.45f, 0.95f, 1f, 0.25f);
        public static readonly Color Text = new Color(0.88f, 0.93f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.6f, 0.68f, 0.8f, 1f);
        public static readonly Color ButtonNormal = new Color(1f, 1f, 1f, 0.04f);
        public static readonly Color ButtonSelected = new Color(0.45f, 0.95f, 1f, 0.16f);
        public static readonly Color Health = new Color(0.95f, 0.97f, 1f, 1f);
        public static readonly Color Resonance = new Color(0.35f, 0.9f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.25f, 0.2f, 1f);
        public const int ButtonFont = 30;
    }

    /// <summary>Helpers to build uGUI hierarchies in code.</summary>
    public static class UIKit
    {
        static Font _font;
        static Sprite _soft, _radial, _glow;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>Rounded soft-edged rectangle, 9-sliced.</summary>
        public static Sprite Soft
        {
            get
            {
                if (_soft != null) return _soft;
                const int n = 32; const float r = 10f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "lw_soft" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                        float a = Mathf.Clamp01(r - d);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                _soft = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                return _soft;
            }
        }

        /// <summary>Radial vignette (transparent centre, opaque edges).</summary>
        public static Sprite Radial
        {
            get
            {
                if (_radial != null) return _radial;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "lw_radial" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1.15f, d));
                        tex.SetPixel(x, y, new Color(1, 1, 1, a));
                    }
                tex.Apply();
                _radial = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return _radial;
            }
        }

        /// <summary>Soft round glow blob (opaque centre fading out).</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "lw_glow" };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                    }
                tex.Apply();
                _glow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
                return _glow;
            }
        }

        /// <summary>"LUMEN" → "L U M E N" (uGUI Text has no letter spacing).</summary>
        public static string Spaced(string s, int spaces = 1)
        {
            var pad = new string(' ', spaces);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(s[i]);
                if (i < s.Length - 1) sb.Append(pad);
            }
            return sb.ToString();
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60;
            return $"{m:00}:{s:00.00}";
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sliced && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Dark rounded panel with a thin glowing top line.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size)
        {
            var img = Image(parent, name, UIStyle.Panel, Soft);
            img.raycastTarget = true;
            var rt = Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var line = Image(rt, "Line", UIStyle.PanelLine);
            line.rectTransform.anchorMin = new Vector2(0.08f, 1f); line.rectTransform.anchorMax = new Vector2(0.92f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.sizeDelta = new Vector2(0, 2);
            line.rectTransform.anchoredPosition = new Vector2(0, -1);
            var glow = Image(rt, "LineGlow", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.18f), Glow);
            glow.rectTransform.anchorMin = new Vector2(0.05f, 1f); glow.rectTransform.anchorMax = new Vector2(0.95f, 1f);
            glow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            glow.rectTransform.sizeDelta = new Vector2(0, 26);
            glow.rectTransform.anchoredPosition = new Vector2(0, -1);
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null, string name = "Label")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? UIStyle.Text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Adds a soft cyan glow (two outlines) to a text.</summary>
        public static void GlowText(Text t, Color glow, float dist = 2f)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(glow.r, glow.g, glow.b, 0.35f);
            o.effectDistance = new Vector2(dist, dist);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(glow.r, glow.g, glow.b, 0.2f);
            s.effectDistance = new Vector2(-dist, -dist);
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        static void Height(GameObject go, float h, float w = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = h; le.minHeight = h;
            if (w > 0) le.preferredWidth = w;
        }

        public static Button Button(Transform parent, string label, Action onClick, float height = 62, int fontSize = UIStyle.ButtonFont, string name = null)
        {
            var bg = Image(parent, name ?? label, Color.white, Soft);
            bg.raycastTarget = true;
            Height(bg.gameObject, height);
            bg.rectTransform.sizeDelta = new Vector2(420, height);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var cb = b.colors;
            cb.normalColor = UIStyle.ButtonNormal;
            cb.highlightedColor = UIStyle.ButtonSelected;
            cb.selectedColor = UIStyle.ButtonSelected;
            cb.pressedColor = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.35f);
            cb.disabledColor = new Color(1, 1, 1, 0.02f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            var t = Label(bg.rectTransform, UIKit.Spaced(label.ToUpperInvariant()), fontSize, TextAnchor.MiddleCenter, UIStyle.TextDim);
            Stretch(t.rectTransform);
            var fx = bg.gameObject.AddComponent<UISelectFx>();
            fx.label = t;
            if (onClick != null) b.onClick.AddListener(() => { if (!UIRebind.Busy) onClick(); });
            return b;
        }

        public static Slider Slider(Transform parent, string label, float value, Action<float> onChange)
        {
            var row = Rect(parent, label);
            Height(row.gameObject, 56);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = Soft; bg.type = UnityEngine.UI.Image.Type.Sliced; bg.color = Color.white;
            var t = Label(row, UIKit.Spaced(label.ToUpperInvariant()), 24, TextAnchor.MiddleLeft, UIStyle.TextDim);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(0.45f, 1);
            t.rectTransform.offsetMin = new Vector2(24, 0); t.rectTransform.offsetMax = Vector2.zero;

            var area = Rect(row, "Track");
            area.anchorMin = new Vector2(0.48f, 0.5f); area.anchorMax = new Vector2(0.94f, 0.5f);
            area.sizeDelta = new Vector2(0, 6); area.anchoredPosition = Vector2.zero;
            var track = area.gameObject.AddComponent<Image>(); track.color = new Color(1, 1, 1, 0.12f);
            var fillArea = Rect(area, "Fill Area"); Stretch(fillArea);
            var fill = Image(fillArea, "Fill", UIStyle.Accent);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect(area, "Handle Area"); Stretch(handleArea);
            var handle = Image(handleArea, "Handle", Color.white, Glow, false);
            handle.rectTransform.sizeDelta = new Vector2(28, 28);

            var s = row.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = bg;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1;
            s.value = value;
            var cb = s.colors;
            cb.normalColor = UIStyle.ButtonNormal; cb.highlightedColor = UIStyle.ButtonSelected; cb.selectedColor = UIStyle.ButtonSelected;
            s.colors = cb;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            var fx = row.gameObject.AddComponent<UISelectFx>();
            fx.label = t;
            return s;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var row = Rect(parent, label);
            Height(row.gameObject, 56);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = Soft; bg.type = UnityEngine.UI.Image.Type.Sliced; bg.color = Color.white;
            var t = Label(row, UIKit.Spaced(label.ToUpperInvariant()), 24, TextAnchor.MiddleLeft, UIStyle.TextDim);
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(0.7f, 1);
            t.rectTransform.offsetMin = new Vector2(24, 0); t.rectTransform.offsetMax = Vector2.zero;
            var box = Image(row, "Box", new Color(1, 1, 1, 0.15f), Soft);
            Place(box.rectTransform, new Vector2(0.9f, 0.5f), Vector2.zero, new Vector2(34, 34), new Vector2(0.5f, 0.5f));
            var check = Image(box.rectTransform, "Check", UIStyle.Accent, Soft);
            Stretch(check.rectTransform, 7);
            var tg = row.gameObject.AddComponent<Toggle>();
            tg.targetGraphic = bg;
            tg.graphic = check;
            tg.isOn = value;
            var cb = tg.colors;
            cb.normalColor = UIStyle.ButtonNormal; cb.highlightedColor = UIStyle.ButtonSelected; cb.selectedColor = UIStyle.ButtonSelected;
            tg.colors = cb;
            tg.onValueChanged.AddListener(v => onChange?.Invoke(v));
            var fx = row.gameObject.AddComponent<UISelectFx>();
            fx.label = t;
            return tg;
        }

        public static void Select(GameObject go)
        {
            if (go == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(go);
        }

        /// <summary>True when the player pressed "back" this frame (Esc / gamepad B / Pause).</summary>
        public static bool BackPressed()
        {
            if (UIRebind.Busy) return false;
            bool k = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            bool g = Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame);
            return k || g;
        }
    }

    /// <summary>Selection feedback: label brightens + a glowing bar appears on the left; hover selects.</summary>
    public class UISelectFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Text label;
        Image _bar;
        bool _selected;
        float _t;

        void Awake()
        {
            _bar = UIKit.Image(transform, "SelectBar", UIStyle.Accent);
            _bar.rectTransform.anchorMin = new Vector2(0, 0.2f); _bar.rectTransform.anchorMax = new Vector2(0, 0.8f);
            _bar.rectTransform.pivot = new Vector2(0, 0.5f);
            _bar.rectTransform.sizeDelta = new Vector2(3, 0);
            _bar.rectTransform.anchoredPosition = new Vector2(6, 0);
            _bar.color = new Color(1, 1, 1, 0);
        }

        public void OnSelect(BaseEventData e) { _selected = true; }
        public void OnDeselect(BaseEventData e) { _selected = false; }
        public void OnPointerEnter(PointerEventData e)
        {
            var s = GetComponent<Selectable>();
            if (s != null && s.interactable) UIKit.Select(gameObject);
        }

        void OnDisable() { _selected = false; _t = 0; Apply(); }

        void Update()
        {
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            _t = Mathf.MoveTowards(_t, _selected ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            Apply();
        }

        void Apply()
        {
            if (label != null) label.color = Color.Lerp(UIStyle.TextDim, Color.white, _t);
            if (_bar != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                _bar.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, _t * pulse);
            }
        }
    }

    /// <summary>A stack of panels: one visible at a time, Back pops.</summary>
    public class MenuStack : MonoBehaviour
    {
        readonly System.Collections.Generic.List<(GameObject panel, GameObject first)> _stack = new System.Collections.Generic.List<(GameObject, GameObject)>();
        public Action onRootBack;
        public bool handleBack = true;
        public int Depth => _stack.Count;

        public void Push(GameObject panel, GameObject first)
        {
            if (_stack.Count > 0) _stack[_stack.Count - 1].panel.SetActive(false);
            _stack.Add((panel, first));
            panel.SetActive(true);
            UIKit.Select(first);
        }

        public void Pop()
        {
            if (_stack.Count <= 1) { onRootBack?.Invoke(); return; }
            var top = _stack[_stack.Count - 1];
            top.panel.SetActive(false);
            _stack.RemoveAt(_stack.Count - 1);
            var cur = _stack[_stack.Count - 1];
            cur.panel.SetActive(true);
            UIKit.Select(cur.first);
        }

        public void Clear()
        {
            foreach (var e in _stack) if (e.panel != null) e.panel.SetActive(false);
            _stack.Clear();
        }

        void Update()
        {
            if (!handleBack || _stack.Count == 0) return;
            if (UIKit.BackPressed()) Pop();
            // Keep something selected for keyboard/gamepad users.
            if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
            {
                var first = _stack[_stack.Count - 1].first;
                if (first != null && first.activeInHierarchy && (Keyboard.current?.anyKey.wasPressedThisFrame == true || (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.2f)))
                    UIKit.Select(first);
            }
        }
    }
}
