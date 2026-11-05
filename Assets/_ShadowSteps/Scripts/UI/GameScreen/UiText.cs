using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
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
}
