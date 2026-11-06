using System;
using System.Collections.Generic;
using ShadowSteps.Data;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>Named game sounds. Add a value here and a recipe in <see cref="AudioService"/> to extend; a real clip can be assigned per sound in the SfxLibrary asset (D-069).</summary>
    public enum Sfx
    {
        Footstep, Tick, Rotate, Gadget, Alert, Unlock, Clear, Tap, Error,
        // D-069: sounds for the board-piece presentation (hop, knockout, gate, gadgets)
        Land,     // token touches down after a hop
        Leap,     // attacker leaves the ground for a knockout
        Impact,   // attacker slams onto the guard's node
        Whoosh,   // the flung guard piece leaves the board
        Thud,     // the flung piece lands beside the board
        Key,      // key picked up
        DoorOpen, // gate swings open
        Smoke,    // smoke dropped
        Emp,      // EMP fired
    }

    /// <summary>
    /// Plays short procedurally generated tones (original, licence-free) and a soft generated ambient loop.
    /// Real audio files can replace any clip by assigning them in the inspector of the AudioService object or a theme later.
    /// Respects the Music/SFX toggles from <see cref="SettingsData"/>. Audio never affects rules.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        [Range(0f, 1f)] public float SfxVolume = 0.8f;
        [Range(0f, 1f)] public float MusicVolume = 0.35f;
        public bool SfxEnabled = true;
        public bool MusicEnabled = true;

        private AudioSource _sfx;
        private AudioSource _music;
        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private const int SampleRate = 22050;

        public static AudioService Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;
            _music.clip = BuildAmbientLoop();
            ApplySettings(AppServices.Save?.Settings);
        }

        public void ApplySettings(SettingsData settings)
        {
            if (settings == null) return;
            SfxEnabled = settings.SfxOn;
            MusicEnabled = settings.MusicOn;
            _music.volume = MusicVolume;
            if (MusicEnabled && !_music.isPlaying) _music.Play();
            if (!MusicEnabled && _music.isPlaying) _music.Stop();
        }

        /// <summary>When a presentation (the 2D bridge) times the movement sounds itself (landing, knockout beats), the flow skips its per-action footstep.</summary>
        public static bool MovementSoundsByPresentation;

        public void Play(Sfx sfx)
        {
            if (!SfxEnabled || _sfx == null) return;
            var entry = SfxLibrary.Current != null ? SfxLibrary.Current.Find(sfx) : null;
            AudioClip clip = entry != null ? entry.clip : null; float volume = entry != null ? entry.volume : SfxLibrary.DefaultVolume(sfx);
            if (clip == null && !_clips.TryGetValue(sfx, out clip)) { clip = Build(sfx); _clips[sfx] = clip; }
            float spread = entry != null ? entry.pitchVariation : 0.04f;
            _sfx.pitch = spread > 0f ? 1f + UnityEngine.Random.Range(-spread, spread) : 1f;
            _sfx.PlayOneShot(clip, SfxVolume * volume);
        }

        /// <summary>The clip that <see cref="Play"/> would use for <paramref name="sfx"/> (library clip or generated tone); for tests and tools.</summary>
        public static AudioClip Resolve(Sfx sfx)
        {
            var entry = SfxLibrary.Current != null ? SfxLibrary.Current.Find(sfx) : null;
            return entry != null && entry.clip != null ? entry.clip : Build(sfx);
        }

        // ---- procedural recipes: (frequency curve, duration, envelope, noise mix)
        private static AudioClip Build(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Footstep: return Tone("footstep", 0.07f, t => 180f - 60f * t, 0.9f, 0.5f);
                case Sfx.Tick: return Tone("tick", 0.05f, t => 1200f, 1f, 0.1f);
                case Sfx.Rotate: return Tone("rotate", 0.12f, t => 400f + 300f * t, 0.6f, 0.2f);
                case Sfx.Gadget: return Tone("gadget", 0.30f, t => 300f + 500f * Mathf.Sin(t * 12f), 0.5f, 0.15f);
                case Sfx.Alert: return Tone("alert", 0.45f, t => 880f - 400f * t, 0.9f, 0.05f);
                case Sfx.Unlock: return Tone("unlock", 0.25f, t => 500f + 700f * t, 0.6f, 0.05f);
                case Sfx.Clear: return Tone("clear", 0.60f, t => 440f * Mathf.Pow(2f, Mathf.Floor(t * 4f) / 4f), 0.5f, 0.0f);
                case Sfx.Tap: return Tone("tap", 0.04f, t => 900f, 1f, 0.2f);
                // D-069 board-piece sounds (original synthesis): soft wooden tap, rising whoosh, heavy slam, airy sweep, dull landing, bright key, creaking swing, puff, zap.
                case Sfx.Land: return Tone("land", 0.06f, t => 220f - 90f * t, 1f, 0.35f);
                case Sfx.Leap: return Tone("leap", 0.16f, t => 260f + 520f * t, 0.5f, 0.45f);
                case Sfx.Impact: return Tone("impact", 0.14f, t => 140f - 70f * t, 1f, 0.55f);
                case Sfx.Whoosh: return Tone("whoosh", 0.28f, t => 900f - 500f * t, 0.3f, 0.85f);
                case Sfx.Thud: return Tone("thud", 0.10f, t => 110f - 40f * t, 1f, 0.5f);
                case Sfx.Key: return Tone("key", 0.22f, t => 1200f + 600f * Mathf.Floor(t * 3f) / 3f, 0.8f, 0.0f);
                case Sfx.DoorOpen: return Tone("dooropen", 0.30f, t => 180f + 120f * Mathf.Sin(t * 9f), 0.4f, 0.3f);
                case Sfx.Smoke: return Tone("smoke", 0.35f, t => 300f - 200f * t, 0.2f, 0.95f);
                case Sfx.Emp: return Tone("emp", 0.25f, t => 1500f - 1300f * t * t, 0.9f, 0.35f);
                default: return Tone("error", 0.15f, t => 160f, 0.8f, 0.3f);
            }
        }

        private static AudioClip Tone(string name, float seconds, Func<float, float> freq, float attackSharpness, float noise)
        {
            int n = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Pow(1f - t, 2f) * Mathf.Min(1f, t * 40f * attackSharpness);
                phase += 2f * Mathf.PI * freq(t) / SampleRate;
                float s = Mathf.Sin(phase) * (1f - noise) + ((float)rng.NextDouble() * 2f - 1f) * noise;
                data[i] = s * env * 0.6f;
            }
            var clip = AudioClip.Create("ss_" + name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildAmbientLoop()
        {
            const float seconds = 8f;
            int n = (int)(seconds * SampleRate);
            var data = new float[n];
            float[] chord = { 110f, 164.81f, 220f, 329.63f };
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float s = 0f;
                for (int c = 0; c < chord.Length; c++) s += Mathf.Sin(2f * Mathf.PI * chord[c] * t + c) * (0.5f + 0.5f * Mathf.Sin(t * 0.4f + c));
                float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / seconds);
                data[i] = s / chord.Length * 0.25f * swell;
            }
            var clip = AudioClip.Create("ss_ambient", n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>Thin wrapper over device vibration with the user toggle; no-op on platforms without haptics.</summary>
    public static class HapticsService
    {
        public static bool Enabled => AppServices.Save == null || AppServices.Save.Settings.HapticsOn;

        public static void Pulse()
        {
            if (!Enabled) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
