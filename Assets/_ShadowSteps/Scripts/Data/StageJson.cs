using System;
using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;

namespace ShadowSteps.Data
{
    /// <summary>A loaded schema-3 pack.</summary>
    public sealed class StagePack
    {
        public IReadOnlyList<Stage> Stages { get; }
        public IReadOnlyList<string> Warnings { get; }
        public StagePack(IReadOnlyList<Stage> stages, IReadOnlyList<string> warnings) { Stages = stages; Warnings = warnings; }
        public Stage FindByCode(string code) { foreach (var s in Stages) if (s.Code == code) return s; return null; }
        public Stage FindById(int id) { foreach (var s in Stages) if (s.Id == id) return s; return null; }
    }

    /// <summary>
    /// Schema 3 stage packs (`schemaVersion: 3`). Rows encode walls; connections are implicit (every adjacent floor pair)
    /// minus `cuts`; doors are locked connections; cameras mount on wall nodes. Scoring is solver-written.
    /// </summary>
    public static class StageJson
    {
        private static readonly HashSet<string> KnownStageKeys = new HashSet<string>
            { "id", "code", "group", "title", "intro", "rows", "start", "exit", "key", "cuts", "doors", "guards", "cameras", "gadgets", "alarmBudget", "scoring", "notes" };

        public static StagePack Parse(string json)
        {
            var warnings = new List<string>();
            var root = JsonAccess.AsObject(MiniJson.Parse(json), "root");
            int schema = JsonAccess.OptionalInt(root, "schemaVersion", 0, "root");
            if (schema != Stage.SchemaVersion) throw new LevelDataException("root.schemaVersion must be " + Stage.SchemaVersion + ", got " + schema);
            var list = new List<Stage>();
            var arr = JsonAccess.AsArray(JsonAccess.Require(root, "levels", "root"), "root.levels");
            for (int i = 0; i < arr.Count; i++) list.Add(ParseStage(JsonAccess.AsObject(arr[i], "levels[" + i + "]"), "levels[" + i + "]", warnings));
            return new StagePack(list, warnings);
        }

        public static Stage ParseStage(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownStageKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");
            var b = new StageBuilder
            {
                Id = JsonAccess.RequireInt(o, "id", path),
                Code = JsonAccess.OptionalString(o, "code", null, path),
                Group = JsonAccess.OptionalString(o, "group", "Main", path),
                Title = JsonAccess.RequireString(o, "title", path),
                Intro = JsonAccess.OptionalString(o, "intro", string.Empty, path),
                AlarmBudget = JsonAccess.OptionalInt(o, "alarmBudget", 4, path)
            };
            if (b.Code == null) b.Code = "LEVEL-" + b.Id;
            var rows = new List<string>();
            foreach (var r in JsonAccess.AsArray(JsonAccess.Require(o, "rows", path), path + ".rows")) rows.Add(JsonAccess.AsString(r, path + ".rows"));
            b.FromRows(rows);
            b.Start = Coord(JsonAccess.Require(o, "start", path), path + ".start");
            b.Exit = Coord(JsonAccess.Require(o, "exit", path), path + ".exit");
            if (o.TryGetValue("key", out var kv) && kv != null) b.Key = Coord(kv, path + ".key");
            if (o.TryGetValue("cuts", out var cv) && cv != null)
            {
                var arr = JsonAccess.AsArray(cv, path + ".cuts");
                for (int i = 0; i < arr.Count; i++)
                {
                    var pair = JsonAccess.AsArray(arr[i], path + ".cuts[" + i + "]");
                    if (pair.Count != 2) throw new LevelDataException(path + ".cuts[" + i + "] must be [[x,y],[x,y]]");
                    b.Cuts.Add(new EdgeKey(Coord(pair[0], path + ".cuts"), Coord(pair[1], path + ".cuts")));
                }
            }
            if (o.TryGetValue("doors", out var dv) && dv != null)
            {
                var arr = JsonAccess.AsArray(dv, path + ".doors");
                for (int i = 0; i < arr.Count; i++)
                {
                    var d = JsonAccess.AsObject(arr[i], path + ".doors[" + i + "]");
                    b.Doors.Add(new DoorSpec(Coord(JsonAccess.Require(d, "a", path), path + ".doors.a"), Coord(JsonAccess.Require(d, "b", path), path + ".doors.b")));
                }
            }
            if (o.TryGetValue("guards", out var gv) && gv != null)
            {
                var arr = JsonAccess.AsArray(gv, path + ".guards");
                for (int i = 0; i < arr.Count; i++)
                {
                    var g = JsonAccess.AsObject(arr[i], path + ".guards[" + i + "]"); string gp = path + ".guards[" + i + "]";
                    var kind = ParseGuardKind(JsonAccess.RequireString(g, "kind", gp), gp);
                    var route = new List<GridCoord>();
                    if (g.TryGetValue("route", out var rv) && rv != null) foreach (var c in JsonAccess.AsArray(rv, gp + ".route")) route.Add(Coord(c, gp + ".route"));
                    if (g.TryGetValue("at", out var av) && av != null) route.Insert(0, Coord(av, gp + ".at"));
                    b.Guards.Add(new GuardSpec(JsonAccess.OptionalString(g, "id", "g" + (i + 1), gp), kind, route, ParseDir(JsonAccess.OptionalString(g, "facing", "N", gp), gp + ".facing"),
                        JsonAccess.OptionalBool(g, "clockwise", true, gp), JsonAccess.OptionalInt(g, "offset", 0, gp)));
                }
            }
            if (o.TryGetValue("cameras", out var camv) && camv != null)
            {
                var arr = JsonAccess.AsArray(camv, path + ".cameras");
                for (int i = 0; i < arr.Count; i++)
                {
                    var c = JsonAccess.AsObject(arr[i], path + ".cameras[" + i + "]"); string cp = path + ".cameras[" + i + "]";
                    var mount = ParseDir(JsonAccess.RequireString(c, "mountFace", cp), cp + ".mountFace");
                    b.Cameras.Add(new CameraSpec(JsonAccess.OptionalString(c, "id", "cam" + (i + 1), cp), Coord(JsonAccess.Require(c, "wall", cp), cp + ".wall"), mount,
                        ParseDir(JsonAccess.OptionalString(c, "facing", DirectionUtil.ToToken(mount), cp), cp + ".facing"),
                        ParseCameraKind(JsonAccess.OptionalString(c, "kind", "fixed", cp), cp), JsonAccess.OptionalBool(c, "clockwise", true, cp)));
                }
            }
            if (o.TryGetValue("gadgets", out var gad) && gad != null)
            {
                var g = JsonAccess.AsObject(gad, path + ".gadgets");
                b.SmokeCharges = JsonAccess.OptionalInt(g, "smoke", 0, path + ".gadgets");
                b.EmpCharges = JsonAccess.OptionalInt(g, "emp", 0, path + ".gadgets");
            }
            if (o.TryGetValue("scoring", out var sc) && sc != null)
            {
                var s = JsonAccess.AsObject(sc, path + ".scoring");
                b.Par = JsonAccess.OptionalInt(s, "par", 0, path + ".scoring");
                b.ThreeStarActions = JsonAccess.OptionalInt(s, "threeStar", 0, path + ".scoring");
                b.TwoStarActions = JsonAccess.OptionalInt(s, "twoStar", 0, path + ".scoring");
                b.GhostOffered = JsonAccess.OptionalBool(s, "ghostOffered", false, path + ".scoring");
            }
            if (o.TryGetValue("notes", out var nv) && nv != null) foreach (var n in JsonAccess.AsArray(nv, path + ".notes")) b.Notes.Add((string)n);
            return b.Build();
        }

        public static string ToJson(IReadOnlyList<Stage> stages)
        {
            var root = new JsonObject().Set("schemaVersion", (long)Stage.SchemaVersion).Set("format", "ShadowSteps stage pack").Set("coordinateSystem", "origin top-left, x right, y down");
            var list = new List<object>();
            foreach (var s in stages) list.Add(ToJsonObject(s));
            root["levels"] = list;
            return MiniJson.Serialize(root, true) + "\n";
        }

        public static JsonObject ToJsonObject(Stage s)
        {
            var o = new JsonObject().Set("id", (long)s.Id).Set("code", s.Code).Set("group", s.Group).Set("title", s.Title).Set("intro", s.Intro);
            var rows = new List<object>(); foreach (var r in StageBuilder.RowsOf(s)) rows.Add(r);
            o["rows"] = rows;
            o["start"] = Coord(s.Start); o["exit"] = Coord(s.Exit); o["key"] = s.Key.HasValue ? Coord(s.Key.Value) : null;
            var cuts = new List<object>(); foreach (var c in s.Cuts) cuts.Add(new List<object> { Coord(c.Lo), Coord(c.Hi) });
            o["cuts"] = cuts;
            var doors = new List<object>(); foreach (var d in s.Doors) doors.Add(new JsonObject().Set("a", Coord(d.A)).Set("b", Coord(d.B)));
            o["doors"] = doors;
            var guards = new List<object>();
            foreach (var g in s.Guards)
            {
                var route = new List<object>(); foreach (var c in g.Route) route.Add(Coord(c));
                var go = new JsonObject().Set("id", g.Id).Set("kind", GuardKindToken(g.Kind)).Set("route", route).Set("facing", DirectionUtil.ToToken(g.Facing));
                if (g.Kind == GuardKind.Rotating) go["clockwise"] = g.Clockwise;
                if (g.Moves) go["offset"] = (long)g.Offset;
                guards.Add(go);
            }
            o["guards"] = guards;
            var cams = new List<object>();
            foreach (var c in s.Cameras)
                cams.Add(new JsonObject().Set("id", c.Id).Set("wall", Coord(c.WallCell)).Set("mountFace", DirectionUtil.ToToken(c.MountFace)).Set("facing", DirectionUtil.ToToken(c.Facing))
                    .Set("kind", CameraKindToken(c.Kind)).Set("clockwise", c.Clockwise));
            o["cameras"] = cams;
            o["gadgets"] = new JsonObject().Set("smoke", (long)s.SmokeCharges).Set("emp", (long)s.EmpCharges);
            o["alarmBudget"] = (long)s.AlarmBudget;
            o["scoring"] = new JsonObject().Set("par", (long)s.Par).Set("threeStar", (long)s.ThreeStarActions).Set("twoStar", (long)s.TwoStarActions).Set("ghostOffered", s.GhostOffered);
            if (s.Notes.Count > 0) o["notes"] = new List<object>(s.Notes);
            return o;
        }

        public static string GuardKindToken(GuardKind k) => k == GuardKind.Static ? "static" : k == GuardKind.Rotating ? "rotating" : k == GuardKind.PingPong ? "pingPong" : "loop";
        public static string CameraKindToken(CameraKind k) => k == CameraKind.Fixed ? "fixed" : k == CameraKind.Rotating ? "rotating" : "sweeping";

        public static GuardKind ParseGuardKind(string token, string path)
        {
            switch ((token ?? "").Trim().ToLowerInvariant())
            {
                case "static": case "fixed": return GuardKind.Static;
                case "rotating": case "rot": case "rotate": return GuardKind.Rotating;
                case "pingpong": case "ping_pong": case "bounce": case "patrol": return GuardKind.PingPong;
                case "loop": return GuardKind.Loop;
                default: throw new LevelDataException(path + ".kind '" + token + "' is not one of static, rotating, pingPong, loop");
            }
        }

        public static CameraKind ParseCameraKind(string token, string path)
        {
            switch ((token ?? "").Trim().ToLowerInvariant())
            {
                case "fixed": return CameraKind.Fixed;
                case "rotating": case "rot": return CameraKind.Rotating;
                case "sweeping": case "sweep": return CameraKind.Sweeping;
                default: throw new LevelDataException(path + ".kind '" + token + "' is not one of fixed, rotating, sweeping");
            }
        }

        public static Direction ParseDir(string token, string path)
        {
            switch ((token ?? "").Trim().ToUpperInvariant())
            {
                case "N": case "NORTH": return Direction.North;
                case "E": case "EAST": return Direction.East;
                case "S": case "SOUTH": return Direction.South;
                case "W": case "WEST": return Direction.West;
                default: throw new LevelDataException(path + " '" + token + "' is not N, E, S or W");
            }
        }

        public static GridCoord Coord(object v, string path)
        {
            var arr = JsonAccess.AsArray(v, path);
            if (arr.Count != 2) throw new LevelDataException(path + " must be [x, y]");
            return new GridCoord(JsonAccess.AsInt(arr[0], path + "[0]"), JsonAccess.AsInt(arr[1], path + "[1]"));
        }

        public static List<object> Coord(GridCoord c) => new List<object> { (long)c.X, (long)c.Y };
    }

    /// <summary>
    /// Converts the browser playtest export (V3 `levels_v3_playtest_reference.json` or V4 `shadow_steps_levels_v4.json` with `X` cover and `optimalActions`/`ghostAvailable`)
    /// into schema 3 stages. Browser pars/Ghost flags are kept as notes for parity checks; scoring is recomputed later.
    /// </summary>
    public static class BrowserLevelImporter
    {
        public sealed class Imported
        {
            public Stage Stage;
            public int BrowserPar;
            public bool BrowserGhost;
        }

        public static List<Imported> Import(string json)
        {
            var root = JsonAccess.AsObject(MiniJson.Parse(json), "root");
            var arr = JsonAccess.AsArray(JsonAccess.Require(root, "levels", "root"), "root.levels");
            var list = new List<Imported>();
            for (int i = 0; i < arr.Count; i++) list.Add(ImportOne(JsonAccess.AsObject(arr[i], "levels[" + i + "]"), "levels[" + i + "]"));
            return list;
        }

        public static Imported ImportOne(JsonObject o, string path)
        {
            string label = JsonAccess.OptionalString(o, "label", "", path);
            var b = new StageBuilder
            {
                Id = JsonAccess.RequireInt(o, "id", path),
                Group = JsonAccess.OptionalString(o, "group", "Main", path),
                Title = JsonAccess.RequireString(o, "title", path),
                Intro = JsonAccess.OptionalString(o, "intro", string.Empty, path),
                SmokeCharges = JsonAccess.OptionalInt(o, "smoke", 0, path),
                EmpCharges = JsonAccess.OptionalInt(o, "emp", 0, path)
            };
            int budget = JsonAccess.OptionalInt(o, "budget", 4, path);
            b.AlarmBudget = budget > 0 ? budget : 4; // prototype: L.budget || 4
            b.Code = label.StartsWith("FTUE", StringComparison.OrdinalIgnoreCase) ? "FTUE-" + label.Substring(label.LastIndexOf(' ') + 1) : "LEVEL-" + label.Substring(label.LastIndexOf(' ') + 1);
            var rows = new List<string>();
            foreach (var r in JsonAccess.AsArray(JsonAccess.Require(o, "rows", path), path + ".rows")) rows.Add(JsonAccess.AsString(r, path + ".rows"));
            b.FromRows(rows);
            b.Start = StageJson.Coord(JsonAccess.Require(o, "start", path), path + ".start");
            b.Exit = StageJson.Coord(JsonAccess.Require(o, "exit", path), path + ".exit");
            if (o.TryGetValue("key", out var kv) && kv != null) b.Key = StageJson.Coord(kv, path + ".key");
            if (o.TryGetValue("cuts", out var cv) && cv != null)
                foreach (var c in JsonAccess.AsArray(cv, path + ".cuts"))
                {
                    var parts = ((string)c).Split('|');
                    b.Cuts.Add(new EdgeKey(ParseXY(parts[0]), ParseXY(parts[1])));
                }
            if (o.TryGetValue("doors", out var dv) && dv != null)
                foreach (var d in JsonAccess.AsArray(dv, path + ".doors"))
                {
                    var dobj = JsonAccess.AsObject(d, path + ".doors");
                    b.Doors.Add(new DoorSpec(StageJson.Coord(dobj["a"], path + ".doors.a"), StageJson.Coord(dobj["b"], path + ".doors.b")));
                }
            if (o.TryGetValue("guards", out var gv) && gv != null)
            {
                var arr = JsonAccess.AsArray(gv, path + ".guards");
                for (int i = 0; i < arr.Count; i++)
                {
                    var g = JsonAccess.AsObject(arr[i], path + ".guards[" + i + "]"); string gp = path + ".guards[" + i + "]";
                    string type = JsonAccess.RequireString(g, "type", gp);
                    var kind = StageJson.ParseGuardKind(type, gp);
                    var route = new List<GridCoord>();
                    if (g.TryGetValue("path", out var pv) && pv != null) foreach (var c in JsonAccess.AsArray(pv, gp + ".path")) route.Add(StageJson.Coord(c, gp + ".path"));
                    if (g.TryGetValue("at", out var av) && av != null) route.Insert(0, StageJson.Coord(av, gp + ".at"));
                    var facing = StageJson.ParseDir(JsonAccess.OptionalString(g, "dir", "N", gp), gp + ".dir");
                    bool ccw = JsonAccess.OptionalBool(g, "ccw", false, gp);
                    b.Guards.Add(new GuardSpec("g" + (i + 1), kind, route, facing, !ccw, JsonAccess.OptionalInt(g, "offset", 0, gp)));
                }
            }
            if (o.TryGetValue("cams", out var camv) && camv != null)
            {
                var arr = JsonAccess.AsArray(camv, path + ".cams");
                for (int i = 0; i < arr.Count; i++)
                {
                    var c = JsonAccess.AsObject(arr[i], path + ".cams[" + i + "]"); string cp = path + ".cams[" + i + "]";
                    var mount = StageJson.ParseDir(JsonAccess.OptionalString(c, "mountDir", JsonAccess.OptionalString(c, "dir", "S", cp), cp), cp + ".mountDir");
                    var facing = StageJson.ParseDir(JsonAccess.OptionalString(c, "dir", DirectionUtil.ToToken(mount), cp), cp + ".dir");
                    var kind = StageJson.ParseCameraKind(JsonAccess.OptionalString(c, "type", "fixed", cp), cp);
                    bool ccw = JsonAccess.OptionalBool(c, "ccw", false, cp);
                    b.Cameras.Add(new CameraSpec("cam" + (i + 1), StageJson.Coord(c["wall"], cp + ".wall"), mount, facing, kind, !ccw));
                }
            }
            // Handover §5: cameras mount on physical walls. The browser export leaves a floor notch under three cameras
            // (rows "###.###"); make that node a wall so the camera has a real mounting surface. Vision is unaffected
            // (the lens sits outside the wall) and the player can no longer stand inside the camera.
            foreach (var cam in b.Cameras)
                if (!b.Walls.Contains(cam.WallCell) && cam.WallCell.X >= 0 && cam.WallCell.X < b.Width && cam.WallCell.Y >= 0 && cam.WallCell.Y < b.Height)
                {
                    b.Walls.Add(cam.WallCell);
                    b.Notes.Add("camera " + cam.Id + " mounting node " + cam.WallCell + " was floor in the browser export; converted to wall");
                }
            // V4: rows are authoritative; the redundant blockedNodes list must agree exactly (handover §2.1.7).
            if (o.TryGetValue("blockedNodes", out var bnv) && bnv != null)
            {
                var listed = new HashSet<GridCoord>();
                foreach (var c in JsonAccess.AsArray(bnv, path + ".blockedNodes")) listed.Add(StageJson.Coord(c, path + ".blockedNodes"));
                var fromRows = new HashSet<GridCoord>(b.Blocked);
                foreach (var c in listed) if (!fromRows.Contains(c)) throw new LevelDataException(label + ": blockedNodes lists " + c + " but rows have no 'X' there");
                foreach (var c in fromRows) if (!listed.Contains(c)) throw new LevelDataException(label + ": rows have 'X' at " + c + " but blockedNodes does not list it");
            }
            b.Notes.Add("imported from browser playtest export (" + label + (b.Blocked.Count > 0 ? ", " + b.Blocked.Count + " blocked cover nodes" : "") + ")");
            int browserPar = o.ContainsKey("optimalActions") ? JsonAccess.OptionalInt(o, "optimalActions", 0, path) : JsonAccess.OptionalInt(o, "par", 0, path);
            bool browserGhost = o.ContainsKey("ghostAvailable") ? JsonAccess.OptionalBool(o, "ghostAvailable", false, path) : JsonAccess.OptionalBool(o, "ghostOffered", false, path);
            return new Imported { Stage = b.Build(), BrowserPar = browserPar, BrowserGhost = browserGhost };
        }

        private static GridCoord ParseXY(string s)
        {
            var p = s.Split(',');
            return new GridCoord(int.Parse(p[0]), int.Parse(p[1]));
        }
    }
}
