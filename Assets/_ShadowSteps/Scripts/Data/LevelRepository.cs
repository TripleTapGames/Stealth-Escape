using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.Data
{
    /// <summary>
    /// Converts levels.json text into immutable <see cref="LevelDefinition"/>s with strict field validation.
    /// Field names and semantics follow reference/levels.json (format_version 1). Unknown fields are reported
    /// as warnings, not errors, so older builds tolerate newer data. Bump <see cref="LevelPack.CurrentFormatVersion"/>
    /// and add a migration in <see cref="Migrate"/> when the schema changes.
    /// </summary>
    public static class LevelPackLoader
    {
        private static readonly HashSet<string> KnownLevelKeys = new HashSet<string>
            { "id", "name", "size", "start", "exit", "walls", "guards", "smoke", "emp", "key", "door", "hint" };
        private static readonly HashSet<string> KnownGuardKeys = new HashSet<string>
            { "mode", "route", "facing", "range", "step", "rotation" };

        /// <summary>Parses and structurally validates. Throws <see cref="LevelDataException"/> with a JSON path on bad data.</summary>
        public static LevelPack Parse(string json)
        {
            object rootValue;
            try { rootValue = MiniJson.Parse(json); }
            catch (JsonParseException e) { throw new LevelDataException("levels.json is not valid JSON: " + e.Message, e); }

            var root = JsonAccess.AsObject(rootValue, "root");
            var warnings = new List<string>();
            int formatVersion = JsonAccess.OptionalInt(root, "format_version", -1, "root");
            if (formatVersion < 0) throw new LevelDataException("root.format_version is missing");
            if (formatVersion > LevelPack.CurrentFormatVersion)
                throw new LevelDataException("root.format_version " + formatVersion + " is newer than supported " + LevelPack.CurrentFormatVersion);
            if (formatVersion < LevelPack.CurrentFormatVersion) Migrate(root, formatVersion, warnings);

            string coords = JsonAccess.OptionalString(root, "coordinate_system", "origin top-left, x right, y down", "root");
            var levelsJson = JsonAccess.AsArray(JsonAccess.Require(root, "levels", "root"), "root.levels");
            var levels = new List<LevelDefinition>(levelsJson.Count);
            for (int i = 0; i < levelsJson.Count; i++)
                levels.Add(ParseLevel(JsonAccess.AsObject(levelsJson[i], "levels[" + i + "]"), "levels[" + i + "]", warnings));
            return new LevelPack(LevelPack.CurrentFormatVersion, coords, levels, warnings);
        }

        /// <summary>Parses, then runs <see cref="LevelValidator"/> and throws if any level has errors.</summary>
        public static LevelPack ParseAndValidate(string json, out ValidationReport report)
        {
            var pack = Parse(json);
            report = LevelValidator.ValidatePack(pack.Levels);
            if (!report.IsValid) throw new LevelDataException("levels.json failed validation:\n" + report);
            return pack;
        }

        public static LevelDefinition ParseLevel(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownLevelKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");

            int id = JsonAccess.RequireInt(o, "id", path);
            string name = JsonAccess.RequireString(o, "name", path);
            var size = ParsePair(JsonAccess.Require(o, "size", path), path + ".size");
            var start = ParseCoord(JsonAccess.Require(o, "start", path), path + ".start");
            var exit = ParseCoord(JsonAccess.Require(o, "exit", path), path + ".exit");

            var walls = new List<GridCoord>();
            var wallsJson = JsonAccess.AsArray(JsonAccess.Require(o, "walls", path), path + ".walls");
            for (int i = 0; i < wallsJson.Count; i++) walls.Add(ParseCoord(wallsJson[i], path + ".walls[" + i + "]"));

            var guards = new List<GuardDefinition>();
            var guardsJson = JsonAccess.AsArray(JsonAccess.Require(o, "guards", path), path + ".guards");
            for (int i = 0; i < guardsJson.Count; i++)
                guards.Add(ParseGuard(JsonAccess.AsObject(guardsJson[i], path + ".guards[" + i + "]"), path + ".guards[" + i + "]", warnings));

            int smoke = JsonAccess.OptionalInt(o, "smoke", 0, path);
            int emp = JsonAccess.OptionalInt(o, "emp", 0, path);
            GridCoord? key = o.TryGetValue("key", out var kv) && kv != null ? ParseCoord(kv, path + ".key") : (GridCoord?)null;
            GridCoord? door = o.TryGetValue("door", out var dv) && dv != null ? ParseCoord(dv, path + ".door") : (GridCoord?)null;
            string hint = JsonAccess.OptionalString(o, "hint", null, path);

            return new LevelDefinition(id, name, size.X, size.Y, start, exit, walls, guards, smoke, emp, key, door, hint);
        }

        private static GuardDefinition ParseGuard(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownGuardKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");
            string modeToken = JsonAccess.RequireString(o, "mode", path);
            if (!GuardModeUtil.TryParse(modeToken, out var mode))
                throw new LevelDataException(path + ".mode '" + modeToken + "' is not one of fixed, rotate, patrol, patrol_rotate, loop");
            var routeJson = JsonAccess.AsArray(JsonAccess.Require(o, "route", path), path + ".route");
            var route = new List<GridCoord>(routeJson.Count);
            for (int i = 0; i < routeJson.Count; i++) route.Add(ParseCoord(routeJson[i], path + ".route[" + i + "]"));
            int facing = JsonAccess.RequireInt(o, "facing", path);
            if (!DirectionUtil.IsValid(facing)) throw new LevelDataException(path + ".facing must be 0 (N), 1 (E), 2 (S) or 3 (W), got " + facing);
            int range = JsonAccess.RequireInt(o, "range", path);
            int step = JsonAccess.OptionalInt(o, "step", 1, path);
            int rotation = JsonAccess.OptionalInt(o, "rotation", 1, path);
            return new GuardDefinition(mode, route, (Direction)facing, range, step, rotation);
        }

        private static GridCoord ParsePair(object v, string path)
        {
            var a = JsonAccess.AsArray(v, path);
            if (a.Count != 2) throw new LevelDataException(path + " must be a 2-element [x, y] array, got " + a.Count + " elements");
            return new GridCoord(JsonAccess.AsInt(a[0], path + "[0]"), JsonAccess.AsInt(a[1], path + "[1]"));
        }

        public static GridCoord ParseCoord(object v, string path) => ParsePair(v, path);

        private static void Migrate(JsonObject root, int fromVersion, List<string> warnings)
        {
            // No older formats exist yet. When format 2 ships, transform `root` in place here, one version at a time.
            warnings.Add("migrated level pack from format_version " + fromVersion + " to " + LevelPack.CurrentFormatVersion);
            root["format_version"] = (long)LevelPack.CurrentFormatVersion;
        }
    }

    /// <summary>Serialises levels back to the reference JSON layout (used by the level editor export).</summary>
    public static class LevelPackWriter
    {
        public static string ToJson(LevelPack pack) => ToJson(pack.Levels, pack.CoordinateSystem);

        public static string ToJson(IReadOnlyList<LevelDefinition> levels, string coordinateSystem = "origin top-left, x right, y down")
        {
            var root = new JsonObject()
                .Set("format_version", (long)LevelPack.CurrentFormatVersion)
                .Set("coordinate_system", coordinateSystem);
            var list = new List<object>();
            foreach (var l in levels) list.Add(ToJsonObject(l));
            root["levels"] = list;
            return MiniJson.Serialize(root, true) + "\n";
        }

        public static JsonObject ToJsonObject(LevelDefinition l)
        {
            var o = new JsonObject()
                .Set("id", (long)l.Id)
                .Set("name", l.Name)
                .Set("size", Pair(l.Width, l.Height))
                .Set("start", Coord(l.Start))
                .Set("exit", Coord(l.Exit));
            var walls = new List<object>();
            foreach (var w in l.Walls) walls.Add(Coord(w));
            o["walls"] = walls;
            var guards = new List<object>();
            foreach (var g in l.Guards)
            {
                var route = new List<object>();
                foreach (var c in g.Route) route.Add(Coord(c));
                guards.Add(new JsonObject()
                    .Set("mode", GuardModeUtil.ToToken(g.Mode))
                    .Set("route", route)
                    .Set("facing", (long)(int)g.Facing)
                    .Set("range", (long)g.Range)
                    .Set("step", (long)g.Step)
                    .Set("rotation", (long)g.Rotation));
            }
            o["guards"] = guards;
            o["smoke"] = (long)l.SmokeCharges;
            o["emp"] = (long)l.EmpCharges;
            o["key"] = l.Key.HasValue ? Coord(l.Key.Value) : null;
            o["door"] = l.Door.HasValue ? Coord(l.Door.Value) : null;
            if (!string.IsNullOrEmpty(l.Hint)) o["hint"] = l.Hint;
            return o;
        }

        private static List<object> Pair(int a, int b) => new List<object> { (long)a, (long)b };
        private static List<object> Coord(GridCoord c) => Pair(c.X, c.Y);
    }
}
