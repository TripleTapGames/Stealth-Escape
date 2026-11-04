using System.IO;
using ShadowSteps.Core;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Editor
{
    /// <summary>
    /// Imports the hand-made 2D art the user drops under <c>Art2D/Sprites/Backgrounds</c> and
    /// <c>Art2D/Sprites/Characters/Player|Guards</c> into <see cref="SS2DSpriteLibrary"/>: sets each PNG's importer to
    /// Sprite (2D and UI) with transparency, one consistent pixels-per-unit and pivot per character set, and assigns the
    /// four directional frames (Up = North, Right = East, Down = South, Left = West) plus the courtyard background.
    /// The PNGs themselves are never written; only their .meta importer settings and the library asset change.
    /// Safe to run again: it is idempotent, and the sprite generator / character capture call it afterwards so
    /// regenerating placeholders never hides the final art.
    /// </summary>
    public static class SS2DFinalArtImporter
    {
        public const string SpritesRoot = "Assets/_ShadowSteps/Art2D/Sprites/";
        public const string BackgroundPath = SpritesRoot + "Backgrounds/Courtyard-Natural-Sides-1400x2000.png";
        /// <summary>File stems in <see cref="Direction"/> order: North, East, South, West.</summary>
        static readonly string[] PlayerFiles = { "Characters/Player/Suit-Faceless-Up.png", "Characters/Player/Suit-Faceless-Right.png", "Characters/Player/Suit-Faceless-Down.png", "Characters/Player/Suit-Faceless-Left.png" };
        static readonly string[] GuardFiles = { "Characters/Guards/Blue-Guard-Faceless-Up.png", "Characters/Guards/Blue-Guard-Faceless-Right.png", "Characters/Guards/Blue-Guard-Faceless-Down.png", "Characters/Guards/Blue-Guard-Faceless-Left.png" };

        /// <summary>Character frames are 1024×2048; the whole frame height is one unit so the library's characterHeight is the drawn frame height in node spacings.</summary>
        const int CharacterPpu = 2048;
        /// <summary>Centre of the painted base disc, measured from the alpha of all eight frames (disc band rows 1110–1505 of 2048); the node sits under the disc centre.</summary>
        static readonly Vector2 CharacterPivot = new Vector2(0.5f, 0.363f);
        const int BackgroundPpu = 256;
        /// <summary>Courtyard floor inside the walls, normalized from the bottom-left (measured: x 388–992 of 1400, y 576–1312 of 2000 from the top).</summary>
        static readonly Rect CourtyardFloor = new Rect(0.277f, 0.344f, 0.432f, 0.368f);
        static readonly Color Water = new Color32(0x01, 0x6E, 0xC0, 0xFF);
        public const string ObstacleDir = SpritesRoot + "Obstacles/";
        public const string BushPath = ObstacleDir + "obstacle bush.png", WallSidePath = ObstacleDir + "obstacle wall side.png", WallFrontPath = ObstacleDir + "obstacle wall front.png";
        /// <summary>Obstacle art is authored at 2048 px with pillar centres / the planter box ≈ 1000 px apart: 1000 px = one node spacing.</summary>
        const int ObstaclePpu = 1000;
        /// <summary>Pivots measured from the art (normalized, bottom-left): planter box centre; midpoint between the two pillar caps of the side piece; centre of the front piece's top face between its pillars.</summary>
        static readonly Vector2 BushPivot = new Vector2(0.515f, 0.502f), WallSidePivot = new Vector2(0.50f, 0.60f), WallFrontPivot = new Vector2(0.518f, 0.61f);
        public const string LevelArtDir = "Assets/_ShadowSteps/Art2D/LevelArt/";
        public const string CastShadowPath = SpritesRoot + "Characters/character_shadow.png";
        public const string CameraDir = SpritesRoot + "Cameras/";
        /// <summary>Drawn-direction order (North, East, South, West): back view, side right, front (lens), side left.</summary>
        static readonly string[] CameraFiles = { "cam_back.png", "cam_right.png", "cam_front.png", "cam_left.png" };
        /// <summary>Centre of the base plate, shared by all four views (plate x 625–1357, y 1410–1740 of 2048).</summary>
        static readonly Vector2 CameraPivot = new Vector2(0.483f, 0.23f);
        public static bool HasCameraArt() { foreach (var f in CameraFiles) if (!File.Exists(CameraDir + f)) return false; return true; }
        /// <summary>Centre of the shadow's round lobe (the base-disc shadow), measured from the alpha: (1180, 1180) of 2048.</summary>
        static readonly Vector2 CastShadowPivot = new Vector2(0.576f, 0.424f);

        public static bool HasObstacleArt() => File.Exists(BushPath) && File.Exists(WallSidePath) && File.Exists(WallFrontPath);

        public static bool HasFinalArt()
        {
            if (!File.Exists(BackgroundPath)) return false;
            foreach (var f in PlayerFiles) if (!File.Exists(SpritesRoot + f)) return false;
            foreach (var f in GuardFiles) if (!File.Exists(SpritesRoot + f)) return false;
            return true;
        }

        [MenuItem("Shadow Steps/2D/Import final art (background + characters) into the sprite library")]
        public static void ImportMenu()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath);
            if (lib == null) { Debug.LogError("[SS2D] no sprite library at " + SS2DSpriteLibrary.DefaultAssetPath + "; run Generate sprites first"); return; }
            if (!Import(lib)) Debug.LogError("[SS2D] final art PNGs not found under " + SpritesRoot + "Backgrounds / Characters/Player / Characters/Guards");
        }

        public static void ImportIfPresent(SS2DSpriteLibrary lib) { if (lib != null && HasFinalArt()) Import(lib); }

        /// <summary>Configures the importers and assigns the slots. Returns false when the PNGs are missing.</summary>
        public static bool Import(SS2DSpriteLibrary lib)
        {
            if (lib == null || !HasFinalArt()) return false;
            ConfigureBackground(BackgroundPath);
            foreach (var f in PlayerFiles) ConfigureCharacter(SpritesRoot + f);
            foreach (var f in GuardFiles) ConfigureCharacter(SpritesRoot + f);
            AssetDatabase.Refresh();

            lib.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            lib.backgroundTint = Color.white;
            lib.backgroundFit = SS2DBackgroundFit.FrameBoard;
            lib.backgroundBoardRect = CourtyardFloor;
            lib.backgroundBoardPadding = 1f;
            lib.showBoardPanel = false;        // the courtyard is the board surface
            lib.showFloorTiles = false;        // the painted floor is the ground; only nodes, paths, walls, cover and overlays draw on it
            lib.showPerimeterWalls = false;    // the courtyard paints the boundary walls; wall-mounted cameras sit on their own wall stub
            lib.showExitDoor = false; lib.playExitDoorSequence = false; // no door beyond the exit node; the win goes straight to the results screen
            lib.background = Water;            // camera clear colour matches the painted water should any edge show
            lib.spy = Set(PlayerFiles, "Sprites/Characters/Player (final art)");
            lib.guard = Set(GuardFiles, "Sprites/Characters/Guards (final art)");
            lib.characterHeight = 1.5f;        // frame height in node spacings: base disc ≈ 0.68 spacing wide, figure ≈ 0.75 spacing above the node
            // Node & path style from the Camera Vision Guide mock: plain white discs ≈ 0.27 spacing, solid white paths ≈ 0.04 spacing.
            var ink = new Color32(0x46, 0x3E, 0x3E, 0xFF); // user-specified node & path colour
            lib.nodeTint = ink; lib.nodeSize = 0.27f; lib.lineTint = ink; lib.lineWidth = 0.05f;
            if (File.Exists(CastShadowPath))
            {
                Configure(CastShadowPath, CharacterPpu, CastShadowPivot, 1024, SpriteMeshType.Tight);
                AssetDatabase.Refresh();
                lib.characterCastShadow = AssetDatabase.LoadAssetAtPath<Sprite>(CastShadowPath);
                lib.characterCastShadowSize = 1.85f; lib.characterCastShadowTint = new Color(1f, 1f, 1f, 0.85f);
            }
            if (HasCameraArt())
            {
                var art = new Sprite[4];
                for (int d = 0; d < 4; d++) { Configure(CameraDir + CameraFiles[d], CharacterPpu, CameraPivot, 1024, SpriteMeshType.Tight); }
                AssetDatabase.Refresh();
                for (int d = 0; d < 4; d++) art[d] = AssetDatabase.LoadAssetAtPath<Sprite>(CameraDir + CameraFiles[d]);
                lib.cameraArt = art; lib.cameraArtSize = 0.9f;
            }
            if (HasObstacleArt())
            {
                Configure(BushPath, ObstaclePpu, BushPivot, 1024, SpriteMeshType.Tight);
                Configure(WallSidePath, ObstaclePpu, WallSidePivot, 1024, SpriteMeshType.Tight);
                Configure(WallFrontPath, ObstaclePpu, WallFrontPivot, 1024, SpriteMeshType.Tight);
                AssetDatabase.Refresh();
                lib.bush = AssetDatabase.LoadAssetAtPath<Sprite>(BushPath);
                lib.wallSide = AssetDatabase.LoadAssetAtPath<Sprite>(WallSidePath);
                lib.wallFront = AssetDatabase.LoadAssetAtPath<Sprite>(WallFrontPath);
                lib.bushSize = 0.9f; lib.stoneWallSize = 1f; lib.preferStoneCameraMounts = true;
                lib.defaultObstacleArt = SS2DObstacleArt.BushPlanter; // every X cell without a layout entry is a planter
                lib.interiorWallArt = SS2DObstacleArt.StoneWall;      // interior '#' runs are stone walls, lone interior walls planters
                lib.wallRunCameraHug = 0.36f;
                lib.levelArt = new[] { Level19Layout(), Layout("LEVEL-26", new[] { B(2, 3), W(3, 6, SS2DWallAxis.LogicalX) }), Layout("LEVEL-27", new[] { B(4, 3) }),
                    Layout("LEVEL-28", new[] { B(2, 3), W(6, 5, SS2DWallAxis.LogicalY) }, "cam1", "cam2"), Layout("LEVEL-29", new[] { B(3, 2), W(3, 6, SS2DWallAxis.LogicalX) }, "cam1"),
                    Layout("LEVEL-30", new[] { B(4, 2) }, "cam1", "cam2") };
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[SS2D] final art imported: background " + lib.backgroundSprite.name + ", spy " + (lib.spy.IsComplete ? "4/4" : "incomplete") + ", guard " + (lib.guard.IsComplete ? "4/4" : "incomplete"));
            return true;
        }

        /// <summary>LEVEL-19 "Sweep Window" decoration: the X beside the camera wall (4,1) is a stone wall stub running off the boundary (logical Y), the standalone X at (1,3) a bush planter, the camera on a stone wall piece.</summary>
        public static SS2DLevelArtLayout Level19Layout()
        {
            string path = LevelArtDir + "SS2DArt_LEVEL-19.asset";
            Directory.CreateDirectory(LevelArtDir);
            var layout = AssetDatabase.LoadAssetAtPath<SS2DLevelArtLayout>(path);
            if (layout == null) { layout = ScriptableObject.CreateInstance<SS2DLevelArtLayout>(); AssetDatabase.CreateAsset(layout, path); }
            layout.stageCode = "LEVEL-19"; layout.defaultObstacle = SS2DObstacleArt.BushPlanter;
            layout.obstacles = new System.Collections.Generic.List<SS2DObstacleArtEntry>
            {
                // Slid 0.36 toward the camera side (logical −x) so the thin stone body sits on the cell edge where the camera's rays are clipped by this cover cell.
                new SS2DObstacleArtEntry { x = 4, y = 1, art = SS2DObstacleArt.StoneWall, wallAxis = SS2DWallAxis.LogicalY, offset = new Vector2(-0.36f, 0f) },
                new SS2DObstacleArtEntry { x = 1, y = 3, art = SS2DObstacleArt.BushPlanter },
            };
            layout.cameras = new System.Collections.Generic.List<SS2DCameraArtEntry> { new SS2DCameraArtEntry { cameraId = "cam1", mount = SS2DCameraMountArt.StoneWall } };
            EditorUtility.SetDirty(layout);
            return layout;
        }

        static SS2DObstacleArtEntry B(int x, int y) => new SS2DObstacleArtEntry { x = x, y = y, art = SS2DObstacleArt.BushPlanter };
        static SS2DObstacleArtEntry W(int x, int y, SS2DWallAxis axis) => new SS2DObstacleArtEntry { x = x, y = y, art = SS2DObstacleArt.StoneWall, wallAxis = axis };

        /// <summary>Creates or updates `Art2D/LevelArt/SS2DArt_&lt;code&gt;.asset` (idempotent): X-cell art entries and stone-wall camera mounts for the given camera ids.</summary>
        public static SS2DLevelArtLayout Layout(string code, SS2DObstacleArtEntry[] obstacles, params string[] stoneMountCameraIds)
        {
            string path = LevelArtDir + "SS2DArt_" + code + ".asset";
            Directory.CreateDirectory(LevelArtDir);
            var layout = AssetDatabase.LoadAssetAtPath<SS2DLevelArtLayout>(path);
            if (layout == null) { layout = ScriptableObject.CreateInstance<SS2DLevelArtLayout>(); AssetDatabase.CreateAsset(layout, path); }
            layout.stageCode = code; layout.defaultObstacle = SS2DObstacleArt.BushPlanter;
            layout.obstacles = new System.Collections.Generic.List<SS2DObstacleArtEntry>(obstacles);
            layout.cameras = new System.Collections.Generic.List<SS2DCameraArtEntry>();
            foreach (var id in stoneMountCameraIds) layout.cameras.Add(new SS2DCameraArtEntry { cameraId = id, mount = SS2DCameraMountArt.StoneWall });
            EditorUtility.SetDirty(layout);
            return layout;
        }

        static SS2DCharacterSet Set(string[] files, string source)
        {
            var set = new SS2DCharacterSet { sourcePrefab = source, includesBase = true, walkFrameRate = 10f, idleFrameRate = 2f };
            for (int d = 0; d < 4; d++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesRoot + files[d]);
                if (sprite == null) Debug.LogError("[SS2D] final art sprite missing after import: " + files[d]);
                set.facings[d] = new SS2DDirectionFrames { idle = new[] { sprite }, walk = new[] { sprite } }; // one still per facing; walk frames can be added later
            }
            return set;
        }

        static void ConfigureCharacter(string path) => Configure(path, CharacterPpu, CharacterPivot, 1024, SpriteMeshType.Tight);
        static void ConfigureBackground(string path) => Configure(path, BackgroundPpu, new Vector2(0.5f, 0.5f), 2048, SpriteMeshType.FullRect);

        static void Configure(string path, int ppu, Vector2 pivot, int maxSize, SpriteMeshType mesh)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogError("[SS2D] not a texture: " + path); return; }
            var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
            bool same = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single && imp.alphaIsTransparency
                && imp.alphaSource == TextureImporterAlphaSource.FromInput && !imp.mipmapEnabled && imp.filterMode == FilterMode.Bilinear
                && imp.wrapMode == TextureWrapMode.Clamp && imp.sRGBTexture && imp.maxTextureSize == maxSize && imp.textureCompression == TextureImporterCompression.Compressed
                && Mathf.Approximately(imp.spritePixelsPerUnit, ppu) && settings.spriteAlignment == (int)SpriteAlignment.Custom
                && (settings.spritePivot - pivot).sqrMagnitude < 1e-6f && settings.spriteMeshType == mesh;
            if (same) return;
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true; imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true;
            imp.maxTextureSize = maxSize; imp.textureCompression = TextureImporterCompression.Compressed; imp.spritePixelsPerUnit = ppu;
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot; settings.spriteMeshType = mesh; settings.spriteExtrude = 1;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
    }
}
