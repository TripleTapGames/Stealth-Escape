using System;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// D-069: optional real audio clips for the named game sounds. Lives in Resources as "SfxLibrary"; a sound whose clip is empty
    /// falls back to the procedural tone in <see cref="AudioService"/>, so the game always has audio and designers can replace
    /// sounds one at a time by dropping WAV/OGG files into the slots. Volume and pitch variation are per sound.
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/SFX Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public const string ResourceName = "SfxLibrary";
        public const string DefaultAssetPath = "Assets/_ShadowSteps/Resources/SfxLibrary.asset";

        [Serializable]
        public sealed class Entry
        {
            public Sfx sound;
            [Tooltip("Leave empty to use the generated tone.")] public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch spread around 1 (0 = always the same pitch).")] [Range(0f, 0.5f)] public float pitchVariation = 0.04f;
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Find(Sfx s) { if (entries != null) foreach (var e in entries) if (e != null && e.sound == s) return e; return null; }

        /// <summary>One entry per sound, in enum order, keeping any clips already assigned.</summary>
        public void EnsureAllEntries()
        {
            var values = (Sfx[])Enum.GetValues(typeof(Sfx)); var list = new System.Collections.Generic.List<Entry>();
            foreach (var v in values) { var e = Find(v); list.Add(e ?? new Entry { sound = v, volume = DefaultVolume(v) }); }
            entries = list.ToArray();
        }

        public static float DefaultVolume(Sfx s)
        {
            switch (s)
            {
                case Sfx.Land: return 0.55f; case Sfx.Leap: return 0.5f; case Sfx.Impact: return 0.9f; case Sfx.Whoosh: return 0.6f; case Sfx.Thud: return 0.7f;
                case Sfx.Tick: return 0.4f; case Sfx.Tap: return 0.5f; default: return 0.8f;
            }
        }

        private static SfxLibrary _current; private static bool _looked;
        public static SfxLibrary Current { get { if (!_looked) { _looked = true; _current = Resources.Load<SfxLibrary>(ResourceName); } return _current; } set { _current = value; _looked = true; } }
    }
}
