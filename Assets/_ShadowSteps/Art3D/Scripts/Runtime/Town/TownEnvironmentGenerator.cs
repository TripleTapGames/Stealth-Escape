using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Builds an open town environment around a built level from a <see cref="TownEnvironmentProfile"/>: the playable
    /// grid sits on a paved plaza with an open pavement margin and a low kerb (no enclosure), a road with sidewalks,
    /// buildings and vehicles fill the far band, low props, fences, hedges, benches and trees fill the near band, and
    /// grass or concrete covers the visible ground. Every placement is outside the plaza and deterministic from the
    /// profile seed and stage id. Nothing here blocks gameplay; colliders are stripped from every instance.
    /// </summary>
    public sealed class TownEnvironmentGenerator : MonoBehaviour
    {
        public TownEnvironmentProfile profile;
        public SS3DArtPalette palette;
        public SS3DBoardCameraFitter fitter;
        [Tooltip("Kit metres per tile unit (Synty town props are ~1 m per metre).")]
        [Range(0.2f, 1.5f)] public float kitScale = 0.5f;
        private Transform _root;
        private System.Random _rng;
        private readonly System.Collections.Generic.Dictionary<Color, Material> _mats = new System.Collections.Generic.Dictionary<Color, Material>();

        public void Clear() { if (_root != null) SS3DLevelRenderer.SafeDestroy(_root.gameObject); _root = null; }

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            Clear();
            if (board == null || !board.IsBuilt || profile == null || palette == null) return;
            _mats.Clear();
            var stage = board.Stage;
            _rng = new System.Random(profile.seed * 7919 + stage.Id * 104729);
            _root = new GameObject("Town_Environment_Decoration_Only").transform;
            _root.SetParent(transform, false);
            float s = board.tileSpacing, k = kitScale, ft = palette.floorTop;
            float gx0 = board.ToWorld(0, 0).x - 0.5f * s, gx1 = board.ToWorld(stage.Width - 1, 0).x + 0.5f * s;
            float gz1 = board.ToWorld(0, 0).z + 0.5f * s, gz0 = board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
            float margin = profile.marginTiles * s;
            float px0 = gx0 - margin, px1 = gx1 + margin, pz0 = gz0 - margin, pz1 = gz1 + margin;
            float middle = (px0 + px1) * 0.5f, centreZ = (pz0 + pz1) * 0.5f;
            float ground = -0.02f; // pavement top just under the plaza tiles

            var corners = new Vector3[4];
            bool hasCorners = fitter != null && fitter.VisibleGroundCorners(fitter.designAspect, ft, corners);
            Rect visible = hasCorners ? fitter.VisibleGroundRect(fitter.designAspect, ft) : Rect.MinMaxRect(px0 - 5f * s, pz0 - 6f * s, px1 + 5f * s, pz1 + 7f * s);
            float screenBottomZ = hasCorners ? Mathf.Min(corners[0].z, corners[1].z) : pz0 - 5f * s;
            float HalfWidthAt(float z)
            {
                if (!hasCorners) return (px1 - px0) * 0.5f + 3f * s;
                float zb = (corners[0].z + corners[1].z) * 0.5f, zt = (corners[2].z + corners[3].z) * 0.5f;
                return Mathf.Lerp(Mathf.Abs(corners[1].x - corners[0].x) * 0.5f, Mathf.Abs(corners[3].x - corners[2].x) * 0.5f, Mathf.InverseLerp(zb, zt, z));
            }
            float farWest = visible.xMin - 3f * s, farEast = visible.xMax + 3f * s, farNorth = visible.yMax + 3f * s, farSouth = visible.yMin - 3f * s;

            // ---- 1. Ground over the whole visible area (profile prefab fitted, or a tinted slab).
            if (profile.groundPrefab != null)
            {
                var g = Place(profile.groundPrefab, "Ground", Vector3.zero);
                SS3DKitFit.Fit(g, new Vector3((farWest + farEast) * 0.5f, ground - 0.2f, (farNorth + farSouth) * 0.5f), farEast - farWest, 0.2f, farNorth - farSouth, false);
                Tint(g, profile.groundTint);
            }
            else Box("Ground", new Vector3((farWest + farEast) * 0.5f, ground - 0.2f, (farNorth + farSouth) * 0.5f), farEast - farWest, 0.2f, farNorth - farSouth, 0f, profile.groundTint);

            // ---- 2. Plaza: pavement margin in the floor tint (procedural tiles, never on the grid), grout under it, low kerb ring. Open on all sides.
            FillTiles(board, px0, gx0, pz0, pz1, "Pavement_W"); FillTiles(board, gx1, px1, pz0, pz1, "Pavement_E");
            FillTiles(board, gx0, gx1, gz1, pz1, "Pavement_N"); FillTiles(board, gx0, gx1, pz0, gz0, "Pavement_S");
            Box("Plaza_Base", new Vector3(middle, -0.03f, centreZ), (px1 - px0) + 0.2f * s, ft - 0.03f, (pz1 - pz0) + 0.2f * s, 0f, palette.groutColor);
            float kerbW = 0.16f * s;
            Box("Kerb_N", new Vector3(middle, -0.03f, pz1 + kerbW * 0.5f), (px1 - px0) + 2f * kerbW, ft + 0.02f, kerbW, 0.02f, profile.kerbColor);
            Box("Kerb_S", new Vector3(middle, -0.03f, pz0 - kerbW * 0.5f), (px1 - px0) + 2f * kerbW, ft + 0.02f, kerbW, 0.02f, profile.kerbColor);
            Box("Kerb_W", new Vector3(px0 - kerbW * 0.5f, -0.03f, centreZ), kerbW, ft + 0.02f, (pz1 - pz0), 0.02f, profile.kerbColor);
            Box("Kerb_E", new Vector3(px1 + kerbW * 0.5f, -0.03f, centreZ), kerbW, ft + 0.02f, (pz1 - pz0), 0.02f, profile.kerbColor);

            // ---- 3. North band: sidewalk, road with vehicles, sidewalk, then buildings across the visible width facing the plaza.
            float z = pz1 + kerbW + 0.3f * s;
            float tileLen = 2.5f * s; // 5 m kit tiles
            if (profile.roadNorth && profile.roadTile != null)
            {
                float sw = profile.sidewalkTile != null ? 1.2f * s : 0f, rw = 2.4f * s;
                if (profile.sidewalkTile != null) Strip(profile.sidewalkTile, "Sidewalk_N1", farWest, farEast, z, sw, ground, tileLen);
                z += sw;
                Strip(profile.roadTile, "Road_N", farWest, farEast, z, rw, ground - 0.01f, tileLen);
                PlaceVehicles(profile.roadVehicles, "Vehicle_N", z + rw * 0.5f, HalfWidthAt(z) + 1f * s, 0f, 2);
                z += rw;
                if (profile.sidewalkTile != null) Strip(profile.sidewalkTile, "Sidewalk_N2", farWest, farEast, z, sw, ground, tileLen);
                z += sw;
                if (profile.streetLamp != null) { Lamp("Lamp_N_W", new Vector3(middle - HalfWidthAt(z) * 0.55f, ground, z - sw - rw - sw + 0.3f * s)); Lamp("Lamp_N_E", new Vector3(middle + HalfWidthAt(z) * 0.55f, ground, z - sw - rw - sw + 0.3f * s)); }
            }
            else if (profile.streetLamp != null) { Lamp("Lamp_N_W", new Vector3(px0 - 0.5f * s, ground, pz1 + 0.6f * s)); Lamp("Lamp_N_E", new Vector3(px1 + 0.5f * s, ground, pz1 + 0.6f * s)); }
            z += 0.4f * s;
            if (profile.buildings != null && profile.buildings.Length > 0)
            {
                float hw = HalfWidthAt(z + 2f * s) + 2f * s, x = middle - hw;
                int i = 0;
                while (x < middle + hw)
                {
                    var prefab = profile.buildings[(i + profile.seed) % profile.buildings.Length];
                    var b = Place(prefab, "Building_" + i, new Vector3(x, 0f, z), Quaternion.Euler(0f, 180f, 0f)); // Synty façades face +Z; the plaza is at -Z
                    SS3DKitFit.FitUniform(b, new Vector3(x, ground, z), float.PositiveInfinity, profile.buildingHeight * (0.85f + 0.3f * (float)_rng.NextDouble()));
                    var size = SS3DKitFit.WorldSize(b);
                    SS3DKitFit.Ground(b, new Vector3(x + size.x * 0.5f, ground, z + size.z * 0.5f));
                    x += size.x + (0.6f + 0.8f * (float)_rng.NextDouble()) * s; i++;
                    if (i > 12) break;
                }
            }
            if (profile.trees != null && profile.trees.Length > 0)
                for (int i = 0; i < Mathf.Max(0, profile.treeCount / 2); i++)
                {
                    float tx = middle + Lerp(-HalfWidthAt(z) - 1f * s, HalfWidthAt(z) + 1f * s, (i + 0.5f) / Mathf.Max(1, profile.treeCount / 2));
                    Foliage(profile.trees[(i + profile.seed) % profile.trees.Length], "Tree_N_" + i, new Vector3(tx, ground, z + 0.9f * s), 2.4f + 0.8f * (float)_rng.NextDouble());
                }

            // ---- 4. South band: optional road with parked vehicles, fences or hedges along the sides, low props, benches, bins, trees in the lower corners.
            float zs = pz0 - kerbW - 0.3f * s;
            float lastRowZ = board.ToWorld(0, stage.Height - 1).z;
            if (profile.roadSouth && profile.roadTile != null)
            {
                float sw = profile.sidewalkTile != null ? 1.2f * s : 0f, rw = 2.4f * s;
                if (profile.sidewalkTile != null) Strip(profile.sidewalkTile, "Sidewalk_S1", farWest, farEast, zs - sw, sw, ground, tileLen);
                zs -= sw;
                Strip(profile.roadTile, "Road_S", farWest, farEast, zs - rw, rw, ground - 0.01f, tileLen);
                PlaceVehicles(profile.southVehicles, "Vehicle_S", zs - rw * 0.5f, HalfWidthAt(zs - rw) - 0.5f * s, 0f, 2, lastRowZ);
                zs -= rw;
                if (profile.sidewalkTile != null) Strip(profile.sidewalkTile, "Sidewalk_S2", farWest, farEast, zs - sw, sw, ground, tileLen);
                zs -= sw;
            }
            else if (profile.southVehicles != null && profile.southVehicles.Length > 0)
                PlaceVehicles(profile.southVehicles, "Vehicle_S", zs - 1.6f * s, HalfWidthAt(zs - 1.6f * s) - 0.5f * s, 0f, 2, lastRowZ);
            // fences / hedges along both sides of the near band, from the plaza corners down to the screen edge
            float fenceX = Mathf.Max((px1 - px0) * 0.5f + 0.9f * s, HalfWidthAt(zs - 2f * s) - 0.8f * s);
            if (profile.fenceSegment != null)
            {
                FenceRun("Fence_W", new Vector3(middle - fenceX, ground, zs - 0.2f * s), new Vector3(middle - fenceX, ground, Mathf.Min(zs - 0.2f * s, screenBottomZ) - 0.5f * s));
                FenceRun("Fence_E", new Vector3(middle + fenceX, ground, zs - 0.2f * s), new Vector3(middle + fenceX, ground, Mathf.Min(zs - 0.2f * s, screenBottomZ) - 0.5f * s));
            }
            else if (profile.hedge != null)
            {
                HedgeRun("Hedge_W", new Vector3(middle - fenceX, ground, zs - 0.2f * s), new Vector3(middle - fenceX, ground, screenBottomZ - 0.5f * s));
                HedgeRun("Hedge_E", new Vector3(middle + fenceX, ground, zs - 0.2f * s), new Vector3(middle + fenceX, ground, screenBottomZ - 0.5f * s));
            }
            if (profile.bench != null)
            {
                var bw = Place(profile.bench, "Bench_SW", new Vector3(middle - (px1 - px0) * 0.5f - 0.2f * s, ground, zs - 0.8f * s), Quaternion.Euler(0f, 90f, 0f)); SS3DKitFit.Scale(bw, new Vector3(middle - (px1 - px0) * 0.5f - 0.2f * s, ground, zs - 0.8f * s), k);
                var be = Place(profile.bench, "Bench_SE", new Vector3(middle + (px1 - px0) * 0.5f + 0.2f * s, ground, zs - 0.8f * s), Quaternion.Euler(0f, -90f, 0f)); SS3DKitFit.Scale(be, new Vector3(middle + (px1 - px0) * 0.5f + 0.2f * s, ground, zs - 0.8f * s), k);
            }
            if (profile.bin != null) { var bn = Place(profile.bin, "Bin_S", new Vector3(middle + (px1 - px0) * 0.5f + 0.2f * s, ground, zs + 0.1f * s)); SS3DKitFit.Scale(bn, new Vector3(middle + (px1 - px0) * 0.5f + 0.2f * s, ground, zs + 0.1f * s), k); }
            if (profile.southProps != null && profile.southProps.Length > 0)
                for (int i = 0; i < profile.southPropCount; i++)
                {
                    float sx = middle + Lerp(-fenceX + 0.8f * s, fenceX - 0.8f * s, (float)_rng.NextDouble());
                    float sz = zs - 1.0f * s - (float)_rng.NextDouble() * Mathf.Max(0.5f, (zs - 1.0f * s) - screenBottomZ - 0.5f * s);
                    if (Mathf.Abs(sx - middle) < 1.2f * s && sz > zs - 1.6f * s) continue; // keep the axis below the plaza clear
                    var prop = Place(profile.southProps[_rng.Next(profile.southProps.Length)], "Prop_S_" + i, new Vector3(sx, ground, sz), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f));
                    SS3DKitFit.Scale(prop, new Vector3(sx, ground, sz), k);
                    ClampHeight(prop, lastRowZ, sz);
                }
            if (profile.trees != null && profile.trees.Length > 0)
                for (int i = 0; i < profile.treeCount - profile.treeCount / 2; i++)
                {
                    float tz = Mathf.Max(screenBottomZ + 0.8f * s, zs - 2.2f * s - i * 1.1f * s);
                    float tx = middle + (i % 2 == 0 ? -1f : 1f) * (HalfWidthAt(tz) - 0.9f * s);
                    Foliage(profile.trees[(i + 1 + profile.seed) % profile.trees.Length], "Tree_S_" + i, new Vector3(tx, ground, tz), 2.0f + 0.6f * (float)_rng.NextDouble());
                }
            if (profile.bushes != null && profile.bushes.Length > 0)
                for (int i = 0; i < profile.bushCount; i++)
                {
                    bool northSide = i % 2 == 0;
                    float bx = middle + Lerp(-HalfWidthAt(pz1) + 0.6f * s, HalfWidthAt(pz1) - 0.6f * s, (float)_rng.NextDouble());
                    float bz = northSide ? pz1 + kerbW + 0.4f * s + (float)_rng.NextDouble() * 0.3f * s : zs - 0.3f * s - (float)_rng.NextDouble() * 0.6f * s;
                    if (Mathf.Abs(bx - middle) < 1.2f * s) continue;
                    Foliage(profile.bushes[_rng.Next(profile.bushes.Length)], "Bush_" + i, new Vector3(bx, ground, bz), 0.45f + 0.25f * (float)_rng.NextDouble());
                }
            if (profile.accents != null && profile.accents.Length > 0)
                for (int i = 0; i < profile.accentCount; i++)
                {
                    bool north = _rng.NextDouble() < 0.5;
                    float ax = middle + Lerp(-HalfWidthAt(pz1) + 0.8f * s, HalfWidthAt(pz1) - 0.8f * s, (float)_rng.NextDouble());
                    float az = north ? pz1 + kerbW + 0.5f * s : zs - 0.4f * s - (float)_rng.NextDouble() * 0.8f * s;
                    if (Mathf.Abs(ax - middle) < 1.4f * s) continue;
                    var a = Place(profile.accents[_rng.Next(profile.accents.Length)], "Accent_" + i, new Vector3(ax, ground, az), Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f));
                    SS3DKitFit.Scale(a, new Vector3(ax, ground, az), k);
                    if (!north) ClampHeight(a, lastRowZ, az);
                }
        }

        // ---- helpers

        private float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Keeps a near-band object below the line that would hide the last node row at the 58° view (height < 1.6 × distance).</summary>
        private void ClampHeight(GameObject go, float lastRowZ, float z)
        {
            if (go == null) return;
            float limit = 1.6f * Mathf.Max(0.2f, lastRowZ - z) - 0.1f;
            var size = SS3DKitFit.WorldSize(go);
            if (size.y > limit && size.y > 1e-3f) go.transform.localScale *= limit / size.y;
        }

        private void Strip(GameObject tile, string name, float x0, float x1, float z0, float depth, float y, float tileLen)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / tileLen));
            float w = (x1 - x0) / n;
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + w * (i + 0.5f);
                var t = Place(tile, name + "_" + i, new Vector3(cx, y, z0 + depth * 0.5f));
                SS3DKitFit.Fit(t, new Vector3(cx, y - 0.02f, z0 + depth * 0.5f), w + 0.01f, float.NaN, depth, false);
                var sz = SS3DKitFit.WorldSize(t); if (sz.y > 0.08f) { t.transform.localScale = new Vector3(t.transform.localScale.x, t.transform.localScale.y * 0.08f / sz.y, t.transform.localScale.z); SS3DKitFit.Ground(t, new Vector3(cx, y - 0.02f, z0 + depth * 0.5f)); }
            }
        }

        private void PlaceVehicles(GameObject[] prefabs, string name, float z, float halfWidth, float yaw, int count, float lastRowZ = float.NaN)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            for (int i = 0; i < count; i++)
            {
                float x = (i == 0 ? -1f : 1f) * (0.35f + 0.45f * (float)_rng.NextDouble()) * halfWidth;
                var v = Place(prefabs[(i + profile.seed) % prefabs.Length], name + "_" + i, new Vector3(x, 0f, z), Quaternion.Euler(0f, (i == 0 ? 90f : -90f) + yaw, 0f)); // along the road
                SS3DKitFit.Scale(v, new Vector3(x, 0f, z), kitScale);
                if (!float.IsNaN(lastRowZ)) ClampHeight(v, lastRowZ, z);
            }
        }

        private void Lamp(string name, Vector3 p)
        {
            var l = Place(profile.streetLamp, name, p);
            SS3DKitFit.FitUniform(l, p, 0.9f, 2.0f);
        }

        private void Foliage(GameObject prefab, string name, Vector3 p, float height)
        {
            var f = Place(prefab, name, p, Quaternion.Euler(0f, (float)_rng.NextDouble() * 360f, 0f));
            SS3DKitFit.FitUniform(f, p, float.PositiveInfinity, height);
        }

        private void FenceRun(string name, Vector3 a, Vector3 b)
        {
            var dir = b - a; float len = dir.magnitude; if (len < 0.3f) return; dir.Normalize();
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            float segLen = 2.5f * kitScale; int n = Mathf.Max(1, Mathf.RoundToInt(len / segLen)); float seg = len / n;
            for (int i = 0; i < n; i++)
            {
                var c = a + dir * (seg * (i + 0.5f));
                var prefab = profile.fenceGate != null && i == n / 2 && n > 2 ? profile.fenceGate : profile.fenceSegment;
                var f = Place(prefab, name + "_" + i, c, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                SS3DKitFit.Fit(f, c, seg + 0.02f, float.NaN, float.NaN, false);
                var size = SS3DKitFit.WorldSize(f); if (size.y > 1.1f) { f.transform.localScale = new Vector3(f.transform.localScale.x, f.transform.localScale.y * 1.1f / size.y, f.transform.localScale.z); SS3DKitFit.Ground(f, c); }
            }
            if (profile.fencePost != null)
                for (int i = 0; i <= n; i++) { var c = a + dir * (seg * i); var post = Place(profile.fencePost, name + "_Post_" + i, c); SS3DKitFit.FitUniform(post, c, 0.2f, 1.15f); }
        }

        private void HedgeRun(string name, Vector3 a, Vector3 b)
        {
            var dir = b - a; float len = dir.magnitude; if (len < 0.3f) return; dir.Normalize();
            bool alongX = Mathf.Abs(dir.x) > 0.5f;
            float segLen = 2.5f * kitScale; int n = Mathf.Max(1, Mathf.RoundToInt(len / segLen)); float seg = len / n;
            for (int i = 0; i < n; i++)
            {
                var c = a + dir * (seg * (i + 0.5f));
                var h = Place(profile.hedge, name + "_" + i, c, alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));
                SS3DKitFit.Fit(h, c, seg + 0.04f, 0.8f, 0.55f, false);
            }
        }

        private void FillTiles(SS3DLevelRenderer board, float x0, float x1, float z0, float z1, string name)
        {
            float s = board.tileSpacing, w = x1 - x0, d = z1 - z0; if (w < 0.05f || d < 0.05f) return;
            int nx = Mathf.Max(1, Mathf.CeilToInt(w / s - 1e-3f)), nz = Mathf.Max(1, Mathf.CeilToInt(d / s - 1e-3f));
            float tw = w / nx, td = d / nz;
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
                board.CreateStoneTile(name, transform.TransformPoint(new Vector3(x0 + tw * (i + 0.5f), 0f, z0 + td * (j + 0.5f))), (i + j) % 2 == 0, i * 31 + j * 17 + name.Length, _root, tw, td);
        }

        private GameObject Box(string name, Vector3 p, float sizeX, float height, float sizeZ, float chamfer, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = transform.TransformPoint(p);
            go.AddComponent<MeshFilter>().sharedMesh = SS3DMeshes.ChamferedBox(Mathf.Max(0.02f, sizeX), Mathf.Max(0.02f, height), Mathf.Max(0.02f, sizeZ), chamfer);
            if (!_mats.TryGetValue(color, out var mat) || mat == null) { mat = SS3DLevelRenderer.MakeLitMaterial("SS3D Town " + ColorUtility.ToHtmlStringRGB(color), color); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f); _mats[color] = mat; }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private void Tint(GameObject go, Color tint)
        {
            if (go == null || tint == Color.white) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                foreach (var m in SS3DArtTuning.InstanceMaterials(r))
                {
                    if (m == null) continue;
                    string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("_Color") ? "_Color" : null;
                    if (prop != null) m.SetColor(prop, m.GetColor(prop) * tint);
                }
        }

        private GameObject Place(GameObject prefab, string name, Vector3 p, Quaternion? rotation = null)
        {
            if (prefab == null) return null;
            var obj = Instantiate(prefab, transform.TransformPoint(p), rotation ?? Quaternion.identity, _root);
            obj.name = name;
            foreach (var c in obj.GetComponentsInChildren<Collider>(true)) SS3DLevelRenderer.SafeDestroy(c);
            return obj;
        }
    }
}
