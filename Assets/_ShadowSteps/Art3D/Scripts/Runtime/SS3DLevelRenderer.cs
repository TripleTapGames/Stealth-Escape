using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Art3D
{
    /// <summary>
    /// Visual-only 3D renderer of a V4 <see cref="Stage"/>. Builds floor, walls, blocked cover, node markers, connections,
    /// doors, exit, key, characters and wall cameras from the authoritative stage definition, and exposes setters that the
    /// game session (or the editor preview) drives. It never decides movement, visibility, collision or solver results:
    /// every overlay cell it draws is handed to it by the rules engine. All instantiated art has its colliders removed so
    /// decorative meshes can never intercept touches.
    /// </summary>
    public sealed class SS3DLevelRenderer : MonoBehaviour
    {
        public SS3DArtPalette palette;
        [Min(0.5f)] public float tileSpacing = 1f;
        public bool alternateFloorTiles = true;
        public bool renderNodeMarkers = true;
        public bool renderConnectionLines = true;
        public float connectionWidth = 0.03f;
        [Tooltip("Height of overlay quads above the floor top.")]
        public float overlayLift = 0.05f;

        public Stage Stage { get; private set; }
        public bool IsBuilt => Stage != null;
        public Transform PlayerTransform => _player;
        public IReadOnlyList<Transform> GuardTransforms => _guards;
        public IReadOnlyList<Transform> CameraHeads => _cameraHeads;

        private Transform _geometry, _nodes, _lines, _actors, _overlays, _markers;
        private Transform _player;
        private Animator _playerAnimator;
        private readonly List<Transform> _guards = new List<Transform>();
        private readonly List<Animator> _guardAnimators = new List<Animator>();
        private readonly List<Transform> _guardFacing = new List<Transform>();
        private readonly List<Transform> _guardNext = new List<Transform>();
        private readonly List<Transform> _guardRotation = new List<Transform>();
        private readonly List<Transform> _cameraHeads = new List<Transform>();
        private readonly List<Vector3> _cameraPositions = new List<Vector3>();
        private readonly List<Transform> _cameraBadges = new List<Transform>();
        private readonly List<Transform> _cameraHighlights = new List<Transform>();
        private readonly List<Renderer[]> _cameraHeadRenderers = new List<Renderer[]>();
        private readonly List<Light> _cameraLights = new List<Light>();
        private readonly List<LightPool> _cameraPools = new List<LightPool>();
        private Material _poolMat, _rayMat;
        private readonly Dictionary<int, Transform> _doors = new Dictionary<int, Transform>();
        private readonly Dictionary<int, bool> _doorOpen = new Dictionary<int, bool>();
        private readonly Dictionary<GridCoord, Renderer> _nodeMarkers = new Dictionary<GridCoord, Renderer>();
        private readonly List<Renderer> _connectionRenderers = new List<Renderer>();
        private readonly Dictionary<EdgeKey, Renderer> _connectionByEdge = new Dictionary<EdgeKey, Renderer>();
        private Transform _key;
        private readonly OverlayPool _guardVision = new OverlayPool(), _cameraVision = new OverlayPool(), _previewGuard = new OverlayPool(), _previewCamera = new OverlayPool(), _targets = new OverlayPool();
        private readonly List<Transform> _clouds = new List<Transform>();
        private Transform _previewMarker;
        private Material _lineMat, _lineReachableMat, _nodeMat, _nodeReachableMat, _cloudMat, _markerNextMat, _markerRotMat, _markerFacingMat, _badgeMat, _highlightMat;
        private readonly Dictionary<string, Material> _overlayMats = new Dictionary<string, Material>();

        public Vector3 ToWorld(GridCoord c) => Stage == null ? Vector3.zero : transform.TransformPoint(SS3DGrid.ToWorld(Stage, c, tileSpacing));
        public Vector3 ToWorld(int x, int y) => ToWorld(new GridCoord(x, y));
        public GridCoord ToGrid(Vector3 world) => SS3DGrid.ToGrid(Stage, transform.InverseTransformPoint(world), tileSpacing);
        public float FloorTop => palette != null ? palette.floorTop : 0.112f;
        public Vector3 CameraWorldPosition(int i) => i >= 0 && i < _cameraPositions.Count ? _cameraPositions[i] : Vector3.zero;

        // ---------------------------------------------------------------- build

        public void Build(Stage stage)
        {
            if (stage == null) throw new System.ArgumentNullException(nameof(stage));
            if (palette == null) throw new System.InvalidOperationException("SS3DLevelRenderer on '" + name + "' has no SS3DArtPalette. Assign " + SS3DArtPalette.DefaultAssetPath + " (editor tools must load it by path after a scene switch, because Unity unloads assets that are only referenced from C# locals).");
            var problems = new List<string>();
            if (!palette.Validate(problems)) throw new System.InvalidOperationException("SS3DArtPalette '" + palette.name + "' is incomplete:\n - " + string.Join("\n - ", problems));
            Clear();
            Stage = stage;
            _geometry = Root("Generated_Level_Geometry"); _nodes = Root("Movement_Nodes"); _lines = Root("Movement_Connections");
            _actors = Root("Actor_Visuals"); _overlays = Root("Gameplay_Vision_Overlays"); _markers = Root("Direction_Markers");
            float s = tileSpacing;
            int coverIndex = 0;

            for (int y = 0; y < stage.Height; y++)
                for (int x = 0; x < stage.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var pos = ToWorld(c);
                    bool altTile = alternateFloorTiles && ((x + y) % 2 == 1) && palette.floorB != null;
                    var floor = altTile ? palette.floorB : palette.floorA;
                    SS3DArtTuning.Tint(Create(floor, "Floor_" + x + "_" + y, pos, Quaternion.identity, _geometry), altTile ? SS3DTint.FloorB : SS3DTint.FloorA, palette);
                    if (stage.IsWall(c))
                    {
                        var wall = Create(palette.wallBlock, "SolidWall_" + x + "_" + y, pos, Quaternion.identity, _geometry);
                        SS3DArtTuning.Tint(wall, SS3DTint.Wall, palette);
                        if (wall != null) wall.transform.localScale = new Vector3(wall.transform.localScale.x, wall.transform.localScale.y * palette.wallHeightScale, wall.transform.localScale.z);
                    }
                    else if (stage.IsBlocked(c))
                    {
                        // V4 'X': a solid 1×1 obstacle on this cell only. The rule uses the whole cell regardless of the mesh height.
                        var variants = palette.coverObstacles;
                        var prefab = variants != null && variants.Length > 0 ? variants[coverIndex++ % variants.Length] : null;
                        var cover = Create(prefab, "Cover_X_" + x + "_" + y, pos, Quaternion.identity, _geometry);
                        SS3DArtTuning.Tint(cover, coverIndex % 3 == 2 ? SS3DTint.Foliage : SS3DTint.Cover, palette);
                        if (cover != null) cover.transform.localScale = new Vector3(cover.transform.localScale.x, cover.transform.localScale.y * palette.coverHeightScale, cover.transform.localScale.z);
                    }
                    else if (renderNodeMarkers)
                    {
                        var marker = Create(palette.node, "Node_" + x + "_" + y, pos, Quaternion.identity, _nodes);
                        var r = marker != null ? marker.GetComponentInChildren<Renderer>() : null;
                        if (r != null) _nodeMarkers[c] = r;
                    }
                }

            if (renderConnectionLines)
                foreach (var a in stage.FloorNodes())
                    foreach (var d in new[] { Direction.East, Direction.South })
                    {
                        var b = a.Neighbor(d);
                        if (!stage.IsConnected(a, b)) continue;
                        int door = stage.DoorIndex(a, b);
                        AddConnection(a, b);
                        if (door >= 0)
                        {
                            var pa = ToWorld(a); var pb = ToWorld(b);
                            var dir = (pb - pa).normalized;
                            var rot = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
                            var obj = Create(palette.lockedDoor, "Door_" + door, (pa + pb) * 0.5f, rot, _geometry);
                            SS3DArtTuning.Tint(obj, SS3DTint.Wood, palette);
                            if (obj != null) { _doors[door] = obj.transform; _doorOpen[door] = false; }
                        }
                    }

            SS3DArtTuning.Tint(Create(palette.exitPad, "Exit", ToWorld(stage.Exit), Quaternion.identity, _geometry), SS3DTint.Exit, palette);
            if (stage.Key.HasValue) { var k = Create(palette.collectibleKey, "Key", ToWorld(stage.Key.Value), Quaternion.identity, _geometry); _key = k != null ? k.transform : null; }

            var spy = CreateCharacter(palette.Spy, "Spy", ToWorld(stage.Start), SS3DGrid.Facing(Direction.North));
            _player = spy != null ? spy.transform : null;
            _playerAnimator = spy != null ? spy.GetComponentInChildren<Animator>() : null;
            PlayState(_playerAnimator, palette.idleStateName);

            for (int i = 0; i < stage.Guards.Count; i++)
            {
                var pose = GuardTrack.PoseAt(stage.Guards[i], 0);
                var g = CreateCharacter(palette.Guard, "Guard_" + i, ToWorld(pose.Position), SS3DGrid.Facing(pose.Facing));
                var t = g != null ? g.transform : new GameObject("Guard_" + i).transform;
                if (g == null) t.SetParent(_actors, false);
                _guards.Add(t);
                var anim = g != null ? g.GetComponentInChildren<Animator>() : null;
                _guardAnimators.Add(anim); PlayState(anim, palette.idleStateName);
                _guardFacing.Add(MakeArrow("FacingArrow_" + i, Color.white, 0.22f * s, ref _markerFacingMat));
                _guardNext.Add(MakeArrow("NextArrow_" + i, palette.nextMarker, 0.20f * s, ref _markerNextMat));
                var rot = MakeRing("RotationMarker_" + i, palette.rotationMarker, 0.16f * s, ref _markerRotMat);
                var rotArrow = MakeArrow("RotationArrow_" + i, palette.rotationMarker, 0.10f * s, ref _markerRotMat);
                rotArrow.SetParent(rot, false);
                rotArrow.localPosition = new Vector3(0.16f * s, 0f, 0f);
                rotArrow.localRotation = Quaternion.Euler(0f, stage.Guards[i].Clockwise ? 180f : 0f, 0f);
                rot.gameObject.SetActive(stage.Guards[i].Kind == GuardKind.Rotating);
                _guardRotation.Add(rot);
            }

            for (int i = 0; i < stage.Cameras.Count; i++)
            {
                var cam = stage.Cameras[i];
                var basePoint = ToWorld(cam.WallCell) + SS3DGrid.Heading(cam.MountFace) * (0.501f * s);
                _cameraPositions.Add(basePoint);
                Transform head;
                if (palette.cameraMount != null && palette.cameraHead != null)
                {
                    Create(palette.cameraMount, "Camera_" + i + "_Bracket", basePoint, SS3DGrid.Facing(cam.MountFace), _actors);
                    var h = Create(palette.cameraHead, "Camera_" + i + "_Head", basePoint, SS3DGrid.Facing(cam.Facing), _actors);
                    head = h != null ? h.transform : new GameObject("Camera_" + i + "_Head").transform;
                }
                else
                {
                    var h = Create(palette.cameraModel, "Camera_" + i, basePoint, SS3DGrid.Facing(cam.Facing), _actors);
                    head = h != null ? h.transform : new GameObject("Camera_" + i).transform;
                }
                if (head.parent == null) head.SetParent(_actors, false);
                _cameraHeads.Add(head);
                _cameraHeadRenderers.Add(head.GetComponentsInChildren<Renderer>());
                var badge = MakeRing("Camera_" + i + "_Disabled", palette.empDisabled, 0.22f * s, ref _badgeMat);
                badge.position = basePoint + Vector3.up * (FloorTop + 0.9f * s);
                badge.gameObject.SetActive(false);
                _cameraBadges.Add(badge);
                var hl = MakeRing("Camera_" + i + "_Highlight", palette.cameraHighlight, 0.34f * s, ref _highlightMat);
                hl.position = basePoint + Vector3.up * (FloorTop + 0.02f);
                hl.gameObject.SetActive(false);
                _cameraHighlights.Add(hl);

                // Coverage as light: a spot light in the rotating head plus per-cell pools/rays fed by the engine.
                var lightGo = new GameObject("Camera_" + i + "_Light");
                lightGo.transform.SetParent(head, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.12f, 0.3f);
                lightGo.transform.localRotation = Quaternion.Euler(28f, 0f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Spot; light.color = palette.cameraLightColor; light.intensity = palette.cameraLightIntensity;
                light.range = palette.cameraLightRange * s; light.spotAngle = palette.cameraLightSpotAngle; light.shadows = LightShadows.None;
                light.enabled = palette.cameraProjectsLight;
                _cameraLights.Add(light);
                var pool = new LightPool(); pool.Attach(_overlays, basePoint + Vector3.up * (FloorTop + 0.35f * s));
                _cameraPools.Add(pool);
            }

            _previewMarker = MakeRing("PreviewMarker", palette.previewSafe, 0.42f * s, ref _highlightMat);
            _previewMarker.gameObject.SetActive(false);
            foreach (var pool in new[] { _guardVision, _cameraVision, _previewGuard, _previewCamera, _targets }) pool.Attach(_overlays);
        }

        public void Clear()
        {
            foreach (var name in new[] { "Generated_Level_Geometry", "Movement_Nodes", "Movement_Connections", "Actor_Visuals", "Gameplay_Vision_Overlays", "Direction_Markers" })
            {
                var t = transform.Find(name);
                if (t != null) SafeDestroy(t.gameObject);
            }
            _guards.Clear(); _guardAnimators.Clear(); _guardFacing.Clear(); _guardNext.Clear(); _guardRotation.Clear();
            _cameraHeads.Clear(); _cameraPositions.Clear(); _cameraBadges.Clear(); _cameraHighlights.Clear(); _cameraHeadRenderers.Clear(); _cameraLights.Clear(); _cameraPools.Clear();
            _doors.Clear(); _doorOpen.Clear(); _nodeMarkers.Clear(); _connectionRenderers.Clear(); _connectionByEdge.Clear(); _clouds.Clear();
            _player = null; _playerAnimator = null; _key = null; _previewMarker = null;
            foreach (var pool in new[] { _guardVision, _cameraVision, _previewGuard, _previewCamera, _targets }) pool.Reset();
            Stage = null;
        }

        // ---------------------------------------------------------------- state setters (called by the bridge / preview only)

        public void SetPlayer(GridCoord node) { if (_player != null) _player.position = ToWorld(node); }

        public void SetPlayerFacing(Direction d) { if (_player != null) _player.rotation = SS3DGrid.Facing(d) * Quaternion.Euler(0f, palette.characterYawOffset, 0f); }

        public void SetPlayerWalking(bool walking) => PlayState(_playerAnimator, walking ? palette.walkStateName : palette.idleStateName);

        public void SetGuard(int i, GuardPose3 pose, Direction? nextStep)
        {
            if (i < 0 || i >= _guards.Count) return;
            var pos = ToWorld(pose.Position);
            _guards[i].SetPositionAndRotation(pos, SS3DGrid.Facing(pose.Facing) * Quaternion.Euler(0f, palette.characterYawOffset, 0f));
            float s = tileSpacing;
            _guardFacing[i].position = pos + SS3DGrid.Heading(pose.Facing) * (0.38f * s) + Vector3.up * (FloorTop + 0.02f);
            _guardFacing[i].rotation = SS3DGrid.Facing(pose.Facing);
            _guardNext[i].gameObject.SetActive(nextStep.HasValue);
            if (nextStep.HasValue)
            {
                _guardNext[i].position = pos + new Vector3(0.30f * s, FloorTop + 0.9f * s, 0.30f * s);
                _guardNext[i].rotation = SS3DGrid.Facing(nextStep.Value);
            }
            if (_guardRotation[i].gameObject.activeSelf) _guardRotation[i].position = pos + new Vector3(-0.30f * s, FloorTop + 0.9f * s, 0.30f * s);
        }

        public void SetGuardWalking(int i, bool walking) { if (i >= 0 && i < _guardAnimators.Count) PlayState(_guardAnimators[i], walking ? palette.walkStateName : palette.idleStateName); }

        public void SetCamera(int i, Direction facing, int disabledActionsLeft, bool highlight)
        {
            if (i < 0 || i >= _cameraHeads.Count) return;
            _cameraHeads[i].rotation = SS3DGrid.Facing(facing);
            _cameraBadges[i].gameObject.SetActive(disabledActionsLeft > 0);
            _cameraBadges[i].localScale = Vector3.one * (disabledActionsLeft >= 2 ? 1f : 0.7f);
            _cameraHighlights[i].gameObject.SetActive(highlight);
            if (i < _cameraLights.Count && _cameraLights[i] != null)
            {
                _cameraLights[i].enabled = palette.cameraProjectsLight && disabledActionsLeft == 0;
                _cameraLights[i].color = palette.cameraLightColor;
            }
            foreach (var r in _cameraHeadRenderers[i])
                foreach (var m in r.materials)
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", disabledActionsLeft > 0 ? palette.empDisabled : Color.white);
        }

        public void SetDoor(int i, bool open)
        {
            if (!_doors.TryGetValue(i, out var t) || t == null) return;
            if (_doorOpen.TryGetValue(i, out bool wasOpen) && wasOpen == open) return;
            var prefab = open ? palette.openedDoor : palette.lockedDoor;
            var obj = Create(prefab, "Door_" + i, t.position, t.rotation, _geometry);
            SS3DArtTuning.Tint(obj, SS3DTint.Wood, palette);
            SafeDestroy(t.gameObject);
            _doors[i] = obj != null ? obj.transform : null;
            _doorOpen[i] = open;
        }

        public void SetKeyCollected(bool collected) { if (_key != null) _key.gameObject.SetActive(!collected); }

        public void SetReachable(IEnumerable<GridCoord> nodes, IEnumerable<EdgeKey> connections)
        {
            if (_nodeMat == null) { _nodeMat = null; }
            foreach (var kv in _nodeMarkers) Tint(kv.Value, false);
            foreach (var n in nodes) if (_nodeMarkers.TryGetValue(n, out var r)) Tint(r, true);
            foreach (var r in _connectionRenderers) r.sharedMaterial = LineMaterial(false);
            foreach (var e in connections) if (_connectionByEdge.TryGetValue(e, out var r)) r.sharedMaterial = LineMaterial(true);
        }

        public void SetClouds(IReadOnlyList<SmokeCloud> clouds)
        {
            for (int i = 0; i < _clouds.Count; i++) _clouds[i].gameObject.SetActive(false);
            for (int i = 0; i < clouds.Count; i++)
            {
                if (i >= _clouds.Count)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "SmokeCloud_" + i; go.transform.SetParent(_overlays, false);
                    StripColliders(go);
                    if (_cloudMat == null) _cloudMat = MakeOverlayMaterial("SS3D Smoke", palette.smokeCloud);
                    go.GetComponent<Renderer>().sharedMaterial = _cloudMat;
                    _clouds.Add(go.transform);
                }
                var t = _clouds[i];
                t.position = ToWorld(clouds[i].Node) + Vector3.up * (FloorTop + 0.42f * tileSpacing);
                t.localScale = Vector3.one * tileSpacing * (clouds[i].ActionsLeft >= 2 ? 0.95f : 0.75f);
                t.gameObject.SetActive(true);
            }
        }

        /// <summary>Current hazards from the rules engine: red guard sight (larger ring) and amber camera footprint (smaller ring) so overlaps show both.</summary>
        public void SetVision(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells)
        {
            _guardVision.Show(this, guardCells, Mat("guardFill", palette.guardVisionFill), Mat("guardBorder", palette.guardVisionBorder), 0.86f, 0.44f, overlayLift);
            // With light projection on, the amber ring stays (hazard border) but the fill is replaced by the per-camera light pools.
            var fill = palette.cameraProjectsLight ? Mat("camFillNone", new Color(0f, 0f, 0f, 0f)) : Mat("camFill", palette.cameraVisionFill);
            _cameraVision.Show(this, cameraCells, fill, Mat("camBorder", palette.cameraVisionBorder), 0.66f, 0.34f, overlayLift + 0.004f);
        }

        /// <summary>Per-camera detected cells: soft light pools on those cells and faint rays from the lens to each (cells only, so walls and cover cut the coverage truthfully).</summary>
        public void SetCameraCoverage(int cameraIndex, IEnumerable<GridCoord> cells)
        {
            if (cameraIndex < 0 || cameraIndex >= _cameraPools.Count) return;
            if (_poolMat == null) _poolMat = MakePoolMaterial("SS3D Camera Light Pool", palette.cameraPoolColor);
            if (_rayMat == null) _rayMat = MakeOverlayMaterial("SS3D Camera Ray", palette.cameraRayColor);
            _cameraPools[cameraIndex].Show(this, palette.cameraProjectsLight ? cells : null, _poolMat, _rayMat, palette.cameraShowRays, palette.cameraRayColor, overlayLift + 0.002f);
        }

        public void ClearCameraCoverage() { foreach (var p in _cameraPools) p.Clear(); }

        public void SetPreview(IEnumerable<GridCoord> guardCells, IEnumerable<GridCoord> cameraCells, GridCoord? marker, Color markerColor)
        {
            _previewGuard.Show(this, guardCells, Mat("prevFill", palette.previewFill), Mat("prevBorder", palette.previewBorder), 0.76f, 0.40f, overlayLift + 0.008f);
            _previewCamera.Show(this, cameraCells, Mat("prevFill", palette.previewFill), Mat("prevBorder", palette.previewBorder), 0.56f, 0.30f, overlayLift + 0.012f);
            if (_previewMarker == null) return;
            _previewMarker.gameObject.SetActive(marker.HasValue);
            if (marker.HasValue)
            {
                _previewMarker.position = ToWorld(marker.Value) + Vector3.up * (FloorTop + overlayLift + 0.016f);
                var lr = _previewMarker.GetComponent<LineRenderer>();
                if (lr != null) { lr.startColor = markerColor; lr.endColor = markerColor; }
            }
        }

        public void ClearPreview() { _previewGuard.Clear(); _previewCamera.Clear(); if (_previewMarker != null) _previewMarker.gameObject.SetActive(false); }

        public void SetTargets(IEnumerable<GridCoord> cells) => _targets.Show(this, cells, Mat("targetFill", palette.smokeTarget), Mat("targetBorder", palette.smokeTarget), 0.5f, 0.26f, overlayLift + 0.02f);
        public void ClearTargets() => _targets.Clear();

        public Bounds BoardBounds(float verticalExtent = 1.6f)
        {
            if (Stage == null) return new Bounds(transform.position, Vector3.one);
            return new Bounds(transform.position + Vector3.up * verticalExtent * 0.5f, new Vector3(Stage.Width * tileSpacing, verticalExtent, Stage.Height * tileSpacing));
        }

        // ---------------------------------------------------------------- helpers

        private void AddConnection(GridCoord a, GridCoord b)
        {
            var start = ToWorld(a); var end = ToWorld(b);
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "Path_" + a + "_" + b;
            line.transform.SetParent(_lines, false);
            line.transform.position = (start + end) * 0.5f + Vector3.up * (FloorTop + 0.012f);
            line.transform.rotation = Quaternion.LookRotation(end - start, Vector3.up);
            line.transform.localScale = new Vector3(connectionWidth * tileSpacing, 0.012f, Vector3.Distance(start, end) - 0.5f * tileSpacing);
            StripColliders(line);
            var r = line.GetComponent<Renderer>();
            r.sharedMaterial = LineMaterial(false);
            _connectionRenderers.Add(r);
            _connectionByEdge[new EdgeKey(a, b)] = r;
        }

        private Material LineMaterial(bool reachable)
        {
            if (reachable) return _lineReachableMat != null ? _lineReachableMat : (_lineReachableMat = MakeLitMaterial("SS3D Connection Reachable", palette.reachable));
            return _lineMat != null ? _lineMat : (_lineMat = MakeLitMaterial("SS3D Connection", palette.connection));
        }

        private void Tint(Renderer r, bool reachable)
        {
            if (reachable) { if (_nodeReachableMat == null) _nodeReachableMat = MakeLitMaterial("SS3D Node Reachable", palette.reachable); r.sharedMaterial = _nodeReachableMat; }
            else if (_nodeMat != null) r.sharedMaterial = _nodeMat;
            else _nodeMat = r.sharedMaterial;
        }

        private GameObject CreateCharacter(GameObject prefab, string name, Vector3 pos, Quaternion rot)
        {
            var obj = Create(prefab, name, pos, rot * Quaternion.Euler(0f, palette.characterYawOffset, 0f), _actors);
            if (obj == null) return null;
            // Normalise height from the renderer bounds so any character pack fits the one-unit tiles.
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0 && palette.characterTargetHeight > 0f)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 1e-4f)
                {
                    float scale = palette.characterTargetHeight / b.size.y;
                    obj.transform.localScale *= scale;
                    float bottom = b.min.y - pos.y;
                    obj.transform.position = pos + Vector3.up * (FloorTop - bottom * scale);
                }
            }
            return obj;
        }

        private static void PlayState(Animator animator, string state)
        {
            if (animator == null || string.IsNullOrEmpty(state) || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(state);
            if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
        }

        private Transform MakeArrow(string name, Color color, float size, ref Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_markers, false);
            var mf = go.AddComponent<MeshFilter>(); var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = SS3DMeshes.Arrow();
            if (mat == null) mat = MakeOverlayMaterial(name + " Material", color);
            mr.sharedMaterial = mat;
            go.transform.localScale = Vector3.one * size;
            return go.transform;
        }

        private Transform MakeRing(string name, Color color, float radius, ref Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_markers, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 32; lr.widthMultiplier = 0.03f * tileSpacing;
            if (mat == null) mat = MakeOverlayMaterial(name + " Material", color);
            lr.sharedMaterial = mat; lr.startColor = color; lr.endColor = color;
            for (int i = 0; i < 32; i++) { float a = i * Mathf.PI * 2f / 32f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)); }
            return go.transform;
        }

        private Material Mat(string key, Color color)
        {
            if (!_overlayMats.TryGetValue(key, out var m) || m == null) { m = MakeOverlayMaterial("SS3D " + key, color); _overlayMats[key] = m; }
            return m;
        }

        private Transform Root(string name) { var g = new GameObject(name); g.transform.SetParent(transform, false); return g.transform; }

        private static GameObject Create(GameObject prefab, string name, Vector3 pos, Quaternion rot, Transform parent)
        {
            if (prefab == null) return null;
            var obj = Instantiate(prefab, pos, rot, parent);
            obj.name = name;
            StripColliders(obj);
            return obj;
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) SafeDestroy(c);
        }

        public static Material MakeLitMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }

        public static Material MakeOverlayMaterial(string name, Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit"); if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            return mat;
        }

        public static void SafeDestroy(Object o)
        {
            if (o == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { DestroyImmediate(o); return; }
#endif
            Destroy(o);
        }

        private static Texture2D _radial;
        /// <summary>Soft radial alpha texture for light pools (generated once; no external art).</summary>
        public static Texture2D RadialTexture()
        {
            if (_radial != null) return _radial;
            const int n = 64;
            _radial = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SS3D Radial", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            _radial.SetPixels(px); _radial.Apply();
            return _radial;
        }

        public static Material MakePoolMaterial(string name, Color tint)
        {
            var m = MakeOverlayMaterial(name, tint);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", RadialTexture());
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", RadialTexture());
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // URP Unlit: additive-ish look for light
            m.renderQueue = 3001;
            return m;
        }

        /// <summary>One camera's light pools (soft discs on detected cells) and rays from the lens to each cell centre.</summary>
        private sealed class LightPool
        {
            private readonly List<Transform> _discs = new List<Transform>();
            private readonly List<LineRenderer> _rays = new List<LineRenderer>();
            private Transform _parent; private Vector3 _lens;

            public void Attach(Transform parent, Vector3 lens) { _parent = parent; _lens = lens; }

            public void Show(SS3DLevelRenderer r, IEnumerable<GridCoord> cells, Material pool, Material ray, bool showRays, Color rayColor, float lift)
            {
                Clear();
                if (cells == null || _parent == null) return;
                int i = 0;
                foreach (var c in cells)
                {
                    if (i >= _discs.Count)
                    {
                        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "CameraLightPool"; q.transform.SetParent(_parent, false); StripColliders(q);
                        _discs.Add(q.transform);
                        var lineGo = new GameObject("CameraRay"); lineGo.transform.SetParent(_parent, false);
                        var lr = lineGo.AddComponent<LineRenderer>(); lr.useWorldSpace = true; lr.positionCount = 2; lr.widthMultiplier = 0.035f * r.tileSpacing;
                        _rays.Add(lr);
                    }
                    var pos = r.ToWorld(c) + Vector3.up * (r.FloorTop + lift);
                    var d = _discs[i]; d.position = pos; d.rotation = Quaternion.Euler(90f, 0f, 0f); d.localScale = new Vector3(1.25f * r.tileSpacing, 1.25f * r.tileSpacing, 1f);
                    d.GetComponent<Renderer>().sharedMaterial = pool; d.gameObject.SetActive(true);
                    var l = _rays[i]; l.sharedMaterial = ray; l.startColor = rayColor; l.endColor = new Color(rayColor.r, rayColor.g, rayColor.b, 0f);
                    l.SetPosition(0, _lens); l.SetPosition(1, pos); l.gameObject.SetActive(showRays);
                    i++;
                }
            }

            public void Clear()
            {
                foreach (var d in _discs) if (d != null) d.gameObject.SetActive(false);
                foreach (var l in _rays) if (l != null) l.gameObject.SetActive(false);
            }
        }

        /// <summary>Pooled quad + ring per hazard cell.</summary>
        private sealed class OverlayPool
        {
            private readonly List<Transform> _quads = new List<Transform>();
            private readonly List<LineRenderer> _rings = new List<LineRenderer>();
            private readonly List<GridCoord> _shown = new List<GridCoord>();
            private Transform _parent;
            public IReadOnlyList<GridCoord> Shown => _shown;

            public void Attach(Transform parent) { _parent = parent; _quads.Clear(); _rings.Clear(); _shown.Clear(); }
            public void Reset() { _quads.Clear(); _rings.Clear(); _shown.Clear(); _parent = null; }

            public void Show(SS3DLevelRenderer r, IEnumerable<GridCoord> cells, Material fill, Material border, float quadScale, float ringRadius, float lift)
            {
                Clear();
                if (_parent == null || cells == null) return;
                foreach (var c in cells)
                {
                    int i = _shown.Count;
                    if (i >= _quads.Count)
                    {
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.name = "Overlay_Fill"; quad.transform.SetParent(_parent, false);
                        StripColliders(quad);
                        _quads.Add(quad.transform);
                        var ring = new GameObject("Overlay_Border"); ring.transform.SetParent(_parent, false);
                        var lr = ring.AddComponent<LineRenderer>();
                        lr.useWorldSpace = false; lr.loop = true; lr.positionCount = 32; lr.widthMultiplier = 0.03f * r.tileSpacing;
                        _rings.Add(lr);
                    }
                    var q = _quads[i]; var l = _rings[i];
                    var pos = r.ToWorld(c) + Vector3.up * (r.FloorTop + lift);
                    q.position = pos; q.rotation = Quaternion.Euler(90f, 0f, 0f); q.localScale = new Vector3(quadScale * r.tileSpacing, quadScale * r.tileSpacing, 1f);
                    q.GetComponent<Renderer>().sharedMaterial = fill; q.gameObject.SetActive(true);
                    l.transform.position = pos + Vector3.up * 0.004f; l.sharedMaterial = border;
                    var col = border.HasProperty("_BaseColor") ? border.GetColor("_BaseColor") : Color.white; l.startColor = col; l.endColor = col;
                    float rad = ringRadius * r.tileSpacing;
                    for (int k = 0; k < 32; k++) { float a = k * Mathf.PI * 2f / 32f; l.SetPosition(k, new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad)); }
                    l.gameObject.SetActive(true);
                    _shown.Add(c);
                }
            }

            public void Clear()
            {
                foreach (var q in _quads) if (q != null) q.gameObject.SetActive(false);
                foreach (var l in _rings) if (l != null) l.gameObject.SetActive(false);
                _shown.Clear();
            }
        }
    }

    /// <summary>Tiny procedural meshes for direction markers (no external art needed).</summary>
    public static class SS3DMeshes
    {
        private static Mesh _arrow;
        /// <summary>Flat triangle pointing +Z, 1 unit long, lying on XZ.</summary>
        public static Mesh Arrow()
        {
            if (_arrow != null) return _arrow;
            _arrow = new Mesh { name = "SS3D Arrow" };
            _arrow.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f) };
            _arrow.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            _arrow.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            _arrow.RecalculateBounds();
            return _arrow;
        }
    }
}
