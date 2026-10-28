using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Builds uGUI hierarchies from code so screens can be generated into prefabs by the editor tool and also
    /// self-assemble at runtime when no prefab is assigned (one code path, no hand-built scene state).
    /// Uses TextMeshPro when its default font asset is available, otherwise the legacy Text component, so the project
    /// works before "Import TMP Essential Resources" has been run.
    /// </summary>
    public static class UiBuilder
    {
        public static readonly Color PanelColor = new Color(0.09f, 0.11f, 0.16f, 0.92f);
        public static readonly Color ButtonColor = new Color(0.20f, 0.26f, 0.36f, 1f);
        public static readonly Color ButtonAccent = new Color(0.25f, 0.80f, 0.75f, 1f);
        public static readonly Color ButtonDanger = new Color(0.85f, 0.35f, 0.35f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        public const float MinTouchTarget = 96f; // reference pixels at 1080 wide (~48 dp)

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return rt;
        }

        /// <summary>Anchors a rect to a fraction band of its parent (e.g. top bar: yMin 0.86, yMax 1).</summary>
        public static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax, float padding = 0f)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        public static UiText CreateText(string name, Transform parent, string text, float size, TextAnchor align = TextAnchor.MiddleCenter, bool bold = false)
        {
            var rt = CreateRect(name, parent);
            var label = rt.gameObject.AddComponent<UiText>();
            label.Initialize(text, size, align, bold, TextColor);
            return label;
        }

        public static UiButton CreateButton(string name, Transform parent, string label, Color color, float textSize = 36f)
        {
            var rt = CreatePanel(name, parent, color);
            var button = rt.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            var text = CreateText("Label", rt, label, textSize, TextAnchor.MiddleCenter, true);
            Anchor(text.Rect, 0f, 0f, 1f, 1f, 8f);
            var ui = rt.gameObject.AddComponent<UiButton>();
            ui.Button = button;
            ui.Label = text;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = MinTouchTarget;
            le.minWidth = MinTouchTarget;
            le.flexibleWidth = 1f;
            return ui;
        }

        /// <summary>Removes a component correctly both in play mode and while generating prefabs in the editor.</summary>
        public static void RemoveComponent(Component c)
        {
            if (c == null) return;
            if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
        }

        public static HorizontalLayoutGroup HorizontalRow(RectTransform rt, float spacing = 16f, float padding = 16f)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return layout;
        }

        public static VerticalLayoutGroup VerticalStack(RectTransform rt, float spacing = 16f, float padding = 24f)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            return layout;
        }
    }

    /// <summary>Text wrapper that uses TextMeshPro when available and legacy Text otherwise. Code only talks to this.</summary>
    public sealed class UiText : MonoBehaviour
    {
        public TMPro.TextMeshProUGUI Tmp;
        public Text Legacy;
        public RectTransform Rect => (RectTransform)transform;

        public static bool TmpAvailable => TMPro.TMP_Settings.instance != null && TMPro.TMP_Settings.defaultFontAsset != null;

        public void Initialize(string text, float size, TextAnchor align, bool bold, Color color)
        {
            if (TmpAvailable)
            {
                Tmp = gameObject.AddComponent<TMPro.TextMeshProUGUI>();
                Tmp.fontSize = size;
                Tmp.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
                Tmp.alignment = ToTmpAlignment(align);
                Tmp.color = color;
                Tmp.enableWordWrapping = true;
                Tmp.raycastTarget = false;
            }
            else
            {
                Legacy = gameObject.AddComponent<Text>();
                Legacy.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                Legacy.fontSize = Mathf.RoundToInt(size);
                Legacy.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
                Legacy.alignment = align;
                Legacy.color = color;
                Legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
                Legacy.verticalOverflow = VerticalWrapMode.Overflow;
                Legacy.raycastTarget = false;
            }
            Text = text;
        }

        public string Text
        {
            get => Tmp != null ? Tmp.text : Legacy != null ? Legacy.text : string.Empty;
            set { if (Tmp != null) Tmp.text = value; else if (Legacy != null) Legacy.text = value; }
        }

        public Color Color
        {
            set { if (Tmp != null) Tmp.color = value; else if (Legacy != null) Legacy.color = value; }
        }

        private static TMPro.TextAlignmentOptions ToTmpAlignment(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TMPro.TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TMPro.TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TMPro.TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TMPro.TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TMPro.TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TMPro.TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TMPro.TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TMPro.TextAlignmentOptions.BottomRight;
                default: return TMPro.TextAlignmentOptions.Center;
            }
        }
    }

    /// <summary>Button plus its label, with a count badge helper ("SMOKE ×1").</summary>
    public sealed class UiButton : MonoBehaviour
    {
        public Button Button;
        public UiText Label;
        private string _baseLabel;

        public void SetLabel(string text) { _baseLabel = text; Label.Text = text; }
        public void SetCount(int count) { Label.Text = (_baseLabel ?? Label.Text) + " ×" + count; }
        public bool Interactable { get => Button.interactable; set => Button.interactable = value; }
    }

    /// <summary>Shrinks a full-screen RectTransform to the device safe area (notches, gesture bars). Re-applies on resolution change.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _last;
        private RectTransform _rt;

        private void Awake() { _rt = (RectTransform)transform; Apply(); }
        private void Update() { if (Screen.safeArea != _last) Apply(); }

        public void Apply()
        {
            _last = Screen.safeArea;
            var min = _last.position;
            var max = _last.position + _last.size;
            min.x /= Screen.width; min.y /= Screen.height; max.x /= Screen.width; max.y /= Screen.height;
            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
