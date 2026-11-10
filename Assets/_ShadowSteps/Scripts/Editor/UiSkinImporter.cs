using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.UI.Editor
{
    /// <summary>
    /// D-055: turns the user's screen art under Assets/_ShadowSteps/Scripts/UI/{LevelComplete,LevelFailed,LevelSelect,SettingsMenu} into the
    /// <see cref="UiSkin"/> asset in Resources. Each PNG is trimmed to its alpha bounds into Art2D/Sprites/_UI/Screens/ and imported as a single
    /// sprite (stable fileID) with a 9-slice border where the piece stretches (pills: half the height as end caps; boxes and cards: a fraction of
    /// the shorter side on every edge). The originals are never modified.
    /// </summary>
    public static class UiSkinImporter
    {
        const string SourceRoot = "Assets/_ShadowSteps/Scripts/UI/";
        const string OutDir = "Assets/_ShadowSteps/Art2D/Sprites/_UI/Screens/";

        [MenuItem("Shadow Steps/UI/Import screen skin (LevelComplete, LevelFailed, LevelSelect, SettingsMenu)")]
        public static void ImportMenu() { var skin = Import(); if (skin != null) Debug.Log("[UI] screen skin imported: " + (skin.IsComplete ? "complete" : "INCOMPLETE") + " -> " + UiSkin.DefaultAssetPath); }

        /// <summary>Batch entry: import and exit non-zero when a piece is missing.</summary>
        public static void ImportBatch()
        {
            var skin = Import();
            if (skin == null || !skin.IsComplete) { Debug.LogError("[UI] screen skin incomplete"); if (Application.isBatchMode) EditorApplication.Exit(1); }
            else Debug.Log("[UI] screen skin complete");
        }

        public static bool HasSourceArt() => File.Exists(SourceRoot + "LevelFailed/Cop.png") && File.Exists(SourceRoot + "LevelSelect/Level green box.png");

        public static UiSkin Import()
        {
            if (!HasSourceArt()) { Debug.LogError("[UI] screen art not found under " + SourceRoot); return null; }
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(UiSkin.DefaultAssetPath);
            if (skin == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UiSkin.DefaultAssetPath));
                skin = ScriptableObject.CreateInstance<UiSkin>(); AssetDatabase.CreateAsset(skin, UiSkin.DefaultAssetPath);
            }
            skin.card = Trim("LevelComplete/popup box 2.png", "screen_card", 0f, 0.12f);
            skin.cardTall = Trim("LevelSelect/Level select popup box.png", "screen_card_tall", 0f, 0.10f);
            skin.statBox = Trim("SettingsMenu/Settings list patch.png", "screen_row", 0.5f, 0f);
            skin.buttonGreen = Trim("LevelComplete/green button 2.png", "screen_button_green", 0.5f, 0f);
            skin.buttonWhite = Trim("LevelComplete/white button 2.png", "screen_button_white", 0.5f, 0f);
            skin.backButton = Trim("LevelSelect/Back button.png", "screen_back_button", 0f, 0.40f);
            skin.backArrow = Trim("LevelSelect/Back arrow.png", "screen_back_arrow", 0f, 0f);
            skin.chevron = Trim("SettingsMenu/Pricacy policy arrow.png", "screen_chevron", 0f, 0f);
            skin.toggleOn = Trim("SettingsMenu/On.png", "screen_toggle_on", 0f, 0f);
            skin.toggleOff = Trim("SettingsMenu/Off.png", "screen_toggle_off", 0f, 0f);
            skin.headerPill = Trim("LevelSelect/Level header.png", "screen_header_pill", 0.5f, 0f);
            skin.smallPill = Trim("LevelSelect/Level numbers.png", "screen_small_pill", 0f, 0.40f);
            skin.levelBoxOpen = Trim("LevelSelect/Level green box.png", "screen_level_open", 0f, 0.22f);
            skin.levelBoxLocked = Trim("LevelSelect/Level white box_locked.png", "screen_level_locked", 0f, 0.22f);
            skin.lockIcon = Trim("LevelSelect/Lock icon.png", "screen_lock", 0f, 0f);
            skin.starFull = Trim("LevelSelect/Star.png", "screen_star_full", 0f, 0f);
            skin.starBlank = Trim("LevelSelect/blank star.png", "screen_star_blank", 0f, 0f);
            skin.cop = Trim("LevelFailed/Cop.png", "screen_cop", 0f, 0f);
            // Level-complete pieces (user mock Mock_ResultScreen, 2026-11-06): the results screen prefers these and falls back to the shared pieces.
            skin.resultsCard = Trim("LevelComplete/Popup box.png", "screen_results_card", 0f, 0.12f);
            skin.resultsStatBox = Trim("LevelComplete/3 actions box.png", "screen_results_strip", 0.5f, 0f);
            skin.resultsButtonGreen = Trim("LevelComplete/green button.png", "screen_results_button_green", 0.5f, 0f);
            skin.resultsButtonWhite = Trim("LevelComplete/white button.png", "screen_results_button_white", 0.5f, 0f);
            skin.resultsStarFull = Trim("LevelComplete/Star.png", "screen_results_star_full", 0f, 0f);
            skin.resultsStarBlank = Trim("LevelComplete/blank star.png", "screen_results_star_blank", 0f, 0f);
            // Tutorial hand (user art, 2026-11-11): optional, so older art drops still import.
            skin.tapHand = File.Exists(SourceRoot + "Ftue/Tap hand.png") ? Trim("Ftue/Tap hand.png", "screen_tap_hand", 0f, 0f) : skin.tapHand;
            EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
            UiSkin.Current = null;
            return skin;
        }

        /// <summary>Trim to the alpha bounding box (+2 px) into OutDir and import as a single sprite; <paramref name="capFraction"/> = end caps as a fraction of the trimmed height (pills), <paramref name="edgeFraction"/> = border on all four sides as a fraction of the shorter side (boxes).</summary>
        static Sprite Trim(string relativeSource, string spriteName, float capFraction, float edgeFraction)
        {
            string path = SourceRoot + relativeSource;
            if (!File.Exists(path)) { Debug.LogError("[UI] missing screen art: " + path); return null; }
            string outPath = OutDir + spriteName + ".png";
            Directory.CreateDirectory(OutDir);
            var png = new Texture2D(2, 2, TextureFormat.RGBA32, false); png.LoadImage(File.ReadAllBytes(path));
            var px = png.GetPixels32(); int w = png.width, h = png.height, minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (px[y * w + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { Object.DestroyImmediate(png); Debug.LogError("[UI] empty screen sprite: " + path); return null; }
            minX = Mathf.Max(0, minX - 2); minY = Mathf.Max(0, minY - 2); maxX = Mathf.Min(w - 1, maxX + 2); maxY = Mathf.Min(h - 1, maxY + 2);
            int tw = maxX - minX + 1, th = maxY - minY + 1;
            bool stale = !File.Exists(outPath) || File.GetLastWriteTimeUtc(outPath) < File.GetLastWriteTimeUtc(path);
            if (stale)
            {
                var cut = new Texture2D(tw, th, TextureFormat.RGBA32, false);
                var block = new Color32[tw * th];
                for (int y = 0; y < th; y++) for (int x = 0; x < tw; x++) block[y * tw + x] = px[(minY + y) * w + (minX + x)];
                cut.SetPixels32(block); cut.Apply();
                File.WriteAllBytes(outPath, cut.EncodeToPNG()); Object.DestroyImmediate(cut);
                AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            }
            Object.DestroyImmediate(png);
            var imp = AssetImporter.GetAtPath(outPath) as TextureImporter;
            if (imp == null) { Debug.LogError("[UI] trimmed screen sprite did not import: " + outPath); return null; }
            Vector4 border = Vector4.zero;
            if (capFraction > 0f) { float b = Mathf.Floor(th * capFraction); border = new Vector4(b, 0f, b, 0f); }
            else if (edgeFraction > 0f) { float b = Mathf.Floor(Mathf.Min(tw, th) * edgeFraction); border = new Vector4(b, b, b, b); }
            var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
            bool same = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single && imp.alphaIsTransparency && !imp.mipmapEnabled
                && imp.maxTextureSize == 1024 && Mathf.Approximately(imp.spritePixelsPerUnit, 100f) && settings.spriteMeshType == SpriteMeshType.FullRect && (imp.spriteBorder - border).sqrMagnitude < 0.5f
                && settings.spriteAlignment == (int)SpriteAlignment.Center;
            if (!same)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
                imp.sRGBTexture = true; imp.maxTextureSize = 1024; imp.textureCompression = TextureImporterCompression.Compressed; imp.spritePixelsPerUnit = 100; imp.spriteBorder = border;
                imp.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(0.5f, 0.5f); settings.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            var orig = AssetImporter.GetAtPath(path) as TextureImporter;
            if (orig != null && orig.spriteImportMode == SpriteImportMode.Multiple) { orig.spriteImportMode = SpriteImportMode.Single; orig.SaveAndReimport(); }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            if (sprite == null) Debug.LogError("[UI] no sprite at " + outPath);
            return sprite;
        }
    }
}
