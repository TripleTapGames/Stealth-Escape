using System;
using System.Collections.Generic;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Art2D
{
    public enum SS2DObstacleArt { Default, BushPlanter, StoneWall, Crate }
    /// <summary>Logical axis a stone-wall piece runs along (before the portrait rotation): X = along a row, Y = along a column.</summary>
    public enum SS2DWallAxis { LogicalX, LogicalY }
    public enum SS2DCameraMountArt { Auto, StoneWall, Pole, Plate }

    [Serializable]
    public sealed class SS2DObstacleArtEntry
    {
        [Tooltip("Logical node of the authored X cell (stable id: x, y in the level rows).")] public int x, y;
        public SS2DObstacleArt art = SS2DObstacleArt.BushPlanter;
        [Tooltip("Stone wall only: logical axis the piece runs along. The renderer picks the side or front sprite from the drawn axis after rotation.")] public SS2DWallAxis wallAxis = SS2DWallAxis.LogicalX;
        [Tooltip("Decorative offset in node spacings (logical axes, rotated with the board).")] public Vector2 offset;
        [Tooltip("Added to the sprite's sorting order.")] public int sortingOffset;
        [Tooltip("Scale multiplier on the library size.")] public float scale = 1f;
    }

    [Serializable]
    public sealed class SS2DCameraArtEntry
    {
        [Tooltip("Camera id from the level data (stable).")] public string cameraId = "cam1";
        public SS2DCameraMountArt mount = SS2DCameraMountArt.Auto;
        [Tooltip("Decorative offset of the mount piece in node spacings (logical axes).")] public Vector2 offset;
        public int sortingOffset;
    }

    /// <summary>
    /// Per-level decoration choices for the 2D presentation: which authored X cell is a bush planter or a stone wall
    /// (and which way the wall runs), and how each camera is mounted. Pure presentation — the gameplay graph, blocked
    /// cells, camera cells and facings come from the V4 level data and are never read from here. Keyed by stable
    /// logical node coordinates and camera ids, so it survives the portrait rotation (the renderer rotates it).
    /// </summary>
    [CreateAssetMenu(menuName = "Shadow Steps/2D Level Art Layout", fileName = "SS2DArt_LEVEL")]
    public sealed class SS2DLevelArtLayout : ScriptableObject
    {
        public string stageCode = "LEVEL-19";
        [Tooltip("Art for authored X cells not listed below.")] public SS2DObstacleArt defaultObstacle = SS2DObstacleArt.BushPlanter;
        public List<SS2DObstacleArtEntry> obstacles = new List<SS2DObstacleArtEntry>();
        public List<SS2DCameraArtEntry> cameras = new List<SS2DCameraArtEntry>();

        public SS2DObstacleArtEntry Find(GridCoord cell) { foreach (var o in obstacles) if (o.x == cell.X && o.y == cell.Y) return o; return null; }
        public SS2DCameraArtEntry FindCamera(string id) { foreach (var c in cameras) if (c.cameraId == id) return c; return null; }
        public SS2DObstacleArt ArtFor(GridCoord cell) { var e = Find(cell); return e != null && e.art != SS2DObstacleArt.Default ? e.art : defaultObstacle; }
    }
}
