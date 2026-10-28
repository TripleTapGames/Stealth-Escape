using System;
using System.Collections.Generic;
using System.IO;
using ShadowSteps.Core;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    /// <summary>Locates project files without UnityEngine so the same tests run under Unity and under a plain dotnet runner.</summary>
    public static class TestPaths
    {
        public const string LevelsRelative = "Assets/StreamingAssets/ShadowSteps/levels.json";
        public const string ValidationRelative = "reference/validation.json";

        private static string _root;

        public static string ProjectRoot
        {
            get
            {
                if (_root != null) return _root;
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, LevelsRelative))) { _root = dir.FullName; return _root; }
                    dir = dir.Parent;
                }
                var env = Environment.GetEnvironmentVariable("SHADOWSTEPS_PROJECT_ROOT");
                if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, LevelsRelative))) { _root = env; return _root; }
                throw new FileNotFoundException("Could not locate project root containing " + LevelsRelative + " starting from " + Directory.GetCurrentDirectory());
            }
        }

        public static string LevelsJsonPath => Path.Combine(ProjectRoot, LevelsRelative);
        public static string ValidationJsonPath => Path.Combine(ProjectRoot, ValidationRelative);

        private static LevelPack _pack;
        public static LevelPack LoadPack()
        {
            if (_pack == null) _pack = LevelPackLoader.ParseAndValidate(File.ReadAllText(LevelsJsonPath), out _);
            return _pack;
        }

        public static LevelDefinition Level(int id)
        {
            var l = LoadPack().FindById(id);
            if (l == null) throw new InvalidOperationException("No level with id " + id);
            return l;
        }

        public sealed class ReferenceSolution
        {
            public int Level;
            public string Name;
            public int Moves;
            public List<GameAction> Actions;
            public int StatesChecked;
            public int Period;
        }

        private static List<ReferenceSolution> _solutions;
        public static List<ReferenceSolution> LoadReferenceSolutions()
        {
            if (_solutions != null) return _solutions;
            var arr = JsonAccess.AsArray(MiniJson.Parse(File.ReadAllText(ValidationJsonPath)), "validation");
            var list = new List<ReferenceSolution>();
            for (int i = 0; i < arr.Count; i++)
            {
                var o = JsonAccess.AsObject(arr[i], "validation[" + i + "]");
                var tokens = new List<string>();
                foreach (var t in JsonAccess.AsArray(o["actions"], "actions")) tokens.Add((string)t);
                list.Add(new ReferenceSolution
                {
                    Level = JsonAccess.AsInt(o["level"], "level"),
                    Name = (string)o["name"],
                    Moves = JsonAccess.AsInt(o["moves"], "moves"),
                    Actions = GameActionUtil.ParseSequence(tokens),
                    StatesChecked = JsonAccess.AsInt(o["states_checked"], "states_checked"),
                    Period = JsonAccess.AsInt(o["period"], "period")
                });
            }
            _solutions = list;
            return list;
        }
    }

    /// <summary>Small helpers to build synthetic levels for rule tests.</summary>
    public static class LevelBuilder
    {
        public static LevelDefinition Make(int w, int h, GridCoord start, GridCoord exit, GridCoord[] walls = null,
            GuardDefinition[] guards = null, int smoke = 0, int emp = 0, GridCoord? key = null, GridCoord? door = null, int id = 999)
        {
            return new LevelDefinition(id, "Synthetic " + id, w, h, start, exit, walls ?? new GridCoord[0], guards ?? new GuardDefinition[0], smoke, emp, key, door);
        }

        public static GuardDefinition Guard(GuardMode mode, Direction facing, int range, params GridCoord[] route) =>
            new GuardDefinition(mode, route, facing, range, 1, 1);

        public static GuardDefinition Guard(GuardMode mode, Direction facing, int range, int step, int rotation, params GridCoord[] route) =>
            new GuardDefinition(mode, route, facing, range, step, rotation);

        public static GridCoord C(int x, int y) => new GridCoord(x, y);
    }
}
