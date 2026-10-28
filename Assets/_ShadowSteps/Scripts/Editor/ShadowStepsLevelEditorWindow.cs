using System;
using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Editor
{
    /// <summary>
    /// Schema-2 level authoring window (V2 M3): paint nodes/walls/cover, toggle edges (open, one-way, locked),
    /// place start/exit/key/door, draw guard routes, set guard cadences and vision, validate, solve with metrics,
    /// step the simulation, and import/export JSON. Export never overwrites a file without writing a timestamped backup.
    /// Menu: Shadow Steps ▸ Level Editor.
    /// </summary>
    public sealed class ShadowStepsLevelEditorWindow : EditorWindow
    {
        private enum Tool { Node, Wall, Cover, Edge, Start, Exit, Key, Door, Route }

        private LevelAuthoringModel _model = new LevelAuthoringModel();
        private Tool _tool = Tool.Wall;
        private int _selectedGuard = -1;
        private GridCoord? _edgeAnchor;
        private bool _coverTraversable = true;
        private string _validationText = "";
        private string _solverText = "";
        private Vector2 _sideScroll;
        private GameSession _sim;
        private string _lastPath;
        private readonly Stack<string> _undo = new Stack<string>();

        private const float CellPx = 56f;
        private const float Gap = 10f;

        [MenuItem("Shadow Steps/Level Editor")]
        public static void Open()
        {
            var w = GetWindow<ShadowStepsLevelEditorWindow>("Shadow Steps Levels");
            w.minSize = new Vector2(980, 640);
            if (w._model.Nodes.Count == 0) { w._model.FillRectangle(); w._model.AutoEdges(); }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("New", EditorStyles.toolbarButton)) NewLevel();
                if (GUILayout.Button("Import JSON", EditorStyles.toolbarButton)) ImportJson();
                if (GUILayout.Button("Export JSON", EditorStyles.toolbarButton)) ExportJson();
                GUILayout.Space(12);
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) Validate();
                if (GUILayout.Button("Solve + Metrics", EditorStyles.toolbarButton)) Solve();
                if (GUILayout.Button("Auto edges", EditorStyles.toolbarButton)) { Snapshot(); _model.AutoEdges(); }
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_undo.Count == 0))
                    if (GUILayout.Button("Undo edit", EditorStyles.toolbarButton)) RestoreSnapshot();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(_model.Width * (CellPx + Gap) + 40)))
                {
                    DrawToolbar();
                    DrawBoard();
                    DrawSimulation();
                }
                using (var scroll = new EditorGUILayout.ScrollViewScope(_sideScroll))
                {
                    _sideScroll = scroll.scrollPosition;
                    DrawSidePanel();
                }
            }
        }

        // ------------------------------------------------------------ board

        private void DrawToolbar()
        {
            _tool = (Tool)GUILayout.Toolbar((int)_tool, Enum.GetNames(typeof(Tool)));
            if (_tool == Tool.Cover) _coverTraversable = EditorGUILayout.ToggleLeft("Cover is traversable (low cover)", _coverTraversable);
            if (_tool == Tool.Edge) EditorGUILayout.HelpBox("Click a node, then an adjacent node. Cycles: none → open ↔ → one-way → → locked ↔ → none.", MessageType.None);
            if (_tool == Tool.Route) EditorGUILayout.HelpBox("Select a guard on the right, then click adjacent nodes to extend its route. The first node is the start.", MessageType.None);
        }

        private void DrawBoard()
        {
            var level = SafeDefinition();
            var visible = new HashSet<GridCoord>();
            GuardPose[] poses = null;
            GameState simState = _sim?.State;
            if (level != null)
            {
                var state = simState ?? RulesEngine.CreateInitialState(level);
                try { foreach (var c in RulesEngine.GetAllVisionCells(level, state)) visible.Add(c); poses = GuardSchedule.PosesAt(level, state.Phase); } catch { }
            }

            var area = GUILayoutUtility.GetRect(_model.Width * (CellPx + Gap) + Gap, _model.Height * (CellPx + Gap) + Gap);
            EditorGUI.DrawRect(area, new Color(0.12f, 0.13f, 0.17f));
            Rect CellRect(GridCoord c) => new Rect(area.x + Gap + c.X * (CellPx + Gap), area.y + Gap + c.Y * (CellPx + Gap), CellPx, CellPx);

            // Edges first.
            foreach (var from in _model.Edges)
                foreach (var to in from.Value)
                {
                    var a = CellRect(from.Key).center; var b = CellRect(to.Key).center;
                    bool reverse = _model.HasEdge(to.Key, from.Key, out var rt) && rt == to.Value;
                    var color = to.Value == EdgeType.Locked ? new Color(0.95f, 0.72f, 0.25f) : reverse ? new Color(0.45f, 0.52f, 0.65f) : new Color(0.55f, 0.75f, 0.95f);
                    var mid = (a + b) * 0.5f;
                    bool horizontal = Mathf.Abs(a.y - b.y) < 0.5f;
                    var r = horizontal ? new Rect(Mathf.Min(a.x, b.x) + CellPx * 0.4f, mid.y - 3f, Mathf.Abs(a.x - b.x) - CellPx * 0.8f, 6f)
                                       : new Rect(mid.x - 3f, Mathf.Min(a.y, b.y) + CellPx * 0.4f, 6f, Mathf.Abs(a.y - b.y) - CellPx * 0.8f);
                    EditorGUI.DrawRect(r, color);
                    if (!reverse)
                    {
                        var arrow = b + (a - b).normalized * (CellPx * 0.42f);
                        GUI.Label(new Rect(arrow.x - 8, arrow.y - 9, 16, 18), horizontal ? (b.x > a.x ? "▶" : "◀") : (b.y > a.y ? "▼" : "▲"));
                    }
                }

            for (int y = 0; y < _model.Height; y++)
                for (int x = 0; x < _model.Width; x++)
                {
                    var c = new GridCoord(x, y);
                    var rect = CellRect(c);
                    bool exists = _model.Nodes.Contains(c);
                    Color fill = !exists ? new Color(0.09f, 0.1f, 0.12f) : _model.Walls.Contains(c) ? new Color(0.2f, 0.2f, 0.24f) :
                                 _model.Cover.ContainsKey(c) ? new Color(0.3f, 0.36f, 0.3f) : new Color(0.25f, 0.3f, 0.4f);
                    if (visible.Contains(c)) fill = Color.Lerp(fill, new Color(1f, 0.45f, 0.2f), 0.55f);
                    EditorGUI.DrawRect(rect, fill);
                    if (_edgeAnchor.HasValue && _edgeAnchor.Value == c) EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 4), Color.cyan);

                    string label = "";
                    if (c == _model.Start) label += "S";
                    if (c == _model.Exit) label += "X";
                    if (_model.HasKey && c == _model.Key) label += "K";
                    if (_model.HasDoor && c == _model.Door) label += "D";
                    if (_model.Walls.Contains(c)) label += "#";
                    if (_model.Cover.TryGetValue(c, out var tr)) label += tr ? "c" : "C";
                    for (int g = 0; g < _model.Guards.Count; g++)
                    {
                        var route = _model.Guards[g].Route;
                        for (int i = 0; i < route.Count; i++) if (route[i] == c) label += (g == _selectedGuard ? "*" : "") + (char)('A' + g) + (i == 0 ? "" : i.ToString());
                    }
                    if (poses != null)
                        for (int g = 0; g < poses.Length; g++)
                            if (poses[g].Position == c) label += " " + DirectionUtil.ToToken(poses[g].Facing);
                    if (simState != null && simState.PlayerPosition == c) label += " P";
                    var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontSize = 11 };
                    GUI.Label(rect, label, style);
                    GUI.Label(new Rect(rect.x + 2, rect.yMax - 14, 40, 14), x + "," + y, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1, 1, 1, 0.4f) } });

                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        OnCellClick(c, Event.current.button);
                        Event.current.Use();
                        Repaint();
                    }
                }
        }

        private void OnCellClick(GridCoord c, int button)
        {
            Snapshot();
            switch (_tool)
            {
                case Tool.Node:
                    if (_model.Nodes.Contains(c)) { _model.Nodes.Remove(c); _model.Walls.Remove(c); _model.Cover.Remove(c); _model.RemoveEdgesTouching(c); }
                    else _model.Nodes.Add(c);
                    break;
                case Tool.Wall:
                    if (!_model.Nodes.Contains(c)) _model.Nodes.Add(c);
                    if (_model.Walls.Contains(c)) _model.Walls.Remove(c);
                    else { _model.Walls.Add(c); _model.Cover.Remove(c); _model.RemoveEdgesTouching(c); }
                    break;
                case Tool.Cover:
                    if (!_model.Nodes.Contains(c)) _model.Nodes.Add(c);
                    if (_model.Cover.ContainsKey(c)) _model.Cover.Remove(c);
                    else { _model.Cover[c] = _coverTraversable; _model.Walls.Remove(c); if (!_coverTraversable) _model.RemoveEdgesTouching(c); }
                    break;
                case Tool.Edge:
                    if (!_edgeAnchor.HasValue) { _edgeAnchor = c; _undo.Pop(); return; }
                    var a = _edgeAnchor.Value;
                    _edgeAnchor = null;
                    if (a.ManhattanDistance(c) != 1) { _undo.Pop(); return; }
                    _model.CycleEdge(a, c);
                    break;
                case Tool.Start: _model.Start = c; break;
                case Tool.Exit: _model.Exit = c; break;
                case Tool.Key: if (_model.HasKey && _model.Key == c) _model.HasKey = false; else { _model.HasKey = true; _model.Key = c; } break;
                case Tool.Door: if (_model.HasDoor && _model.Door == c) _model.HasDoor = false; else { _model.HasDoor = true; _model.Door = c; } break;
                case Tool.Route:
                    if (_selectedGuard < 0 || _selectedGuard >= _model.Guards.Count) { _undo.Pop(); return; }
                    var g = _model.Guards[_selectedGuard];
                    var route = new List<GridCoord>(g.Route);
                    if (button == 1 && route.Count > 0) route.RemoveAt(route.Count - 1);
                    else route.Add(c);
                    _model.Guards[_selectedGuard] = g.With(route: route);
                    break;
            }
            _sim = null;
        }

        // ------------------------------------------------------------ simulation

        private void DrawSimulation()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Simulate", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset")) { var l = SafeDefinition(); _sim = l != null ? new GameSession(l) : null; }
                using (new EditorGUI.DisabledScope(_sim == null))
                {
                    foreach (var a in GameActionUtil.ReferenceOrder)
                        if (GUILayout.Button(GameActionUtil.ToToken(a)))
                        {
                            var r = _sim.Apply(a);
                            _solverText = r.IsValid ? "step " + GameActionUtil.ToToken(a) + " → " + r.StateAfter + (r.Captured ? " CAPTURED " + r.Capture + " by guard " + r.CapturingGuard : "") + (r.Won ? " WON" : "")
                                                    : "invalid: " + r.Validation.Reason;
                        }
                    if (GUILayout.Button("Undo")) _sim.Undo();
                }
            }
            if (_sim != null) EditorGUILayout.LabelField(_sim.State.ToString(), EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------ side panel

        private void DrawSidePanel()
        {
            EditorGUILayout.LabelField("Level", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _model.Id = EditorGUILayout.IntField("Id", _model.Id);
            _model.Name = EditorGUILayout.TextField("Name", _model.Name);
            _model.World = EditorGUILayout.IntField("World", _model.World);
            _model.Chapter = EditorGUILayout.IntField("Chapter", _model.Chapter);
            _model.Theme = EditorGUILayout.TextField("Theme", _model.Theme);
            _model.Hint = EditorGUILayout.TextField("Tutorial hint", _model.Hint);
            using (new EditorGUILayout.HorizontalScope())
            {
                int w = EditorGUILayout.IntField("Width", _model.Width);
                int h = EditorGUILayout.IntField("Height", _model.Height);
                if ((w != _model.Width || h != _model.Height) && w >= 1 && h >= 1 && w <= 16 && h <= 16) { Snapshot(); Resize(w, h); }
            }
            _model.NodeSpacing = EditorGUILayout.FloatField("Node spacing", _model.NodeSpacing);
            _model.Smoke = EditorGUILayout.IntField("Smoke charges", _model.Smoke);
            _model.Emp = EditorGUILayout.IntField("EMP charges", _model.Emp);
            _model.SmokeTurns = EditorGUILayout.IntField("Smoke turns", _model.SmokeTurns);
            _model.NoWait = EditorGUILayout.Toggle("No WAIT", _model.NoWait);
            _model.NoUndo = EditorGUILayout.Toggle("No undo", _model.NoUndo);
            _model.Detection = (DetectionRule)EditorGUILayout.EnumPopup(new GUIContent("Detection", "beforeOrAfterGuardMove (V2): seen before or after guards move. afterGuardMove (V1): seen after guards move."), _model.Detection);
            if (EditorGUI.EndChangeCheck()) _sim = null;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Guards", EditorStyles.boldLabel);
            for (int i = 0; i < _model.Guards.Count; i++)
            {
                var g = _model.Guards[i];
                using (new EditorGUILayout.VerticalScope(i == _selectedGuard ? "box" : EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Toggle(i == _selectedGuard, "Guard " + (char)('A' + i), EditorStyles.radioButton)) _selectedGuard = i;
                        if (GUILayout.Button("Remove", GUILayout.Width(64))) { Snapshot(); _model.Guards.RemoveAt(i); if (_selectedGuard >= _model.Guards.Count) _selectedGuard = _model.Guards.Count - 1; _sim = null; return; }
                    }
                    EditorGUI.BeginChangeCheck();
                    var mode = (GuardMode)EditorGUILayout.EnumPopup("Movement", g.Mode);
                    var facing = (Direction)EditorGUILayout.EnumPopup("Facing", g.Facing);
                    int range = EditorGUILayout.IntSlider("Vision range", g.Range, 1, 6);
                    int rotation = EditorGUILayout.IntPopup("Rotation step", g.Rotation, new[] { "+1 (clockwise)", "-1 (counter-clockwise)" }, new[] { 1, -1 });
                    int moveEvery = EditorGUILayout.IntSlider("Move every", g.MoveEvery, 1, 6);
                    int rotateEvery = EditorGUILayout.IntSlider("Rotate every", g.RotateEvery, 1, 6);
                    if (EditorGUI.EndChangeCheck()) { Snapshot(); _model.Guards[i] = g.With(mode: mode, facing: facing, range: range, rotation: rotation, moveEvery: moveEvery, rotateEvery: rotateEvery); _sim = null; }
                    EditorGUILayout.LabelField("Route: " + string.Join(" → ", g.Route), EditorStyles.miniLabel);
                    if (GUILayout.Button("Clear route")) { Snapshot(); _model.Guards[i] = g.With(route: new List<GridCoord>()); _sim = null; }
                }
            }
            if (GUILayout.Button("Add guard"))
            {
                Snapshot();
                _model.Guards.Add(new GuardDefinition(GuardMode.Fixed, new List<GridCoord>(), Direction.North, 2, 1, 1, 1, "g" + (_model.Guards.Count + 1)));
                _selectedGuard = _model.Guards.Count - 1;
                _tool = Tool.Route;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_validationText) ? "(press Validate)" : _validationText, GUILayout.MinHeight(90));
            EditorGUILayout.LabelField("Solver", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_solverText) ? "(press Solve + Metrics)" : _solverText, GUILayout.MinHeight(120));
            if (!string.IsNullOrEmpty(_lastPath)) EditorGUILayout.LabelField("File: " + _lastPath, EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------ commands

        private void NewLevel()
        {
            Snapshot();
            _model = new LevelAuthoringModel();
            _model.FillRectangle();
            _model.AutoEdges();
            _sim = null; _validationText = ""; _solverText = ""; _lastPath = null; _selectedGuard = -1;
        }

        private void Resize(int w, int h)
        {
            _model.Resize(w, h);
            _sim = null;
        }

        private LevelDefinition SafeDefinition()
        {
            try { return _model.ToDefinition(); }
            catch (Exception e) { _validationText = "cannot build level: " + e.Message; return null; }
        }

        private void Validate()
        {
            var level = SafeDefinition();
            if (level == null) return;
            var report = LevelValidator.Validate(level);
            _validationText = (report.IsValid ? "VALID" : "INVALID") + " — " + report.ErrorCount + " errors, " + report.WarningCount + " warnings\n" + report;
        }

        private void Solve()
        {
            var level = SafeDefinition();
            if (level == null) return;
            var report = LevelValidator.Validate(level);
            if (!report.IsValid) { _solverText = "fix validation errors first"; Validate(); return; }
            var m = LevelMetricsAnalyzer.Analyze(level);
            _solverText = m + "\nshortest: " + GameActionUtil.ToTokenString(m.ShortestSolution) +
                          "\nWAIT required: " + m.WaitRequired + "  smoke required: " + m.SmokeRequired + "  EMP required: " + m.EmpRequired;
            if (m.ShortestWinTurns < 0) _solverText = "UNSOLVABLE (" + m.ShortestWinTurns + ")\n" + _solverText;
        }

        private void ImportJson()
        {
            string path = EditorUtility.OpenFilePanel("Import level JSON (schema 1 or 2)", Application.dataPath + "/StreamingAssets/ShadowSteps", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var pack = LevelPackLoader.Parse(File.ReadAllText(path));
                int index = 0;
                if (pack.Levels.Count > 1)
                {
                    var names = new string[pack.Levels.Count];
                    for (int i = 0; i < names.Length; i++) names[i] = pack.Levels[i].Id + ". " + pack.Levels[i].Name;
                    index = EditorUtility.DisplayDialogComplex("Pack contains " + names.Length + " levels", "Open the first level now; use the Id field to pick others after import.\nFirst: " + names[0], "Open first", "Cancel", "") == 0 ? 0 : -1;
                    if (index < 0) return;
                }
                Snapshot();
                _model = LevelAuthoringModel.FromDefinition(pack.Levels[index]);
                _lastPath = path;
                _sim = null; _selectedGuard = -1;
                Validate();
            }
            catch (Exception e) { EditorUtility.DisplayDialog("Import failed", e.Message, "OK"); }
        }

        private void ExportJson()
        {
            var level = SafeDefinition();
            if (level == null) return;
            string dir = Application.dataPath + "/StreamingAssets/ShadowSteps";
            string path = EditorUtility.SaveFilePanel("Export level as schema 2 JSON", dir, "level_" + _model.Id + ".json", "json");
            if (string.IsNullOrEmpty(path)) return;
            if (File.Exists(path))
            {
                string backup = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
                File.Copy(path, backup, true);
                Debug.Log("[ShadowSteps] backup written: " + backup);
            }
            File.WriteAllText(path, LevelPackWriter.ToJson(level));
            _lastPath = path;
            AssetDatabase.Refresh();
            Debug.Log("[ShadowSteps] exported " + path);
        }

        private void Snapshot()
        {
            try { _undo.Push(LevelPackWriter.ToJson(_model.ToDefinition())); if (_undo.Count > 50) { var arr = _undo.ToArray(); _undo.Clear(); for (int i = Math.Min(49, arr.Length - 1); i >= 0; i--) _undo.Push(arr[i]); } }
            catch { /* model temporarily invalid; skip snapshot */ }
        }

        private void RestoreSnapshot()
        {
            if (_undo.Count == 0) return;
            try { _model = LevelAuthoringModel.FromDefinition(LevelPackLoader.Parse(_undo.Pop()).Levels[0]); _sim = null; }
            catch (Exception e) { Debug.LogWarning("undo failed: " + e.Message); }
        }
    }
}
