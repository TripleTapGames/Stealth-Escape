using System.IO;
using NUnit.Framework;
using ShadowSteps.Core;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEditor;
using UnityEngine;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>D-067: locked-edge doors draw the user's padlocked gate, and swap to the open gate once the key-holder crosses; both share the frame pivot so the gate stays put.</summary>
    public class SS2DDoorArtTests
    {
        private static StagePack Pack() => StageJson.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ShadowSteps/levels_v4.json")));
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); Assert.IsNotNull(lib); return lib; }

        [Test]
        public void LockedEdge_ShowsTheGateLocked_ThenOpenAfterTheKeyHolderCrosses()
        {
            var lib = Library(); if (!lib.HasDoorArt) Assert.Ignore("door art not imported (Shadow Steps ▸ 2D ▸ Import final art)");
            Assert.AreEqual("Door locked", lib.doorLocked.name); Assert.AreEqual("Door opened", lib.doorOpen.name);
            Assert.AreEqual(lib.doorLocked.pivot / lib.doorLocked.rect.size, lib.doorOpen.pivot / lib.doorOpen.rect.size, "same normalized pivot: the frame stays put when the leaf opens");
            Assert.AreEqual(1f, lib.doorLocked.bounds.size.x, 0.02f, "one unit per 2048 px");
            var stage = Pack().FindByCode("LEVEL-14"); Assert.AreEqual(1, stage.Doors.Count);
            var go = new GameObject("r"); var r = go.AddComponent<ShadowSteps2DRenderer>(); r.library = lib;
            try
            {
                r.Build(stage); float s = r.Layout.NodeSpacing;
                var door = r.transform.Find("Props/Door_0"); Assert.IsNotNull(door, "gate on the locked edge");
                var sr = door.GetComponent<SpriteRenderer>(); Assert.AreEqual(lib.doorLocked, sr.sprite, "locked at the start"); Assert.AreEqual(1f, sr.color.a);
                var mid = (r.Pos(stage.Doors[0].A) + r.Pos(stage.Doors[0].B)) * 0.5f; Assert.Less(Vector3.Distance(door.localPosition, mid), 1e-3f, "on the edge midpoint");
                Assert.AreEqual(lib.lockSize * s, door.localScale.x, 1e-4f, "drawn at lockSize"); Assert.AreEqual(Quaternion.identity, door.localRotation, "upright on screen");
                float frameWidth = lib.lockSize * s * 0.409f; Assert.Greater(frameWidth, 0.45f * s, "the frame is readable"); Assert.Less(frameWidth, 0.8f * s, "and fits between the nodes");
                // Walk the par route: key at (2,3), the door (3,2)-(4,2) opens when crossed with the key.
                var st = StageState.Initial(stage);
                foreach (var d in new[] { Direction.South, Direction.East, Direction.North }) st = StageRules.TryApply(stage, st, StageAction.Move(d)).After;
                Assert.IsTrue(st.HasKey); Assert.IsFalse(st.IsDoorOpen(0), "the key node (2,3) does not touch the door"); r.SetDoor(0, st.IsDoorOpen(0)); Assert.AreEqual(lib.doorLocked, sr.sprite, "still locked while the key-holder is a node away");
                st = StageRules.TryApply(stage, st, StageAction.Move(Direction.East)).After; Assert.AreEqual(stage.Doors[0].A, st.Player, "arrived beside the door"); Assert.IsTrue(st.IsDoorOpen(0), "D-068: the gate unlocks as soon as the key-holder comes next to it");
                r.SetDoor(0, true); Assert.AreEqual(lib.doorOpen, sr.sprite, "open gate while standing beside it"); Assert.AreEqual(1f, sr.color.a, "the open gate art is opaque");
                st = StageRules.TryApply(stage, st, StageAction.Move(Direction.East)).After; Assert.AreEqual(stage.Doors[0].B, st.Player); Assert.IsTrue(st.IsDoorOpen(0));
                Assert.Less(Vector3.Distance(door.localPosition, mid), 1e-3f, "the frame did not move");
                r.SetDoor(0, false); Assert.AreEqual(lib.doorLocked, sr.sprite, "retry locks it again");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
