using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.Runtime;
using ShadowSteps.V3;
using Stage = ShadowSteps.V3.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShadowSteps.Editor
{
    /// <summary>
    /// Stage editor for the shipped schema-3 pack (D-072): opens <c>levels_v4.json</c>, lets you pick any existing stage,
    /// paint its cells ('.', '#', 'X', '-', '|', '+', '_'), move start / exit / key, cut or lock connections, draw guard
    /// routes, mount cameras, edit gadgets and metadata, preview guard and camera sight at any action index, validate,
    /// solve, step the real rules, recompute scoring, save the pack (timestamped backup outside Assets), regenerate
    /// <c>validation_v4.json</c> and play the stage in the Game scene. Menu: Shadow Steps ▸ Stage Editor (levels_v4).
    /// </summary>
    public sealed class StageEditorWindow : EditorWindow
    {
        public const string PackPath = "Assets/StreamingAssets/ShadowSteps/levels_v4.json";
        public const string ValidationPath = "Assets/StreamingAssets/ShadowSteps/validation_v4.json";
        public const string BackupDir = "Backups/levels";
        public const string GameScenePath = "Assets/_ShadowSteps/Scenes/Game.unity";

        private enum Tool { Floor, Wall, Cover, PassH, PassV, PassBoth, Empty, Start, Exit, Key, Cut, Door, Route, Camera }
        private static readonly string[] ToolLabels = { "Floor .", "Wall #", "Cover X", "Pass -", "Pass |", "Pass +", "Empty _", "Start", "Exit", "Key", "Cut", "Door", "Route", "Camera" };
        private enum CellKind { Floor, Wall, Blocked, PassH, PassV, PassBoth, Empty }

        private List<Stage> _stages = new List<Stage>();
        private int _index = -1;
        private StageBuilder _b;
        private bool _dirty;
        private string _loadError;

        private Tool _tool = Tool.Wall;
        private int _selectedGuard = -1;
        private GridCoord? _anchor;
        private CellKind _paintKind;
        private GridCoord? _lastPainted;
        private readonly Stack<string> _undo = new Stack<string>();

        private string _validationText = "", _solverText = "", _simText = "";
        private List<ValidationIssue> _issues = new List<ValidationIssue>();
        private Vector2 _sideScroll, _boardScroll;
        private bool _showVision = true;
        private int _previewT;

        private StageState _sim;
        private readonly Stack<StageState> _simHistory = new Stack<StageState>();
        private List<StageAction> _solution;
        private int _solutionStep;

        private const float CellPx = 46f, Gap = 8f;
        private static readonly Direction[] Dirs = { Direction.North, Direction.East, Direction.South, Direction.West };

        [MenuItem("Shadow Steps/Stage Editor (levels_v4)")]
        public static void Open()
        {
            var w = GetWindow<StageEditorWindow>("Stage Editor");
            w.minSize = new Vector2(1100, 700);
        }

        private void OnEnable() { if (_stages.Count == 0) LoadPack(null); }

        // ------------------------------------------------------------ pack

        private void LoadPack(string keepCode)
        {
            try
            {
                var pack = StageJson.Parse(File.ReadAllText(PackPath));
                _stages = new List<Stage>(pack.Stages);
                foreach (var w in pack.Warnings) Debug.LogWarning("[StageEditor] " + w);
                _loadError = null;
                int i = 0;
                if (keepCode != null) for (int k = 0; k < _stages.Count; k++) if (_stages[k].Code == keepCode) i = k;
                _index = -1;
                Select(i);
                _dirty = false;
                titleContent = new GUIContent("Stage Editor");
            }
            catch (Exception e) { _loadError = e.Message; _stages.Clear(); _b = null; _index = -1; }
        }

        private void Commit()
        {
            if (_b == null || _index < 0 || _index >= _stages.Count) return;
            try { _stages[_index] = _b.Build(); } catch (Exception e) { Debug.LogWarning("[StageEditor] cannot commit stage: " + e.Message); }
        }

        private void Select(int i)
        {
            if (i < 0 || i >= _stages.Count) return;
            Commit();
            _index = i;
            _b = _stages[i].ToBuilder();
            _undo.Clear(); _anchor = null; _selectedGuard = -1; _sim = null; _simHistory.Clear(); _solution = null;
            _validationText = ""; _solverText = ""; _simText = ""; _issues = new List<ValidationIssue>(); _previewT = 0;
        }

        private Stage Current()
        {
            try { return _b.Build(); }
            catch (Exception e) { _validationText = "cannot build stage: " + e.Message; return null; }
        }

        private void MarkDirty() { _dirty = true; _sim = null; _simHistory.Clear(); _solution = null; titleContent = new GUIContent("Stage Editor *"); }

        private void SavePack()
        {
            Commit();
            // The game refuses to load a pack with any validation error (AppServices.InstallStages), so warn before writing one.
            var report = StageValidator.ValidatePack(_stages, new StageValidationOptions { RunSolver = false, RequireNoWait = true });
            if (!report.IsValid)
            {
                var sb = new StringBuilder();
                foreach (var issue in report.Issues) if (issue.Severity == ValidationSeverity.Error) sb.AppendLine(issue.Path + ": " + issue.Message);
                int choice = EditorUtility.DisplayDialogComplex("Pack has " + report.ErrorCount + " validation error(s)",
                    "The game will refuse to load levels_v4.json until these are fixed:\n\n" + sb.ToString().TrimEnd() + "\n\nSave anyway (work in progress) or go back and fix?", "Save anyway", "Don't save", "");
                if (choice != 0) { Debug.LogWarning("[StageEditor] save cancelled; pack has errors:\n" + report); return; }
            }
            string backup = null;
            if (File.Exists(PackPath))
            {
                Directory.CreateDirectory(BackupDir);
                backup = Path.Combine(BackupDir, "levels_v4_" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                File.Copy(PackPath, backup, true);
            }
            File.WriteAllText(PackPath, StageJson.ToJson(_stages));
            AssetDatabase.ImportAsset(PackPath);
            _dirty = false;
            titleContent = new GUIContent("Stage Editor");
            Debug.Log("[StageEditor] saved " + _stages.Count + " stages to " + PackPath + (backup != null ? " (backup " + backup + ")" : ""));
        }

        private void ExportValidation()
        {
            Commit();
            try
            {
                string json = StagePackTools.ValidationJson(_stages, out int unsolvable, (i, n, m) =>
                    !EditorUtility.DisplayCancelableProgressBar("Solving stages for validation_v4.json", (i + 1) + " / " + n + "  " + m.Code + (m.Solvable ? "  par " + m.Par : "  UNSOLVABLE"), (i + 1f) / n));
                if (json == null) { Debug.Log("[StageEditor] validation export cancelled"); return; }
                File.WriteAllText(ValidationPath, json);
                AssetDatabase.ImportAsset(ValidationPath);
                Debug.Log("[StageEditor] wrote " + ValidationPath + (unsolvable > 0 ? " with " + unsolvable + " UNSOLVABLE stage(s)" : ""));
                if (unsolvable > 0) EditorUtility.DisplayDialog("Unsolvable stages", unsolvable + " stage(s) have no route under the shipped rules. The game refuses to load an invalid pack; fix them before shipping.", "OK");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void PlayCurrent()
        {
            var stage = Current();
            if (stage == null) return;
            if (_dirty)
            {
                int choice = EditorUtility.DisplayDialogComplex("Unsaved stage edits", "The Game scene loads levels_v4.json from disk. Save the pack before playing?", "Save and play", "Cancel", "");
                if (choice != 0) return;
                SavePack();
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(GameScenePath)) { EditorUtility.DisplayDialog("Game scene missing", GameScenePath + " was not found. Run Shadow Steps ▸ Setup ▸ Generate All.", "OK"); return; }
            SessionState.SetInt(GameSceneComposer.EditorPlayStageKey, stage.Id);
            EditorSceneManager.OpenScene(GameScenePath);
            EditorApplication.EnterPlaymode();
        }

        // ------------------------------------------------------------ GUI

        private void OnGUI()
        {
            if (_loadError != null)
            {
                EditorGUILayout.HelpBox("Could not load " + PackPath + ":\n" + _loadError, MessageType.Error);
                if (GUILayout.Button("Retry")) LoadPack(null);
                return;
            }
            if (_b == null) { if (GUILayout.Button("Load levels_v4.json")) LoadPack(null); return; }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton) && (!_dirty || EditorUtility.DisplayDialog("Discard edits?", "Reload levels_v4.json from disk and lose unsaved edits?", "Reload", "Keep editing"))) LoadPack(_b.Code);
                using (new EditorGUI.DisabledScope(!_dirty)) if (GUILayout.Button("Save pack", EditorStyles.toolbarButton)) SavePack();
                if (GUILayout.Button("Save + export validation_v4", EditorStyles.toolbarButton)) { SavePack(); ExportValidation(); }
                GUILayout.Space(12);
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) Validate();
                if (GUILayout.Button("Solve", EditorStyles.toolbarButton)) Solve();
                if (GUILayout.Button("Recompute scoring", EditorStyles.toolbarButton)) RecomputeScoring();
                GUILayout.Space(12);
                if (GUILayout.Button("▶ Play stage", EditorStyles.toolbarButton)) PlayCurrent();
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_undo.Count == 0)) if (GUILayout.Button("Undo edit", EditorStyles.toolbarButton)) RestoreSnapshot();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                float boardWidth = Mathf.Max(420f, _b.Width * (CellPx + Gap) + Gap + 24f);
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(boardWidth)))
                {
                    DrawStagePicker();
                    DrawTools();
                    using (var scroll = new EditorGUILayout.ScrollViewScope(_boardScroll, GUILayout.MinHeight(_b.Height * (CellPx + Gap) + Gap + 20f)))
                    {
                        _boardScroll = scroll.scrollPosition;
                        DrawBoard();
                    }
                    DrawPreviewControls();
                    DrawSimulation();
                }
                using (var scroll = new EditorGUILayout.ScrollViewScope(_sideScroll))
                {
                    _sideScroll = scroll.scrollPosition;
                    DrawSidePanel();
                }
            }
        }

        private void DrawStagePicker()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var names = new string[_stages.Count];
                for (int i = 0; i < names.Length; i++) names[i] = _stages[i].Id + "  " + _stages[i].Code + " — " + _stages[i].Title;
                if (_index >= 0) names[_index] = _b.Id + "  " + _b.Code + " — " + _b.Title;
                int pick = EditorGUILayout.Popup(_index, names);
                if (pick != _index) Select(pick);
                using (new EditorGUI.DisabledScope(_index <= 0)) if (GUILayout.Button("◀", GUILayout.Width(28))) Select(_index - 1);
                using (new EditorGUI.DisabledScope(_index >= _stages.Count - 1)) if (GUILayout.Button("▶", GUILayout.Width(28))) Select(_index + 1);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add new after")) AddStage(false);
                if (GUILayout.Button("Duplicate")) AddStage(true);
                using (new EditorGUI.DisabledScope(_index <= 0)) if (GUILayout.Button("Move up")) MoveStage(-1);
                using (new EditorGUI.DisabledScope(_index >= _stages.Count - 1)) if (GUILayout.Button("Move down")) MoveStage(1);
                using (new EditorGUI.DisabledScope(_stages.Count <= 1))
                    if (GUILayout.Button("Delete") && EditorUtility.DisplayDialog("Delete stage", "Remove " + _b.Code + " (id " + _b.Id + ") from the pack? Saved progress keyed by this id will no longer resolve.", "Delete", "Keep"))
                    {
                        _stages.RemoveAt(_index); _b = null; int next = Mathf.Clamp(_index, 0, _stages.Count - 1); _index = -1; Select(next); MarkDirty();
                    }
            }
        }

        private void DrawTools()
        {
            _tool = (Tool)GUILayout.SelectionGrid((int)_tool, ToolLabels, 7);
            string help = null;
            switch (_tool)
            {
                case Tool.Floor: case Tool.Wall: case Tool.Cover: case Tool.PassH: case Tool.PassV: case Tool.PassBoth: case Tool.Empty:
                    help = "Click or drag to paint. Clicking a cell that already has this type turns it back into floor. '#' wall and 'X' cover block walking and sight; '-' '|' '+' passages join two nodes with one long edge; '_' is empty ground with no node."; break;
                case Tool.Cut: help = "Click a node, then the next node along a straight run, to remove (or restore) the connection between them."; break;
                case Tool.Door: help = "Click a node, then the next node along a straight run, to lock (or unlock) that connection. Doors need a key on the board."; break;
                case Tool.Route: help = "Select a guard on the right (or click an empty node to add one), then click nodes to extend its route. Right-click removes the last node."; break;
                case Tool.Camera: help = "Click a wall cell to mount a camera (click again to remove). Set its mount face, facing and kind on the right."; break;
                case Tool.Key: help = "Click a node to place the key; click the key again to remove it."; break;
            }
            if (help != null) EditorGUILayout.HelpBox(help, MessageType.None);
        }

        private void DrawBoard()
        {
            var stage = Current();
            var guardSeen = new HashSet<GridCoord>();
            var camSeen = new HashSet<GridCoord>();
            GuardPose3[] poses = null;
            Direction[] camFacing = null;
            StageState state = _sim;
            int t = _sim != null ? _sim.T : _previewT;
            if (stage != null)
            {
                try
                {
                    state = state ?? StageState.Initial(stage, StageModeUtil.Default);
                    poses = GuardTrack.PosesAt(stage, t);
                    camFacing = new Direction[stage.Cameras.Count];
                    for (int i = 0; i < stage.Cameras.Count; i++) camFacing[i] = CameraTrack.FacingAt(stage.Cameras[i], t);
                    if (_showVision)
                    {
                        for (int i = 0; i < poses.Length; i++) if (!state.IsDefeated(i)) foreach (var c in Sight.GuardVision(stage, state, poses[i])) guardSeen.Add(c);
                        for (int i = 0; i < stage.Cameras.Count; i++) foreach (var c in Sight.CameraVision(stage, state, i, t)) camSeen.Add(c);
                    }
                }
                catch (Exception) { /* mid-edit geometry; overlay skipped this frame */ }
            }
            var issueCells = new HashSet<GridCoord>();
            foreach (var i in _issues) if (i.Position.HasValue) issueCells.Add(i.Position.Value);

            var area = GUILayoutUtility.GetRect(_b.Width * (CellPx + Gap) + Gap, _b.Height * (CellPx + Gap) + Gap);
            EditorGUI.DrawRect(area, new Color(0.11f, 0.12f, 0.15f));
            Rect CellRect(GridCoord c) => new Rect(area.x + Gap + c.X * (CellPx + Gap), area.y + Gap + c.Y * (CellPx + Gap), CellPx, CellPx);

            // Cells.
            for (int y = 0; y < _b.Height; y++)
                for (int x = 0; x < _b.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var rect = CellRect(c);
                    var kind = KindOf(c);
                    Color fill;
                    switch (kind)
                    {
                        case CellKind.Wall: fill = new Color(0.24f, 0.24f, 0.27f); break;
                        case CellKind.Blocked: fill = new Color(0.46f, 0.34f, 0.2f); break;
                        case CellKind.Empty: fill = new Color(0.14f, 0.15f, 0.18f); break;
                        case CellKind.PassH: case CellKind.PassV: case CellKind.PassBoth: fill = new Color(0.17f, 0.2f, 0.25f); break;
                        default: fill = new Color(0.28f, 0.33f, 0.44f); break;
                    }
                    if (guardSeen.Contains(c)) fill = Color.Lerp(fill, new Color(1f, 0.5f, 0.15f), 0.55f);
                    if (camSeen.Contains(c)) fill = Color.Lerp(fill, new Color(0.85f, 0.25f, 0.85f), 0.5f);
                    EditorGUI.DrawRect(rect, fill);
                    if (kind == CellKind.PassH || kind == CellKind.PassBoth) EditorGUI.DrawRect(new Rect(rect.x, rect.center.y - 2, rect.width, 4), new Color(0.6f, 0.65f, 0.75f, 0.6f));
                    if (kind == CellKind.PassV || kind == CellKind.PassBoth) EditorGUI.DrawRect(new Rect(rect.center.x - 2, rect.y, 4, rect.height), new Color(0.6f, 0.65f, 0.75f, 0.6f));
                    if (issueCells.Contains(c)) DrawFrame(rect, new Color(1f, 0.2f, 0.2f), 3);
                    if (_anchor.HasValue && _anchor.Value == c) DrawFrame(rect, Color.cyan, 3);
                }

            // Connections between nodes (east and south runs only, each edge once).
            if (stage != null)
                foreach (var a in stage.FloorNodes())
                    foreach (var d in new[] { Direction.East, Direction.South })
                    {
                        var bOpt = stage.Step(a, d);
                        if (!bOpt.HasValue) continue;
                        var b = bOpt.Value;
                        var ra = CellRect(a).center; var rb = CellRect(b).center;
                        bool cut = stage.IsCut(a, b); bool door = stage.DoorIndex(a, b) >= 0;
                        var color = cut ? new Color(0.9f, 0.25f, 0.25f) : door ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.8f, 0.85f, 0.95f, 0.85f);
                        float thick = cut ? 2f : door ? 6f : 4f;
                        var r = d == Direction.East ? new Rect(ra.x + CellPx * 0.5f, ra.y - thick / 2, rb.x - ra.x - CellPx, thick)
                                                    : new Rect(ra.x - thick / 2, ra.y + CellPx * 0.5f, thick, rb.y - ra.y - CellPx);
                        EditorGUI.DrawRect(r, color);
                        if (cut) GUI.Label(new Rect(r.center.x - 8, r.center.y - 9, 16, 18), "✕", CenteredStyle(new Color(1f, 0.3f, 0.3f), 12));
                        if (door) GUI.Label(new Rect(r.center.x - 8, r.center.y - 9, 16, 18), "L", CenteredStyle(new Color(0.2f, 0.15f, 0f), 11));
                    }

            // Labels and input.
            var labelStyle = CenteredStyle(Color.white, 11);
            var coordStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1, 1, 1, 0.35f) } };
            for (int y = 0; y < _b.Height; y++)
                for (int x = 0; x < _b.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var rect = CellRect(c);
                    var label = new StringBuilder();
                    if (c == _b.Start) label.Append("S ");
                    if (c == _b.Exit) label.Append("EXIT ");
                    if (_b.Key.HasValue && _b.Key.Value == c) label.Append("K ");
                    for (int g = 0; g < _b.Guards.Count; g++)
                    {
                        var route = _b.Guards[g].Route;
                        for (int i = 0; i < route.Count; i++)
                            if (route[i] == c) label.Append(g == _selectedGuard ? "*" : "").Append((char)('A' + g)).Append(i == 0 ? "" : i.ToString()).Append(' ');
                    }
                    if (poses != null)
                        for (int g = 0; g < poses.Length; g++)
                            if (poses[g].Position == c && (state == null || !state.IsDefeated(g))) label.Append(Arrow(poses[g].Facing));
                    for (int i = 0; i < _b.Cameras.Count; i++)
                        if (_b.Cameras[i].WallCell == c) label.Append("C").Append(i + 1).Append(camFacing != null && i < camFacing.Length ? Arrow(camFacing[i]) : Arrow(_b.Cameras[i].Facing)).Append(' ');
                    if (_sim != null && _sim.Player == c) label.Append("P");
                    if (_sim != null && _sim.IsSmoked(c)) label.Append("~");
                    GUI.Label(rect, label.ToString().TrimEnd(), labelStyle);
                    GUI.Label(new Rect(rect.x + 2, rect.yMax - 13, 40, 13), x + "," + y, coordStyle);

                    var e = Event.current;
                    if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && rect.Contains(e.mousePosition))
                    {
                        if (e.type == EventType.MouseDown) { _lastPainted = null; OnCellDown(c, e.button, stage); }
                        else if (IsPaintTool(_tool) && e.button == 0 && (!_lastPainted.HasValue || _lastPainted.Value != c)) { Snapshot(); SetCell(c, _paintKind); _lastPainted = c; MarkDirty(); }
                        e.Use();
                        Repaint();
                    }
                }
        }

        private static GUIStyle CenteredStyle(Color color, int size) => new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = color }, fontSize = size, wordWrap = true };
        private static void DrawFrame(Rect r, Color c, float w)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c); EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c); EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
        private static string Arrow(Direction d) => d == Direction.North ? "▲" : d == Direction.East ? "▶" : d == Direction.South ? "▼" : "◀";
        private static bool IsPaintTool(Tool t) => t <= Tool.Empty;
        private static CellKind KindFor(Tool t) => t == Tool.Wall ? CellKind.Wall : t == Tool.Cover ? CellKind.Blocked : t == Tool.PassH ? CellKind.PassH : t == Tool.PassV ? CellKind.PassV : t == Tool.PassBoth ? CellKind.PassBoth : t == Tool.Empty ? CellKind.Empty : CellKind.Floor;

        private void OnCellDown(GridCoord c, int button, Stage stage)
        {
            if (IsPaintTool(_tool))
            {
                var want = KindFor(_tool);
                _paintKind = button == 1 ? CellKind.Floor : (KindOf(c) == want ? CellKind.Floor : want);
                Snapshot(); SetCell(c, _paintKind); _lastPainted = c; MarkDirty();
                return;
            }
            if (button == 1) { _anchor = null; if (_tool == Tool.Route && _selectedGuard >= 0 && _selectedGuard < _b.Guards.Count) { var g = _b.Guards[_selectedGuard]; if (g.Route.Count > 1) { Snapshot(); var r = new List<GridCoord>(g.Route); r.RemoveAt(r.Count - 1); _b.Guards[_selectedGuard] = With(g, route: r); MarkDirty(); } } return; }
            switch (_tool)
            {
                case Tool.Start: Snapshot(); _b.Start = c; MarkDirty(); break;
                case Tool.Exit: Snapshot(); _b.Exit = c; MarkDirty(); break;
                case Tool.Key: Snapshot(); _b.Key = _b.Key.HasValue && _b.Key.Value == c ? (GridCoord?)null : c; MarkDirty(); break;
                case Tool.Cut:
                case Tool.Door:
                    if (!_anchor.HasValue) { _anchor = c; return; }
                    var a = _anchor.Value; _anchor = null;
                    if (stage == null || a == c) return;
                    if (!stage.Spans(a, c)) { ShowNotification(new GUIContent("Pick the next node along a straight run from " + a)); return; }
                    Snapshot();
                    var key = new EdgeKey(a, c);
                    if (_tool == Tool.Cut) { int i = _b.Cuts.FindIndex(k => k.Equals(key)); if (i >= 0) _b.Cuts.RemoveAt(i); else _b.Cuts.Add(key); }
                    else { int i = _b.Doors.FindIndex(d => d.Key.Equals(key)); if (i >= 0) _b.Doors.RemoveAt(i); else _b.Doors.Add(new DoorSpec(a, c)); }
                    MarkDirty();
                    break;
                case Tool.Route:
                    if (_selectedGuard < 0 || _selectedGuard >= _b.Guards.Count)
                    {
                        Snapshot();
                        _b.Guards.Add(new GuardSpec("g" + (_b.Guards.Count + 1), GuardKind.Static, new List<GridCoord> { c }, Direction.North));
                        _selectedGuard = _b.Guards.Count - 1; MarkDirty();
                        return;
                    }
                    {
                        Snapshot();
                        var g = _b.Guards[_selectedGuard];
                        var r = new List<GridCoord>(g.Route) { c };
                        _b.Guards[_selectedGuard] = With(g, route: r);
                        MarkDirty();
                    }
                    break;
                case Tool.Camera:
                    {
                        int i = _b.Cameras.FindIndex(cam => cam.WallCell == c);
                        Snapshot();
                        if (i >= 0) _b.Cameras.RemoveAt(i);
                        else
                        {
                            if (KindOf(c) != CellKind.Wall) SetCell(c, CellKind.Wall);
                            var mount = Direction.South;
                            foreach (var d in Dirs) { var n = c.Neighbor(d); if (n.IsInside(_b.Width, _b.Height) && KindOf(n) == CellKind.Floor) { mount = d; break; } }
                            _b.Cameras.Add(new CameraSpec("cam" + (_b.Cameras.Count + 1), c, mount, mount, CameraKind.Fixed));
                        }
                        MarkDirty();
                    }
                    break;
            }
        }

        // ------------------------------------------------------------ cell model

        private CellKind KindOf(GridCoord c)
        {
            if (_b.Walls.Contains(c)) return CellKind.Wall;
            if (_b.Blocked.Contains(c)) return CellKind.Blocked;
            if (_b.Empties.Contains(c)) return CellKind.Empty;
            foreach (var p in _b.Passages) if (p.Cell == c) return p.Axis == PassageAxis.Both ? CellKind.PassBoth : p.Axis == PassageAxis.Vertical ? CellKind.PassV : CellKind.PassH;
            return CellKind.Floor;
        }

        private void SetCell(GridCoord c, CellKind kind)
        {
            _b.Walls.Remove(c); _b.Blocked.Remove(c); _b.Empties.Remove(c); _b.Passages.RemoveAll(p => p.Cell == c);
            switch (kind)
            {
                case CellKind.Wall: _b.Walls.Add(c); break;
                case CellKind.Blocked: _b.Blocked.Add(c); break;
                case CellKind.Empty: _b.Empties.Add(c); break;
                case CellKind.PassH: _b.Passages.Add(new Passage(c, PassageAxis.Horizontal)); break;
                case CellKind.PassV: _b.Passages.Add(new Passage(c, PassageAxis.Vertical)); break;
                case CellKind.PassBoth: _b.Passages.Add(new Passage(c, PassageAxis.Both)); break;
            }
            if (kind != CellKind.Floor)
            {
                _b.Cuts.RemoveAll(k => k.Lo == c || k.Hi == c);
                _b.Doors.RemoveAll(d => d.A == c || d.B == c);
                if (_b.Key.HasValue && _b.Key.Value == c) _b.Key = null;
            }
            if (kind != CellKind.Wall) _b.Cameras.RemoveAll(cam => cam.WallCell == c);
        }

        private void Resize(int w, int h)
        {
            _b.Width = w; _b.Height = h;
            bool In(GridCoord c) => c.IsInside(w, h);
            _b.Walls.RemoveAll(c => !In(c)); _b.Blocked.RemoveAll(c => !In(c)); _b.Empties.RemoveAll(c => !In(c)); _b.Passages.RemoveAll(p => !In(p.Cell));
            _b.Cuts.RemoveAll(k => !In(k.Lo) || !In(k.Hi)); _b.Doors.RemoveAll(d => !In(d.A) || !In(d.B)); _b.Cameras.RemoveAll(c => !In(c.WallCell));
            for (int i = 0; i < _b.Guards.Count; i++)
            {
                var g = _b.Guards[i]; var r = new List<GridCoord>(); foreach (var c in g.Route) if (In(c)) r.Add(c);
                if (r.Count == 0) r.Add(new GridCoord(Mathf.Min(g.Route[0].X, w - 1), Mathf.Min(g.Route[0].Y, h - 1)));
                _b.Guards[i] = With(g, route: r);
            }
            _b.Start = new GridCoord(Mathf.Min(_b.Start.X, w - 1), Mathf.Min(_b.Start.Y, h - 1));
            _b.Exit = new GridCoord(Mathf.Min(_b.Exit.X, w - 1), Mathf.Min(_b.Exit.Y, h - 1));
            if (_b.Key.HasValue && !In(_b.Key.Value)) _b.Key = null;
        }

        private static GuardSpec With(GuardSpec g, GuardKind? kind = null, IReadOnlyList<GridCoord> route = null, Direction? facing = null, bool? clockwise = null, int? offset = null, string id = null)
            => new GuardSpec(id ?? g.Id, kind ?? g.Kind, route ?? g.Route, facing ?? g.Facing, clockwise ?? g.Clockwise, offset ?? g.Offset);

        private static CameraSpec With(CameraSpec c, Direction? mount = null, Direction? facing = null, CameraKind? kind = null, bool? clockwise = null, string id = null, int? sweep = null)
            => new CameraSpec(id ?? c.Id, c.WallCell, mount ?? c.MountFace, facing ?? c.Facing, kind ?? c.Kind, clockwise ?? c.Clockwise, sweep ?? c.SweepFacings);

        // ------------------------------------------------------------ preview + simulation

        private void DrawPreviewControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _showVision = GUILayout.Toggle(_showVision, "Show sight", GUILayout.Width(90));
                using (new EditorGUI.DisabledScope(_sim != null))
                {
                    var stage = Current();
                    int period = stage != null ? Mathf.Clamp(stage.Period, 1, 64) : 1;
                    EditorGUILayout.LabelField("Action index t", GUILayout.Width(90));
                    _previewT = period > 1 ? EditorGUILayout.IntSlider(_previewT, 0, period - 1) : 0;
                    EditorGUILayout.LabelField("period " + (stage != null ? stage.Period.ToString() : "?"), GUILayout.Width(70));
                }
            }
            EditorGUILayout.LabelField("Orange = guard sight, magenta = camera sight, red ✕ = cut, yellow = locked door. Letters A, B… are guard routes (A = anchor, A1, A2 = route order).", EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSimulation()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Simulate (shipped rules: " + StageModeUtil.Label(StageModeUtil.Default) + ", no WAIT)", EditorStyles.boldLabel);
            var stage = Current();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset", GUILayout.Width(60)) && stage != null) { _sim = StageState.Initial(stage, StageModeUtil.Default); _simHistory.Clear(); _simText = "init " + _sim; _solutionStep = 0; }
                using (new EditorGUI.DisabledScope(_sim == null || stage == null))
                {
                    foreach (var d in Dirs) if (GUILayout.Button(DirectionUtil.ToToken(d), GUILayout.Width(32))) SimApply(stage, StageAction.Move(d));
                    if (stage != null && stage.WaitLimit != 0 && GUILayout.Button("WAIT", GUILayout.Width(50))) SimApply(stage, StageAction.Wait);
                    if (_sim != null && stage != null && _sim.EmpCharges > 0)
                        for (int i = 0; i < stage.Cameras.Count; i++) if (GUILayout.Button("EMP#" + i, GUILayout.Width(56))) SimApply(stage, StageAction.Emp(i));
                    using (new EditorGUI.DisabledScope(_simHistory.Count == 0)) if (GUILayout.Button("Undo", GUILayout.Width(50))) { _sim = _simHistory.Pop(); _simText = "undo → " + _sim; }
                    using (new EditorGUI.DisabledScope(_solution == null || _sim == null || _solutionStep >= (_solution?.Count ?? 0)))
                        if (GUILayout.Button("Step solution", GUILayout.Width(100))) SimApply(stage, _solution[_solutionStep++]);
                }
            }
            if (_sim != null && stage != null && _sim.SmokeCharges > 0)
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Smoke:", GUILayout.Width(50));
                    foreach (var target in StageRules.SmokeTargets(stage, _sim)) if (GUILayout.Button(target.X + "," + target.Y, GUILayout.Width(44))) SimApply(stage, StageAction.Smoke(target));
                }
            if (_sim != null) EditorGUILayout.LabelField(_simText, EditorStyles.wordWrappedMiniLabel);
        }

        private void SimApply(Stage stage, StageAction a)
        {
            var o = StageRules.TryApply(stage, _sim, a);
            if (!o.Valid) { _simText = "invalid " + a + ": " + o.Reason; return; }
            _simHistory.Push(_sim);
            _sim = o.After;
            var sb = new StringBuilder();
            sb.Append(o.Action.Token);
            if (o.Knockout) sb.Append(" knocked out guard ").Append(o.KnockedOutGuard).Append(" (").Append(o.KnockoutSide).Append(')');
            if (o.KeyCollected) sb.Append(" KEY");
            if (o.DoorOpened >= 0) sb.Append(" door ").Append(o.DoorOpened).Append(" opened");
            if (o.AlarmTriggered) sb.Append(" ALARM");
            if (o.Caught) sb.Append(" CAUGHT: ").Append(_sim.Cause).Append(" (").Append(o.CapturePhase).Append(", guard ").Append(o.CapturingGuard).Append(')');
            if (o.Lockdown) sb.Append(" LOCKDOWN: ").Append(_sim.Cause);
            if (o.Won) sb.Append(" WON in ").Append(_sim.T).Append(" actions");
            sb.Append("\n").Append(_sim);
            _simText = sb.ToString();
        }

        // ------------------------------------------------------------ side panel

        private void DrawSidePanel()
        {
            EditorGUILayout.LabelField("Stage", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int id = EditorGUILayout.IntField(new GUIContent("Id", "Save data and progression are keyed by id. Keep ids stable for shipped stages."), _b.Id);
            string code = EditorGUILayout.TextField(new GUIContent("Code", "Stable code such as FTUE-1 or LEVEL-12; must be unique in the pack."), _b.Code);
            int groupIdx = EditorGUILayout.Popup("Group", _b.Group == "FTUE" ? 0 : 1, new[] { "FTUE", "Main" });
            string title = EditorGUILayout.TextField("Title", _b.Title);
            EditorGUILayout.LabelField("Intro");
            string intro = EditorGUILayout.TextArea(_b.Intro, GUILayout.MinHeight(36));
            if (EditorGUI.EndChangeCheck()) { Snapshot(); _b.Id = id; _b.Code = code; _b.Group = groupIdx == 0 ? "FTUE" : "Main"; _b.Title = title; _b.Intro = intro; MarkDirty(); }

            using (new EditorGUILayout.HorizontalScope())
            {
                int w = EditorGUILayout.IntField("Width", _b.Width);
                int h = EditorGUILayout.IntField("Height", _b.Height);
                if ((w != _b.Width || h != _b.Height) && w >= 1 && h >= 1 && w <= 16 && h <= 16) { Snapshot(); Resize(w, h); MarkDirty(); }
            }

            EditorGUI.BeginChangeCheck();
            int smoke = EditorGUILayout.IntSlider("Smoke charges", _b.SmokeCharges, 0, 5);
            int emp = EditorGUILayout.IntSlider("EMP charges", _b.EmpCharges, 0, 5);
            int alarm = EditorGUILayout.IntSlider(new GUIContent("Alarm budget", "Counted actions granted after the first camera detection."), _b.AlarmBudget, 1, 12);
            int waitLimit = EditorGUILayout.IntField(new GUIContent("Wait limit", "0 = no WAIT (shipped policy D-044); -1 = unlimited; n = at most n WAITs."), _b.WaitLimit);
            if (EditorGUI.EndChangeCheck()) { Snapshot(); _b.SmokeCharges = smoke; _b.EmpCharges = emp; _b.AlarmBudget = alarm; _b.WaitLimit = Mathf.Max(-1, waitLimit); MarkDirty(); }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Scoring (solver-written)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Stealth par " + _b.Par + "  ★★★ ≤ " + _b.ThreeStarActions + "  ★★ ≤ " + _b.TwoStarActions + (_b.GhostOffered ? "  ghost" : ""), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Knockout par " + (_b.KnockoutPar < 0 ? "none" : _b.KnockoutPar.ToString()) + "  ★★★ ≤ " + _b.KnockoutThreeStarActions + "  ★★ ≤ " + _b.KnockoutTwoStarActions, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Use \"Recompute scoring\" after changing the layout; the game scores against these values.", EditorStyles.wordWrappedMiniLabel);

            DrawGuards();
            DrawCameras();
            DrawEdges();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Notes (one per line)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string notes = EditorGUILayout.TextArea(string.Join("\n", _b.Notes), GUILayout.MinHeight(40));
            if (EditorGUI.EndChangeCheck()) { Snapshot(); _b.Notes.Clear(); foreach (var line in notes.Split('\n')) if (line.Trim().Length > 0) _b.Notes.Add(line.TrimEnd()); MarkDirty(); }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_validationText) ? "(press Validate)" : _validationText, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(70));
            EditorGUILayout.LabelField("Solver", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_solverText) ? "(press Solve)" : _solverText, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(70));
            EditorGUILayout.LabelField("Pack: " + PackPath + (_dirty ? "  (unsaved edits)" : ""), EditorStyles.miniLabel);
        }

        private void DrawGuards()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Guards", EditorStyles.boldLabel);
            for (int i = 0; i < _b.Guards.Count; i++)
            {
                var g = _b.Guards[i];
                using (new EditorGUILayout.VerticalScope(i == _selectedGuard ? "box" : EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(i == _selectedGuard, "Guard " + (char)('A' + i) + "  (" + g.Id + ")", EditorStyles.radioButton)) { if (_selectedGuard != i) { _selectedGuard = i; _tool = Tool.Route; } }
                        if (GUILayout.Button("Remove", GUILayout.Width(64))) { Snapshot(); _b.Guards.RemoveAt(i); if (_selectedGuard >= _b.Guards.Count) _selectedGuard = _b.Guards.Count - 1; MarkDirty(); return; }
                    }
                    EditorGUI.BeginChangeCheck();
                    string gid = EditorGUILayout.TextField("Id", g.Id);
                    var kind = (GuardKind)EditorGUILayout.EnumPopup(new GUIContent("Kind", "Static holds; Rotating turns 90° per action; PingPong walks its route and back; Loop wraps from the last node to the first."), g.Kind);
                    var facing = (Direction)EditorGUILayout.EnumPopup(new GUIContent("Facing", "Authored facing (Static), initial facing (Rotating) or fallback facing (moving)."), g.Facing);
                    bool cw = g.Clockwise; int offset = g.Offset;
                    if (kind == GuardKind.Rotating) cw = EditorGUILayout.Toggle("Clockwise", g.Clockwise);
                    if (kind == GuardKind.PingPong || kind == GuardKind.Loop) offset = EditorGUILayout.IntField(new GUIContent("Offset", "Schedule offset: position index at t is derived from (t + offset)."), g.Offset);
                    if (EditorGUI.EndChangeCheck()) { Snapshot(); _b.Guards[i] = With(g, kind: kind, facing: facing, clockwise: cw, offset: offset, id: gid); MarkDirty(); }
                    EditorGUILayout.LabelField("Route: " + string.Join(" → ", g.Route) + (g.Moves ? "" : "  (anchor only)"), EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(g.Route.Count <= 1))
                        {
                            if (GUILayout.Button("Drop last node")) { Snapshot(); var r = new List<GridCoord>(g.Route); r.RemoveAt(r.Count - 1); _b.Guards[i] = With(g, route: r); MarkDirty(); }
                            if (GUILayout.Button("Keep anchor only")) { Snapshot(); _b.Guards[i] = With(g, route: new List<GridCoord> { g.Route[0] }); MarkDirty(); }
                        }
                    }
                }
            }
            if (GUILayout.Button("Add guard (then click its node on the board)")) { _selectedGuard = -1; _tool = Tool.Route; }
        }

        private void DrawCameras()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Cameras", EditorStyles.boldLabel);
            for (int i = 0; i < _b.Cameras.Count; i++)
            {
                var c = _b.Cameras[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("Camera " + (i + 1) + "  (" + c.Id + ") on wall " + c.WallCell);
                        if (GUILayout.Button("Remove", GUILayout.Width(64))) { Snapshot(); _b.Cameras.RemoveAt(i); MarkDirty(); return; }
                    }
                    EditorGUI.BeginChangeCheck();
                    string cid = EditorGUILayout.TextField("Id", c.Id);
                    var mount = (Direction)EditorGUILayout.EnumPopup(new GUIContent("Mount face", "Side of the wall the lens hangs from: must face a floor node."), c.MountFace);
                    var facing = (Direction)EditorGUILayout.EnumPopup("Facing (t=0)", c.Facing);
                    var kind = (CameraKind)EditorGUILayout.EnumPopup(new GUIContent("Kind", "Fixed; Rotating turns 90° per action; Sweeping pans back and forth over 2 or 3 facings, two actions each."), c.Kind);
                    bool cw = c.Clockwise; int sweep = c.SweepFacings;
                    if (kind != CameraKind.Fixed) cw = EditorGUILayout.Toggle("Clockwise", c.Clockwise);
                    if (kind == CameraKind.Sweeping)
                    {
                        sweep = EditorGUILayout.IntPopup(new GUIContent("Sweep arc", "2 facings: facing and its neighbour (A A B B). 3 facings: a half turn out and back (A A B B C C B B), e.g. W → N → E."), Mathf.Clamp(c.SweepFacings, 2, CameraSpec.MaxSweepFacings), new[] { new GUIContent("2 facings (quarter turn)"), new GUIContent("3 facings (half turn)") }, new[] { 2, 3 });
                        var seq = new List<string>(); foreach (var d in With(c, kind: kind, clockwise: cw, sweep: sweep).Facings()) seq.Add(DirectionUtil.ToToken(d));
                        EditorGUILayout.LabelField("Covers " + string.Join(" → ", seq) + " and back, period " + CameraTrack.Period(With(c, kind: kind, clockwise: cw, sweep: sweep)) + " actions", EditorStyles.miniLabel);
                    }
                    if (EditorGUI.EndChangeCheck()) { Snapshot(); _b.Cameras[i] = With(c, mount: mount, facing: facing, kind: kind, clockwise: cw, id: cid, sweep: sweep); MarkDirty(); }
                }
            }
            if (GUILayout.Button("Add camera (then click a wall cell)")) _tool = Tool.Camera;
        }

        private void DrawEdges()
        {
            if (_b.Doors.Count > 0 || _b.Cuts.Count > 0) EditorGUILayout.Space(6);
            if (_b.Doors.Count > 0)
            {
                EditorGUILayout.LabelField("Locked doors" + (_b.Key.HasValue ? "  (key at " + _b.Key.Value + ")" : "  — NO KEY on the board"), EditorStyles.boldLabel);
                for (int i = 0; i < _b.Doors.Count; i++)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(_b.Doors[i].ToString());
                        if (GUILayout.Button("Unlock", GUILayout.Width(64))) { Snapshot(); _b.Doors.RemoveAt(i); MarkDirty(); return; }
                    }
            }
            if (_b.Cuts.Count > 0)
            {
                EditorGUILayout.LabelField("Cut connections", EditorStyles.boldLabel);
                for (int i = 0; i < _b.Cuts.Count; i++)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(_b.Cuts[i].Lo + " ✕ " + _b.Cuts[i].Hi);
                        if (GUILayout.Button("Restore", GUILayout.Width(64))) { Snapshot(); _b.Cuts.RemoveAt(i); MarkDirty(); return; }
                    }
            }
        }

        // ------------------------------------------------------------ commands

        private void Validate()
        {
            var stage = Current();
            if (stage == null) return;
            var metrics = new StageMetrics();
            var report = StageValidator.Validate(stage, new StageValidationOptions { RequireNoWait = true }, metrics);
            _issues = new List<ValidationIssue>(report.Issues);
            _validationText = (report.IsValid ? "VALID" : "INVALID") + " — " + report.ErrorCount + " errors, " + report.WarningCount + " warnings\n" + report;
            if (report.IsValid && metrics.Solvable) _validationText += "\n" + metrics;
            for (int i = 0; i < _stages.Count; i++)
                if (i != _index && (_stages[i].Id == stage.Id || _stages[i].Code == stage.Code))
                    _validationText += "\nError pack: id " + stage.Id + " / code " + stage.Code + " collides with " + _stages[i].Code + " (id " + _stages[i].Id + ")";
        }

        private void Solve()
        {
            var stage = Current();
            if (stage == null) return;
            var m = StageMetrics.Analyze(stage, StagePackTools.ShippedSolveOptions());
            var sb = new StringBuilder();
            sb.AppendLine(m.Solvable ? "SOLVABLE" + (m.HitLimit ? " (state limit hit)" : "") : "UNSOLVABLE under " + StageModeUtil.Label(StageModeUtil.Default) + " / no WAIT" + (m.HitLimit ? " (state limit hit)" : ""));
            sb.AppendLine(m.ToString());
            if (m.Solvable)
            {
                var tokens = new List<string>(); foreach (var a in m.Solution) tokens.Add(a.Token);
                sb.AppendLine("solution: " + string.Join(" ", tokens));
                sb.Append("smoke required: " + m.SmokeRequired + "   EMP required: " + m.EmpRequired + "   knockouts in par: " + m.KnockoutsInPar + "   without knockout: " + (m.ShortestWithoutKnockout < 0 ? "none" : m.ShortestWithoutKnockout.ToString()));
                if (_b.KnockoutPar != m.Par) sb.Append("\nStored knockout par " + _b.KnockoutPar + " differs from solver par " + m.Par + ": press Recompute scoring.");
                _solution = m.Solution; _solutionStep = 0;
                _sim = StageState.Initial(stage, StageModeUtil.Default); _simHistory.Clear(); _simText = "init " + _sim + "\n(use Step solution to walk the solver's route)";
            }
            _solverText = sb.ToString();
        }

        private void RecomputeScoring()
        {
            var stage = Current();
            if (stage == null) return;
            try
            {
                EditorUtility.DisplayProgressBar("Recomputing scoring", stage.Code, 0.3f);
                var updated = StagePackTools.WithRecomputedScoring(stage, out var m);
                Snapshot();
                _b.Par = updated.Par; _b.ThreeStarActions = updated.ThreeStarActions; _b.TwoStarActions = updated.TwoStarActions; _b.GhostOffered = updated.GhostOffered;
                _b.KnockoutPar = updated.KnockoutPar; _b.KnockoutThreeStarActions = updated.KnockoutThreeStarActions; _b.KnockoutTwoStarActions = updated.KnockoutTwoStarActions;
                MarkDirty();
                _solverText = "scoring recomputed under " + StageModeUtil.Label(StageModeUtil.Default) + " / no WAIT\n" + m + (m.Solvable ? "" : "\nNo route: the stage cannot ship like this (the game refuses an unsolvable pack).");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void AddStage(bool duplicate)
        {
            Commit();
            int maxId = 0, maxLevel = 0;
            foreach (var s in _stages)
            {
                maxId = Mathf.Max(maxId, s.Id);
                if (s.Code.StartsWith("LEVEL-", StringComparison.Ordinal) && int.TryParse(s.Code.Substring(6), out int n)) maxLevel = Mathf.Max(maxLevel, n);
            }
            StageBuilder nb;
            if (duplicate) { nb = _stages[_index].ToBuilder(); nb.Title = nb.Title + " copy"; nb.Notes.Add("duplicated from " + nb.Code + " in the stage editor"); }
            else { nb = new StageBuilder { Width = 5, Height = 7, Title = "New Level", Start = new GridCoord(0, 6), Exit = new GridCoord(4, 0) }; nb.Notes.Add("created in the stage editor"); }
            nb.Id = maxId + 1; nb.Code = "LEVEL-" + (maxLevel + 1); nb.Group = "Main";
            nb.Par = 0; nb.ThreeStarActions = 0; nb.TwoStarActions = 0; nb.GhostOffered = false; nb.KnockoutPar = -1; nb.KnockoutThreeStarActions = -1; nb.KnockoutTwoStarActions = -1;
            _stages.Insert(_index + 1, nb.Build());
            _b = null; int next = _index + 1; _index = -1; Select(next);
            MarkDirty();
        }

        private void MoveStage(int delta)
        {
            Commit();
            int j = _index + delta;
            if (j < 0 || j >= _stages.Count) return;
            var tmp = _stages[_index]; _stages[_index] = _stages[j]; _stages[j] = tmp;
            _b = null; _index = -1; Select(j);
            MarkDirty();
        }

        private void Snapshot()
        {
            try
            {
                _undo.Push(StagePackTools.StageToJson(_b.Build()));
                if (_undo.Count > 60) { var arr = _undo.ToArray(); _undo.Clear(); for (int i = 59; i >= 0; i--) _undo.Push(arr[i]); }
            }
            catch { /* model temporarily invalid; skip snapshot */ }
        }

        private void RestoreSnapshot()
        {
            if (_undo.Count == 0) return;
            try { _b = StagePackTools.StageFromJson(_undo.Pop()).ToBuilder(); _anchor = null; MarkDirty(); }
            catch (Exception e) { Debug.LogWarning("[StageEditor] undo failed: " + e.Message); }
        }
    }
}
