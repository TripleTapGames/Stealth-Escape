using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>Per-stage cosmetic choices for the sunlit theme (deterministic from the stage id; never touches gameplay).</summary>
    public struct SunlitVariant
    {
        public bool waterfallNiches, benches, cornerTrees, flowerBeds, extraLanterns, pinkFlowers;
        /// <summary>First10 manifest choices for stage ids 1-10; later stages get a stable hash-based mix.</summary>
        public static SunlitVariant For(int stageId)
        {
            var v = new SunlitVariant();
            switch (stageId)
            {
                case 1: v.benches = true; v.flowerBeds = true; break;                                   // Entrance Walk: flowerbeds beyond side rails
                case 2: v.extraLanterns = true; break;                                                  // Side Passage: stepped balustrade + wall lanterns
                case 3: v.cornerTrees = true; v.extraLanterns = true; break;                            // Guard Court: side canal + two tree clusters
                case 4: v.flowerBeds = true; break;                                                     // Clockwise Pavilion: gold-trimmed facade, banners, lanterns
                case 5: v.benches = true; v.waterfallNiches = true; break;                              // Patrol Terrace: bench group + water cascade
                case 6: v.flowerBeds = true; break;                                                     // Side Garden: marble surround + water channel
                case 7: v.benches = true; v.pinkFlowers = true; v.cornerTrees = true; break;            // Clockwise: red carpet, pink flowers beyond walls
                case 8: v.extraLanterns = true; break;                                                  // One Step Ahead: canal arcade, low wall pillars
                case 9: v.waterfallNiches = true; v.extraLanterns = true; v.cornerTrees = true; break;  // Double Trouble: symmetrical lanterns, waterfall niches
                case 10: v.cornerTrees = true; v.flowerBeds = true; v.benches = true; break;            // Corner Patrol: corner trees, pool edges, planters
                default:
                    int h = stageId * 2654435761u.GetHashCode();
                    v.waterfallNiches = (h & 1) != 0; v.benches = (h & 2) != 0; v.cornerTrees = (h & 4) != 0; v.flowerBeds = (h & 8) != 0; v.extraLanterns = (h & 16) != 0; v.pinkFlowers = (h & 32) != 0;
                    break;
            }
            return v;
        }
    }

    public sealed partial class SS3DCourtyardFrame
    {
        /// <summary>
        /// SunlitCourtyard theme (First10 art direction): ivory stone platform with a bevelled plinth, low parapet with
        /// pilasters and ball-topped corner piers, benches, a symmetric entrance façade (arched wooden doors, lanterns,
        /// blue/gold banners, optional waterfall niches), a red carpet OUTSIDE the grid, a turquoise moat all around with
        /// stone kerbs, a bridge to the gate, a staircase with ball-topped newels to a path, and grouped greenery
        /// beyond the water. Kit assets are used only for foliage, benches fall back to the starter bench. Small boards
        /// get a proportionally larger platform so the architecture never shrinks to the grid.
        /// </summary>
        private void BuildSunlit(SS3DLevelRenderer board)
        {
            var stage = board.Stage;
            float s = board.tileSpacing;
            var variant = SunlitVariant.For(stage.Id);
            float gx0 = board.ToWorld(0, 0).x - 0.5f * s, gx1 = board.ToWorld(stage.Width - 1, 0).x + 0.5f * s;
            float gz1 = board.ToWorld(0, 0).z + 0.5f * s, gz0 = board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
            // Open floor between the grid and the parapet on every side (reference look), never less than a 6 × 4 platform.
            float margin = palette.sunlitMarginTiles * s;
            float borderX = Mathf.Max(margin, (6f * s - (gx1 - gx0)) * 0.5f), borderZ = Mathf.Max(margin, (4f * s - (gz1 - gz0)) * 0.5f);
            float px0 = gx0 - borderX, px1 = gx1 + borderX, pz0 = gz0 - borderZ, pz1 = gz1 + borderZ;
            float middle = (px0 + px1) * 0.5f, centreZ = (pz0 + pz1) * 0.5f;
            float left = px0 + 0.3f * s, right = px1 - 0.3f * s, north = pz1 - 0.3f * s, south = pz0 + 0.3f * s;
            float drop = -terraceDrop;
            float ft = palette.floorTop;

            var corners = new Vector3[4];
            bool hasCorners = fitter != null && fitter.VisibleGroundCorners(fitter.designAspect, ft, corners);
            Rect visible = hasCorners ? fitter.VisibleGroundRect(fitter.designAspect, ft) : Rect.MinMaxRect(px0 - 4f * s, pz0 - 6f * s, px1 + 4f * s, pz1 + 6f * s);
            float screenBottomZ = hasCorners ? Mathf.Min(corners[0].z, corners[1].z) : pz0 - 5f * s;
            float screenTopZ = hasCorners ? Mathf.Max(corners[2].z, corners[3].z) : pz1 + 5f * s;
            float farWest = visible.xMin - 2f * s, farEast = visible.xMax + 2f * s, farNorth = visible.yMax + 2f * s, farSouth = visible.yMin - 2f * s;

            // ---- 1. Ground (grass) and water: one moat ring around the platform, outer kerb, water texture.
            if (palette.kitGround != null)
            {
                var g = Place(palette.kitGround, "Ground_Plane", Vector3.zero);
                SS3DKitFit.Fit(g, new Vector3((farWest + farEast) * 0.5f, drop - 0.26f, (farNorth + farSouth) * 0.5f), farEast - farWest, 0.16f, farNorth - farSouth, false);
                SS3DArtTuning.Tint(g, SS3DTint.Foliage, palette);
            }
            else Box("Ground_Plane", new Vector3((farWest + farEast) * 0.5f, drop - 0.26f, (farNorth + farSouth) * 0.5f), farEast - farWest, 0.16f, farNorth - farSouth, 0f, palette.foliageGreen * 0.85f);
            float plinthHalfW = (px1 - px0) * 0.5f + 0.4f * s, plinthHalfD = (pz1 - pz0) * 0.5f + 0.4f * s;
            float moatW = 1.0f * s, kerb = 0.14f * s, waterTop = drop - 0.08f;
            WaterBox("Moat", new Vector3(middle, waterTop - 0.06f, centreZ), 2f * (plinthHalfW + moatW), 0.06f, 2f * (plinthHalfD + moatW));
            float ox = plinthHalfW + moatW, oz = plinthHalfD + moatW;
            Box("Moat_Kerb_W", new Vector3(middle - ox - kerb * 0.5f, drop - 0.12f, centreZ), kerb, 0.14f, 2f * oz + 2f * kerb, 0.02f, palette.plinthStone);
            Box("Moat_Kerb_E", new Vector3(middle + ox + kerb * 0.5f, drop - 0.12f, centreZ), kerb, 0.14f, 2f * oz + 2f * kerb, 0.02f, palette.plinthStone);
            Box("Moat_Kerb_S", new Vector3(middle, drop - 0.12f, centreZ - oz - kerb * 0.5f), 2f * ox + 2f * kerb, 0.14f, kerb, 0.02f, palette.plinthStone);
            Box("Moat_Kerb_N", new Vector3(middle, drop - 0.12f, centreZ + oz + kerb * 0.5f), 2f * ox + 2f * kerb, 0.14f, kerb, 0.02f, palette.plinthStone);
            // the plinth continues down into the water
            Box("Platform_Plinth_Lower", new Vector3(middle, drop - 0.30f, centreZ), 2f * plinthHalfW, 0.30f, 2f * plinthHalfD, 0.06f, palette.warmStone);
            Box("Platform_Plinth_Upper", new Vector3(middle, drop, centreZ), (px1 - px0) + 0.3f * s, terraceDrop + 0.005f, (pz1 - pz0) + 0.3f * s, 0.08f, palette.plinthStone);

            // ---- 2. Platform floor: stone tiles over the whole border ring (never on the grid), grout underneath, ivory trim frame.
            FillTiles(board, px0, gx0, pz0, pz1, "Terrace_W"); FillTiles(board, gx1, px1, pz0, pz1, "Terrace_E");
            FillTiles(board, gx0, gx1, gz1, pz1, "Terrace_N"); FillTiles(board, gx0, gx1, pz0, gz0, "Terrace_S");
            Box("Terrace_Grout", new Vector3(middle, -0.012f, centreZ), (px1 - px0) + 0.1f * s, ft - 0.022f, (pz1 - pz0) + 0.1f * s, 0f, palette.groutColor);
            float trimH = ft + 0.03f;
            Box("Trim_N", new Vector3(middle, 0f, north - 0.22f * s), (right - left) - 0.6f * s, trimH, 0.08f * s, 0.02f, palette.trimIvory);
            Box("Trim_S", new Vector3(middle, 0f, south + 0.22f * s), (right - left) - 0.6f * s, trimH, 0.08f * s, 0.02f, palette.trimIvory);
            Box("Trim_W", new Vector3(left + 0.22f * s, 0f, centreZ), 0.08f * s, trimH, (north - south) - 0.6f * s, 0.02f, palette.trimIvory);
            Box("Trim_E", new Vector3(right - 0.22f * s, 0f, centreZ), 0.08f * s, trimH, (north - south) - 0.6f * s, 0.02f, palette.trimIvory);

            // ---- 3. Parapet: low ivory course + cap + pilasters, corner piers with ball finials, gaps on the axis for door and stairs.
            float railY = 0.46f;
            void Run(string name, Vector3 a, Vector3 b, bool gap)
            {
                var dir = b - a; float len = dir.magnitude; dir.Normalize(); bool alongX = Mathf.Abs(dir.x) > 0.5f;
                if (gap) { float half = (len - 1.4f * s) * 0.5f; Run(name + "_A", a, a + dir * half, false); Run(name + "_B", b - dir * half, b, false); return; }
                var mid = (a + b) * 0.5f;
                Box(name + "_Base", mid, alongX ? len : 0.30f * s, railY, alongX ? 0.30f * s : len, 0f, palette.wallIvory);
                Box(name + "_Cap", new Vector3(mid.x, railY, mid.z), (alongX ? len : 0.42f * s) + 0.02f, 0.10f, (alongX ? 0.42f * s : len) + 0.02f, 0.035f, palette.trimIvory);
                for (float d = 1.0f * s; d < len - 0.6f * s; d += 2f * s) { var pp = a + dir * d; Box(name + "_Pilaster", new Vector3(pp.x, 0f, pp.z), 0.42f * s, railY + 0.18f, 0.42f * s, 0.05f, palette.wallIvory); }
            }
            Run("Parapet_North", new Vector3(left, 0f, north), new Vector3(right, 0f, north), true);
            Run("Parapet_South", new Vector3(left, 0f, south), new Vector3(right, 0f, south), true);
            Run("Parapet_West", new Vector3(left, 0f, south), new Vector3(left, 0f, north), false);
            Run("Parapet_East", new Vector3(right, 0f, south), new Vector3(right, 0f, north), false);
            foreach (var (n, cx, cz) in new[] { ("NW", left, north), ("NE", right, north), ("SW", left, south), ("SE", right, south) })
            {
                Box("Pier_" + n, new Vector3(cx, 0f, cz), 0.60f * s, railY + 0.34f, 0.60f * s, 0.08f, palette.wallIvory);
                Box("Pier_Cap_" + n, new Vector3(cx, railY + 0.34f, cz), 0.52f * s, 0.12f, 0.52f * s, 0.05f, palette.trimIvory);
                Ball("Pier_Ball_" + n, new Vector3(cx, railY + 0.46f + 0.17f * s, cz), 0.34f * s, palette.trimIvory);
            }
            if (variant.benches)
                foreach (float bz in new[] { centreZ - 1.6f * s, centreZ + 1.6f * s })
                {
                    if (bz < south + 0.8f * s || bz > north - 0.8f * s) continue;
                    Wood(palette.bench, "Bench_W", new Vector3(left + 0.42f * s, ft, bz), Quaternion.Euler(0f, 90f, 0f), 1.1f * s);
                    Wood(palette.bench, "Bench_E", new Vector3(right - 0.42f * s, ft, bz), Quaternion.Euler(0f, -90f, 0f), 1.1f * s);
                }

            // ---- 4. Entrance façade on the axis beyond the moat: plinth, wall, cornice, door bay with arch + wooden doors, lanterns, banners, niches.
            float facadeZ = pz1 + 0.4f * s + moatW + 1.1f * s; // behind the north kerb
            float facadeW = (px1 - px0) + 1.4f * s, fy = drop + 0.30f;
            Box("Facade_Plinth", new Vector3(middle, drop - 0.1f, facadeZ + 0.1f * s), facadeW + 0.3f * s, 0.40f, 1.6f * s, 0.05f, palette.plinthStone);
            Box("Facade_Wall", new Vector3(middle, fy, facadeZ + 0.25f * s), facadeW, 2.0f, 1.1f * s, 0.06f, palette.wallIvory);
            Box("Facade_Cornice", new Vector3(middle, fy + 2.0f, facadeZ + 0.25f * s), facadeW + 0.25f * s, 0.22f, 1.3f * s, 0.08f, palette.trimIvory);
            Box("Facade_Attic", new Vector3(middle, fy + 2.22f, facadeZ + 0.45f * s), Mathf.Min(facadeW * 0.55f, 4.2f * s), 0.5f, 0.75f * s, 0.06f, palette.wallIvory);
            Box("Door_Bay", new Vector3(middle, fy, facadeZ - 0.40f * s), 2.3f * s, 1.85f, 0.5f * s, 0.05f, palette.plinthStone);
            Box("Door_Lintel", new Vector3(middle, fy + 1.85f, facadeZ - 0.45f * s), 2.6f * s, 0.24f, 0.6f * s, 0.07f, palette.trimIvory);
            Stone(palette.archFrame, "Palace_Arch", new Vector3(middle, fy, facadeZ - 0.58f * s), SS3DTint.Stone);
            Wood(palette.palaceDoor, "Palace_Doors", new Vector3(middle, fy, facadeZ - 0.54f * s));
            Box("Pilaster_L", new Vector3(middle - 1.5f * s, fy, facadeZ - 0.5f * s), 0.40f * s, 2.0f, 0.40f * s, 0.05f, palette.trimIvory);
            Box("Pilaster_R", new Vector3(middle + 1.5f * s, fy, facadeZ - 0.5f * s), 0.40f * s, 2.0f, 0.40f * s, 0.05f, palette.trimIvory);
            Place(palette.lantern, "Lantern_L", new Vector3(middle - 1.05f * s, fy + 0.95f, facadeZ - 0.85f * s));
            Place(palette.lantern, "Lantern_R", new Vector3(middle + 1.05f * s, fy + 0.95f, facadeZ - 0.85f * s));
            Place(palette.banner, "Banner_L", new Vector3(middle - 2.3f * s, fy + 0.45f, facadeZ - 0.62f * s));
            Place(palette.banner, "Banner_R", new Vector3(middle + 2.3f * s, fy + 0.45f, facadeZ - 0.62f * s));
            if (variant.waterfallNiches)
                foreach (var (n, nx) in new[] { ("L", middle - (facadeW * 0.5f - 1.0f * s)), ("R", middle + (facadeW * 0.5f - 1.0f * s)) })
                {
                    if (Mathf.Abs(nx - middle) < 2.9f * s) continue;
                    Box("Niche_Post_" + n + "1", new Vector3(nx - 0.6f * s, fy, facadeZ - 0.55f * s), 0.22f * s, 1.5f, 0.3f * s, 0.03f, palette.trimIvory);
                    Box("Niche_Post_" + n + "2", new Vector3(nx + 0.6f * s, fy, facadeZ - 0.55f * s), 0.22f * s, 1.5f, 0.3f * s, 0.03f, palette.trimIvory);
                    Box("Niche_Lintel_" + n, new Vector3(nx, fy + 1.5f, facadeZ - 0.55f * s), 1.42f * s, 0.22f, 0.34f * s, 0.05f, palette.trimIvory);
                    WaterBox("Waterfall_" + n, new Vector3(nx, fy, facadeZ - 0.45f * s), 0.9f * s, 1.45f, 0.08f * s);
                    WaterBox("Waterfall_Pool_" + n, new Vector3(nx, drop - 0.09f, facadeZ - 1.05f * s), 1.3f * s, 0.06f, 0.9f * s);
                    Box("Waterfall_Pool_Kerb_" + n, new Vector3(nx, drop - 0.12f, facadeZ - 1.55f * s), 1.5f * s, 0.14f, 0.12f * s, 0.02f, palette.plinthStone);
                }
            // steps from the façade plinth down to the forecourt, bridge over the moat, carpet from the doors to the parapet gap (outside the grid).
            for (int i = 0; i < 3; i++) Box("Entrance_Step_" + i, new Vector3(middle, fy - (i + 1) * 0.10f, facadeZ - 0.9f * s - i * 0.26f * s), (1.9f + i * 0.3f) * s, 0.10f, 0.28f * s, 0.02f, palette.plinthStone);
            float forecourtZ0 = centreZ + oz + kerb, forecourtZ1 = facadeZ - 0.55f * s;
            Box("Forecourt", new Vector3(middle, drop - 0.12f, (forecourtZ0 + forecourtZ1) * 0.5f), facadeW + 0.6f * s, 0.12f, Mathf.Max(0.3f, forecourtZ1 - forecourtZ0), 0.02f, palette.warmStone);
            Box("North_Bridge", new Vector3(middle, drop - 0.1f, (centreZ + plinthHalfD - 0.2f * s + forecourtZ0) * 0.5f), 1.5f * s, 0.12f, forecourtZ0 - (centreZ + plinthHalfD - 0.2f * s), 0.03f, palette.plinthStone);
            Box("Entrance_Step_Terrace_0", new Vector3(middle, -0.12f, pz1 + 0.12f * s), 1.5f * s, 0.12f, 0.28f * s, 0.02f, palette.plinthStone);
            Box("Entrance_Step_Terrace_1", new Vector3(middle, -0.24f, pz1 + 0.40f * s), 1.5f * s, 0.12f, 0.28f * s, 0.02f, palette.plinthStone);
            Carpet("Carpet_Entrance", new Vector3(middle, drop + 0.03f, (pz1 + 0.55f * s + facadeZ - 1.0f * s) * 0.5f), 0.9f * s, Mathf.Max(0.4f, facadeZ - 1.0f * s - (pz1 + 0.55f * s)));
            Carpet("Carpet_Gap_N", new Vector3(middle, ft + 0.012f, (gz1 + pz1) * 0.5f), 0.9f * s, Mathf.Max(0.3f, pz1 - gz1 - 0.05f * s)); // on the border strip only, never on a grid node

            // ---- 5. South: landing, four ivory steps bridging the moat, cheek walls, ball-topped newels, path, flower beds.
            float landingDepth = (pz0 - (centreZ - oz - kerb)) + 0.2f * s;
            Box("Stair_Landing", new Vector3(middle, drop - 0.30f, pz0 - landingDepth * 0.5f), 3.4f * s, 0.30f + terraceDrop, landingDepth, 0.05f, palette.plinthStone);
            const int steps = 4; float stepRise = terraceDrop / steps, stepDepth = 0.32f * s, stairStart = pz0 - landingDepth;
            for (int i = 0; i < steps; i++)
            {
                float top = -(i + 1) * stepRise, z = stairStart - stepDepth * (i + 0.5f);
                Box("Entry_Step_" + i, new Vector3(middle, drop - 0.14f, z), 2.6f * s, top - (drop - 0.14f), stepDepth + 0.01f, 0.02f, palette.plinthStone);
            }
            float stairEnd = stairStart - stepDepth * steps;
            Box("Stair_Cheek_L", new Vector3(middle - 1.5f * s, drop - 0.14f, (pz0 + stairEnd) * 0.5f), 0.30f * s, terraceDrop + 0.24f, pz0 - stairEnd, 0.04f, palette.wallIvory);
            Box("Stair_Cheek_R", new Vector3(middle + 1.5f * s, drop - 0.14f, (pz0 + stairEnd) * 0.5f), 0.30f * s, terraceDrop + 0.24f, pz0 - stairEnd, 0.04f, palette.wallIvory);
            foreach (var (n, cx, cz) in new[] { ("L1", middle - 1.5f * s, pz0 - 0.1f * s), ("R1", middle + 1.5f * s, pz0 - 0.1f * s), ("L2", middle - 1.5f * s, stairEnd + 0.1f * s), ("R2", middle + 1.5f * s, stairEnd + 0.1f * s) })
            {
                float hN = n.EndsWith("1") ? railY + 0.30f : terraceDrop + 0.30f;
                float baseY = n.EndsWith("1") ? 0f : drop - 0.14f;
                Box("Newel_" + n, new Vector3(cx, baseY, cz), 0.54f * s, hN, 0.54f * s, 0.07f, palette.wallIvory);
                Box("Newel_Cap_" + n, new Vector3(cx, baseY + hN, cz), 0.46f * s, 0.10f, 0.46f * s, 0.05f, palette.trimIvory);
                Ball("Newel_Ball_" + n, new Vector3(cx, baseY + hN + 0.10f + 0.15f * s, cz), 0.30f * s, palette.trimIvory);
                if (variant.extraLanterns && n.EndsWith("1")) Place(palette.lantern, "Newel_Lantern_" + n, new Vector3(cx + (cx < middle ? -0.2f : 0.2f) * s, baseY + hN * 0.6f, cz - 0.2f * s));
            }
            float apronZ0 = centreZ - oz - kerb, apronZ1 = stairEnd - 0.5f * s;
            Box("South_Apron", new Vector3(middle, drop - 0.12f, (apronZ0 + apronZ1) * 0.5f), 2f * ox + 2f * kerb + 1.2f * s, 0.12f, apronZ0 - apronZ1, 0.02f, palette.warmStone);
            float pathEnd = Mathf.Min(apronZ1 - 1f * s, screenBottomZ - 1f * s);
            Box("South_Path", new Vector3(middle, drop - 0.1f, (apronZ1 + 0.1f * s + pathEnd) * 0.5f), 1.7f * s, 0.10f, apronZ1 + 0.1f * s - pathEnd, 0.02f, palette.plinthStone);
            if (variant.flowerBeds || variant.pinkFlowers)
                foreach (var (n, fx) in new[] { ("L", middle - 2.9f * s), ("R", middle + 2.9f * s) })
                {
                    Box("FlowerBed_" + n, new Vector3(fx, drop - 0.1f, stairEnd - 0.1f * s), 1.5f * s, 0.26f, 0.8f * s, 0.03f, palette.trimIvory);
                    Planting(palette.kitBush != null ? palette.kitBush : palette.flowerbed, "FlowerBed_Plants_" + n, new Vector3(fx, drop + 0.16f, stairEnd - 0.1f * s), 1.3f * s, 0.5f);
                }

            // ---- 6. Greenery beyond the water: grouped trees at the four outer corners, bushes along the lower banks, hedges flanking the path. Nothing inside the platform.
            if (decorateWithOutsidePlants)
            {
                float bankX = ox + kerb + 0.9f * s;
                if (variant.cornerTrees && palette.kitTree != null)
                {
                    Tree("Tree_NW", new Vector3(middle - (facadeW * 0.5f + 0.9f * s), drop - 0.1f, facadeZ + 0.6f * s), 2.6f);
                    Tree("Tree_NE", new Vector3(middle + (facadeW * 0.5f + 0.9f * s), drop - 0.1f, facadeZ + 0.6f * s), 2.6f);
                    Tree("Tree_SW", new Vector3(middle - bankX - 0.4f * s, drop - 0.1f, apronZ1 - 0.6f * s), 2.2f);
                    Tree("Tree_SE", new Vector3(middle + bankX + 0.4f * s, drop - 0.1f, apronZ1 - 0.6f * s), 2.2f);
                }
                if (palette.kitTree != null)
                    foreach (float tx in new[] { middle - 2.6f * s, middle + 2.6f * s }) Tree("Tree_N", new Vector3(tx, drop - 0.1f, facadeZ + 1.3f * s), 3.0f);
                var bushPrefab = palette.kitBush != null ? palette.kitBush : palette.flowerbed;
                foreach (var (n, bx) in new[] { ("W", middle - bankX), ("E", middle + bankX) })
                {
                    Planting(bushPrefab, "Bank_Bushes_" + n + "1", new Vector3(bx, drop - 0.1f, apronZ0 - 0.6f * s), 1.1f * s, 0.6f);
                    Planting(bushPrefab, "Bank_Bushes_" + n + "2", new Vector3(bx, drop - 0.1f, centreZ + oz + 0.6f * s), 1.1f * s, 0.6f);
                }
                if (palette.kitHedge != null)
                {
                    float hedgeX = 2.9f * s;
                    HedgeRun("Hedge_SW", new Vector3(middle - hedgeX, drop - 0.1f, apronZ1 - 0.3f * s), new Vector3(middle - hedgeX, drop - 0.1f, pathEnd));
                    HedgeRun("Hedge_SE", new Vector3(middle + hedgeX, drop - 0.1f, apronZ1 - 0.3f * s), new Vector3(middle + hedgeX, drop - 0.1f, pathEnd));
                }
            }
        }

        /// <summary>Stone tiles over a rectangle of the platform floor, tiles never larger than one tile and never on the grid.</summary>
        private void FillTiles(SS3DLevelRenderer board, float x0, float x1, float z0, float z1, string name)
        {
            float s = board.tileSpacing;
            float w = x1 - x0, d = z1 - z0; if (w < 0.05f || d < 0.05f) return;
            int nx = Mathf.Max(1, Mathf.CeilToInt(w / s - 1e-3f)), nz = Mathf.Max(1, Mathf.CeilToInt(d / s - 1e-3f));
            float tw = w / nx, td = d / nz;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = transform.TransformPoint(new Vector3(x0 + tw * (i + 0.5f), 0f, z0 + td * (j + 0.5f)));
                    board.CreateStoneTile(name, p, (i + j) % 2 == 0, i * 31 + j * 17 + name.Length, _frame, tw, td);
                }
        }

        private Mesh _sphere;
        /// <summary>Ball finial (built-in sphere mesh) in a lit material.</summary>
        private void Ball(string name, Vector3 centre, float diameter, Color color)
        {
            if (_sphere == null) _sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            if (_sphere == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(_frame, false);
            go.transform.position = transform.TransformPoint(centre);
            go.transform.localScale = Vector3.one * diameter;
            go.AddComponent<MeshFilter>().sharedMesh = _sphere;
            if (!_boxMats.TryGetValue(color, out var mat) || mat == null) { mat = SS3DLevelRenderer.MakeLitMaterial("SS3D Trim " + ColorUtility.ToHtmlStringRGB(color), color); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", palette.stoneSmoothness); _boxMats[color] = mat; }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Procedural water slab (bottom-centre origin) in the shared caustic water material.</summary>
        private void WaterBox(string name, Vector3 p, float sizeX, float height, float sizeZ)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_frame, false);
            go.transform.position = transform.TransformPoint(p);
            go.AddComponent<MeshFilter>().sharedMesh = SS3DMeshes.ChamferedBox(Mathf.Max(0.02f, sizeX), Mathf.Max(0.02f, height), Mathf.Max(0.02f, sizeZ), 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = SS3DArtTuning.WaterMaterial(palette);
        }

        /// <summary>Foliage prefab fitted into a footprint (kit or starter), tinted as foliage.</summary>
        private void Planting(GameObject prefab, string name, Vector3 p, float footprint, float height)
        {
            var obj = Place(prefab, name, p, Quaternion.Euler(0f, (p.x * 37f + p.z * 11f) % 360f, 0f));
            if (obj == null) return;
            SS3DKitFit.FitUniform(obj, p, footprint, height);
            SS3DArtTuning.Tint(obj, SS3DTint.Foliage, palette);
        }
    }
}
