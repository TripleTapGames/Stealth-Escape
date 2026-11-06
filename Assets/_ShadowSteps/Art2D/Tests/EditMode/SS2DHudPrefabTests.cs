using NUnit.Framework;
using ShadowSteps.Art2D.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.Art2D.Tests
{
    /// <summary>D-062: the HUD ships as a baked prefab whose hierarchy the runtime reuses, so it can be edited in the editor like the screens.</summary>
    public class SS2DHudPrefabTests
    {
        private static SS2DSpriteLibrary Library() { var lib = AssetDatabase.LoadAssetAtPath<SS2DSpriteLibrary>(SS2DSpriteLibrary.DefaultAssetPath); if (lib == null || !lib.HasUiSkin) Assert.Ignore("HUD skin not imported"); return lib; }

        [Test]
        public void ShippedHudPrefab_IsBaked_WithEveryReferenceAndOnlySavedSprites()
        {
            Library();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SS2DHudBaker.PrefabPath); Assert.IsNotNull(prefab, "SS2DHud.prefab");
            var hud = prefab.GetComponent<SS2DHudCanvas>(); Assert.IsNotNull(hud);
            Assert.IsTrue(hud.IsBaked, "the prefab carries the baked hierarchy");
            Assert.IsNotNull(hud.canvas); Assert.IsNotNull(hud.levelCaption); Assert.IsNotNull(hud.levelLabel); Assert.IsNotNull(hud.messageLabel); Assert.IsNotNull(hud.starsLabel); Assert.IsNotNull(hud.actionsValue); Assert.IsNotNull(hud.actionsParLabel);
            Assert.IsNotNull(hud.pauseButton); Assert.IsNotNull(hud.smokeButton); Assert.IsNotNull(hud.empButton); Assert.IsNotNull(hud.smokeCount); Assert.IsNotNull(hud.empCount); Assert.IsNotNull(hud.smokeLabel); Assert.IsNotNull(hud.empLabel);
            Assert.IsNotNull(hud.lockdownPanel); Assert.IsNotNull(hud.lockdownLabel); Assert.IsNotNull(hud.alertOverlay);
            foreach (var img in prefab.GetComponentsInChildren<Image>(true)) if (img.sprite != null) Assert.IsTrue(AssetDatabase.Contains(img.sprite), img.name + ": every sprite is a saved asset (no runtime-generated shapes)");
            Assert.IsTrue(hud.levelLabel.resizeTextForBestFit, "D-061 title shrink-to-fit is baked in");
        }

        [Test]
        public void BakedInstance_KeepsItsHierarchyOnBuild_AndCanBeEditedInPlace()
        {
            Library();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SS2DHudBaker.PrefabPath); Assert.IsNotNull(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var hud = go.GetComponent<SS2DHudCanvas>(); var canvas = hud.canvas; var title = hud.levelLabel; int children = go.transform.childCount;
                // A designer's edit on the instance (or the prefab) survives Build(): nothing is torn down and rebuilt.
                title.fontSize = 33;
                hud.Build();
                Assert.AreSame(canvas, hud.canvas, "the canvas object is reused"); Assert.AreSame(title, hud.levelLabel); Assert.AreEqual(33, hud.levelLabel.fontSize, "the edit is kept"); Assert.AreEqual(children, go.transform.childCount);
                // The message pill behaves as on the code-built HUD (D-061).
                Assert.IsFalse(hud.MessagePillVisible); hud.ShowMessage("Tap a highlighted node to drop smoke", 0f); Assert.IsTrue(hud.MessagePillVisible); hud.ShowMessage(string.Empty, 0f); Assert.IsFalse(hud.MessagePillVisible);
                // Clearing the mark makes Build rebuild from code again (the escape hatch after a broken hand edit).
                hud.MarkBaked(false); hud.Build();
                Assert.AreNotSame(canvas, hud.canvas, "rebuilt from code"); Assert.IsNotNull(hud.levelCaption);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Bake_WritesAPrefabWithTheSkinnedLayout()
        {
            Library();
            const string path = "Assets/_ShadowSteps/Art2D/Prefabs/_Test_SS2DHud.prefab";
            try
            {
                var prefab = SS2DHudBaker.Bake(path); Assert.IsNotNull(prefab, "bake succeeds with the shipped skin");
                var hud = prefab.GetComponent<SS2DHudCanvas>(); Assert.IsTrue(hud.IsBaked); Assert.IsNotNull(hud.transform.Find("Canvas/TitlePill")); Assert.IsNotNull(hud.transform.Find("Canvas/SmokeButton/Badge"));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
