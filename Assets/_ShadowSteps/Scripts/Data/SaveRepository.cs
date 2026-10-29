using System;
using System.Collections.Generic;
using System.IO;

namespace ShadowSteps.Data
{
    public enum SaveLoadStatus
    {
        /// <summary>Main file read and parsed.</summary>
        Loaded,
        /// <summary>Main file missing or corrupt; the backup was used.</summary>
        RestoredFromBackup,
        /// <summary>No save existed; defaults created.</summary>
        CreatedNew,
        /// <summary>Both files corrupt; defaults created and the broken file kept as *.corrupt for inspection.</summary>
        ResetAfterCorruption
    }

    /// <summary>JSON (de)serialisation of <see cref="SaveData"/>; separate from file IO so it is unit-testable.</summary>
    public static class SaveSerializer
    {
        public static string ToJson(SaveData d)
        {
            var root = new JsonObject()
                .Set("version", (long)d.Version)
                .Set("highest_unlocked_level_id", (long)d.HighestUnlockedLevelId)
                .Set("last_played_level_id", (long)d.LastPlayedLevelId)
                .Set("debug_unlock_all", d.DebugUnlockAll);
            var results = new List<object>();
            var ids = new List<int>(d.Results.Keys);
            ids.Sort();
            foreach (var id in ids)
            {
                var r = d.Results[id];
                results.Add(new JsonObject().Set("level_id", (long)id).Set("completed", r.Completed).Set("best_turns", (long)r.BestTurns).Set("best_stars", (long)r.BestStars).Set("ghost", r.Ghost));
            }
            root["results"] = results;
            root["settings"] = new JsonObject()
                .Set("music", d.Settings.MusicOn)
                .Set("sfx", d.Settings.SfxOn)
                .Set("haptics", d.Settings.HapticsOn)
                .Set("reduced_motion", d.Settings.ReducedMotion)
                .Set("color_blind_assist", d.Settings.ColorBlindAssist);
            root["seen_tutorials"] = new List<object>(d.SeenTutorials);
            return MiniJson.Serialize(root, true) + "\n";
        }

        /// <summary>Throws on malformed data. Missing optional fields fall back to defaults so older saves load.</summary>
        public static SaveData FromJson(string json)
        {
            var root = JsonAccess.AsObject(MiniJson.Parse(json), "save");
            var d = new SaveData();
            int version = JsonAccess.OptionalInt(root, "version", 1, "save");
            if (version > SaveData.CurrentVersion) throw new LevelDataException("save version " + version + " is newer than supported " + SaveData.CurrentVersion);
            d.Version = SaveData.CurrentVersion;
            d.HighestUnlockedLevelId = Math.Max(1, JsonAccess.OptionalInt(root, "highest_unlocked_level_id", 1, "save"));
            d.LastPlayedLevelId = Math.Max(1, JsonAccess.OptionalInt(root, "last_played_level_id", 1, "save"));
            d.DebugUnlockAll = JsonAccess.OptionalBool(root, "debug_unlock_all", false, "save");
            if (root.TryGetValue("results", out var rv) && rv != null)
            {
                var arr = JsonAccess.AsArray(rv, "save.results");
                for (int i = 0; i < arr.Count; i++)
                {
                    string p = "save.results[" + i + "]";
                    var o = JsonAccess.AsObject(arr[i], p);
                    int id = JsonAccess.RequireInt(o, "level_id", p);
                    d.Results[id] = new LevelResult(JsonAccess.OptionalBool(o, "completed", false, p), JsonAccess.OptionalInt(o, "best_turns", 0, p),
                        JsonAccess.OptionalInt(o, "best_stars", 0, p), JsonAccess.OptionalBool(o, "ghost", false, p));
                }
            }
            if (root.TryGetValue("settings", out var sv) && sv != null)
            {
                var s = JsonAccess.AsObject(sv, "save.settings");
                d.Settings.MusicOn = JsonAccess.OptionalBool(s, "music", true, "save.settings");
                d.Settings.SfxOn = JsonAccess.OptionalBool(s, "sfx", true, "save.settings");
                d.Settings.HapticsOn = JsonAccess.OptionalBool(s, "haptics", true, "save.settings");
                d.Settings.ReducedMotion = JsonAccess.OptionalBool(s, "reduced_motion", false, "save.settings");
                d.Settings.ColorBlindAssist = JsonAccess.OptionalBool(s, "color_blind_assist", false, "save.settings");
            }
            if (root.TryGetValue("seen_tutorials", out var tv) && tv != null)
            {
                var arr = JsonAccess.AsArray(tv, "save.seen_tutorials");
                for (int i = 0; i < arr.Count; i++) d.SeenTutorials.Add(JsonAccess.AsString(arr[i], "save.seen_tutorials[" + i + "]"));
            }
            return d;
        }
    }

    /// <summary>
    /// Atomic file persistence: write to a temp file, rotate the previous save to .bak, then move into place.
    /// Load order: main -> backup -> defaults. Never throws out of Load; status and messages are exposed instead.
    /// </summary>
    public sealed class SaveRepository
    {
        public string Path { get; }
        public string BackupPath => Path + ".bak";
        public string TempPath => Path + ".tmp";
        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.CreatedNew;
        public string LastMessage { get; private set; } = string.Empty;

        public SaveRepository(string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public SaveData Load()
        {
            if (TryRead(Path, out var data, out var mainError))
            {
                LastLoadStatus = SaveLoadStatus.Loaded;
                LastMessage = "loaded " + Path;
                return data;
            }
            bool mainExisted = File.Exists(Path);
            if (TryRead(BackupPath, out data, out var backupError))
            {
                LastLoadStatus = SaveLoadStatus.RestoredFromBackup;
                LastMessage = "main save unreadable (" + mainError + "); restored backup";
                return data;
            }
            if (mainExisted)
            {
                try { File.Copy(Path, Path + ".corrupt", true); } catch { /* best effort */ }
                LastLoadStatus = SaveLoadStatus.ResetAfterCorruption;
                LastMessage = "save and backup unreadable (" + mainError + "; " + backupError + "); reset to defaults, kept .corrupt copy";
            }
            else
            {
                LastLoadStatus = SaveLoadStatus.CreatedNew;
                LastMessage = "no save found; created defaults";
            }
            return new SaveData();
        }

        /// <summary>Returns false (with message) on IO failure instead of throwing, so gameplay never crashes on a full disk.</summary>
        public bool Save(SaveData data)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(TempPath, SaveSerializer.ToJson(data));
                if (File.Exists(Path))
                {
                    if (File.Exists(BackupPath)) File.Delete(BackupPath);
                    File.Move(Path, BackupPath);
                }
                File.Move(TempPath, Path);
                LastMessage = "saved " + Path;
                return true;
            }
            catch (Exception e)
            {
                LastMessage = "save failed: " + e.Message;
                return false;
            }
        }

        private static bool TryRead(string path, out SaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                if (!File.Exists(path)) { error = "missing"; return false; }
                data = SaveSerializer.FromJson(File.ReadAllText(path));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
