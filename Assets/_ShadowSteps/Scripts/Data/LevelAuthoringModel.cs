using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.Data
{
    /// <summary>
    /// Mutable, engine-free authoring model behind the level editor window: nodes, walls, cover, directed typed edges,
    /// objects and guards. Converts to/from <see cref="LevelDefinition"/> so the window never touches JSON directly and
    /// the editing rules (edge cycling, resizing, auto edges) are unit-testable without Unity.
    /// </summary>
public sealed class LevelAuthoringModel
    {
        public int Id = 101;
        public string Name = "New Level";
        public int World = 1, Chapter = 1;
        public string Theme = "";
        public string Hint = "";
        public int Width = 5, Height = 5;
        public float NodeSpacing = 1f;
        public int Smoke, Emp, SmokeTurns = 2;
        public bool NoWait, NoUndo;
        public DetectionRule Detection = LevelDefinition.DefaultDetection;
        public GridCoord Start = new GridCoord(0, 4), Exit = new GridCoord(4, 0);
        public bool HasKey, HasDoor;
        public GridCoord Key, Door;
        public HashSet<GridCoord> Nodes = new HashSet<GridCoord>();
        public HashSet<GridCoord> Walls = new HashSet<GridCoord>();
        public Dictionary<GridCoord, bool> Cover = new Dictionary<GridCoord, bool>(); // value = traversable
        public Dictionary<GridCoord, Dictionary<GridCoord, EdgeType>> Edges = new Dictionary<GridCoord, Dictionary<GridCoord, EdgeType>>();
        public List<GuardDefinition> Guards = new List<GuardDefinition>();

        public bool HasEdge(GridCoord a, GridCoord b, out EdgeType t)
        {
            if (Edges.TryGetValue(a, out var m) && m.TryGetValue(b, out t)) return true;
            t = EdgeType.Open; return false;
        }
        public void SetEdge(GridCoord a, GridCoord b, EdgeType? t)
        {
            if (!Edges.TryGetValue(a, out var m)) { m = new Dictionary<GridCoord, EdgeType>(); Edges[a] = m; }
            if (t.HasValue) m[b] = t.Value; else m.Remove(b);
        }
        public bool Traversable(GridCoord c) => Nodes.Contains(c) && !Walls.Contains(c) && !(Cover.TryGetValue(c, out var tr) && !tr);

        public void FillRectangle()
        {
            Nodes.Clear();
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++) Nodes.Add(new GridCoord(x, y));
        }

        public void AutoEdges()
        {
            Edges.Clear();
            foreach (var c in Nodes)
            {
                if (!Traversable(c)) continue;
                foreach (var d in new[] { Direction.East, Direction.South })
                {
                    var n = c.Neighbor(d);
                    if (!Traversable(n)) continue;
                    SetEdge(c, n, EdgeType.Open); SetEdge(n, c, EdgeType.Open);
                }
            }
            if (HasDoor) foreach (var kv in Edges) if (kv.Value.ContainsKey(Door)) kv.Value[Door] = EdgeType.Locked;
        }

        public void RemoveEdgesTouching(GridCoord c)
        {
            Edges.Remove(c);
            foreach (var m in Edges.Values) m.Remove(c);
        }

        public LevelDefinition ToDefinition()
        {
            var s = new LevelSpec
            {
                Id = Id, Name = Name, World = World, Chapter = Chapter, Theme = string.IsNullOrEmpty(Theme) ? null : Theme, Hint = string.IsNullOrEmpty(Hint) ? null : Hint,
                Width = Width, Height = Height, NodeSpacing = NodeSpacing, Start = Start, Exit = Exit,
                Key = HasKey ? Key : (GridCoord?)null, Door = HasDoor ? Door : (GridCoord?)null,
                SmokeCharges = Smoke, EmpCharges = Emp, SmokeTurns = SmokeTurns, NoWait = NoWait, NoUndo = NoUndo, Detection = Detection
            };
            s.NodeMask = new List<GridCoord>(Nodes);
            s.NodeMask.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            s.Walls.AddRange(Walls);
            foreach (var kv in Cover) s.Cover.Add(new CoverDefinition(kv.Key, kv.Value));
            s.ExplicitEdges = new List<Edge>();
            foreach (var from in Edges) foreach (var to in from.Value) s.ExplicitEdges.Add(new Edge(from.Key, to.Key, to.Value));
            s.Guards.AddRange(Guards);
            return LevelDefinition.FromSpec(s);
        }

        public static LevelAuthoringModel FromDefinition(LevelDefinition l)
        {
            var m = new LevelAuthoringModel
            {
                Id = l.Id, Name = l.Name, World = l.World, Chapter = l.Chapter, Theme = l.Theme ?? "", Hint = l.Hint ?? "",
                Width = l.Width, Height = l.Height, NodeSpacing = (float)l.NodeSpacing, Smoke = l.SmokeCharges, Emp = l.EmpCharges, SmokeTurns = l.SmokeTurns,
                NoWait = l.NoWait, NoUndo = l.NoUndo, Detection = l.Detection, Start = l.Start, Exit = l.Exit, HasKey = l.HasKey, HasDoor = l.HasDoor,
                Key = l.Key ?? default, Door = l.Door ?? default
            };
            foreach (var n in l.Graph.Nodes()) m.Nodes.Add(n);
            foreach (var w in l.Walls) m.Walls.Add(w);
            foreach (var c in l.Cover) m.Cover[c.Position] = c.Traversable;
            foreach (var e in l.Graph.Edges) m.SetEdge(e.From, e.To, e.Type);
            m.Guards.AddRange(l.Guards);
            return m;
        }

        /// <summary>Edge tool cycle between two adjacent nodes: none → open both ways → one-way a→b → locked both ways → none.</summary>
        public void CycleEdge(GridCoord a, GridCoord b)
        {
            bool ab = HasEdge(a, b, out var tab);
            bool ba = HasEdge(b, a, out var tba);
            if (!ab && !ba) { SetEdge(a, b, EdgeType.Open); SetEdge(b, a, EdgeType.Open); }
            else if (ab && ba && tab == EdgeType.Open) { SetEdge(b, a, null); }
            else if (ab && !ba) { SetEdge(a, b, EdgeType.Locked); SetEdge(b, a, EdgeType.Locked); }
            else { SetEdge(a, b, null); SetEdge(b, a, null); }
        }

        /// <summary>Changes the board size, dropping anything outside, adding floor nodes for new cells and regenerating edges.</summary>
        public void Resize(int w, int h)
        {
            Width = w; Height = h;
            Nodes.RemoveWhere(c => !c.IsInside(w, h));
            Walls.RemoveWhere(c => !c.IsInside(w, h));
            var removeCover = new List<GridCoord>();
            foreach (var c in Cover.Keys) if (!c.IsInside(w, h)) removeCover.Add(c);
            foreach (var c in removeCover) Cover.Remove(c);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Nodes.Add(new GridCoord(x, y));
            foreach (var c in new List<GridCoord>(Edges.Keys)) if (!c.IsInside(w, h)) RemoveEdgesTouching(c);
            AutoEdges();
        }
    }
}
