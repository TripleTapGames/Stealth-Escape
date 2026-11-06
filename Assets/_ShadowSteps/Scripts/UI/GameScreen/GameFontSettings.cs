using TMPro;
using UnityEngine;

namespace ShadowSteps.UI
{
    [CreateAssetMenu(menuName = "Shadow Steps/Game Fonts", fileName = "GameFontSettings")]
    public sealed class GameFontSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_ShadowSteps/Resources/GameFontSettings.asset";
        public TMP_FontAsset tmpFont;
        public Font legacyFont;

        public static GameFontSettings Current => Resources.Load<GameFontSettings>("GameFontSettings");
        public static TMP_FontAsset TmpFont => Current != null && Current.tmpFont != null
            ? Current.tmpFont : TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
        public static Font LegacyFont => Current != null && Current.legacyFont != null
            ? Current.legacyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
