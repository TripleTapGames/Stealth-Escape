using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Designed courtyard perimeter (milestone 2): the playable grid sits on a raised terrace with a low stone skirt;
    /// outside it a single calm ground plane, two long canals, grouped hedges and planters, a palace façade on the
    /// central axis at the top and the staircase at the bottom, joined by a carpet. Few large shapes instead of a
    /// repeated tile grid. Everything is OUTSIDE the logical tile centres, under one toggleable root, colliders removed;
    /// it introduces no walls, blocked cells or vision blockers.
    /// </summary>
    public sealed class SS3DCourtyardFrame : MonoBehaviour
    {
        public SS3DArtPalette palette;
        public bool generateExterior = true;
        public bool decorateWithOutsidePlants = true;
        [Tooltip("Gap between the last tile centre and the parapet line, in tiles.")]
        public float borderOffset = 0.7f;
        [Tooltip("How far the surrounding ground sits below the terrace floor (world units).")]
        public float terraceDrop = 0.35f;
        [Tooltip("Optional fitter; when set, the ground plane and canals extend to the visible ground rectangle of its design aspect.")]
        public SS3DBoardCameraFitter fitter;
        public int fallbackRings = 4;
        private Transform _frame;

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
            float north = board.ToWorld(0, 0).z + s * borderOffset;
            float south = board.ToWorld(0, stage.Height - 1).z - s * borderOffset;
            float left = board.ToWorld(0, 0).x - s * borderOffset;
            float right = board.ToWorld(stage.Width - 1, 0).x + s * borderOffset;
            float middle = (left + right) * 0.5f;
            float drop = -terraceDrop;

            Rect visible = fitter != null ? fitter.VisibleGroundRect(fitter.designAspect, palette.floorTop) : Rect.MinMaxRect(left - fallbackRings * s, south - fallbackRings * s, right + fallbackRings * s, north + fallbackRings * s);
            float farNorth = Mathf.Max(visible.yMax + s, north + 3f * s), farSouth = Mathf.Min(visible.yMin - s, south - 3f * s);
            float farWest = Mathf.Min(visible.xMin - s, left - 2.5f * s), farEast = Mathf.Max(visible.xMax + s, right + 2.5f * s);

            // ---- 1. One calm ground plane under everything outside the terrace (warm stone, slightly darker than the floor).
            Slab(palette.floorA, "Ground_Plane", new Vector3((farWest + farEast) * 0.5f, drop - 0.02f, (farNorth + farSouth) * 0.5f), (farEast - farWest), (farNorth - farSouth), SS3DTint.Stone);

            // ---- 2. Raised terrace: inset grid floor border, border trim, and a two-step bevelled plinth under the whole platform.
            for (float x = left - 0.5f * s; x <= right + 0.5f * s + 1e-3f; x += s)
                for (float z = south - 0.5f * s; z <= north + 0.5f * s + 1e-3f; z += s)
                {
                    bool insideGrid = x > board.ToWorld(0, 0).x - 0.5f * s && x < board.ToWorld(stage.Width - 1, 0).x + 0.5f * s && z < board.ToWorld(0, 0).z + 0.5f * s && z > board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
                    if (insideGrid) continue;
                    var t = Place(palette.floorA, "Terrace_Border", new Vector3(x, 0f, z));
                    if (t != null) { t.transform.localScale = new Vector3(palette.tileInset, 1f, palette.tileInset); SS3DArtTuning.Tint(t, SS3DTint.FloorB, palette); }
                }
            float platW = (right - left) + 2.0f * s, platD = (north - south) + 2.0f * s;
            Box("Terrace_Grout", new Vector3(middle, -0.012f, (north + south) * 0.5f), platW, palette.floorTop - 0.022f, platD, 0f, palette.groutColor); // sits just below the renderer's grid grout
            Box("Platform_Plinth_Upper", new Vector3(middle, drop, (north + south) * 0.5f), platW + 0.30f * s, terraceDrop + 0.005f, platD + 0.30f * s, 0.08f, palette.plinthStone);
            Box("Platform_Plinth_Lower", new Vector3(middle, drop - 0.22f, (north + south) * 0.5f), platW + 0.80f * s, 0.22f, platD + 0.80f * s, 0.06f, palette.warmStone);
            // border trim: a thin bevelled ivory frame just inside the parapets, around the grid
            float trimH = palette.floorTop + 0.035f;
            Box("Trim_N", new Vector3(middle, 0f, north - 0.32f * s), (right - left) - 0.5f * s, trimH, 0.10f * s, 0.02f, palette.trimIvory);
            Box("Trim_S", new Vector3(middle, 0f, south + 0.32f * s), (right - left) - 0.5f * s, trimH, 0.10f * s, 0.02f, palette.trimIvory);
            Box("Trim_W", new Vector3(left + 0.32f * s, 0f, (north + south) * 0.5f), 0.10f * s, trimH, (north - south) - 0.5f * s, 0.02f, palette.trimIvory);
            Box("Trim_E", new Vector3(right - 0.32f * s, 0f, (north + south) * 0.5f), 0.10f * s, trimH, (north - south) - 0.5f * s, 0.02f, palette.trimIvory);
            float skirtN = north + 0.95f * s, skirtS = south - 0.95f * s, skirtW = left - 0.95f * s, skirtE = right + 0.95f * s;

            // ---- 3. Parapets: thick base course + bevelled cap rail + pilasters every two tiles, corner piers with caps; gaps at entrance and stairs.
            float railY = 0.62f;
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
            foreach (var (n, cx, cz) in new[] { ("NW", left, north), ("NE", right, north), ("SW", left, south), ("SE", right, south) })
            {
                Box("Pier_" + n, new Vector3(cx, 0f, cz), 0.62f * s, railY + 0.42f, 0.62f * s, 0.08f, palette.wallIvory);
                Box("Pier_Cap_" + n, new Vector3(cx, railY + 0.42f, cz), 0.52f * s, 0.16f, 0.52f * s, 0.07f, palette.trimIvory);
            }
            Wood(palette.bench, "Bench_W", new Vector3(left + 0.45f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, 90f, 0f));
            Wood(palette.bench, "Bench_E", new Vector3(right - 0.45f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, -90f, 0f));

            // ---- 4. Entrance façade on the axis: stepped plinth, pilasters, recessed door bay, lintel + cornice, lanterns, banners, three steps down to the terrace.
            float facadeZ = north + 1.9f * s;
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
            for (int step = 0; step < 3; step++)
                Box("Entrance_Step_" + step, new Vector3(middle, drop + 0.30f - (step + 1) * 0.10f, facadeZ - 1.15f * s - step * 0.28f * s), (1.9f + step * 0.35f) * s, 0.10f, 0.30f * s, 0.02f, palette.plinthStone);
            if (decorateWithOutsidePlants)
            {
                Hedge("Topiary_L", new Vector3(middle - 2.1f * s, drop + 0.30f, facadeZ - 1.25f * s), 1.0f);
                Hedge("Topiary_R", new Vector3(middle + 2.1f * s, drop + 0.30f, facadeZ - 1.25f * s), 1.0f);
            }
            Carpet("Carpet_North", new Vector3(middle, drop + 0.03f, (north + 0.95f * s + facadeZ - 1.2f * s) * 0.5f), 0.9f * s, Mathf.Max(0.3f, facadeZ - 1.2f * s - (north + 0.95f * s)));
            Carpet("Carpet_Terrace_N", new Vector3(middle, palette.floorTop + 0.012f, north + 0.5f * s), 0.9f * s, 0.9f * s);
            Carpet("Carpet_Terrace_S", new Vector3(middle, palette.floorTop + 0.012f, south - 0.5f * s), 0.9f * s, 0.9f * s);

            // ---- 5. Staircase zone: landing slab, double stairs with cheek walls and bevelled newel piers, part of the same plinth.
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

            // ---- 6. Two long canals along the sides (one stretched tile each) with a cross channel behind the façade; hedges at the far corners.
            float canalW = 1.3f * s;
            Canal("Canal_West", new Vector3(skirtW - 0.75f * s - canalW * 0.5f, drop - 0.06f, (farNorth + farSouth) * 0.5f), canalW, farNorth - farSouth);
            Canal("Canal_East", new Vector3(skirtE + 0.75f * s + canalW * 0.5f, drop - 0.06f, (farNorth + farSouth) * 0.5f), canalW, farNorth - farSouth);
            Canal("Canal_North", new Vector3(middle, drop - 0.06f, facadeZ + 1.6f * s), farEast - farWest, 1.2f * s);
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

        // ---- shape helpers (all modular prefabs, scaled; never primitives)

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

        private void Canal(string name, Vector3 p, float sizeX, float sizeZ)
        {
            var obj = Place(palette.water, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
            SS3DArtTuning.Tint(obj, SS3DTint.Water, palette);
        }

        private void Hedge(string name, Vector3 p, float scale)
        {
            var obj = Place(palette.flowerbed, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(scale, Mathf.Sqrt(scale), scale);
            SS3DArtTuning.Tint(obj, SS3DTint.Foliage, palette);
        }

        private void Carpet(string name, Vector3 p, float width, float length)
        {
            var obj = Place(palette.floorA, name, p);
            if (obj == null) return;
            obj.transform.localScale = new Vector3(width, 0.2f, Mathf.Max(0.2f, length));
            foreach (var r in obj.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.78f, 0.20f, 0.22f)); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f); }
        }

        private void Stone(GameObject prefab, string name, Vector3 p, SS3DTint tint, Quaternion? rot = null) => Tinted(prefab, name, p, tint, rot);
        private void Wood(GameObject prefab, string name, Vector3 p, Quaternion? rot = null) => Tinted(prefab, name, p, SS3DTint.Wood, rot);

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
