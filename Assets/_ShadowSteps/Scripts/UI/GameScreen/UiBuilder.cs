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
            EnsureEventSystem();
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

        /// <summary>uGUI only receives taps/clicks through an EventSystem; every canvas builder calls this so no scene can forget it.</summary>
        public static void EnsureEventSystem()
        {
            if (!Application.isPlaying) return;
            if (UnityEngine.EventSystems.EventSystem.current != null || Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
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
}
