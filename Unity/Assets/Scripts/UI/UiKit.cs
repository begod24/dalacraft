using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dala
{
    /// <summary>
    /// Runtime uGUI builder using the pixel sprites painted in Blender (Blender/tools/ui_assets.py → Resources/UI).
    /// Text uses the built-in dynamic font so Cyrillic and Kazakh letters render.
    /// </summary>
    public static class UiKit
    {
        public const float Px = 3f;   // screen pixels per sprite pixel at 1080p
        static readonly Dictionary<string, Sprite> sprites = new();
        static Font font;

        public static Font Font => font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static readonly Color Gold = new(0.98f, 0.82f, 0.38f);
        public static readonly Color Cream = new(0.98f, 0.95f, 0.86f);
        public static readonly Color Ink = new(0.18f, 0.11f, 0.08f);

        public static Sprite Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>("UI/" + name);
            sprites[name] = s;
            return s;
        }

        public static Canvas Canvas(string name, int order, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var sc = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            EnsureEventSystem();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Center(string name, Transform parent, Vector2 size, Vector2 pos = default) =>
            Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static UnityEngine.UI.Image Image(RectTransform rt, string sprite, Color? color = null, bool sliced = false, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite != null ? Sprite(sprite) : null;
            img.color = color ?? Color.white;
            img.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            // sprites are 16 PPU, canvas reference is 100 units → border pixels drawn at Px screen pixels each
            img.pixelsPerUnitMultiplier = sliced ? 100f / 16f / Px : 1f;
            img.raycastTarget = raycast;
            return img;
        }

        public static UnityEngine.UI.Text Text(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            var rt = Stretch("Text", parent);
            var t = rt.gameObject.AddComponent<UnityEngine.UI.Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<UnityEngine.UI.Outline>(); o.effectColor = new Color(0.1f, 0.05f, 0.03f, 0.9f); o.effectDistance = new Vector2(2, -2);
                var sh = rt.gameObject.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(3, -3);
            }
            return t;
        }

        public static UnityEngine.UI.Image Panel(Transform parent, Vector2 size, Vector2 pos = default, bool dark = false)
        {
            var rt = Center(dark ? "PanelDark" : "Panel", parent, size, pos);
            return Image(rt, dark ? "UI_PanelDark" : "UI_Panel", null, true, true);
        }

        public static UnityEngine.UI.Button Button(Transform parent, string label, Vector2 size, Vector2 pos, System.Action onClick, bool enabled = true, string style = "UI_Button")
        {
            var rt = Center("Button_" + label, parent, size, pos);
            var img = Image(rt, style, null, true, true);
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            b.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            b.spriteState = new UnityEngine.UI.SpriteState { highlightedSprite = Sprite("UI_ButtonHover"), selectedSprite = Sprite(style), pressedSprite = Sprite("UI_ButtonHover"), disabledSprite = Sprite("UI_ButtonDisabled") };
            b.interactable = enabled;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Text(rt, label, 30, enabled ? Cream : new Color(0.7f, 0.68f, 0.65f));
            t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
            return b;
        }

        public static UnityEngine.UI.Slider Slider(Transform parent, string label, float min, float max, float value, Vector2 pos, System.Func<float, string> fmt, System.Action<float> onChange, bool whole = false)
        {
            var row = Center("Slider_" + label, parent, new Vector2(620, 60), pos);
            var bg = Image(row, "UI_Button", new Color(0.55f, 0.45f, 0.4f), true, false);
            var lbl = Text(row, "", 26, Cream);
            var track = Center("Track", row, new Vector2(560, 18), new Vector2(0, -14));
            Image(track, "UI_SliderBg", null, true, false);
            var s = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
            var fill = Stretch("Fill", track); Image(fill, null, new Color(0.95f, 0.7f, 0.25f, 0.35f));
            var handleArea = Stretch("HandleArea", track);
            var handle = Center("Handle", handleArea, new Vector2(24, 42));
            var hImg = Image(handle, "UI_SliderKnob", null, false, true);
            s.handleRect = handle; s.targetGraphic = hImg; s.fillRect = fill;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = whole; s.value = value;
            lbl.rectTransform.offsetMin = new Vector2(0, 12);
            lbl.text = $"{label}: {fmt(value)}";
            s.onValueChanged.AddListener(v => { lbl.text = $"{label}: {fmt(v)}"; onChange(v); });
            return s;
        }

        public static UnityEngine.UI.Toggle Toggle(Transform parent, string label, bool value, Vector2 pos, System.Action<bool> onChange)
        {
            var row = Center("Toggle_" + label, parent, new Vector2(620, 50), pos);
            Image(row, "UI_Button", new Color(0.55f, 0.45f, 0.4f), true, true);
            var box = Rect("Box", row, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(36, 36));
            var bgImg = Image(box, "UI_CheckOff", null, false, true);
            var check = Stretch("Check", box); var chkImg = Image(check, "UI_CheckOn");
            var t = row.gameObject.AddComponent<UnityEngine.UI.Toggle>();
            t.targetGraphic = bgImg; t.graphic = chkImg; t.isOn = value;
            var txt = Text(row, label, 26, Cream, TextAnchor.MiddleLeft);
            txt.rectTransform.offsetMin = new Vector2(68, 0);
            t.onValueChanged.AddListener(v => onChange(v));
            return t;
        }

        public static UnityEngine.UI.InputField Input(Transform parent, string placeholder, string value, Vector2 pos, Vector2 size)
        {
            var rt = Center("Input", parent, size, pos);
            var img = Image(rt, "UI_PanelDark", null, true, true);
            var f = rt.gameObject.AddComponent<UnityEngine.UI.InputField>();
            var t = Text(rt, "", 28, Cream, TextAnchor.MiddleLeft, false); t.rectTransform.offsetMin = new Vector2(18, 0); t.rectTransform.offsetMax = new Vector2(-18, 0);
            t.supportRichText = false;
            var ph = Text(rt, placeholder, 28, new Color(0.7f, 0.65f, 0.6f, 0.7f), TextAnchor.MiddleLeft, false); ph.rectTransform.offsetMin = new Vector2(18, 0);
            f.textComponent = t; f.placeholder = ph; f.targetGraphic = img; f.text = value;
            return f;
        }
    }
}
