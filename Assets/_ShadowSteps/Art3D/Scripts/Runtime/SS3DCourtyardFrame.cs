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

            // ---- 2. Raised terrace: floor border ring one tile around the grid + low stone skirt on the four sides.
            for (float x = left - 0.5f * s; x <= right + 0.5f * s + 1e-3f; x += s)
                for (float z = south - 0.5f * s; z <= north + 0.5f * s + 1e-3f; z += s)
                {
                    bool insideGrid = x > board.ToWorld(0, 0).x - 0.5f * s && x < board.ToWorld(stage.Width - 1, 0).x + 0.5f * s && z < board.ToWorld(0, 0).z + 0.5f * s && z > board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
                    if (insideGrid) continue;
                    Tinted(palette.floorA, "Terrace_Border", new Vector3(x, 0f, z), SS3DTint.FloorB);
                }
            float skirtN = north + 0.95f * s, skirtS = south - 0.95f * s, skirtW = left - 0.95f * s, skirtE = right + 0.95f * s;
            Skirt("Skirt_North", new Vector3(middle, drop, skirtN), skirtE - skirtW, 0f);
            Skirt("Skirt_South", new Vector3(middle, drop, skirtS), skirtE - skirtW, 0f);
            Skirt("Skirt_West", new Vector3(skirtW, drop, (skirtN + skirtS) * 0.5f), skirtN - skirtS, 90f);
            Skirt("Skirt_East", new Vector3(skirtE, drop, (skirtN + skirtS) * 0.5f), skirtN - skirtS, 90f);

            // ---- 3. Parapets with corner pillars, a gap at the entrance (north) and the stairs (south); two benches.
            for (float x = left; x <= right + 1e-3f; x += s)
            {
                if (Mathf.Abs(x - middle) >= 0.6f * s)
                {
                    Stone(palette.wallStraight, "North_Parapet", new Vector3(x, 0f, north), SS3DTint.Wall);
                    Stone(palette.wallStraight, "South_Parapet", new Vector3(x, 0f, south), SS3DTint.Wall);
                }
            }
            for (float z = south; z <= north + 1e-3f; z += s)
            {
                Stone(palette.wallStraight, "West_Parapet", new Vector3(left, 0f, z), SS3DTint.Wall, Quaternion.Euler(0f, 90f, 0f));
                Stone(palette.wallStraight, "East_Parapet", new Vector3(right, 0f, z), SS3DTint.Wall, Quaternion.Euler(0f, 90f, 0f));
            }
            Stone(palette.pillar, "Corner_NW", new Vector3(left, 0f, north), SS3DTint.Stone); Stone(palette.pillar, "Corner_NE", new Vector3(right, 0f, north), SS3DTint.Stone);
            Stone(palette.pillar, "Corner_SW", new Vector3(left, 0f, south), SS3DTint.Stone); Stone(palette.pillar, "Corner_SE", new Vector3(right, 0f, south), SS3DTint.Stone);
            Wood(palette.bench, "Bench_W", new Vector3(left + 0.42f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, 90f, 0f));
            Wood(palette.bench, "Bench_E", new Vector3(right - 0.42f * s, 0f, (north + south) * 0.5f), Quaternion.Euler(0f, -90f, 0f));

            // ---- 4. Palace façade on the axis: two-course wall, arch + double door, flanking pillars, lanterns, banners, topiary pair.
            float facadeZ = north + 1.9f * s;
            float facadeW = Mathf.Max(right - left, 5f * s) + 1.2f * s;
            Slab(palette.wallBlock, "Facade_Base", new Vector3(middle, drop, facadeZ + 0.1f * s), facadeW, 1.2f * s, SS3DTint.Wall, 0.95f);
            Slab(palette.wallBlock, "Facade_Upper", new Vector3(middle, drop + 0.9f, facadeZ + 0.35f * s), facadeW * 0.72f, 0.7f * s, SS3DTint.Wall, 0.8f);
            Stone(palette.archFrame, "Palace_Entrance", new Vector3(middle, drop, facadeZ - 0.45f * s), SS3DTint.Stone);
            Wood(palette.palaceDoor, "Palace_Door", new Vector3(middle, drop, facadeZ - 0.4f * s));
            Stone(palette.pillar, "Entrance_Pillar_L", new Vector3(middle - 1.35f * s, drop, facadeZ - 0.4f * s), SS3DTint.Stone);
            Stone(palette.pillar, "Entrance_Pillar_R", new Vector3(middle + 1.35f * s, drop, facadeZ - 0.4f * s), SS3DTint.Stone);
            Place(palette.lantern, "Lamp_Left", new Vector3(middle - 0.95f * s, drop, facadeZ - 0.75f * s));
            Place(palette.lantern, "Lamp_Right", new Vector3(middle + 0.95f * s, drop, facadeZ - 0.75f * s));
            Place(palette.banner, "Banner_Left", new Vector3(middle - 2.2f * s, drop, facadeZ - 0.6f * s));
            Place(palette.banner, "Banner_Right", new Vector3(middle + 2.2f * s, drop, facadeZ - 0.6f * s));
            if (decorateWithOutsidePlants)
            {
                Hedge("Topiary_L", new Vector3(middle - 2.0f * s, drop, facadeZ - 1.1f * s), 1.1f);
                Hedge("Topiary_R", new Vector3(middle + 2.0f * s, drop, facadeZ - 1.1f * s), 1.1f);
            }
            // carpet axis: door → terrace gap, and terrace gap → stairs
            Carpet("Carpet_North", new Vector3(middle, drop + 0.03f, (north + 0.95f * s + facadeZ - 0.4f * s) * 0.5f), 0.9f * s, facadeZ - 0.4f * s - (north + 0.95f * s));
            Carpet("Carpet_South", new Vector3(middle, drop + 0.03f, south - 1.35f * s), 0.9f * s, 0.8f * s);

            // ---- 5. South terrace: one wide double staircase on the axis, sphere pillars, two large planter groups.
            Stone(palette.stairs, "Entry_Stairs_Upper", new Vector3(middle, drop, south - 1.5f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Stone(palette.stairs, "Entry_Stairs_Lower", new Vector3(middle, drop - 0.38f, south - 3.1f * s), SS3DTint.Stone, Quaternion.Euler(0f, 180f, 0f));
            Stone(palette.pillar, "Stair_Pillar_L", new Vector3(middle - 1.3f * s, drop, south - 1.2f * s), SS3DTint.Stone);
            Stone(palette.pillar, "Stair_Pillar_R", new Vector3(middle + 1.3f * s, drop, south - 1.2f * s), SS3DTint.Stone);
            if (decorateWithOutsidePlants)
            {
                Hedge("Planter_Group_SW", new Vector3(left - 0.3f * s, drop, south - 2.0f * s), 1.6f);
                Hedge("Planter_Group_SE", new Vector3(right + 0.3f * s, drop, south - 2.0f * s), 1.6f);
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

        private void Skirt(string name, Vector3 p, float length, float yaw)
        {
            var obj = Place(palette.wallStraight, name, p, Quaternion.Euler(0f, yaw, 0f));
            if (obj == null) return;
            obj.transform.localScale = new Vector3(length / 1.08f, 0.42f, 1.0f);
            SS3DArtTuning.Tint(obj, SS3DTint.Stone, palette);
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
