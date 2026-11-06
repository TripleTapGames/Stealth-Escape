using NUnit.Framework;
using ShadowSteps.Runtime;
using UnityEngine;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>D-069: every named sound has a generated fallback clip, and a library clip overrides it per sound.</summary>
    public class AudioSfxTests
    {
        [Test]
        public void EverySound_HasAGeneratedClip_OfSensibleLength()
        {
            var keep = SfxLibrary.Current; SfxLibrary.Current = null;
            try
            {
                foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
                {
                    var clip = AudioService.Resolve(s); Assert.IsNotNull(clip, s.ToString());
                    Assert.That(clip.length, Is.GreaterThan(0.03f).And.LessThan(1f), s + " is a short one-shot");
                    Assert.AreEqual(1, clip.channels);
                }
            }
            finally { SfxLibrary.Current = keep; }
        }

        [Test]
        public void ShippedLibrary_HasAClipInEverySlot_AndTheyAreShortMonoOneShots()
        {
            var lib = Resources.Load<SfxLibrary>(SfxLibrary.ResourceName); if (lib == null) Assert.Ignore("no SfxLibrary in Resources");
            Assert.AreEqual(System.Enum.GetValues(typeof(Sfx)).Length, lib.entries.Length);
            foreach (var e in lib.entries)
            {
                Assert.IsNotNull(e.clip, e.sound + ": clip assigned (D-070)");
                Assert.That(e.clip.length, Is.GreaterThan(0.03f).And.LessThan(1.2f), e.sound + " is a short one-shot"); Assert.AreEqual(1, e.clip.channels, e.sound + " is mono");
                Assert.AreEqual(44100, e.clip.frequency, e.sound + " at 44.1 kHz");
                Assert.That(e.volume, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void LibraryClip_OverridesTheGeneratedTone_OnlyForItsSound()
        {
            var keep = SfxLibrary.Current; var lib = ScriptableObject.CreateInstance<SfxLibrary>(); lib.EnsureAllEntries();
            var real = AudioClip.Create("real_impact", 2205, 1, 22050, false);
            try
            {
                Assert.AreEqual(System.Enum.GetValues(typeof(Sfx)).Length, lib.entries.Length, "one slot per sound");
                lib.Find(Sfx.Impact).clip = real; SfxLibrary.Current = lib;
                Assert.AreSame(real, AudioService.Resolve(Sfx.Impact), "the assigned clip wins");
                Assert.AreNotSame(real, AudioService.Resolve(Sfx.Land)); Assert.IsTrue(AudioService.Resolve(Sfx.Land).name.StartsWith("ss_"), "others keep the generated tone");
                Assert.AreEqual(0.9f, lib.Find(Sfx.Impact).volume, 1e-4f, "default per-sound volume"); Assert.AreEqual(0.55f, lib.Find(Sfx.Land).volume, 1e-4f);
                var before = lib.entries.Length; lib.EnsureAllEntries(); Assert.AreEqual(before, lib.entries.Length); Assert.AreSame(real, lib.Find(Sfx.Impact).clip, "refreshing keeps assigned clips");
            }
            finally { SfxLibrary.Current = keep; Object.DestroyImmediate(real); Object.DestroyImmediate(lib); }
        }
    }
}
