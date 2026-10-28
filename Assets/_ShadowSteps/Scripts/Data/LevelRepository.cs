using System;
using System.Collections.Generic;
using ShadowSteps.Core;

namespace ShadowSteps.Data
{
    /// <summary>
    /// Converts level JSON into immutable <see cref="LevelDefinition"/>s with strict field validation.
    /// Two formats are understood:
    ///   - schema 1 (legacy, root key <c>format_version: 1</c>): rectangular grid, walls, implicit adjacency. Loaded through
    ///     <see cref="LegacyLevelMigrator"/>, which generates the explicit edge graph and records what it converted.
    ///   - schema 2 (root key <c>schemaVersion: 2</c>): explicit nodes, edges, occluders, guard cadences (LEVEL_DESIGN_PARAMETERS.md §6).
    /// Unknown fields are warnings so older builds tolerate newer data; structural errors carry a JSON path.
    /// </summary>
    public static class LevelPackLoader
    {
        private static readonly HashSet<string> KnownLegacyLevelKeys = new HashSet<string>
            { "id", "name", "size", "start", "exit", "walls", "guards", "smoke", "emp", "key", "door", "hint" };
        private static readonly HashSet<string> KnownLegacyGuardKeys = new HashSet<string>
            { "mode", "route", "facing", "range", "step", "rotation" };
        private static readonly HashSet<string> KnownV2LevelKeys = new HashSet<string>
            { "schemaVersion", "id", "name", "world", "chapter", "theme", "board", "player", "objective", "guards", "gadgets", "restrictions", "rules", "presentation", "scoring", "conversionNotes", "migratedFromSchema" };
        private static readonly HashSet<string> KnownV2BoardKeys = new HashSet<string>
            { "width", "height", "nodeSpacing", "nodes", "nodeMask", "edges", "blockedEdges", "lockedEdges", "walls", "cover", "decor" };
        private static readonly HashSet<string> KnownV2GuardKeys = new HashSet<string>
            { "id", "movementMode", "mode", "route", "facing", "rotationStep", "rotation", "moveEvery", "rotateEvery", "visionRangeNodes", "range", "coneAngleDegrees" };

        /// <summary>Parses either format and structurally validates. Throws <see cref="LevelDataException"/> with a JSON path on bad data.</summary>
        public static LevelPack Parse(string json)
        {
            object rootValue;
            try { rootValue = MiniJson.Parse(json); }
            catch (JsonParseException e) { throw new LevelDataException("level data is not valid JSON: " + e.Message, e); }

            var root = JsonAccess.AsObject(rootValue, "root");
            var warnings = new List<string>();
            int schema = DetectSchema(root);
            if (schema == 1) return ParseLegacyPack(root, warnings);
            if (schema == 2) return ParseV2Pack(root, warnings);
            throw new LevelDataException("root.schemaVersion " + schema + " is not supported (supported: format_version 1, schemaVersion 2)");
        }

        /// <summary>Parses, then runs <see cref="LevelValidator"/> and throws if any level has errors.</summary>
        public static LevelPack ParseAndValidate(string json, out ValidationReport report)
        {
            var pack = Parse(json);
            report = LevelValidator.ValidatePack(pack.Levels);
            if (!report.IsValid) throw new LevelDataException("level data failed validation:\n" + report);
            return pack;
        }

        public static int DetectSchema(JsonObject root)
        {
            if (root.TryGetValue("schemaVersion", out var sv) && sv != null) return JsonAccess.AsInt(sv, "root.schemaVersion");
            if (root.TryGetValue("format_version", out var fv) && fv != null) return JsonAccess.AsInt(fv, "root.format_version");
            throw new LevelDataException("root.schemaVersion (or legacy root.format_version) is missing");
        }

        // ---------------------------------------------------------------- schema 1 (legacy)

        private static LevelPack ParseLegacyPack(JsonObject root, List<string> warnings)
        {
            string coords = JsonAccess.OptionalString(root, "coordinate_system", "origin top-left, x right, y down", "root");
            var levelsJson = JsonAccess.AsArray(JsonAccess.Require(root, "levels", "root"), "root.levels");
            var levels = new List<LevelDefinition>(levelsJson.Count);
            for (int i = 0; i < levelsJson.Count; i++)
            {
                var legacy = ParseLegacyLevel(JsonAccess.AsObject(levelsJson[i], "levels[" + i + "]"), "levels[" + i + "]", warnings);
                levels.Add(LegacyLevelMigrator.Migrate(legacy));
            }
            return new LevelPack(1, coords, levels, warnings);
        }

        /// <summary>Parses one legacy level into a schema-1 definition (graph auto-generated from grid adjacency).</summary>
        public static LevelDefinition ParseLegacyLevel(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownLegacyLevelKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");

            int id = JsonAccess.RequireInt(o, "id", path);
            string name = JsonAccess.RequireString(o, "name", path);
            var size = ParseCoord(JsonAccess.Require(o, "size", path), path + ".size");
            var start = ParseCoord(JsonAccess.Require(o, "start", path), path + ".start");
            var exit = ParseCoord(JsonAccess.Require(o, "exit", path), path + ".exit");
            var walls = ParseCoordList(JsonAccess.Require(o, "walls", path), path + ".walls");

            var guards = new List<GuardDefinition>();
            var guardsJson = JsonAccess.AsArray(JsonAccess.Require(o, "guards", path), path + ".guards");
            for (int i = 0; i < guardsJson.Count; i++)
                guards.Add(ParseLegacyGuard(JsonAccess.AsObject(guardsJson[i], path + ".guards[" + i + "]"), path + ".guards[" + i + "]", warnings));

            int smoke = JsonAccess.OptionalInt(o, "smoke", 0, path);
            int emp = JsonAccess.OptionalInt(o, "emp", 0, path);
            GridCoord? key = o.TryGetValue("key", out var kv) && kv != null ? ParseCoord(kv, path + ".key") : (GridCoord?)null;
            GridCoord? door = o.TryGetValue("door", out var dv) && dv != null ? ParseCoord(dv, path + ".door") : (GridCoord?)null;
            string hint = JsonAccess.OptionalString(o, "hint", null, path);

            return new LevelDefinition(id, name, size.X, size.Y, start, exit, walls, guards, smoke, emp, key, door, hint);
        }

        private static GuardDefinition ParseLegacyGuard(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownLegacyGuardKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");
            string modeToken = JsonAccess.RequireString(o, "mode", path);
            if (!GuardModeUtil.TryParse(modeToken, out var mode))
                throw new LevelDataException(path + ".mode '" + modeToken + "' is not one of fixed, rotate, patrol, patrol_rotate, loop");
            var route = ParseCoordList(JsonAccess.Require(o, "route", path), path + ".route");
            int facing = JsonAccess.RequireInt(o, "facing", path);
            if (!DirectionUtil.IsValid(facing)) throw new LevelDataException(path + ".facing must be 0 (N), 1 (E), 2 (S) or 3 (W), got " + facing);
            int range = JsonAccess.RequireInt(o, "range", path);
            int step = JsonAccess.OptionalInt(o, "step", 1, path);
            int rotation = JsonAccess.OptionalInt(o, "rotation", 1, path);
            return new GuardDefinition(mode, route, (Direction)facing, range, step, rotation);
        }

        // ---------------------------------------------------------------- schema 2

        private static LevelPack ParseV2Pack(JsonObject root, List<string> warnings)
        {
            string coords = JsonAccess.OptionalString(root, "coordinateSystem", "origin top-left, x right, y down", "root");
            var levels = new List<LevelDefinition>();
            if (root.ContainsKey("levels"))
            {
                var levelsJson = JsonAccess.AsArray(root["levels"], "root.levels");
                for (int i = 0; i < levelsJson.Count; i++)
                    levels.Add(ParseV2Level(JsonAccess.AsObject(levelsJson[i], "levels[" + i + "]"), "levels[" + i + "]", warnings));
            }
            else if (root.ContainsKey("id"))
            {
                levels.Add(ParseV2Level(root, "level", warnings)); // single-level file (editor export)
            }
            else throw new LevelDataException("root.levels is missing");
            return new LevelPack(2, coords, levels, warnings);
        }

        public static LevelDefinition ParseV2Level(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownV2LevelKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");
            var s = new LevelSpec
            {
                SchemaVersion = JsonAccess.OptionalInt(o, "schemaVersion", 2, path),
                MigratedFromSchema = JsonAccess.OptionalInt(o, "migratedFromSchema", 0, path),
                Id = JsonAccess.RequireInt(o, "id", path),
                Name = JsonAccess.RequireString(o, "name", path),
                World = JsonAccess.OptionalInt(o, "world", 1, path),
                Chapter = JsonAccess.OptionalInt(o, "chapter", 1, path),
                Theme = JsonAccess.OptionalString(o, "theme", null, path)
            };
            if (s.SchemaVersion != 2) throw new LevelDataException(path + ".schemaVersion must be 2, got " + s.SchemaVersion);

            // board
            var board = JsonAccess.AsObject(JsonAccess.Require(o, "board", path), path + ".board");
            string bp = path + ".board";
            foreach (var k in board.Keys) if (!KnownV2BoardKeys.Contains(k)) warnings?.Add(bp + ": unknown field '" + k + "' ignored");
            s.Width = JsonAccess.RequireInt(board, "width", bp);
            s.Height = JsonAccess.RequireInt(board, "height", bp);
            s.NodeSpacing = OptionalDouble(board, "nodeSpacing", LevelGraph.DefaultNodeSpacing, bp);
            if (board.TryGetValue("nodes", out var nodesV) && nodesV != null) s.NodeMask = ParseCoordList(nodesV, bp + ".nodes");
            else if (board.TryGetValue("nodeMask", out var maskV) && maskV != null) s.NodeMask = ParseCoordList(maskV, bp + ".nodeMask");
            if (board.TryGetValue("walls", out var wallsV) && wallsV != null) s.Walls = ParseCoordList(wallsV, bp + ".walls");
            if (board.TryGetValue("cover", out var coverV) && coverV != null)
            {
                var arr = JsonAccess.AsArray(coverV, bp + ".cover");
                for (int i = 0; i < arr.Count; i++)
                {
                    string cp = bp + ".cover[" + i + "]";
                    var co = JsonAccess.AsObject(arr[i], cp);
                    s.Cover.Add(new CoverDefinition(ParseCoord(JsonAccess.Require(co, "pos", cp), cp + ".pos"), JsonAccess.OptionalBool(co, "traversable", false, cp)));
                }
            }
            if (board.TryGetValue("blockedEdges", out var blockedV) && blockedV != null) s.BlockedEdges = ParsePairList(blockedV, bp + ".blockedEdges");
            if (board.TryGetValue("lockedEdges", out var lockedV) && lockedV != null) s.LockedEdges = ParsePairList(lockedV, bp + ".lockedEdges");
            if (board.TryGetValue("edges", out var edgesV) && edgesV != null)
            {
                if (edgesV is string mode)
                {
                    if (mode != "autoCardinalExceptBlocked") throw new LevelDataException(bp + ".edges mode '" + mode + "' is not supported (use \"autoCardinalExceptBlocked\" or an explicit array)");
                }
                else s.ExplicitEdges = ParseEdgeList(edgesV, bp + ".edges");
            }

            // player / objective
            var player = JsonAccess.AsObject(JsonAccess.Require(o, "player", path), path + ".player");
            s.Start = ParseCoord(JsonAccess.Require(player, "start", path + ".player"), path + ".player.start");
            var objective = JsonAccess.AsObject(JsonAccess.Require(o, "objective", path), path + ".objective");
            string op = path + ".objective";
            string objType = JsonAccess.OptionalString(objective, "type", "reachExit", op);
            if (objType != "reachExit") throw new LevelDataException(op + ".type '" + objType + "' is not supported (only reachExit)");
            s.Exit = ParseCoord(JsonAccess.Require(objective, "exit", op), op + ".exit");
            if (objective.TryGetValue("key", out var keyV) && keyV != null) s.Key = ParseCoord(keyV, op + ".key");
            if (objective.TryGetValue("door", out var doorV) && doorV != null) s.Door = ParseCoord(doorV, op + ".door");
            if (objective.TryGetValue("switches", out var swV) && swV != null && JsonAccess.AsArray(swV, op + ".switches").Count > 0)
                throw new LevelDataException(op + ".switches are not simulated yet; remove them or wait for the switch milestone");

            // guards
            if (o.TryGetValue("guards", out var guardsV) && guardsV != null)
            {
                var arr = JsonAccess.AsArray(guardsV, path + ".guards");
                for (int i = 0; i < arr.Count; i++) s.Guards.Add(ParseV2Guard(JsonAccess.AsObject(arr[i], path + ".guards[" + i + "]"), path + ".guards[" + i + "]", warnings));
            }

            // gadgets / restrictions / presentation / scoring
            if (o.TryGetValue("gadgets", out var gadV) && gadV != null)
            {
                var g = JsonAccess.AsObject(gadV, path + ".gadgets");
                string gp = path + ".gadgets";
                s.SmokeCharges = JsonAccess.OptionalInt(g, "smokeCharges", 0, gp);
                s.EmpCharges = JsonAccess.OptionalInt(g, "empCharges", 0, gp);
                s.DecoyCharges = JsonAccess.OptionalInt(g, "decoyCharges", 0, gp);
                s.SmokeTurns = JsonAccess.OptionalInt(g, "smokeTurns", LevelDefinition.DefaultSmokeTurns, gp);
                s.EmpTurns = JsonAccess.OptionalInt(g, "empTurns", LevelDefinition.DefaultEmpTurns, gp);
            }
            if (o.TryGetValue("restrictions", out var resV) && resV != null)
            {
                var rr = JsonAccess.AsObject(resV, path + ".restrictions");
                string rp = path + ".restrictions";
                s.MaxMoves = JsonAccess.OptionalInt(rr, "maxMoves", 0, rp);
                s.RequiredGadget = JsonAccess.OptionalString(rr, "requiredGadget", null, rp);
                s.NoWait = JsonAccess.OptionalBool(rr, "noWait", false, rp);
                s.NoUndo = JsonAccess.OptionalBool(rr, "noUndo", false, rp);
            }
            if (o.TryGetValue("rules", out var rulesV) && rulesV != null)
            {
                var ru = JsonAccess.AsObject(rulesV, path + ".rules");
                string rup = path + ".rules";
                foreach (var k in ru.Keys) if (k != "detection") warnings?.Add(rup + ": unknown field '" + k + "' ignored");
                string token = JsonAccess.OptionalString(ru, "detection", DetectionRuleUtil.ToToken(LevelDefinition.DefaultDetection), rup);
                if (!DetectionRuleUtil.TryParse(token, out var rule))
                    throw new LevelDataException(rup + ".detection '" + token + "' is not one of " + DetectionRuleUtil.AfterToken + ", " + DetectionRuleUtil.BeforeAndAfterToken);
                s.Detection = rule;
            }
            if (o.TryGetValue("presentation", out var presV) && presV != null)
            {
                var pr = JsonAccess.AsObject(presV, path + ".presentation");
                s.Theme = JsonAccess.OptionalString(pr, "theme", s.Theme, path + ".presentation");
                s.Hint = JsonAccess.OptionalString(pr, "tutorialHint", null, path + ".presentation");
            }
            if (o.TryGetValue("scoring", out var scV) && scV != null)
            {
                var sc = JsonAccess.AsObject(scV, path + ".scoring");
                s.ParTurns = JsonAccess.OptionalInt(sc, "parTurns", 0, path + ".scoring");
                if (sc.TryGetValue("starThresholds", out var stV) && stV != null)
                {
                    var arr = JsonAccess.AsArray(stV, path + ".scoring.starThresholds");
                    for (int i = 0; i < arr.Count; i++) s.StarThresholds.Add(JsonAccess.AsInt(arr[i], path + ".scoring.starThresholds[" + i + "]"));
                }
            }
            if (o.TryGetValue("conversionNotes", out var cnV) && cnV != null)
                foreach (var n in JsonAccess.AsArray(cnV, path + ".conversionNotes")) s.ConversionNotes.Add(JsonAccess.AsString(n, path + ".conversionNotes[]"));

            return LevelDefinition.FromSpec(s);
        }

        private static GuardDefinition ParseV2Guard(JsonObject o, string path, List<string> warnings)
        {
            foreach (var k in o.Keys) if (!KnownV2GuardKeys.Contains(k)) warnings?.Add(path + ": unknown field '" + k + "' ignored");
            string modeToken = JsonAccess.OptionalString(o, "movementMode", null, path) ?? JsonAccess.RequireString(o, "mode", path);
            if (!GuardModeUtil.TryParse(modeToken, out var mode))
                throw new LevelDataException(path + ".movementMode '" + modeToken + "' is not one of fixed, rotate, pingPong, moveRotate, loop");
            var route = ParseCoordList(JsonAccess.Require(o, "route", path), path + ".route");
            var facing = ParseFacing(JsonAccess.Require(o, "facing", path), path + ".facing");
            int rotation = o.ContainsKey("rotationStep") ? JsonAccess.OptionalInt(o, "rotationStep", 1, path) : JsonAccess.OptionalInt(o, "rotation", 1, path);
            int moveEvery = JsonAccess.OptionalInt(o, "moveEvery", 1, path);
            int rotateEvery = JsonAccess.OptionalInt(o, "rotateEvery", 1, path);
            int range = o.ContainsKey("visionRangeNodes") ? JsonAccess.RequireInt(o, "visionRangeNodes", path) : JsonAccess.RequireInt(o, "range", path);
            int cone = JsonAccess.OptionalInt(o, "coneAngleDegrees", 90, path);
            if (cone != 90) throw new LevelDataException(path + ".coneAngleDegrees must be 90 (only the discrete 90-degree cone is simulated), got " + cone);
            string id = JsonAccess.OptionalString(o, "id", null, path);
            return new GuardDefinition(mode, route, facing, range, moveEvery, rotateEvery, rotation, id);
        }

        public static Direction ParseFacing(object v, string path)
        {
            if (v is string s)
            {
                switch (s.Trim().ToUpperInvariant())
                {
                    case "N": case "NORTH": return Direction.North;
                    case "E": case "EAST": return Direction.East;
                    case "S": case "SOUTH": return Direction.South;
                    case "W": case "WEST": return Direction.West;
                    default: throw new LevelDataException(path + " must be N, E, S or W, got '" + s + "'");
                }
            }
            int f = JsonAccess.AsInt(v, path);
            if (!DirectionUtil.IsValid(f)) throw new LevelDataException(path + " must be 0..3 or N/E/S/W, got " + f);
            return (Direction)f;
        }

        public static GridCoord ParseCoord(object v, string path)
        {
            var a = JsonAccess.AsArray(v, path);
            if (a.Count != 2) throw new LevelDataException(path + " must be a 2-element [x, y] array, got " + a.Count + " elements");
            return new GridCoord(JsonAccess.AsInt(a[0], path + "[0]"), JsonAccess.AsInt(a[1], path + "[1]"));
        }

        public static List<GridCoord> ParseCoordList(object v, string path)
        {
            var arr = JsonAccess.AsArray(v, path);
            var list = new List<GridCoord>(arr.Count);
            for (int i = 0; i < arr.Count; i++) list.Add(ParseCoord(arr[i], path + "[" + i + "]"));
            return list;
        }

        private static List<KeyValuePair<GridCoord, GridCoord>> ParsePairList(object v, string path)
        {
            var arr = JsonAccess.AsArray(v, path);
            var list = new List<KeyValuePair<GridCoord, GridCoord>>(arr.Count);
            for (int i = 0; i < arr.Count; i++)
            {
                var pair = JsonAccess.AsArray(arr[i], path + "[" + i + "]");
                if (pair.Count != 2) throw new LevelDataException(path + "[" + i + "] must be [[x1,y1],[x2,y2]]");
                list.Add(new KeyValuePair<GridCoord, GridCoord>(ParseCoord(pair[0], path + "[" + i + "][0]"), ParseCoord(pair[1], path + "[" + i + "][1]")));
            }
            return list;
        }

        private static List<Edge> ParseEdgeList(object v, string path)
        {
            var arr = JsonAccess.AsArray(v, path);
            var list = new List<Edge>();
            for (int i = 0; i < arr.Count; i++)
            {
                string ep = path + "[" + i + "]";
                var o = JsonAccess.AsObject(arr[i], ep);
                var from = ParseCoord(JsonAccess.Require(o, "from", ep), ep + ".from");
                var to = ParseCoord(JsonAccess.Require(o, "to", ep), ep + ".to");
                string type = JsonAccess.OptionalString(o, "type", "open", ep);
                bool bidirectional = JsonAccess.OptionalBool(o, "bidirectional", type != "oneWay", ep);
                EdgeType et;
                switch (type)
                {
                    case "open": et = EdgeType.Open; break;
                    case "oneWay": et = EdgeType.Open; bidirectional = false; break;
                    case "locked": et = EdgeType.Locked; break;
                    case "switchable": throw new LevelDataException(ep + ".type 'switchable' is not simulated yet (no switches in this version)");
                    default: throw new LevelDataException(ep + ".type '" + type + "' is not one of open, locked, oneWay");
                }
                list.Add(new Edge(from, to, et));
                if (bidirectional) list.Add(new Edge(to, from, et));
            }
            return list;
        }

        private static double OptionalDouble(JsonObject o, string key, double fallback, string path)
        {
            if (!o.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is double d) return d;
            throw new LevelDataException(path + "." + key + " must be a number");
        }
    }

    /// <summary>
    /// Converts legacy (schema 1) levels to schema 2 definitions. The topology is generated by
    /// <see cref="LevelGraph.BuildLegacyGrid"/> (every cell a node, walls as wall nodes, bidirectional open edges between
    /// adjacent walkable nodes, edges into the door node locked), which reproduces V1 movement exactly. The record of what
    /// was converted is kept in <see cref="LevelDefinition.ConversionNotes"/> and exported with the level.
    /// </summary>
    public static class LegacyLevelMigrator
    {
        public static LevelDefinition Migrate(LevelDefinition legacy)
        {
            if (legacy.SchemaVersion != 1) return legacy;
            var s = legacy.ToSpec(keepExplicitEdges: true);
            s.SchemaVersion = 2;
            s.MigratedFromSchema = 1;
            s.ConversionNotes.Clear();
            s.ConversionNotes.Add("migrated from schema 1 (grid adjacency) to schema 2 (explicit edges)");
            s.ConversionNotes.Add("generated " + legacy.Graph.EdgeCount + " directed edges between cardinally adjacent walkable cells; " + legacy.Walls.Count + " wall cells kept as wall nodes");
            if (legacy.Door.HasValue)
            {
                int locked = 0;
                foreach (var e in legacy.Graph.Edges) if (e.Type == EdgeType.Locked) locked++;
                s.ConversionNotes.Add("door " + legacy.Door.Value + " converted to " + locked + " locked incoming edges; door node occludes sight until the key is held");
            }
            foreach (var g in legacy.Guards)
                if (g.MoveEvery != 1 || g.RotateEvery != 1) { s.ConversionNotes.Add("guard step " + g.MoveEvery + " mapped to moveEvery and rotateEvery"); break; }
            s.ConversionNotes.Add("detection rule kept as " + DetectionRuleUtil.ToToken(s.Detection) + " (V1 semantics); set rules.detection to " + DetectionRuleUtil.BeforeAndAfterToken + " to adopt the V2 rule");
            return LevelDefinition.FromSpec(s);
        }
    }

    /// <summary>Serialises levels. Schema 2 is canonical (explicit expanded edge list); the legacy writer exists for byte-faithful V1 round trips.</summary>
    public static class LevelPackWriter
    {
        public static string ToJson(LevelPack pack) => ToJson(pack.Levels, pack.CoordinateSystem);

        /// <summary>Canonical schema-2 pack.</summary>
        public static string ToJson(IReadOnlyList<LevelDefinition> levels, string coordinateSystem = "origin top-left, x right, y down")
        {
            var root = new JsonObject()
                .Set("schemaVersion", (long)LevelDefinition.CurrentSchemaVersion)
                .Set("coordinateSystem", coordinateSystem);
            var list = new List<object>();
            foreach (var l in levels) list.Add(ToJsonObject(l));
            root["levels"] = list;
            return MiniJson.Serialize(root, true) + "\n";
        }

        /// <summary>Single-level schema-2 document (editor export).</summary>
        public static string ToJson(LevelDefinition level) => MiniJson.Serialize(ToJsonObject(level), true) + "\n";

        public static JsonObject ToJsonObject(LevelDefinition l)
        {
            var o = new JsonObject()
                .Set("schemaVersion", (long)LevelDefinition.CurrentSchemaVersion)
                .Set("id", (long)l.Id)
                .Set("name", l.Name)
                .Set("world", (long)l.World)
                .Set("chapter", (long)l.Chapter);
            if (!string.IsNullOrEmpty(l.Theme)) o["theme"] = l.Theme;
            if (l.MigratedFromSchema > 0) o["migratedFromSchema"] = (long)l.MigratedFromSchema;

            var board = new JsonObject()
                .Set("width", (long)l.Width)
                .Set("height", (long)l.Height)
                .Set("nodeSpacing", l.NodeSpacing);
            var nodes = new List<object>();
            foreach (var n in l.Graph.Nodes()) nodes.Add(Coord(n));
            board["nodes"] = nodes;
            board["walls"] = CoordList(l.Walls);
            if (l.Cover.Count > 0)
            {
                var cover = new List<object>();
                foreach (var c in l.Cover) cover.Add(new JsonObject().Set("pos", Coord(c.Position)).Set("traversable", c.Traversable));
                board["cover"] = cover;
            }
            board["edges"] = EdgeList(l.Graph);
            o["board"] = board;

            o["player"] = new JsonObject().Set("start", Coord(l.Start));
            var objective = new JsonObject().Set("type", "reachExit").Set("exit", Coord(l.Exit));
            objective["key"] = l.Key.HasValue ? Coord(l.Key.Value) : null;
            objective["door"] = l.Door.HasValue ? Coord(l.Door.Value) : null;
            o["objective"] = objective;

            var guards = new List<object>();
            for (int i = 0; i < l.Guards.Count; i++)
            {
                var g = l.Guards[i];
                guards.Add(new JsonObject()
                    .Set("id", g.Id ?? ("g" + (i + 1)))
                    .Set("movementMode", GuardModeUtil.ToV2Token(g.Mode))
                    .Set("route", CoordList(g.Route))
                    .Set("facing", DirectionUtil.ToToken(g.Facing))
                    .Set("rotationStep", (long)g.Rotation)
                    .Set("moveEvery", (long)g.MoveEvery)
                    .Set("rotateEvery", (long)g.RotateEvery)
                    .Set("visionRangeNodes", (long)g.Range)
                    .Set("coneAngleDegrees", 90L));
            }
            o["guards"] = guards;
            o["gadgets"] = new JsonObject()
                .Set("smokeCharges", (long)l.SmokeCharges)
                .Set("empCharges", (long)l.EmpCharges)
                .Set("decoyCharges", (long)l.DecoyCharges)
                .Set("smokeTurns", (long)l.SmokeTurns)
                .Set("empTurns", (long)l.EmpTurns);
            if (l.NoWait || l.NoUndo || l.MaxMoves > 0 || !string.IsNullOrEmpty(l.RequiredGadget))
            {
                var r = new JsonObject();
                if (l.MaxMoves > 0) r["maxMoves"] = (long)l.MaxMoves;
                if (!string.IsNullOrEmpty(l.RequiredGadget)) r["requiredGadget"] = l.RequiredGadget;
                if (l.NoWait) r["noWait"] = true;
                if (l.NoUndo) r["noUndo"] = true;
                o["restrictions"] = r;
            }
            o["rules"] = new JsonObject().Set("detection", DetectionRuleUtil.ToToken(l.Detection));
            if (!string.IsNullOrEmpty(l.Hint) || !string.IsNullOrEmpty(l.Theme))
            {
                var pr = new JsonObject();
                if (!string.IsNullOrEmpty(l.Theme)) pr["theme"] = l.Theme;
                if (!string.IsNullOrEmpty(l.Hint)) pr["tutorialHint"] = l.Hint;
                o["presentation"] = pr;
            }
            if (l.ParTurns > 0 || l.StarThresholds.Count > 0)
            {
                var sc = new JsonObject();
                if (l.ParTurns > 0) sc["parTurns"] = (long)l.ParTurns;
                if (l.StarThresholds.Count > 0) { var st = new List<object>(); foreach (var v in l.StarThresholds) st.Add((long)v); sc["starThresholds"] = st; }
                o["scoring"] = sc;
            }
            if (l.ConversionNotes.Count > 0) o["conversionNotes"] = new List<object>(l.ConversionNotes);
            return o;
        }

        /// <summary>Explicit edge list: symmetric pairs merged into one bidirectional entry; directed-only edges exported as oneWay (or locked, bidirectional false).</summary>
        private static List<object> EdgeList(LevelGraph graph)
        {
            var list = new List<object>();
            var done = new HashSet<Edge>();
            foreach (var e in graph.Edges)
            {
                if (done.Contains(e)) continue;
                bool reverse = graph.TryGetEdge(e.To, e.From, out var rt) && rt == e.Type;
                var o = new JsonObject().Set("from", Coord(e.From)).Set("to", Coord(e.To));
                if (e.Type == EdgeType.Locked) { o["type"] = "locked"; o["bidirectional"] = reverse; }
                else if (reverse) { o["type"] = "open"; o["bidirectional"] = true; }
                else { o["type"] = "oneWay"; o["bidirectional"] = false; }
                list.Add(o);
                done.Add(e);
                if (reverse) done.Add(new Edge(e.To, e.From, e.Type));
            }
            return list;
        }

        /// <summary>Legacy schema-1 layout (reference/levels.json shape). Only valid for levels whose topology is a plain grid.</summary>
        public static string ToLegacyJson(IReadOnlyList<LevelDefinition> levels, string coordinateSystem = "origin top-left, x right, y down")
        {
            var root = new JsonObject()
                .Set("format_version", 1L)
                .Set("coordinate_system", coordinateSystem);
            var list = new List<object>();
            foreach (var l in levels) list.Add(ToLegacyJsonObject(l));
            root["levels"] = list;
            return MiniJson.Serialize(root, true) + "\n";
        }

        public static JsonObject ToLegacyJsonObject(LevelDefinition l)
        {
            var o = new JsonObject()
                .Set("id", (long)l.Id)
                .Set("name", l.Name)
                .Set("size", Pair(l.Width, l.Height))
                .Set("start", Coord(l.Start))
                .Set("exit", Coord(l.Exit))
                .Set("walls", CoordList(l.Walls));
            var guards = new List<object>();
            foreach (var g in l.Guards)
            {
                guards.Add(new JsonObject()
                    .Set("mode", GuardModeUtil.ToToken(g.Mode))
                    .Set("route", CoordList(g.Route))
                    .Set("facing", (long)(int)g.Facing)
                    .Set("range", (long)g.Range)
                    .Set("step", (long)g.MoveEvery)
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
        private static List<object> CoordList(IReadOnlyList<GridCoord> coords)
        {
            var list = new List<object>(coords.Count);
            foreach (var c in coords) list.Add(Coord(c));
            return list;
        }
    }
}
