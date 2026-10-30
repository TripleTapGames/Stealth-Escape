using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ShadowSteps.Art3D
{
    // Visual-only DTOs. This is a reading adapter for the supplied V4 JSON.
    // Do NOT use these as a second authoritative gameplay/rules engine.
    [Serializable]
    public class SS3DLevelSet { public List<SS3DLevel> levels; public string version; }

    [Serializable]
    public class SS3DLevel
    {
        public string title, label, group, intro;
        public int id, w, h, smoke, emp, budget, optimalActions;
        public bool ghostAvailable;
        public string[] rows;
        public int[] start, exit, key;
        public SS3DGuard[] guards;
        public SS3DCamera[] cams;
        public SS3DDoor[] doors;
        public string[] cuts;
        public int[][] blockedNodes;

        public bool IsWalkable(int x, int y)
        {
            return rows != null && y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length
                && rows[y][x] != '#' && rows[y][x] != 'X';
        }
        public bool IsSolid(int x, int y)
        {
            return rows != null && y >= 0 && y < rows.Length && x >= 0 && x < rows[y].Length
                && (rows[y][x] == '#' || rows[y][x] == 'X');
        }
        public static VectorCoord Position(int[] v) => new VectorCoord(v[0], v[1]);
    }

    public readonly struct VectorCoord
    {
        public readonly int x, y;
        public VectorCoord(int x, int y) { this.x = x; this.y = y; }
    }

    [Serializable]
    public class SS3DGuard
    {
        public string type, dir;
        public bool ccw;
        public int[] at;
        public int[][] path;
        public int offset;
    }

    [Serializable]
    public class SS3DCamera
    {
        public string type, mountDir, dir;
        public bool ccw;
        public int[] wall;
    }

    [Serializable]
    public class SS3DDoor { public int[] a, b; }

    public static class SS3DV4Reader
    {
        public static SS3DLevelSet Parse(string json)
        {
            var set = JsonConvert.DeserializeObject<SS3DLevelSet>(json);
            if (set == null || set.levels == null || set.levels.Count == 0)
                throw new InvalidOperationException("V4 JSON contains no levels.");
            return set;
        }
    }
}
