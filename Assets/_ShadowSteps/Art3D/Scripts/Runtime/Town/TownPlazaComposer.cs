using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Builds a <see cref="TownPlazaLayout"/> around a built level: the plaza pavement (procedural stone in the layout
    /// tints, never on the grid), a kerb, the ground over the visible area, then every authored placement at its
    /// plaza-edge anchor. Deterministic (no random numbers), visual only, colliders stripped from every instance.
    /// Also applies the layout's camera and lighting so one asset fully describes the scene look.
    /// </summary>
    public sealed class TownPlazaComposer : MonoBehaviour
    {
        public TownPlazaLayout layout;
        public SS3DArtPalette palette;
        public SS3DBoardCameraFitter fitter;
        private Transform _root;
        private readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();
        public Rect PlazaRect { get; private set; }
        public Rect VisibleRect { get; private set; }

        public void Clear() { if (_root != null) SS3DLevelRenderer.SafeDestroy(_root.gameObject); _root = null; }

        /// <summary>Camera settings from the layout; call before the fitter fits the board.</summary>
        public void ApplyCamera(SS3DBoardCameraFitter f)
        {
            if (f == null || layout == null) return;
            f.usePerspective = !layout.orthographic; f.fieldOfView = layout.fieldOfView;
            f.downwardAngle = layout.pitch; f.yaw = layout.yaw; f.paddingTiles = layout.paddingTiles;
            f.topReservedFraction = layout.topReserved; f.bottomReservedFraction = layout.bottomReserved;
            f.minWidthTiles = 0f; f.minDepthTiles = 0f;
        }

        public void ApplyLighting(Camera camera)
        {
            if (layout == null) return;
            var sun = SS3DArtTuning.ApplyLighting(palette, camera);
            if (sun != null) { sun.color = layout.sunColor; sun.intensity = layout.sunIntensity; sun.shadowStrength = layout.shadowStrength; sun.transform.rotation = Quaternion.Euler(layout.sunAngles.x, layout.sunAngles.y, 0f); sun.shadows = LightShadows.Soft; }
            RenderSettings.ambientSkyColor = layout.ambientSky; RenderSettings.ambientEquatorColor = layout.ambientEquator; RenderSettings.ambientGroundColor = layout.ambientGround;
            if (camera != null) camera.backgroundColor = layout.background;
            const string fillName = "Plaza_Fill_Light";
            var fillGo = GameObject.Find(fillName); var fill = fillGo != null ? fillGo.GetComponent<Light>() : null;
            if (fill == null) { fill = new GameObject(fillName).AddComponent<Light>(); fill.type = LightType.Directional; }
            fill.shadows = LightShadows.None; fill.color = layout.fillColor; fill.intensity = layout.fillIntensity; fill.enabled = layout.fillIntensity > 0f;
            fill.transform.rotation = camera != null ? Quaternion.Euler(camera.transform.eulerAngles.x - 8f, camera.transform.eulerAngles.y, 0f) : Quaternion.Euler(45f, 45f, 0f);
            var volume = SS3DArtTuning.ApplyPostProcessing(palette, camera);
            if (volume != null && volume.sharedProfile != null)
            {
                if (volume.sharedProfile.TryGet<ColorAdjustments>(out var adj)) { adj.postExposure.Override(layout.postExposure); adj.saturation.Override(layout.saturation); adj.contrast.Override(layout.contrast); }
                if (volume.sharedProfile.TryGet<Vignette>(out var vig)) vig.intensity.Override(layout.vignette);
                if (volume.sharedProfile.TryGet<DepthOfField>(out var dof))
                {
                    dof.active = layout.depthOfField > 0f;
                    if (camera != null)
                    {
                        float d = Vector3.Distance(camera.transform.position, Vector3.zero);
                        dof.gaussianStart.Override(d + 3.5f); dof.gaussianEnd.Override(d + 3.5f + 12f * (1.05f - layout.depthOfField)); dof.gaussianMaxRadius.Override(0.8f);
                    }
                }
            }
        }

        [ContextMenu("Build Around Level")]
        public void Build(SS3DLevelRenderer board)
        {
            Clear();
            if (board == null || !board.IsBuilt || layout == null || palette == null) return;
            _mats.Clear();
            var stage = board.Stage;
            _root = new GameObject("Town_Plaza_Decoration_Only").transform;
            _root.SetParent(transform, false);
            float s = board.tileSpacing, ft = palette.floorTop;
            float gx0 = board.ToWorld(0, 0).x - 0.5f * s, gx1 = board.ToWorld(stage.Width - 1, 0).x + 0.5f * s;
            float gz1 = board.ToWorld(0, 0).z + 0.5f * s, gz0 = board.ToWorld(0, stage.Height - 1).z - 0.5f * s;
            float px0 = gx0 - layout.marginWest * s, px1 = gx1 + layout.marginEast * s, pz0 = gz0 - layout.marginSouth * s, pz1 = gz1 + layout.marginNorth * s;
            PlazaRect = Rect.MinMaxRect(px0, pz0, px1, pz1);
            float middle = (px0 + px1) * 0.5f, centreZ = (pz0 + pz1) * 0.5f;
            float ground = -0.02f;

            Rect visible = fitter != null ? fitter.VisibleGroundRect(fitter.designAspect, ft) : Rect.MinMaxRect(px0 - 6f * s, pz0 - 6f * s, px1 + 6f * s, pz1 + 8f * s);
            visible = Rect.MinMaxRect(visible.xMin - 3f * s, visible.yMin - 3f * s, visible.xMax + 3f * s, visible.yMax + 3f * s);
            VisibleRect = visible;

            // 1. Ground over the visible area.
            var groundCentre = new Vector3(visible.center.x, ground - 0.2f, visible.center.y);
            if (layout.groundPrefab != null)
            {
                var g = Place(layout.groundPrefab, "Ground", Vector3.zero, Quaternion.identity);
                SS3DKitFit.Fit(g, groundCentre, visible.width, 0.2f, visible.height, false);
                Tint(g, layout.groundTint);
            }
            else Box("Ground", groundCentre, visible.width, 0.2f, visible.height, 0f, layout.groundTint);

            // 2. Plaza pavement around the grid (stone tiles in the pavement tints), grout slab, low kerb.
            board.FillStoneTiles(px0, gx0, pz0, pz1, "Pavement_W", _root, layout.pavementA, layout.pavementB);
            board.FillStoneTiles(gx1, px1, pz0, pz1, "Pavement_E", _root, layout.pavementA, layout.pavementB);
            board.FillStoneTiles(gx0, gx1, gz1, pz1, "Pavement_N", _root, layout.pavementA, layout.pavementB);
            board.FillStoneTiles(gx0, gx1, pz0, gz0, "Pavement_S", _root, layout.pavementA, layout.pavementB);
            Box("Plaza_Base", new Vector3(middle, -0.03f, centreZ), (px1 - px0) + 0.2f * s, ft - 0.03f, (pz1 - pz0) + 0.2f * s, 0f, layout.groutColor);
            float kerbW = 0.14f * s;
            Box("Kerb_N", new Vector3(middle, -0.03f, pz1 + kerbW * 0.5f), (px1 - px0) + 2f * kerbW, ft + 0.03f, kerbW, 0.02f, layout.kerbColor);
            Box("Kerb_S", new Vector3(middle, -0.03f, pz0 - kerbW * 0.5f), (px1 - px0) + 2f * kerbW, ft + 0.03f, kerbW, 0.02f, layout.kerbColor);
            Box("Kerb_W", new Vector3(px0 - kerbW * 0.5f, -0.03f, centreZ), kerbW, ft + 0.03f, (pz1 - pz0), 0.02f, layout.kerbColor);
            Box("Kerb_E", new Vector3(px1 + kerbW * 0.5f, -0.03f, centreZ), kerbW, ft + 0.03f, (pz1 - pz0), 0.02f, layout.kerbColor);
            if (layout.stepWidth > 0f)
            {
                float sw = layout.stepWidth * s, sh = (ft + 0.03f) * 0.55f, ox = px1 - px0 + 2f * kerbW, oz = pz1 - pz0 + 2f * kerbW;
                Box("Step_N", new Vector3(middle, -0.03f, pz1 + kerbW + sw * 0.5f), ox + 2f * sw, sh, sw, 0.02f, layout.stepColor);
                Box("Step_S", new Vector3(middle, -0.03f, pz0 - kerbW - sw * 0.5f), ox + 2f * sw, sh, sw, 0.02f, layout.stepColor);
                Box("Step_W", new Vector3(px0 - kerbW - sw * 0.5f, -0.03f, centreZ), sw, sh, oz, 0.02f, layout.stepColor);
                Box("Step_E", new Vector3(px1 + kerbW + sw * 0.5f, -0.03f, centreZ), sw, sh, oz, 0.02f, layout.stepColor);
            }

            if (layout.outerPavement)
            {
                float ox0 = px0 - kerbW - Mathf.Max(0f, layout.stepWidth) * s, ox1 = px1 + kerbW + Mathf.Max(0f, layout.stepWidth) * s;
                float oz0 = pz0 - kerbW - Mathf.Max(0f, layout.stepWidth) * s, oz1 = pz1 + kerbW + Mathf.Max(0f, layout.stepWidth) * s;
                board.FillStoneTiles(visible.xMin, ox0, visible.yMin, visible.yMax, "Outer_W", _root, layout.outerPavementA, layout.outerPavementB, layout.outerPaverSize * s);
                board.FillStoneTiles(ox1, visible.xMax, visible.yMin, visible.yMax, "Outer_E", _root, layout.outerPavementA, layout.outerPavementB, layout.outerPaverSize * s);
                board.FillStoneTiles(ox0, ox1, oz1, visible.yMax, "Outer_N", _root, layout.outerPavementA, layout.outerPavementB, layout.outerPaverSize * s);
                board.FillStoneTiles(ox0, ox1, visible.yMin, oz0, "Outer_S", _root, layout.outerPavementA, layout.outerPavementB, layout.outerPaverSize * s);
            }

            // 3. Authored placements.
            if (layout.placements == null) return;
            foreach (var p in layout.placements)
            {
                if (p == null || p.prefab == null) continue;
                for (int r = 0; r <= Mathf.Max(0, p.repeat); r++)
                {
                    float ax = Mathf.Lerp(px0, px1, (p.anchor.x + 1f) * 0.5f), az = Mathf.Lerp(pz0, pz1, (p.anchor.y + 1f) * 0.5f);
                    var pos = new Vector3(ax + (p.offset.x + r * p.repeatStep.x) * s, ground + p.elevation * s, az + (p.offset.y + r * p.repeatStep.y) * s);
                    var go = Place(p.prefab, r == 0 ? p.name : p.name + "_" + r, pos, Quaternion.Euler(0f, p.yaw, 0f));
                    if (go == null) continue;
                    switch (p.fit)
                    {
                        case TownPlazaLayout.FitMode.KitScale: SS3DKitFit.Scale(go, pos, layout.kitScale); break;
                        case TownPlazaLayout.FitMode.Height: SS3DKitFit.FitUniform(go, pos, float.PositiveInfinity, p.size.y * s); break;
                        case TownPlazaLayout.FitMode.Footprint: SS3DKitFit.FitUniform(go, pos, p.size.x * s, p.size.y * s); break;
                        case TownPlazaLayout.FitMode.Box: SS3DKitFit.Fit(go, pos, p.size.x > 0f ? p.size.x * s : float.NaN, p.size.y > 0f ? p.size.y * s : float.NaN, p.size.z > 0f ? p.size.z * s : float.NaN, false); break;
                    }
                    Tint(go, p.tint);
                }
            }
        }

        // ---- helpers

        private GameObject Box(string name, Vector3 p, float sizeX, float height, float sizeZ, float chamfer, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = transform.TransformPoint(p);
            go.AddComponent<MeshFilter>().sharedMesh = SS3DMeshes.ChamferedBox(Mathf.Max(0.02f, sizeX), Mathf.Max(0.02f, height), Mathf.Max(0.02f, sizeZ), chamfer);
            if (!_mats.TryGetValue(color, out var mat) || mat == null) { mat = SS3DLevelRenderer.MakeLitMaterial("SS3D Plaza " + ColorUtility.ToHtmlStringRGB(color), color); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f); _mats[color] = mat; }
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static void Tint(GameObject go, Color tint)
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

        private GameObject Place(GameObject prefab, string name, Vector3 p, Quaternion rotation)
        {
            if (prefab == null) return null;
            var obj = Instantiate(prefab, transform.TransformPoint(p), rotation, _root);
            obj.name = name;
            foreach (var c in obj.GetComponentsInChildren<Collider>(true)) SS3DLevelRenderer.SafeDestroy(c);
            return obj;
        }
    }
}
