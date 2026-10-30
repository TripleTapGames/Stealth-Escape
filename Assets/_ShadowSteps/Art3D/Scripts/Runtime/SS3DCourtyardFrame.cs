using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Designed courtyard perimeter. The playable grid sits on a raised cream-stone terrace with a balustrade, inside a
    /// moat. In kit mode (palette.kitMode) the composition lives only where the portrait camera can see it: a palace
    /// wall with gate house and towers across the top, a stone bridge and carpet to the terrace, and at the bottom a
    /// staircase bridging the moat onto a path flanked by hedges and trees. Outside kit mode the milestone-3
    /// procedural trim is used. Everything is OUTSIDE the logical tile centres, under one toggleable root, colliders
    /// removed; it introduces no walls, blocked cells or vision blockers, and nothing south of the board is tall enough
    /// to hide a node at the 58° view (height &lt; 1.6 × distance from the last row).
    /// </summary>
    public sealed partial class SS3DCourtyardFrame : MonoBehaviour
    {
        public SS3DArtPalette palette;
        public bool generateExterior = true;
        public bool decorateWithOutsidePlants = true;
        [Tooltip("Gap between the last tile centre and the parapet line, in tiles.")]
        public float borderOffset = 0.7f;
        [Tooltip("How far the surrounding ground sits below the terrace floor (world units).")]
        public float terraceDrop = 0.35f;
        [Tooltip("Optional fitter; when set, decoration is laid out inside the ground area visible at its design aspect.")]
        public SS3DBoardCameraFitter fitter;
        public int fallbackRings = 4;
        private Transform _frame;

        private bool Kit => palette != null && palette.kitMode;
        private float K => Kit ? palette.kitScale : 1f;

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            if (board == null || !board.IsBuilt || palette == null) return;
            if (_frame != null) SS3DLevelRenderer.SafeDestroy(_frame.gameObject);
            _boxMats.Clear();
            var root = new GameObject("Exterior_Decoration_Only");
            root.transform.SetParent(transform, false);
            _frame = root.transform;
            if (!generateExterior) return;
            var stage = board.Stage;
            float s = board.tileSpacing;
            if (palette.sunlitCourtyard) { BuildSunlit(board); return; }
            float north = board.ToWorld(0, 0).z + s * borderOffset;
            float south = board.ToWorld(0, stage.Height - 1).z - s * borderOffset;
            float left = board.ToWorld(0, 0).x - s * borderOffset;
            float right = board.ToWorld(stage.Width - 1, 0).x + s * borderOffset;
            float middle = (left + right) * 0.5f, centreZ = (north + south) * 0.5f;
            float drop = -terraceDrop;
            bool kit = Kit; float k = K;

            // Visible ground: the full rectangle (top is widest in perspective) and the width at any depth.
            var corners = new Vector3[4];
            bool hasCorners = fitter != null && fitter.VisibleGroundCorners(fitter.designAspect, palette.floorTop, corners);
            Rect visible = hasCorners ? fitter.VisibleGroundRect(fitter.designAspect, palette.floorTop) : Rect.MinMaxRect(left - fallbackRings * s, south - fallbackRings * s, right + fallbackRings * s, north + fallbackRings * s);
            float HalfWidthAt(float z)
            {
                if (!hasCorners) return (right - left) * 0.5f + 2.5f * s;
                float zb = (corners[0].z + corners[1].z) * 0.5f, zt = (corners[2].z + corners[3].z) * 0.5f;
                float wb = Mathf.Abs(corners[1].x - corners[0].x) * 0.5f, wt = Mathf.Abs(corners[3].x - corners[2].x) * 0.5f;
                return Mathf.Lerp(wb, wt, Mathf.InverseLerp(zb, zt, z));
            }
            float screenBottomZ = hasCorners ? Mathf.Min(corners[0].z, corners[1].z) : south - 5f * s;
            float farNorth = Mathf.Max(visible.yMax + s, north + 3f * s), farSouth = Mathf.Min(visible.yMin - s, south - 3f * s);
            float farWest = Mathf.Min(visible.xMin - s, left - 2.5f * s), farEast = Mathf.Max(visible.xMax + s, right + 2.5f * s);
            float facadeZ = kit ? north + 2.2f * s : north + 1.9f * s;

            // ---- 1. Ground: kit grass slab fitted to the visible rectangle, or a warm-stone slab.
            if (kit && palette.kitGround != null)
            {
                var g = Place(palette.kitGround, "Ground_Plane", Vector3.zero);
                SS3DKitFit.Fit(g, new Vector3((farWest + farEast) * 0.5f, drop - 0.16f, (farNorth + farSouth) * 0.5f), farEast - farWest + 2f * s, 0.16f, farNorth - farSouth + 2f * s, false);
                SS3DArtTuning.Tint(g, SS3DTint.Foliage, palette);
            }
            else Slab(palette.floorA, "Ground_Plane", new Vector3((farWest + farEast) * 0.5f, drop - 0.02f, (farNorth + farSouth) * 0.5f), (farEast - farWest), (farNorth - farSouth), SS3DTint.Stone);

            // ---- 2. Raised terrace: floor border in the board stone (half a tile in kit mode so the board stays the biggest shape), grout, two-step plinth, ivory trim.
            float gx0 = board.ToWorld(0, 0).x - 0.5f * s, gx1 = board.ToWorld(stage.Width - 1, 0).x + 0.5f * s;
            float gz1 = board.ToWorld(0, 0).z + 0.5f * s, gz0 = board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
            float border = kit ? 0.5f * s : 1.0f * s;
            if (kit && palette.proceduralFloorTiles)
            {
                float hb = border * 0.5f;
                for (int yy = 0; yy < stage.Height; yy++)
                {
                    float z = board.ToWorld(0, yy).z;
                    board.CreateStoneTile("Terrace_Border_W", transform.TransformPoint(new Vector3(gx0 - hb, 0f, z)), yy % 2 == 0, yy * 13, _frame, border, s);
                    board.CreateStoneTile("Terrace_Border_E", transform.TransformPoint(new Vector3(gx1 + hb, 0f, z)), yy % 2 == 1, yy * 17, _frame, border, s);
                }
                for (int xx = 0; xx < stage.Width; xx++)
                {
                    float x = board.ToWorld(xx, 0).x;
                    board.CreateStoneTile("Terrace_Border_N", transform.TransformPoint(new Vector3(x, 0f, gz1 + hb)), xx % 2 == 0, xx * 7, _frame, s, border);
                    board.CreateStoneTile("Terrace_Border_S", transform.TransformPoint(new Vector3(x, 0f, gz0 - hb)), xx % 2 == 1, xx * 11, _frame, s, border);
                }
                foreach (var (cx, cz) in new[] { (gx0 - hb, gz1 + hb), (gx1 + hb, gz1 + hb), (gx0 - hb, gz0 - hb), (gx1 + hb, gz0 - hb) })
                    board.CreateStoneTile("Terrace_Border_Corner", transform.TransformPoint(new Vector3(cx, 0f, cz)), true, Mathf.RoundToInt(cx * 3f + cz * 5f), _frame, border, border);
            }
            else
                for (float x = left - 0.5f * s; x <= right + 0.5f * s + 1e-3f; x += s)
                    for (float z = south - 0.5f * s; z <= north + 0.5f * s + 1e-3f; z += s)
                    {
                        bool insideGrid = x > gx0 && x < gx1 && z < gz1 && z > gz0;
                        if (insideGrid) continue;
                        if (palette.proceduralFloorTiles) board.CreateStoneTile("Terrace_Border", transform.TransformPoint(new Vector3(x, 0f, z)), Mathf.RoundToInt(x + z) % 2 == 0, Mathf.RoundToInt(x * 7f + z * 13f), _frame);
                        else
                        {
                            var t = Place(palette.floorA, "Terrace_Border", new Vector3(x, 0f, z));
                            if (t != null) { t.transform.localScale = new Vector3(palette.tileInset, 1f, palette.tileInset); SS3DArtTuning.Tint(t, SS3DTint.FloorB, palette); }
                        }
                    }
            float platW = (gx1 - gx0) + 2f * border + 0.4f * s, platD = (gz1 - gz0) + 2f * border + 0.4f * s;
            Box("Terrace_Grout", new Vector3(middle, -0.012f, centreZ), platW, palette.floorTop - 0.022f, platD, 0f, palette.groutColor);
            Box("Platform_Plinth_Upper", new Vector3(middle, drop, centreZ), platW + 0.30f * s, terraceDrop + 0.005f, platD + 0.30f * s, 0.08f, palette.plinthStone);
            Box("Platform_Plinth_Lower", new Vector3(middle, drop - 0.22f, centreZ), platW + 0.80f * s, 0.22f, platD + 0.80f * s, 0.06f, palette.warmStone);
            float trimH = palette.floorTop + 0.035f;
            Box("Trim_N", new Vector3(middle, 0f, north - 0.32f * s), (right - left) - 0.5f * s, trimH, 0.10f * s, 0.02f, palette.trimIvory);
            Box("Trim_S", new Vector3(middle, 0f, south + 0.32f * s), (right - left) - 0.5f * s, trimH, 0.10f * s, 0.02f, palette.trimIvory);
            Box("Trim_W", new Vector3(left + 0.32f * s, 0f, centreZ), 0.10f * s, trimH, (north - south) - 0.5f * s, 0.02f, palette.trimIvory);
            Box("Trim_E", new Vector3(right - 0.32f * s, 0f, centreZ), 0.10f * s, trimH, (north - south) - 0.5f * s, 0.02f, palette.trimIvory);
            float skirtW = left - 0.95f * s, skirtE = right + 0.95f * s;
            float plinthHalfW = platW * 0.5f + 0.40f * s, plinthHalfD = platD * 0.5f + 0.40f * s;

            // ---- 3. Parapets: kit balustrade (rail segments + posts) or procedural base course + cap rail + pilasters. Gaps on the axis.
            bool kitRail = kit && palette.kitParapetRail != null;
            float railY = kitRail ? 0.95f * k : 0.62f;
            void ParapetRun(string name, Vector3 a, Vector3 b, bool gapAtMiddle)
            {
                var dir = (b - a); float len = dir.magnitude; dir.Normalize();
                bool alongX = Mathf.Abs(dir.x) > 0.5f;
                if (gapAtMiddle)
                {
                    float half = (len - 1.3f * s) * 0.5f;
                    ParapetRun(name + "_A", a, a + dir * half, false);
                    ParapetRun(name + "_B", b - dir * half, b, false);
                    return;
                }
                var mid = (a + b) * 0.5f;
                if (kitRail)
                {
                    float segLen = 2.5f * k; int n = Mathf.Max(1, Mathf.RoundToInt(len / segLen)); float seg = len / n;
                    var rot = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                    for (int i = 0; i < n; i++)
                    {
                        var c = a + dir * (seg * (i + 0.5f));
                        var rail = Place(palette.kitParapetRail, name + "_Rail_" + i, c, rot);
                        SS3DKitFit.Fit(rail, new Vector3(c.x, palette.floorTop, c.z), seg + 0.02f, railY, float.NaN, false);
                        SS3DArtTuning.Tint(rail, SS3DTint.Wall, palette);
                    }
                    if (palette.kitParapetPost != null)
                        for (int i = 0; i <= n; i++)
                        {
                            var c = a + dir * (seg * i);
                            var post = Place(palette.kitParapetPost, name + "_Post_" + i, c);
                            SS3DKitFit.FitUniform(post, new Vector3(c.x, palette.floorTop, c.z), 0.3f * k, railY + 0.08f);
                            SS3DArtTuning.Tint(post, SS3DTint.Wall, palette);
                        }
                    return;
                }
                Box(name + "_Base", mid, alongX ? len : 0.34f * s, railY, alongX ? 0.34f * s : len, 0.0f, palette.wallIvory);
                Box(name + "_Cap", new Vector3(mid.x, railY, mid.z), (alongX ? len : 0.46f * s) + 0.02f, 0.12f, (alongX ? 0.46f * s : len) + 0.02f, 0.04f, palette.trimIvory);
                for (float d = 1.0f * s; d < len - 0.5f * s; d += 2f * s)
                {
                    var pp = a + dir * d;
                    Box(name + "_Pilaster", new Vector3(pp.x, 0f, pp.z), 0.46f * s, railY + 0.22f, 0.46f * s, 0.05f, palette.wallIvory);
                }
            }
            ParapetRun("Parapet_North", new Vector3(left, 0f, north), new Vector3(right, 0f, north), true);
            ParapetRun("Parapet_South", new Vector3(left, 0f, south), new Vector3(right, 0f, south), true);
            ParapetRun("Parapet_West", new Vector3(left, 0f, south), new Vector3(left, 0f, north), false);
            ParapetRun("Parapet_East", new Vector3(right, 0f, south), new Vector3(right, 0f, north), false);
            float pierH = kitRail ? railY + 0.18f : railY + 0.42f;
            foreach (var (n, cx, cz) in new[] { ("NW", left, north), ("NE", right, north), ("SW", left, south), ("SE", right, south) })
            {
                Box("Pier_" + n, new Vector3(cx, 0f, cz), 0.62f * s, pierH, 0.62f * s, 0.08f, palette.wallIvory);
                Box("Pier_Cap_" + n, new Vector3(cx, pierH, cz), 0.52f * s, 0.16f, 0.52f * s, 0.07f, palette.trimIvory);
                Urn("Pier_Urn_" + n, new Vector3(cx, pierH + 0.16f, cz), 0.45f);
            }
            Wood(palette.bench, "Bench_W", new Vector3(left + 0.45f * s, 0f, centreZ), Quaternion.Euler(0f, 90f, 0f), 1.2f * s);
            Wood(palette.bench, "Bench_E", new Vector3(right - 0.45f * s, 0f, centreZ), Quaternion.Euler(0f, -90f, 0f), 1.2f * s);

            if (kit)
            {
                BuildKitSurroundings(board, s, k, gz0, gz1, border, middle, centreZ, drop, facadeZ, plinthHalfW, plinthHalfD, farWest, farEast, farNorth, farSouth, screenBottomZ, HalfWidthAt, railY);
                return;
            }

            // ---- 4. (procedural) Entrance façade: stepped plinth, pilasters, recessed door bay, lintel + cornice, lanterns, banners, three steps down.
            float facadeW = Mathf.Max(right - left, 5f * s) + 1.2f * s;
            Box("Facade_Plinth", new Vector3(middle, drop, facadeZ + 0.1f * s), facadeW + 0.3f * s, 0.30f, 1.5f * s, 0.05f, palette.plinthStone);
            Box("Facade_Wall", new Vector3(middle, drop + 0.30f, facadeZ + 0.2f * s), facadeW, 2.1f, 1.1f * s, 0.06f, palette.wallIvory);
            Box("Facade_Cornice", new Vector3(middle, drop + 2.40f, facadeZ + 0.2f * s), facadeW + 0.25f * s, 0.22f, 1.25f * s, 0.08f, palette.trimIvory);
            Box("Facade_Attic", new Vector3(middle, drop + 2.62f, facadeZ + 0.35f * s), facadeW * 0.62f, 0.55f, 0.8f * s, 0.06f, palette.wallIvory);
            Box("Door_Bay", new Vector3(middle, drop + 0.30f, facadeZ - 0.45f * s), 2.4f * s, 1.95f, 0.5f * s, 0.05f, palette.plinthStone);
            Box("Door_Lintel", new Vector3(middle, drop + 2.0f, facadeZ - 0.5f * s), 2.7f * s, 0.26f, 0.62f * s, 0.07f, palette.trimIvory);
            Stone(palette.archFrame, "Palace_Entrance", new Vector3(middle, drop + 0.30f, facadeZ - 0.62f * s), SS3DTint.Stone);
            Wood(palette.palaceDoor, "Palace_Door", new Vector3(middle, drop + 0.30f, facadeZ - 0.58f * s));
            Box("Pilaster_L", new Vector3(middle - 1.55f * s, drop + 0.30f, facadeZ - 0.55f * s), 0.42f * s, 2.1f, 0.42f * s, 0.05f, palette.trimIvory);
            Box("Pilaster_R", new Vector3(middle + 1.55f * s, drop + 0.30f, facadeZ - 0.55f * s), 0.42f * s, 2.1f, 0.42f * s, 0.05f, palette.trimIvory);
            Place(palette.lantern, "Lamp_Left", new Vector3(middle - 1.1f * s, drop + 0.30f, facadeZ - 0.95f * s));
            Place(palette.lantern, "Lamp_Right", new Vector3(middle + 1.1f * s, drop + 0.30f, facadeZ - 0.95f * s));
            Place(palette.banner, "Banner_Left", new Vector3(middle - 2.4f * s, drop + 0.30f, facadeZ - 0.7f * s));
            Place(palette.banner, "Banner_Right", new Vector3(middle + 2.4f * s, drop + 0.30f, facadeZ - 0.7f * s));
            if (decorateWithOutsidePlants)
            {
                Hedge("Topiary_L", new Vector3(middle - 2.1f * s, drop + 0.30f, facadeZ - 1.25f * s), 1.0f);
                Hedge("Topiary_R", new Vector3(middle + 2.1f * s, drop + 0.30f, facadeZ - 1.25f * s), 1.0f);
            }
            for (int step = 0; step < 3; step++)
                Box("Entrance_Step_" + step, new Vector3(middle, drop + 0.30f - (step + 1) * 0.10f, facadeZ - 1.15f * s - step * 0.28f * s), (1.9f + step * 0.35f) * s, 0.10f, 0.30f * s, 0.02f, palette.plinthStone);
            Carpet("Carpet_North", new Vector3(middle, drop + 0.03f, (north + 0.95f * s + facadeZ - 1.2f * s) * 0.5f), 0.9f * s, Mathf.Max(0.3f, facadeZ - 1.2f * s - (north + 0.95f * s)));
            Carpet("Carpet_Terrace_N", new Vector3(middle, palette.floorTop + 0.012f, north + 0.5f * s), 0.9f * s, 0.9f * s);
            Carpet("Carpet_Terrace_S", new Vector3(middle, palette.floorTop + 0.012f, south - 0.5f * s), 0.9f * s, 0.9f * s);

            // ---- 5. (procedural) Staircase zone: landing, double stairs, cheek walls, newel piers.
            Box("Stair_Landing", new Vector3(middle, drop - 0.22f, south - 1.35f * s), 3.6f * s, 0.22f + terraceDrop, 0.9f * s, 0.05f, palette.plinthStone);
            Stone(palette.stairs, "Entry_Stairs_Upper", new Vector3(middle, drop - 0.22f, south - 2.4f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Stone(palette.stairs, "Entry_Stairs_Lower", new Vector3(middle, drop - 0.62f, south - 4.0f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Box("Stair_Cheek_L", new Vector3(middle - 1.25f * s, drop - 0.62f, south - 3.0f * s), 0.34f * s, 0.62f + terraceDrop, 3.8f * s, 0.04f, palette.wallIvory);
            Box("Stair_Cheek_R", new Vector3(middle + 1.25f * s, drop - 0.62f, south - 3.0f * s), 0.34f * s, 0.62f + terraceDrop, 3.8f * s, 0.04f, palette.wallIvory);
            foreach (var (n, cx) in new[] { ("L", middle - 1.25f * s), ("R", middle + 1.25f * s) })
            {
                Box("Newel_" + n, new Vector3(cx, drop - 0.22f, south - 1.0f * s), 0.58f * s, railY + 0.42f + 0.22f, 0.58f * s, 0.07f, palette.wallIvory);
                Box("Newel_Cap_" + n, new Vector3(cx, drop - 0.22f + railY + 0.64f, south - 1.0f * s), 0.50f * s, 0.16f, 0.50f * s, 0.07f, palette.trimIvory);
            }
            if (decorateWithOutsidePlants)
            {
                Hedge("Planter_Group_SW", new Vector3(left - 0.3f * s, drop, south - 2.2f * s), 1.5f);
                Hedge("Planter_Group_SE", new Vector3(right + 0.3f * s, drop, south - 2.2f * s), 1.5f);
            }

            // ---- 6. (procedural) Two long canals and a cross channel; hedges along the sides and at the far corners.
            float canalW = 1.3f * s;
            Canal("Canal_West", new Vector3(skirtW - 0.75f * s - canalW * 0.5f, drop, (farNorth + farSouth) * 0.5f), canalW, farNorth - farSouth);
            Canal("Canal_East", new Vector3(skirtE + 0.75f * s + canalW * 0.5f, drop, (farNorth + farSouth) * 0.5f), canalW, farNorth - farSouth);
            Canal("Canal_North", new Vector3(middle, drop, facadeZ + 1.6f * s), farEast - farWest, 1.2f * s);
            if (decorateWithOutsidePlants)
            {
                float hedgeX = skirtW - 0.75f * s - canalW - 0.9f * s, hedgeXE = skirtE + 0.75f * s + canalW + 0.9f * s;
                for (float z = south + 0.5f * s; z <= north + 1e-3f; z += 2.5f * s)
                {
                    Hedge("Hedge_W", new Vector3(hedgeX, drop, z), 1.4f);
                    Hedge("Hedge_E", new Vector3(hedgeXE, drop, z), 1.4f);
                }
                Hedge("Hedge_NW", new Vector3(farWest + 1.6f * s, drop, facadeZ + 0.3f * s), 2.2f);
                Hedge("Hedge_NE", new Vector3(farEast - 1.6f * s, drop, facadeZ + 0.3f * s), 2.2f);
                Hedge("Hedge_SW", new Vector3(farWest + 1.6f * s, drop, south - 3.2f * s), 2.2f);
                Hedge("Hedge_SE", new Vector3(farEast - 1.6f * s, drop, south - 3.2f * s), 2.2f);
            }
        }

        /// <summary>
        /// Kit-mode surroundings, laid out only where the portrait camera sees them: at the board's depth the terrace
        /// already fills the width, so everything lives in the north band (wall, gate, bridge) and the south band
        /// (staircase bridge, path, hedges, trees), with a moat ring tying the two together.
        /// </summary>
        private void BuildKitSurroundings(SS3DLevelRenderer board, float s, float k, float gz0, float gz1, float border, float middle, float centreZ, float drop, float facadeZ,
                                          float plinthHalfW, float plinthHalfD, float farWest, float farEast, float farNorth, float farSouth, float screenBottomZ, System.Func<float, float> halfWidthAt, float railY)
        {
            // ---- Water only where the portrait view has room: one strip across the north and one across the south of the plinth, kerbed on three sides.
            //      Paved forecourt between the north strip and the palace wall, paved apron around the stair foot; grass beyond. The sides stay plain.
            float moatW = 1.0f * s, kerb = 0.14f * s, apronHalfW = plinthHalfW + 1.0f * s;
            float moatS0 = centreZ - plinthHalfD, moatS1 = moatS0 - moatW, moatN0 = centreZ + plinthHalfD, moatN1 = moatN0 + moatW;
            Canal("Moat_S", new Vector3(middle, drop, (moatS0 + moatS1) * 0.5f), 2f * plinthHalfW, moatW);
            Canal("Moat_N", new Vector3(middle, drop, (moatN0 + moatN1) * 0.5f), 2f * plinthHalfW, moatW);
            foreach (var (n, z0, z1, sign) in new[] { ("S", moatS1, moatS0, -1f), ("N", moatN0, moatN1, 1f) })
            {
                Box("Moat_Kerb_" + n, new Vector3(middle, drop - 0.04f, (sign < 0 ? z0 : z1) + sign * kerb * 0.5f), 2f * plinthHalfW + 2f * kerb, 0.12f, kerb, 0.02f, palette.plinthStone);
                Box("Moat_Kerb_" + n + "W", new Vector3(middle - plinthHalfW - kerb * 0.5f, drop - 0.04f, (z0 + z1) * 0.5f), kerb, 0.12f, moatW, 0.02f, palette.plinthStone);
                Box("Moat_Kerb_" + n + "E", new Vector3(middle + plinthHalfW + kerb * 0.5f, drop - 0.04f, (z0 + z1) * 0.5f), kerb, 0.12f, moatW, 0.02f, palette.plinthStone);
            }
            float forecourtZ0 = moatN1 + kerb, forecourtZ1 = facadeZ + 0.2f * s;
            Box("Forecourt_Paving", new Vector3(middle, drop - 0.03f, (forecourtZ0 + forecourtZ1) * 0.5f), 2f * apronHalfW, 0.05f, forecourtZ1 - forecourtZ0, 0.02f, palette.warmStone);

            // ---- North: palace line across the whole visible width (gate house centred, window walls + battlements, towers), bridge + carpet + steps to the terrace.
            float wallH = 2.4f;
            float gateW = 2.5f * s;
            if (palette.kitWallGate != null)
            {
                var facing = Quaternion.Euler(0f, palette.kitFacadeYaw, 0f);
                var gate = Place(palette.kitWallGate, "Palace_Gate", new Vector3(middle, 0f, facadeZ), facing);
                SS3DKitFit.FitUniform(gate, new Vector3(middle, drop, facadeZ), float.PositiveInfinity, wallH * 1.12f);
                SS3DArtTuning.Tint(gate, SS3DTint.Wall, palette);
                gateW = Mathf.Max(1.5f * s, SS3DKitFit.WorldSize(gate).x);
                foreach (float sign in new[] { -1f, 1f })
                {
                    float from = middle + sign * gateW * 0.5f, to = sign < 0 ? farWest - 1f * s : farEast + 1f * s;
                    float len = Mathf.Abs(to - from); float segLen = 5f * k; int n = Mathf.Max(1, Mathf.CeilToInt(len / segLen)); float seg = len / n;
                    for (int i = 0; i < n; i++)
                    {
                        float cx = from + sign * seg * (i + 0.5f);
                        var wall = Place(palette.kitWallRun, "Palace_Wall_" + (sign < 0 ? "W" : "E") + i, new Vector3(cx, 0f, facadeZ), facing);
                        SS3DKitFit.Fit(wall, new Vector3(cx, drop, facadeZ), seg + 0.02f, wallH, float.NaN, false);
                        SS3DArtTuning.Tint(wall, SS3DTint.Wall, palette);
                        if (palette.kitBattlement != null)
                        {
                            var bat = Place(palette.kitBattlement, "Palace_Battlement_" + (sign < 0 ? "W" : "E") + i, new Vector3(cx, 0f, facadeZ), facing);
                            SS3DKitFit.Fit(bat, new Vector3(cx, drop + wallH - 0.01f, facadeZ), seg + 0.02f, wallH * 0.28f, float.NaN, false);
                            SS3DArtTuning.Tint(bat, SS3DTint.Wall, palette);
                        }
                    }
                    if (palette.kitTower != null)
                    {
                        float towerX = middle + sign * (gateW * 0.5f + 0.62f * s);
                        var tower = Place(palette.kitTower, "Palace_Tower_" + (sign < 0 ? "W" : "E"), new Vector3(towerX, 0f, facadeZ), facing);
                        SS3DKitFit.FitUniform(tower, new Vector3(towerX, drop, facadeZ), float.PositiveInfinity, wallH * 1.45f);
                        SS3DArtTuning.Tint(tower, SS3DTint.Wall, palette);
                    }
                    if (palette.kitAccents)
                    {
                        float bannerX = middle + sign * (gateW * 0.5f + 1.9f * s);
                        var banner = Place(palette.banner, "Banner_" + (sign < 0 ? "W" : "E"), new Vector3(bannerX, 0f, facadeZ - 0.2f * s));
                        SS3DKitFit.FitUniform(banner, new Vector3(bannerX, drop + wallH * 0.42f, facadeZ - 0.2f * s), float.PositiveInfinity, 1.1f);
                        float sx = middle + sign * (gateW * 0.5f + 2.6f * s), sz = facadeZ - 1.0f * s, top = drop;
                        if (palette.kitStatuePlinth != null) { var pl = Place(palette.kitStatuePlinth, "Statue_Plinth_" + (sign < 0 ? "W" : "E"), new Vector3(sx, 0f, sz)); SS3DKitFit.FitUniform(pl, new Vector3(sx, drop, sz), 0.9f * s, 0.55f); SS3DArtTuning.Tint(pl, SS3DTint.Stone, palette); top = drop + SS3DKitFit.WorldSize(pl).y; }
                        if (palette.kitStatue != null) { var st = Place(palette.kitStatue, "Statue_" + (sign < 0 ? "W" : "E"), new Vector3(sx, 0f, sz), Quaternion.Euler(0f, 180f, 0f)); SS3DKitFit.FitUniform(st, new Vector3(sx, top, sz), 0.8f * s, 1.05f); SS3DArtTuning.Tint(st, SS3DTint.Stone, palette); }
                    }
                }
                Urn("Gate_Urn_W", new Vector3(middle - gateW * 0.5f - 0.25f * s, drop, facadeZ - 0.75f * s), 0.8f);
                Urn("Gate_Urn_E", new Vector3(middle + gateW * 0.5f + 0.25f * s, drop, facadeZ - 0.75f * s), 0.8f);
            }
            // Bridge over the north moat from the terrace plinth to the gate, carpet on top, two steps up onto the terrace.
            float bridgeFrom = moatN0 - 0.3f * s, bridgeTo = facadeZ - 0.15f * s;
            Box("North_Bridge", new Vector3(middle, drop - 0.03f, (bridgeFrom + bridgeTo) * 0.5f), 1.5f * s, 0.09f, bridgeTo - bridgeFrom, 0.03f, palette.plinthStone);
            float terraceEdgeN = gz1 + border;
            Carpet("Carpet_North", new Vector3(middle, drop + 0.065f, (terraceEdgeN + bridgeTo - 0.1f * s) * 0.5f), 0.9f * s, bridgeTo - 0.1f * s - terraceEdgeN);
            Box("Entrance_Step_0", new Vector3(middle, -0.12f, terraceEdgeN + 0.12f * s), 1.5f * s, 0.12f, 0.30f * s, 0.02f, palette.plinthStone);
            Box("Entrance_Step_1", new Vector3(middle, -0.24f, terraceEdgeN + 0.42f * s), 1.5f * s, 0.12f, 0.30f * s, 0.02f, palette.plinthStone);
            Lamp("Lamp_Gate_W", new Vector3(middle - 1.1f * s, drop, forecourtZ0 + 0.35f * s), 1.1f);
            Lamp("Lamp_Gate_E", new Vector3(middle + 1.1f * s, drop, forecourtZ0 + 0.35f * s), 1.1f);
            Carpet("Carpet_Terrace_N", new Vector3(middle, palette.floorTop + 0.012f, gz1 + border * 0.5f), 0.9f * s, border);
            Carpet("Carpet_Terrace_S", new Vector3(middle, palette.floorTop + 0.012f, gz0 - border * 0.5f), 0.9f * s, border);
            if (decorateWithOutsidePlants && palette.kitTree != null)
                foreach (float tx in new[] { farWest + 1.5f * s, middle - 3.6f * s, middle + 3.6f * s, farEast - 1.5f * s }) Tree("Tree_N", new Vector3(tx, drop, facadeZ + 1.3f * s), 3.3f);

            // ---- South: landing + cream steps bridging the moat, low cheek walls, newels with urns, a path to the screen edge, hedges and trees beside it.
            float terraceEdgeS = gz0 - border, landingDepth = (terraceEdgeS - moatS1) + 0.1f * s;
            Box("Stair_Landing", new Vector3(middle, drop - 0.22f, terraceEdgeS - landingDepth * 0.5f), 3.4f * s, 0.22f + terraceDrop, landingDepth, 0.05f, palette.plinthStone);
            const int steps = 4; float stepRise = terraceDrop / steps, stepDepth = 0.32f * s, stairStart = terraceEdgeS - landingDepth;
            for (int i = 0; i < steps; i++)
            {
                float top = -(i + 1) * stepRise, z = stairStart - stepDepth * (i + 0.5f);
                Box("Entry_Step_" + i, new Vector3(middle, drop - 0.03f, z), 2.6f * s, top - (drop - 0.03f), stepDepth + 0.01f, 0.02f, palette.plinthStone);
            }
            float stairEnd = stairStart - stepDepth * steps;
            float apronZ0 = moatS1 - kerb, apronZ1 = stairEnd - 0.7f * s;
            Box("South_Apron", new Vector3(middle, drop - 0.03f, (apronZ0 + apronZ1) * 0.5f), 2f * apronHalfW, 0.05f, apronZ0 - apronZ1, 0.02f, palette.warmStone);
            float cheekH = terraceDrop + 0.14f;
            Box("Stair_Cheek_L", new Vector3(middle - 1.5f * s, drop - 0.05f, (terraceEdgeS + stairEnd) * 0.5f), 0.30f * s, cheekH, terraceEdgeS - stairEnd, 0.04f, palette.wallIvory);
            Box("Stair_Cheek_R", new Vector3(middle + 1.5f * s, drop - 0.05f, (terraceEdgeS + stairEnd) * 0.5f), 0.30f * s, cheekH, terraceEdgeS - stairEnd, 0.04f, palette.wallIvory);
            foreach (var (n, cx) in new[] { ("L", middle - 1.5f * s), ("R", middle + 1.5f * s) })
            {
                float newelH = railY + 0.40f;
                Box("Newel_" + n, new Vector3(cx, drop - 0.22f, terraceEdgeS - 0.1f * s), 0.56f * s, newelH + 0.22f, 0.56f * s, 0.07f, palette.wallIvory);
                Box("Newel_Cap_" + n, new Vector3(cx, drop + newelH, terraceEdgeS - 0.1f * s), 0.48f * s, 0.16f, 0.48f * s, 0.07f, palette.trimIvory);
                Urn("Newel_Urn_" + n, new Vector3(cx, drop + newelH + 0.16f, terraceEdgeS - 0.1f * s), 0.35f);
            }
            float pathEnd = Mathf.Min(apronZ1 - 1f * s, screenBottomZ - 1f * s);
            Box("South_Path", new Vector3(middle, drop - 0.01f, (apronZ1 + 0.1f * s + pathEnd) * 0.5f), 1.7f * s, 0.05f, apronZ1 + 0.1f * s - pathEnd, 0.02f, palette.plinthStone);
            Lamp("Lamp_Stairs_W", new Vector3(middle - 2.2f * s, drop, stairEnd - 0.3f * s), 0.9f);
            Lamp("Lamp_Stairs_E", new Vector3(middle + 2.2f * s, drop, stairEnd - 0.3f * s), 0.9f);
            if (decorateWithOutsidePlants)
            {
                float hedgeX = Mathf.Max(2.9f * s, halfWidthAt(apronZ1 - 1f * s) - 1.0f * s);
                if (palette.kitHedge != null)
                {
                    HedgeRun("Hedge_SW", new Vector3(middle - hedgeX, drop, apronZ1 - 0.3f * s), new Vector3(middle - hedgeX, drop, pathEnd));
                    HedgeRun("Hedge_SE", new Vector3(middle + hedgeX, drop, apronZ1 - 0.3f * s), new Vector3(middle + hedgeX, drop, pathEnd));
                }
                if (palette.kitTree != null)
                {
                    float treeX = Mathf.Max(3.6f * s, halfWidthAt(screenBottomZ) - 0.9f * s);
                    Tree("Tree_SW", new Vector3(middle - treeX, drop, screenBottomZ + 1.6f * s), 2.2f);
                    Tree("Tree_SE", new Vector3(middle + treeX, drop, screenBottomZ + 1.6f * s), 2.2f);
                }
                Bush("Bush_SW", new Vector3(middle - 2.6f * s, drop, stairEnd - 0.9f * s), 0.55f);
                Bush("Bush_SE", new Vector3(middle + 2.6f * s, drop, stairEnd - 0.9f * s), 0.55f);
                Flowers("Flowers_NW", new Vector3(middle - 1.9f * s, drop, facadeZ - 1.1f * s));
                Flowers("Flowers_NE", new Vector3(middle + 1.9f * s, drop, facadeZ - 1.1f * s));
            }
        }

        // ---- shape helpers (modular prefabs fitted from bounds, or procedural bevelled blocks; never primitives)

        private readonly System.Collections.Generic.Dictionary<Color, Material> _boxMats = new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>Procedural bevelled block (origin at the bottom centre) with a lit material in the given colour; the backbone of the layered trim.</summary>
        private GameObject Box(string name, Vector3 p, float sizeX, float height, float sizeZ, float chamfer, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_frame, false);
            go.transform.position = transform.TransformPoint(p);
            go.AddComponent<MeshFilter>().sharedMesh = SS3DMeshes.ChamferedBox(Mathf.Max(0.02f, sizeX), Mathf.Max(0.02f, height), Mathf.Max(0.02f, sizeZ), chamfer);
            if (!_boxMats.TryGetValue(color, out var mat) || mat == null) { mat = SS3DLevelRenderer.MakeLitMaterial("SS3D Trim " + ColorUtility.ToHtmlStringRGB(color), color); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", palette.stoneSmoothness); _boxMats[color] = mat; }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private void Slab(GameObject prefab, string name, Vector3 p, float sizeX, float sizeZ, SS3DTint tint, float heightScale = 1f)
        {
            var obj = Place(prefab, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(sizeX, heightScale, sizeZ);
            SS3DArtTuning.Tint(obj, tint, palette);
        }

        /// <summary>Water strip whose top sits just above the given ground height.</summary>
        private void Canal(string name, Vector3 p, float sizeX, float sizeZ)
        {
            var obj = Place(palette.water, name, p);
            if (obj == null) return;
            if (Kit) SS3DKitFit.Fit(obj, new Vector3(p.x, p.y - 0.06f, p.z), sizeX, 0.08f, sizeZ, false);
            else { obj.transform.position = transform.TransformPoint(new Vector3(p.x, p.y - 0.06f, p.z)); obj.transform.localScale = new Vector3(sizeX, 1f, sizeZ); }
            SS3DArtTuning.Tint(obj, SS3DTint.Water, palette);
        }

        private void Hedge(string name, Vector3 p, float scale)
        {
            var obj = Place(palette.flowerbed, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(scale, Mathf.Sqrt(scale), scale);
            SS3DArtTuning.Tint(obj, SS3DTint.Foliage, palette);
        }

        /// <summary>Continuous kit hedge between two points, built from stretched segments of about 2.5 kit metres.</summary>
        private void HedgeRun(string name, Vector3 a, Vector3 b)
        {
            var dir = b - a; float len = dir.magnitude; if (len < 0.2f) return; dir.Normalize();
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            float segLen = 2.5f * K; int n = Mathf.Max(1, Mathf.RoundToInt(len / segLen)); float seg = len / n;
            for (int i = 0; i < n; i++)
            {
                var c = a + dir * (seg * (i + 0.5f));
                var h = Place(palette.kitHedge, name + "_" + i, c, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                SS3DKitFit.Fit(h, c, seg + 0.04f, 0.70f, 0.55f, false);
                SS3DArtTuning.Tint(h, SS3DTint.Foliage, palette);
            }
        }

        private void Tree(string name, Vector3 p, float height)
        {
            var t = Place(palette.kitTree, name, p, Quaternion.Euler(0f, (p.x * 57f) % 360f, 0f));
            SS3DKitFit.FitUniform(t, p, float.PositiveInfinity, height);
            SS3DArtTuning.Tint(t, SS3DTint.Foliage, palette);
        }

        private void Bush(string name, Vector3 p, float height)
        {
            if (!Kit || !palette.kitAccents || palette.kitBush == null) return;
            var b = Place(palette.kitBush, name, p);
            SS3DKitFit.FitUniform(b, p, 0.9f, height);
            SS3DArtTuning.Tint(b, SS3DTint.Foliage, palette);
        }

        private void Flowers(string name, Vector3 p)
        {
            if (!Kit || !palette.kitAccents || palette.kitFlowers == null) return;
            var f = Place(palette.kitFlowers, name, p, Quaternion.Euler(0f, (p.z * 41f) % 360f, 0f));
            SS3DKitFit.FitUniform(f, p, 0.8f, 0.4f);
        }

        private void Urn(string name, Vector3 p, float height)
        {
            if (!Kit || !palette.kitAccents || palette.kitUrn == null) return;
            var u = Place(palette.kitUrn, name, p);
            SS3DKitFit.FitUniform(u, p, 0.5f, height);
            SS3DArtTuning.Tint(u, SS3DTint.Stone, palette);
        }

        /// <summary>Lamp standing on the given point (p.y is the ground height for the fit).</summary>
        private void Lamp(string name, Vector3 p, float height)
        {
            var prefab = Kit && palette.kitLamp != null ? palette.kitLamp : palette.lantern;
            var l = Place(prefab, name, p);
            if (l == null) return;
            if (Kit) SS3DKitFit.FitUniform(l, p, 0.5f, height);
            SS3DArtTuning.Tint(l, SS3DTint.Stone, palette);
        }

        private void Carpet(string name, Vector3 p, float width, float length)
        {
            if (Kit && palette.kitCarpet != null)
            {
                var rug = Place(palette.kitCarpet, name, p);
                SS3DKitFit.Fit(rug, p, width, float.NaN, Mathf.Max(0.2f, length), false);
                return;
            }
            var obj = Place(palette.floorA, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(width, 0.2f, Mathf.Max(0.2f, length));
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) foreach (var m in SS3DArtTuning.InstanceMaterials(r)) { if (m == null) continue; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.78f, 0.20f, 0.22f)); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f); }
        }

        private void Stone(GameObject prefab, string name, Vector3 p, SS3DTint tint, Quaternion? rot = null) => Tinted(prefab, name, p, tint, rot);

        private void Wood(GameObject prefab, string name, Vector3 p, Quaternion? rot = null, float kitLength = 0f)
        {
            var obj = Place(prefab, name, p, rot);
            if (obj == null) return;
            if (Kit && kitLength > 0f) SS3DKitFit.Fit(obj, new Vector3(p.x, palette.floorTop, p.z), kitLength, float.NaN, float.NaN, true);
            SS3DArtTuning.Tint(obj, SS3DTint.Wood, palette);
        }

        private GameObject Place(GameObject prefab, string name, Vector3 p, Quaternion? rotation = null)
        {
            if (prefab == null) return null;
            var obj = Instantiate(prefab, transform.TransformPoint(p), rotation ?? Quaternion.identity, _frame);
            obj.name = name;
            foreach (var c in obj.GetComponentsInChildren<Collider>(true)) SS3DLevelRenderer.SafeDestroy(c);
            return obj;
        }

        private void Tinted(GameObject prefab, string name, Vector3 p, SS3DTint tint, Quaternion? rotation = null)
        {
            var obj = Place(prefab, name, p, rotation);
            SS3DArtTuning.Tint(obj, tint, palette);
        }
    }
}
