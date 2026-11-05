using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>
    /// D-047 long edges on the 2D renderer: a passage cell gets no node disc or ring, the connection across it is one line
    /// spanning the whole edge, the reachable highlight lights the far node, and the guard fan reaches the far seen node.
    /// </summary>
    public class SS2DPassageTests
    {
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }

        [Test]
        public void PassageCells_DrawNoNode_OneLongPath_AndReachAcrossTheEdge()
        {
            // (0,0) ─────── (3,0) ── (4,0)      (1,0),(2,0) passages
            // (0,1) ──(1,1)──(2,1)──(3,1)──(4,1)
            var b = new StageBuilder { Id = 900, Code = "P2D", Title = "passage" }.FromRows(new[] { ".--..", "....." }); b.Start = new GridCoord(0, 0); b.Exit = new GridCoord(4, 0);
            b.Guards.Add(new GuardSpec("g", GuardKind.Static, new[] { new GridCoord(4, 0) }, Direction.West));
            var stage = b.Build();
            var go = new GameObject("test-renderer"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = Library(); r.autoRotatePortrait = false; r.Build(stage);
            try
            {
                float s = r.Layout.NodeSpacing;
                Assert.IsNull(r.transform.Find("Nodes/Node_1_0"), "no node disc on a passage"); Assert.IsNull(r.transform.Find("Nodes/NodeRing_2_0"));
                Assert.IsNotNull(r.transform.Find("Nodes/Node_0_0")); Assert.IsNotNull(r.transform.Find("Nodes/Node_3_0"));
                int paths = 0; Transform longPath = null;
                foreach (Transform t in r.transform.Find("Paths")) { if (!t.name.StartsWith("Path_")) continue; paths++; if (t.name == "Path_(0, 0)_(3, 0)") longPath = t; }
                Assert.AreEqual(2 + 4 + 3, paths, "row 0: the long edge and (3,0)-(4,0); row 1: 4 lattice edges; 3 vertical links (none under the passages) = 9");
                Assert.IsNotNull(longPath, "one line for the whole long edge");
                var sr = longPath.GetComponent<SpriteRenderer>();
                Assert.AreEqual(3f * s - r.library.nodeSize * 1.1f * s, sr.size.x, 1e-3f, "the line spans three squares less the node discs");
                Assert.Less(Vector3.Distance(longPath.localPosition, (r.Pos(new GridCoord(0, 0)) + r.Pos(new GridCoord(3, 0))) * 0.5f), 1e-4f, "centred on the edge");
                Assert.IsNull(r.transform.Find("Paths/Path_(1, 0)_(1, 1)"), "no vertical link under a horizontal passage");
                // Reachable highlight from the start: the far node across the passages and the node below.
                var reachNodes = new List<GridCoord>(); var reachEdges = new List<EdgeKey>();
                for (int d = 0; d < 4; d++) { var n = stage.Neighbor(new GridCoord(0, 0), (Direction)d); if (n.HasValue) { reachNodes.Add(n.Value); reachEdges.Add(new EdgeKey(new GridCoord(0, 0), n.Value)); } }
                CollectionAssert.AreEquivalent(new[] { new GridCoord(3, 0), new GridCoord(0, 1) }, reachNodes);
                r.SetReachable(reachNodes, reachEdges);
                Assert.IsTrue(r.transform.Find("Nodes/NodeRing_3_0").gameObject.activeSelf, "far node lit");
                // Guard at (4,0) facing W sees (3,0); (2,0) two squares away is a passage and (0,0) is out of reach (D-050: sight is measured in squares).
                var state = StageState.Initial(stage);
                var seen = Sight.GuardVision(stage, state, GuardTrack.PoseAt(stage.Guards[0], 0));
                CollectionAssert.AreEqual(new[] { new GridCoord(3, 0) }, seen);
                r.SetGuard(0, GuardTrack.PoseAt(stage.Guards[0], 0), null); r.SetVision(seen, new List<GridCoord>());
                Assert.IsTrue(new HashSet<GridCoord>(seen).SetEquals(r.GuardOverlay.DrawnCells));
                Assert.AreEqual(1, r.GuardCones.Count);
                float minX = float.MaxValue; foreach (var p in r.GuardCones[0]) minX = Mathf.Min(minX, p.x);
                Assert.Less(minX, r.Pos(new GridCoord(3, 0)).x + 0.01f, "the fan reaches the seen node"); Assert.Greater(minX, r.Pos(new GridCoord(0, 0)).x + 0.5f * s, "…but never the far end of the walkway");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
