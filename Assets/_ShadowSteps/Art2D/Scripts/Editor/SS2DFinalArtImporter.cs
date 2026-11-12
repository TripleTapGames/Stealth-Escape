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
        public const string BackgroundDir = SpritesRoot + "Backgrounds/";
        /// <summary>The one backdrop of the campaign (user art v2, 2026-11-05; user's call 2026-11-06: "use only the courtyard v2"). The museum and desert paintings in the same folder stay unused.</summary>
        public const string BackgroundPath = BackgroundDir + "Courtyard-Natural-Sides-1400x2000-v2.png";
        /// <summary>File stems in <see cref="Direction"/> order: North, East, South, West.</summary>
        static readonly string[] PlayerFiles = { "Characters/Player/Suit-Faceless-Up.png", "Characters/Player/Suit-Faceless-Right.png", "Characters/Player/Suit-Faceless-Down.png", "Characters/Player/Suit-Faceless-Left.png" };
        static readonly string[] GuardFiles = { "Characters/Guards/Blue-Guard-Faceless-Up.png", "Characters/Guards/Blue-Guard-Faceless-Right.png", "Characters/Guards/Blue-Guard-Faceless-Down.png", "Characters/Guards/Blue-Guard-Faceless-Left.png" };

        /// <summary>Character frames are 1024×2048; the whole frame height is one unit so the library's characterHeight is the drawn frame height in node spacings.</summary>
        const int CharacterPpu = 2048;
        /// <summary>Centre of the painted base disc, measured from the alpha of all eight frames (disc band rows 1110–1505 of 2048); the node sits under the disc centre.</summary>
        static readonly Vector2 CharacterPivot = new Vector2(0.5f, 0.363f);
        const int BackgroundPpu = 256;
        /// <summary>Courtyard v2 floor between the inner wall faces, normalized from the bottom-left (measured from the pixels: x 334–1036 of 1400, y 532–1686 of 2000 from the top; the v1 floor was x 388–992, y 576–1312).</summary>
        static readonly Rect CourtyardFloor = new Rect(0.240f, 0.157f, 0.499f, 0.577f);
        static readonly Color Water = new Color32(0x01, 0x6E, 0xC0, 0xFF);
        /// <summary>Backdrop themes (D-058) with their level-id ranges. Empty by the user's call (2026-11-06): the courtyard v2 above paints every level. Add entries here (name, PNG path, measured floor rect, first/last id, clear colour) to bring a second setting back; the museum and desert floors were measured as (0.321, 0.280, 0.357, 0.4475) and (0.279, 0.245, 0.443, 0.455).</summary>
        static readonly (string name, string path, Rect floor, int first, int last, Color clear)[] Themes =
        {
            // D-081 (levels 38-50 handoff, 2026-11-11): the four painted settings as campaign chapters by board tier. Ids 1-40 keep the courtyard v2 (the library's single background).
            ("Museum", BackgroundDir + "Museum-Sculpture-Court-Final-1400x2000-v6.png", new Rect(0.321f, 0.280f, 0.357f, 0.4475f), 41, 44, new Color(0.486f, 0.796f, 0.176f)),
            ("Desert", BackgroundDir + "Desert-Palace-Terrace-Final-1400x2000-v2.png", new Rect(0.279f, 0.245f, 0.443f, 0.455f), 45, 47, new Color(0.914f, 0.765f, 0.561f)),
            ("Courtyard (original)", BackgroundDir + "Courtyard-Natural-Sides-1400x2000.png", new Rect(0.277f, 0.344f, 0.432f, 0.368f), 48, 50, new Color(0.004f, 0.431f, 0.753f)),
        };
        public const string ObstacleDir = SpritesRoot + "Obstacles/";
        public const string BushPath = ObstacleDir + "obstacle bush.png", WallSidePath = ObstacleDir + "obstacle wall side.png", WallFrontPath = ObstacleDir + "obstacle wall front.png";
        /// <summary>Obstacle art is authored at 2048 px with pillar centres / the planter box ≈ 1000 px apart: 1000 px = one node spacing.</summary>
        const int ObstaclePpu = 1000;
        /// <summary>Pivots measured from the art (normalized, bottom-left): planter box centre; midpoint between the two pillar caps of the side piece; centre of the front piece's top face between its pillars.</summary>
        static readonly Vector2 BushPivot = new Vector2(0.515f, 0.502f), WallSidePivot = new Vector2(0.50f, 0.60f), WallFrontPivot = new Vector2(0.518f, 0.61f);
        public const string LevelArtDir = "Assets/_ShadowSteps/Art2D/LevelArt/";
        public const string CastShadowPath = SpritesRoot + "Characters/character_shadow.png";
        public const string RotationArrowPath = SpritesRoot + "Markers/rotation_arrow.png";
        /// <summary>Exit marker art (user, 2026-11-06): the green "home" disc and its glow ring, both 2048² with the shape centred.</summary>
        public const string HomePath = SpritesRoot + "Markers/Home.png", HomeGlowPath = SpritesRoot + "Markers/Home glow.png";
        public static bool HasHomeArt() => File.Exists(HomePath) && File.Exists(HomeGlowPath);
        /// <summary>Guard facing arrow (user, 2026-11-06): dark arrowhead pointing right, 2048², drawn from under the base disc rim (D-071).</summary>
        public const string FacingArrowPath = SpritesRoot + "Markers/cap_arrow.png";
        /// <summary>Locked-edge door art (user, 2026-11-06): the padlocked gate and the open gate, 2048², same frame footprint; pivot at the frame's base centre (measured from the solid pixels: x 632–1470, feet at y 1380 from the top).</summary>
        public const string DoorLockedPath = SpritesRoot + "Door locked.png", DoorOpenPath = SpritesRoot + "Door opened.png";
        static readonly Vector2 DoorPivot = new Vector2(0.513f, 0.326f);
        public static bool HasDoorArt() => File.Exists(DoorLockedPath) && File.Exists(DoorOpenPath);
        public const string UiDir = SpritesRoot + "_UI/";
        public static bool HasUiArt() => File.Exists(UiDir + "round button.png") && File.Exists(UiDir + "Level_footsteps_stars.png") && File.Exists(UiDir + "info box.png");
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
            lib.backgroundThemes = ImportThemes(lib.backgroundThemes);
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
            if (File.Exists(RotationArrowPath))
            {
                // User art (2026-11-04): the curved arrow drawn twice around a rotating guard, as in the reference mock.
                Configure(RotationArrowPath, CharacterPpu, new Vector2(0.5f, 0.5f), 1024, SpriteMeshType.Tight);
                AssetDatabase.Refresh();
                lib.rotationArrowArt = AssetDatabase.LoadAssetAtPath<Sprite>(RotationArrowPath);
            }
            if (HasDoorArt())
            {
                // Locked-edge doors (D-067): the gate stands upright on the edge midpoint; frame ≈ 0.41 unit wide, drawn ≈ 0.6 spacing at lockSize 1.5.
                Configure(DoorLockedPath, CharacterPpu, DoorPivot, 1024, SpriteMeshType.Tight);
                Configure(DoorOpenPath, CharacterPpu, DoorPivot, 1024, SpriteMeshType.Tight);
                AssetDatabase.Refresh();
                lib.doorLocked = AssetDatabase.LoadAssetAtPath<Sprite>(DoorLockedPath);
                lib.doorOpen = AssetDatabase.LoadAssetAtPath<Sprite>(DoorOpenPath);
                lib.lockSize = 1.5f;
            }
            if (File.Exists(FacingArrowPath))
            {
                Configure(FacingArrowPath, CharacterPpu, new Vector2(0.5f, 0.5f), 512, SpriteMeshType.Tight);
                AssetDatabase.Refresh();
                lib.facingArrow = AssetDatabase.LoadAssetAtPath<Sprite>(FacingArrowPath);
                lib.facingArrowSize = 0.45f; lib.facingArrowOffset = 0.26f;
            }
            if (HasHomeArt())
            {
                // Exit "home" marker (D-063): disc + glow ring, pivot at the centre, 2048 px = 1 unit; the renderer scales by exitHomeSize.
                Configure(HomePath, CharacterPpu, new Vector2(0.5f, 0.5f), 1024, SpriteMeshType.Tight);
                Configure(HomeGlowPath, CharacterPpu, new Vector2(0.5f, 0.5f), 1024, SpriteMeshType.FullRect);
                AssetDatabase.Refresh();
                lib.exitHome = AssetDatabase.LoadAssetAtPath<Sprite>(HomePath);
                lib.exitHomeGlow = AssetDatabase.LoadAssetAtPath<Sprite>(HomeGlowPath);
            }
            if (HasUiArt())
            {
                // HUD skin (user art, 2026-11-05): each PNG is a 1024² canvas with a lot of transparent margin, so the sprite rect is trimmed to the
                // visible pixels; pills get round-cap 9-slice borders (half the visible height) so they stretch to any text width.
                lib.uiPill = ConfigureTrimmed(UiDir + "Level_footsteps_stars.png", "ui_pill", 0.5f);
                lib.uiPillSlim = ConfigureTrimmed(UiDir + "info box.png", "ui_pill_slim", 0.5f);
                lib.uiRoundButton = ConfigureTrimmed(UiDir + "round button.png", "ui_round_button", 0f);
                lib.uiPauseIcon = ConfigureTrimmed(UiDir + "Pause lines.png", "ui_pause", 0f);
                lib.uiSmokeIcon = ConfigureTrimmed(UiDir + "Smoke.png", "ui_smoke", 0f);
                lib.uiEmpIcon = ConfigureTrimmed(UiDir + "Emp.png", "ui_emp", 0f);
                lib.uiStarIcon = ConfigureTrimmed(UiDir + "Star.png", "ui_star", 0f);
                lib.uiFootIcon = ConfigureTrimmed(UiDir + "foot icon.png", "ui_foot", 0f);
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
                    Layout("LEVEL-30", new[] { B(4, 2) }, "cam1", "cam2"),
                    // D-081: levels 38-50 handoff (codes LEVEL-33..45). Planters and stone pieces on the authored X cells, stone mounts on perimeter cameras, poles on the two interior wall cameras.
                    Layout("LEVEL-33", new[] { B(4, 4), W(6, 6, SS2DWallAxis.LogicalX) }),
                    Layout("LEVEL-34", new[] { B(6, 4), W(8, 6, SS2DWallAxis.LogicalY) }, "cam1"),
                    Layout("LEVEL-35", new[] { W(6, 4, SS2DWallAxis.LogicalX), B(8, 6) }, "cam1"),
                    Layout("LEVEL-36", new[] { B(8, 4), W(6, 6, SS2DWallAxis.LogicalX) }, "cam1"),
                    Layout("LEVEL-37", new[] { B(6, 4), W(8, 6, SS2DWallAxis.LogicalY), B(10, 8) }, "cam1"),
                    Layout("LEVEL-38", new[] { W(8, 4, SS2DWallAxis.LogicalX), B(6, 6) }, "cam1"),
                    Layout("LEVEL-39", new[] { B(6, 4), W(8, 6, SS2DWallAxis.LogicalX) }, "cam1", "cam2"),
                    Layout("LEVEL-40", new[] { B(6, 4), W(10, 6, SS2DWallAxis.LogicalY), B(4, 8) }, "cam1", "cam2"),
                    LayoutWithPoles("LEVEL-41", new[] { W(6, 4, SS2DWallAxis.LogicalX), B(8, 6) }, new[] { "cam2" }, new[] { "cam1" }),
                    Layout("LEVEL-42", new[] { B(8, 4), W(6, 6, SS2DWallAxis.LogicalX) }, "cam1", "cam2"),
                    Layout("LEVEL-43", new[] { W(6, 6, SS2DWallAxis.LogicalX), B(10, 6), B(4, 8) }, "cam1", "cam2"),
                    Layout("LEVEL-44", new[] { B(6, 6), W(10, 6, SS2DWallAxis.LogicalY), B(4, 8) }, "cam1", "cam2", "cam3"),
                    LayoutWithPoles("LEVEL-45", new[] { W(8, 4, SS2DWallAxis.LogicalX), B(6, 6), W(10, 6, SS2DWallAxis.LogicalY) }, new[] { "cam2", "cam3" }, new[] { "cam1" }) };
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[SS2D] final art imported: background " + lib.backgroundSprite.name + ", spy " + (lib.spy.IsComplete ? "4/4" : "incomplete") + ", guard " + (lib.guard.IsComplete ? "4/4" : "incomplete"));
            return true;
        }

        /// <summary>LEVEL-19 "Sweep Window" decoration (D-074 board: no cover cells; the 3-facing sweep camera on the bottom wall (3,6) sits on a stone mount). Before D-074: the X beside the camera wall (4,1) was a stone wall stub running off the boundary (logical Y), the standalone X at (1,3) a bush planter, the camera on a stone wall piece.</summary>
        public static SS2DLevelArtLayout Level19Layout()
        {
            string path = LevelArtDir + "SS2DArt_LEVEL-19.asset";
            Directory.CreateDirectory(LevelArtDir);
            var layout = AssetDatabase.LoadAssetAtPath<SS2DLevelArtLayout>(path);
            if (layout == null) { layout = ScriptableObject.CreateInstance<SS2DLevelArtLayout>(); AssetDatabase.CreateAsset(layout, path); }
            layout.stageCode = "LEVEL-19"; layout.defaultObstacle = SS2DObstacleArt.BushPlanter;
            layout.obstacles = new System.Collections.Generic.List<SS2DObstacleArtEntry>(); // D-074: the redesigned board has no X cells (entries are only read for authored X cells anyway)
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

        /// <summary>As <see cref="Layout"/>, plus pole mounts for cameras on interior wall cells (the art has its own pedestal).</summary>
        public static SS2DLevelArtLayout LayoutWithPoles(string code, SS2DObstacleArtEntry[] obstacles, string[] stoneMountCameraIds, string[] poleMountCameraIds)
        {
            var layout = Layout(code, obstacles, stoneMountCameraIds);
            foreach (var id in poleMountCameraIds) layout.cameras.Add(new SS2DCameraArtEntry { cameraId = id, mount = SS2DCameraMountArt.Pole });
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

        /// <summary>
        /// Trims a UI PNG to its alpha bounding box (plus a 2 px margin) into a generated copy under _UI/Trimmed/ and imports THAT as a plain
        /// single sprite (stable fileID 21300000 on every machine; Multiple-mode sub-sprite IDs are not), with an optional 9-slice border
        /// expressed as a fraction of the trimmed height (0.5 = round caps on a pill). The user's original PNG is never modified.
        /// </summary>
        static Sprite ConfigureTrimmed(string path, string spriteName, float borderHeightFraction)
        {
            if (!File.Exists(path)) { Debug.LogError("[SS2D] missing UI art: " + path); return null; }
            string dir = UiDir + "Trimmed/"; string outPath = dir + spriteName + ".png";
            Directory.CreateDirectory(dir);
            var png = new Texture2D(2, 2, TextureFormat.RGBA32, false); png.LoadImage(File.ReadAllBytes(path));
            var px = png.GetPixels32(); int w = png.width, h = png.height, minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (px[y * w + x].a > 8) { if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y; }
            if (maxX < 0) { Object.DestroyImmediate(png); Debug.LogError("[SS2D] empty UI sprite: " + path); return null; }
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
            if (imp == null) { Debug.LogError("[SS2D] trimmed UI sprite did not import: " + outPath); return null; }
            float b = Mathf.Floor(th * borderHeightFraction);
            var border = borderHeightFraction > 0f ? new Vector4(b, 0f, b, 0f) : Vector4.zero;
            var settings = new TextureImporterSettings(); imp.ReadTextureSettings(settings);
            bool same = imp.textureType == TextureImporterType.Sprite && imp.spriteImportMode == SpriteImportMode.Single && imp.alphaIsTransparency && !imp.mipmapEnabled
                && imp.maxTextureSize == 1024 && Mathf.Approximately(imp.spritePixelsPerUnit, 100f) && settings.spriteMeshType == SpriteMeshType.FullRect && (imp.spriteBorder - border).sqrMagnitude < 1e-6f
                && settings.spriteAlignment == (int)SpriteAlignment.Center;
            if (!same)
            {
                imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true; imp.alphaSource = TextureImporterAlphaSource.FromInput; imp.mipmapEnabled = false; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
                imp.sRGBTexture = true; imp.maxTextureSize = 1024; imp.textureCompression = TextureImporterCompression.Compressed; imp.spritePixelsPerUnit = 100; imp.spriteBorder = border;
                imp.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(0.5f, 0.5f); settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteExtrude = 1; imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            // The original stays a plain single sprite (no stray sub-sprites from earlier Multiple-mode imports).
            var orig = AssetImporter.GetAtPath(path) as TextureImporter;
            if (orig != null && orig.spriteImportMode == SpriteImportMode.Multiple) { orig.spriteImportMode = SpriteImportMode.Single; orig.SaveAndReimport(); }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath);
            if (sprite == null) Debug.LogError("[SS2D] no sprite at " + outPath);
            return sprite;
        }

        static void ConfigureBackground(string path) => Configure(path, BackgroundPpu, new Vector2(0.5f, 0.5f), 2048, SpriteMeshType.FullRect);

        /// <summary>
        /// Imports every backdrop PNG that exists (same importer settings as the default background) and returns the theme list with the
        /// measured floor rects. A level-id range the user already edited on a theme with the same sprite is kept; a theme whose PNG is
        /// missing is dropped.
        /// </summary>
        static SS2DBackgroundTheme[] ImportThemes(SS2DBackgroundTheme[] existing)
        {
            var list = new System.Collections.Generic.List<SS2DBackgroundTheme>();
            foreach (var t in Themes)
            {
                if (!File.Exists(t.path)) continue;
                ConfigureBackground(t.path);
                AssetDatabase.Refresh();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(t.path);
                if (sprite == null) { Debug.LogError("[SS2D] no sprite at " + t.path); continue; }
                var theme = new SS2DBackgroundTheme { name = t.name, sprite = sprite, boardRect = t.floor, firstLevelId = t.first, lastLevelId = t.last, clearColor = t.clear };
                if (existing != null) foreach (var e in existing) if (e != null && e.sprite == sprite && (e.firstLevelId != 0 || e.lastLevelId != 0)) { theme.firstLevelId = e.firstLevelId; theme.lastLevelId = e.lastLevelId; }
                list.Add(theme);
            }
            return list.ToArray();
        }

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
