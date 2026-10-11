using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LumenWave.UI
{
    /// <summary>Colours and sizes for LumenWave's UI style (deep night blue, cyan light accents, warm gold highlights).</summary>
    public static class UIStyle
    {
        public static readonly Color Panel = new Color(0.015f, 0.022f, 0.045f, 0.9f);
        public static readonly Color PanelLine = new Color(0.45f, 0.95f, 1f, 0.55f);
        public static readonly Color Accent = new Color(0.45f, 0.95f, 1f, 1f);
        public static readonly Color AccentDim = new Color(0.45f, 0.95f, 1f, 0.25f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.45f, 1f);
        public static readonly Color Text = new Color(0.9f, 0.95f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.62f, 0.7f, 0.82f, 1f);
        public static readonly Color ButtonNormal = new Color(0.02f, 0.035f, 0.07f, 0.6f);
        public static readonly Color ButtonSelected = new Color(0.45f, 0.95f, 1f, 0.17f);
        public static readonly Color Health = new Color(0.95f, 0.97f, 1f, 1f);
        public static readonly Color Resonance = new Color(0.35f, 0.9f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.25f, 0.2f, 1f);
        public static readonly Color KeyCap = new Color(0.03f, 0.05f, 0.09f, 0.85f);
        public const int ButtonFont = 28;
    }

    /// <summary>Helpers to build uGUI hierarchies in code.</summary>
    public static class UIKit
    {
        static Font _builtin, _font, _bold, _light, _display;
        static Sprite _soft, _radial, _glow, _hgrad, _vgrad, _diamond, _waveIcon, _ring;

        static Font Builtin => _builtin != null ? _builtin : (_builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        static Font Load(ref Font cache, string name)
        {
            if (cache == null) cache = Resources.Load<Font>("Art/GGBotNetFonts/" + name);
            return cache != null ? cache : Builtin;
        }

        /// <summary>Body font: Jupiteroid Regular (GGBotNet, CC0), a clean humanist sans. Sentences, hints, credits.</summary>
        public static Font Font => Load(ref _font, "Jupiteroid-Regular");
        /// <summary>Jupiteroid Bold: key caps, buttons, HUD labels, the tutorial banner.</summary>
        public static Font Bold => Load(ref _bold, "Jupiteroid-Bold");
        /// <summary>Jupiteroid Light: quiet secondary lines (taglines, end line).</summary>
        public static Font Light => Load(ref _light, "Jupiteroid-Light");
        /// <summary>OSerif (GGBotNet, CC0), an elegant capital serif: logo, headings, level names, big numbers.</summary>
        public static Font Display => Load(ref _display, "OSerif-Regular");
        /// <summary>Font for short uppercase UI labels (buttons, bar labels, chips): Jupiteroid Bold.</summary>
        public static Font Narrow => Bold;

        static Sprite MakeSprite(int w, int h, string name, Func<float, float, float> alpha, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = name };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha((x + 0.5f) / w, (y + 0.5f) / h)) * 255f));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>Horizontal gradient: opaque on the left, transparent on the right (smooth).</summary>
        public static Sprite HGradient => _hgrad != null ? _hgrad : (_hgrad = MakeSprite(128, 4, "lw_hgrad", (u, v) => 1f - Mathf.SmoothStep(0f, 1f, u)));
        /// <summary>Vertical gradient: opaque at the bottom, transparent at the top.</summary>
        public static Sprite VGradient => _vgrad != null ? _vgrad : (_vgrad = MakeSprite(4, 128, "lw_vgrad", (u, v) => 1f - Mathf.SmoothStep(0f, 1f, v)));
        /// <summary>Soft-edged diamond (a light shard): the health icon.</summary>
        public static Sprite Diamond => _diamond != null ? _diamond : (_diamond = MakeSprite(64, 64, "lw_diamond", (u, v) =>
        {
            float d = Mathf.Abs(u - 0.5f) * 2f + Mathf.Abs(v - 0.5f) * 2f * 0.78f;
            return Mathf.Clamp01((0.92f - d) * 14f);
        }));
        /// <summary>One sine period drawn as a thick soft line: the resonance icon.</summary>
        public static Sprite WaveIcon => _waveIcon != null ? _waveIcon : (_waveIcon = MakeSprite(64, 64, "lw_wave", (u, v) =>
        {
            float best = 1f;
            for (int i = 0; i <= 24; i++)
            {
                float x = 0.06f + 0.88f * i / 24f;
                float y = 0.5f + 0.3f * Mathf.Sin((x - 0.06f) / 0.88f * Mathf.PI * 2f);
                float dx = u - x, dy = v - y;
                best = Mathf.Min(best, dx * dx + dy * dy);
            }
            return Mathf.Clamp01((0.075f - Mathf.Sqrt(best)) * 30f);
        }));
        /// <summary>Thin soft ring.</summary>
        public static Sprite Ring => _ring != null ? _ring : (_ring = MakeSprite(128, 128, "lw_ring", (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
            float x = (d - 0.86f) / 0.06f;
            return Mathf.Exp(-x * x);
        }));

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

        /// <summary>Dark rounded panel with a soft drop shadow, a faint inner gradient and a glowing top line.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size)
        {
            // Shadow first (sibling behind the panel) so the panel lifts off any background.
            var shadow = Image(parent, name + "Shadow", new Color(0f, 0f, 0f, 0.55f), Glow);
            Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), size + new Vector2(120, 120));
            var img = Image(parent, name, UIStyle.Panel, Soft);
            img.raycastTarget = true;
            var rt = Place(img.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            // The shadow follows the panel's active state.
            var follow = rt.gameObject.AddComponent<UIFollowActive>();
            follow.target = shadow.gameObject;
            var sheen = Image(rt, "Sheen", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.022f), VGradient, false);
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.55f); sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
            sheen.rectTransform.offsetMin = new Vector2(6, 0); sheen.rectTransform.offsetMax = new Vector2(-6, -4);
            sheen.rectTransform.localScale = new Vector3(1f, -1f, 1f);   // bright at the top
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

        /// <summary>Big letter-spaced OSerif heading.</summary>
        public static Text Heading(Transform parent, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null, string name = "Title", int spacing = 1)
        {
            var t = Label(parent, spacing > 0 ? Spaced(text.ToUpperInvariant(), spacing) : text, size, anchor, color, name);
            t.font = Display;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// <summary>Panel title: serif heading + a short glowing underline.</summary>
        public static Text PanelTitle(RectTransform panel, string text, float y = -62, int size = 40)
        {
            var title = Heading(panel, text, size, TextAnchor.MiddleCenter, Color.white, "Title", 1);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(Mathf.Max(400, panel.sizeDelta.x - 80), 60), new Vector2(0.5f, 0.5f));
            GlowText(title, UIStyle.Accent, 1.5f);
            var line = Image(panel, "TitleUnderline", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.7f), HGradientCentered);
            Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, y - 38), new Vector2(260, 2), new Vector2(0.5f, 0.5f));
            return title;
        }

        static Sprite _hgradC;
        /// <summary>Horizontal line, opaque in the middle, fading out at both ends.</summary>
        public static Sprite HGradientCentered => _hgradC != null ? _hgradC : (_hgradC = MakeSprite(128, 4, "lw_hgradc", (u, v) => Mathf.Sin(u * Mathf.PI)));

        /// <summary>A key-cap box ("Space", "J", "LMB") with a raised look, auto-sized to the text.</summary>
        public static RectTransform KeyCap(Transform parent, string key, int fontSize, out Text label, string name = "KeyCap")
        {
            var box = Rect(parent, name);
            var edge = Image(box, "Edge", new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.45f), Soft);
            Stretch(edge.rectTransform, -2f);
            var fill = Image(box, "Fill", UIStyle.KeyCap, Soft);
            Stretch(fill.rectTransform);
            var lip = Image(box, "Lip", new Color(0f, 0f, 0f, 0.35f), Soft);
            lip.rectTransform.anchorMin = new Vector2(0f, 0f); lip.rectTransform.anchorMax = new Vector2(1f, 0.22f);
            lip.rectTransform.offsetMin = new Vector2(1, 1); lip.rectTransform.offsetMax = new Vector2(-1, 0);
            label = Label(box, key, fontSize, TextAnchor.MiddleCenter, Color.white, "Key");
            label.font = Font;   // Regular reads cleaner than Bold at key-cap sizes
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(0, 2);
            SizeKeyCap(box, label);
            return box;
        }

        /// <summary>Re-fits a key cap to its text.</summary>
        public static void SizeKeyCap(RectTransform box, Text label)
        {
            float h = label.fontSize * 1.55f;
            float w = Mathf.Max(h, label.preferredWidth + label.fontSize * 0.9f);
            box.sizeDelta = new Vector2(w, h);
            var le = box.GetComponent<LayoutElement>();
            if (le != null) { le.preferredWidth = w; le.minWidth = w; le.preferredHeight = h; }
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
            cb.disabledColor = new Color(0.02f, 0.03f, 0.05f, 0.35f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            var t = Label(bg.rectTransform, UIKit.Spaced(label.ToUpperInvariant()), fontSize, TextAnchor.MiddleCenter, UIStyle.TextDim);
            t.font = Narrow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            var t = Label(row, UIKit.Spaced(label.ToUpperInvariant()), 19, TextAnchor.MiddleLeft, UIStyle.TextDim);
            t.font = Narrow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;   // never wrap mid-word ("VOLU / ME")
            t.rectTransform.anchorMin = new Vector2(0, 0); t.rectTransform.anchorMax = new Vector2(0.5f, 1);
            t.rectTransform.offsetMin = new Vector2(24, 0); t.rectTransform.offsetMax = Vector2.zero;

            var area = Rect(row, "Track");
            area.anchorMin = new Vector2(0.6f, 0.5f); area.anchorMax = new Vector2(0.94f, 0.5f);
            area.sizeDelta = new Vector2(0, 10); area.anchoredPosition = Vector2.zero;
            var track = area.gameObject.AddComponent<Image>(); track.color = new Color(1, 1, 1, 0.1f);
            track.sprite = Soft; track.type = UnityEngine.UI.Image.Type.Sliced; track.pixelsPerUnitMultiplier = 2.4f;
            var fillArea = Rect(area, "Fill Area"); Stretch(fillArea);
            var fill = Image(fillArea, "Fill", UIStyle.Accent, Soft);
            fill.pixelsPerUnitMultiplier = 2.4f;
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
            var t = Label(row, UIKit.Spaced(label.ToUpperInvariant()), 19, TextAnchor.MiddleLeft, UIStyle.TextDim);
            t.font = Narrow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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

    /// <summary>Selection feedback: label brightens + a glowing bar on the left + a soft light sweep; hover selects.</summary>
    public class UISelectFx : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
    {
        public Text label;
        Image _bar, _sweep;
        bool _selected;
        float _t;
        Vector2 _labelMin, _labelMax;
        bool _labelInit;

        void Awake()
        {
            _sweep = UIKit.Image(transform, "SelectGlow", new Color(1, 1, 1, 0), UIKit.HGradient, false);
            _sweep.rectTransform.anchorMin = new Vector2(0f, 0f); _sweep.rectTransform.anchorMax = new Vector2(0.8f, 1f);
            _sweep.rectTransform.offsetMin = new Vector2(2, 2); _sweep.rectTransform.offsetMax = new Vector2(0, -2);
            _sweep.transform.SetAsFirstSibling();
            _bar = UIKit.Image(transform, "SelectBar", UIStyle.Accent, UIKit.Soft);
            _bar.pixelsPerUnitMultiplier = 6f;
            _bar.rectTransform.anchorMin = new Vector2(0, 0.18f); _bar.rectTransform.anchorMax = new Vector2(0, 0.82f);
            _bar.rectTransform.pivot = new Vector2(0, 0.5f);
            _bar.rectTransform.sizeDelta = new Vector2(4, 0);
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
            if (label != null)
            {
                if (!_labelInit) { _labelMin = label.rectTransform.offsetMin; _labelMax = label.rectTransform.offsetMax; _labelInit = true; }
                label.color = Color.Lerp(UIStyle.TextDim, Color.white, _t);
                // Left-aligned labels slide in a little when selected.
                if (label.alignment == TextAnchor.MiddleLeft)
                {
                    var slide = new Vector2(10f * (1f - (1f - _t) * (1f - _t)), 0f);
                    label.rectTransform.offsetMin = _labelMin + slide;
                    label.rectTransform.offsetMax = _labelMax + slide;
                }
            }
            if (_bar != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                _bar.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, _t * pulse);
            }
            if (_sweep != null) _sweep.color = new Color(UIStyle.Accent.r, UIStyle.Accent.g, UIStyle.Accent.b, 0.2f * _t);
        }

        /// <summary>Call after changing the label's rect so the slide uses the new rest position.</summary>
        public void ResetLabelRest() { _labelInit = false; }
    }

    /// <summary>Mirrors this object's active state onto another (e.g. a panel's drop shadow sibling).</summary>
    public class UIFollowActive : MonoBehaviour
    {
        public GameObject target;
        void OnEnable() { if (target != null) target.SetActive(true); }
        void OnDisable() { if (target != null) target.SetActive(false); }
    }

    /// <summary>Vertical colour gradient on a Graphic's vertices, plus an optional moving highlight band (the logo).</summary>
    public class UIGradient : BaseMeshEffect
    {
        public Color top = Color.white, bottom = new Color(0.45f, 0.95f, 1f, 1f);
        public float shimmer;      // 0..1 strength of the moving highlight band
        public float shimmerPos;   // position of the band across the rect (0..1)

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = graphic.rectTransform.rect;
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float ty = Mathf.InverseLerp(rect.yMin + rect.height * 0.25f, rect.yMax - rect.height * 0.25f, v.position.y);
                float tx = Mathf.InverseLerp(rect.xMin, rect.xMax, v.position.x);
                var c = Color.Lerp(bottom, top, ty);
                if (shimmer > 0f)
                {
                    float d = Mathf.Abs(tx - shimmerPos);
                    c = Color.Lerp(c, Color.white, shimmer * Mathf.Clamp01(1f - d * 7f));
                }
                Color orig = v.color;
                c.a *= orig.a;
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }

        public void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }
    }

    /// <summary>
    /// A procedurally drawn, animated waveform line (thick strip mesh with soft edges via vertex alpha).
    /// A few harmonics under a Gaussian envelope, scrolling over time: the logo's sound wave.
    /// </summary>
    public class UIWaveLine : MaskableGraphic
    {
        public int segments = 160;
        public float thickness = 4f;
        public float amplitude = 0.35f;     // fraction of half the rect height
        public float speed = 1.6f;
        public float glowWidth = 0f;        // extra soft fringe in px
        public float energy = 1f;           // scales amplitude (e.g. on the beat)
        public float phaseOffset;
        public float envelopeWidth = 2.6f;  // larger = narrower envelope
        CanvasRenderer _cr;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            float t = Application.isPlaying ? Time.unscaledTime * speed + phaseOffset : phaseOffset;
            int n = Mathf.Max(8, segments);
            float half = thickness * 0.5f;
            Color32 col = color;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                float x = Mathf.Lerp(r.xMin, r.xMax, u);
                float e = (u - 0.5f) * envelopeWidth;
                float env = Mathf.Exp(-e * e);
                float w = Mathf.Sin(u * 22f - t * 2.2f) * 0.55f + Mathf.Sin(u * 47f + t * 3.1f) * 0.22f + Mathf.Sin(u * 9f - t) * 0.35f;
                float y = r.center.y + w * env * amplitude * energy * r.height * 0.5f;
                float edgeA = Mathf.Clamp01(u * 10f) * Mathf.Clamp01((1f - u) * 10f) * (0.25f + 0.75f * env);
                var c = col; c.a = (byte)(col.a * edgeA);
                var cg = c; cg.a = 0;
                int b = vh.currentVertCount;
                vh.AddVert(new Vector3(x, y + half + glowWidth), cg, Vector4.zero);
                vh.AddVert(new Vector3(x, y + half * 0.3f), c, Vector4.zero);
                vh.AddVert(new Vector3(x, y - half * 0.3f), c, Vector4.zero);
                vh.AddVert(new Vector3(x, y - half - glowWidth), cg, Vector4.zero);
                if (i > 0)
                {
                    int a = b - 4;
                    for (int k = 0; k < 3; k++)
                    {
                        vh.AddTriangle(a + k, b + k, a + k + 1);
                        vh.AddTriangle(a + k + 1, b + k, b + k + 1);
                    }
                }
            }
        }

        void Update()
        {
            // Skip the rebuild while invisible (e.g. faded-out level card).
            if (!Application.isPlaying) return;
            if (_cr == null && !TryGetComponent(out _cr)) return;   // torn down (tests destroy hierarchies piecemeal)
            if (_cr.GetInheritedAlpha() > 0.01f) SetVerticesDirty();
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
