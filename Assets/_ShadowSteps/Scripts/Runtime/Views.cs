using System.Collections.Generic;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>Sorting orders used by the board. All on the default sorting layer; order decides stacking.</summary>
    public static class BoardSorting
    {
        public const int Connection = 0;
        public const int Node = 10;
        public const int Vision = 20;
        public const int VisionBorder = 21;
        public const int CameraVision = 22;
        public const int CameraVisionBorder = 23;
        public const int Target = 24;
        public const int Prop = 30;
        public const int Ghost = 35;
        public const int Character = 40;
        public const int Arrow = 41;
        public const int Marker = 42;
        public const int Camera = 44;
        public const int Cloud = 50;
        public const int Badge = 52;
    }

    /// <summary>Small helpers shared by the views so every sprite object is created the same way.</summary>
    public static class ViewUtil
    {
        public static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Color color, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static void SetAlpha(SpriteRenderer sr, float a) { var c = sr.color; c.a = a; sr.color = c; }

        /// <summary>Unit vector in world space for a logical direction (y is flipped: north is +Y on screen).</summary>
        public static Vector3 WorldDir(Direction d) => new Vector3(DirectionUtil.DeltaX(d), -DirectionUtil.DeltaY(d), 0f);
    }

    public enum CellKind { Floor, Wall, Cover }

    /// <summary>One board cell: floor node disc, solid wall block, or blocked cover crate (V4 'X': no node, no connections).</summary>
    public sealed class NodeView : MonoBehaviour
    {
        public GridCoord Coord { get; private set; }
        public CellKind Kind { get; private set; }
        public bool IsWall => Kind != CellKind.Floor;
        private SpriteRenderer _sprite;
        private SpriteRenderer _outline;
        private SpriteRenderer _reachRing;
        private BoardTheme _theme;

        public void Setup(GridCoord coord, CellKind kind, BoardTheme theme, float spacing)
        {
            Coord = coord; Kind = kind; _theme = theme;
            if (_sprite == null)
            {
                _outline = ViewUtil.MakeSprite(transform, "Outline", SpriteFactory.RoundedSquare(), theme.WallOutline, 1f, BoardSorting.Node - 1);
                _sprite = ViewUtil.MakeSprite(transform, "Sprite", SpriteFactory.Circle(), theme.Node, 1f, BoardSorting.Node);
                _reachRing = ViewUtil.MakeSprite(transform, "Reachable", SpriteFactory.Ring(), theme.NodeReachable, 1f, BoardSorting.Node + 1);
                _reachRing.gameObject.SetActive(false);
            }
            _reachRing.transform.localScale = Vector3.one * theme.NodeRadius * 2.4f * spacing;
            if (kind == CellKind.Wall)
            {
                _outline.gameObject.SetActive(true);
                _outline.color = theme.WallOutline;
                _outline.transform.localScale = Vector3.one * theme.WallSize * spacing;
                _sprite.sprite = theme.WallOrDefault; _sprite.color = theme.Wall;
                _sprite.transform.localScale = Vector3.one * (theme.WallSize - 0.08f) * spacing;
            }
            else if (kind == CellKind.Cover)
            {
                // Temporary crate: the logical occluder is the full cell even though the sprite is drawn slightly smaller.
                _outline.gameObject.SetActive(true);
                _outline.color = theme.CoverOutline;
                _outline.transform.localScale = Vector3.one * theme.CoverSize * spacing;
                _sprite.sprite = theme.CoverOrDefault; _sprite.color = theme.Cover;
                _sprite.transform.localScale = Vector3.one * (theme.CoverSize - 0.06f) * spacing;
            }
            else
            {
                _outline.gameObject.SetActive(false);
                _sprite.sprite = theme.NodeOrDefault; _sprite.color = theme.Node;
                _sprite.transform.localScale = Vector3.one * theme.NodeRadius * 2f * spacing;
            }
        }

        /// <summary>Cyan ring = traversable from the player's node right now (reachable, not "safe"). A ring, so it never looks like a token.</summary>
        public void SetReachable(bool reachable) { if (!IsWall) _reachRing.gameObject.SetActive(reachable); }
    }

    /// <summary>A connection between two adjacent floor nodes; door connections carry a padlock that opens.</summary>
    public sealed class ConnectionView : MonoBehaviour
    {
        public GridCoord A { get; private set; }
        public GridCoord B { get; private set; }
        public int DoorIndex { get; private set; } = -1;
        private SpriteRenderer _bar;
        private SpriteRenderer _door;
        private BoardTheme _theme;

        public void Setup(GridCoord a, GridCoord b, int doorIndex, BoardTheme theme, BoardLayout layout)
        {
            A = a; B = b; DoorIndex = doorIndex; _theme = theme;
            var pa = layout.WorldPosition(a); var pb = layout.WorldPosition(b);
            transform.position = (pa + pb) * 0.5f;
            var dir = (pb - pa).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (_bar == null)
            {
                _bar = ViewUtil.MakeSprite(transform, "Bar", SpriteFactory.Pixel(), theme.Connection, 1f, BoardSorting.Connection);
                _door = ViewUtil.MakeSprite(transform, "Door", theme.DoorOrDefault, theme.DoorClosed, 1f, BoardSorting.Prop);
            }
            float length = Vector3.Distance(pa, pb) - theme.NodeRadius * 2f * layout.NodeSpacing * 0.9f;
            _bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _bar.transform.localScale = new Vector3(Mathf.Max(0.05f, length), theme.ConnectionWidth * layout.NodeSpacing, 1f);
            _door.gameObject.SetActive(doorIndex >= 0);
            _door.transform.localScale = Vector3.one * theme.PropSize * 0.8f * layout.NodeSpacing;
            _door.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        public void SetReachable(bool reachable) => _bar.color = reachable ? _theme.ConnectionReachable : _theme.Connection;

        public void SetDoorOpen(bool open)
        {
            if (DoorIndex < 0) return;
            _door.color = open ? _theme.DoorOpen : _theme.DoorClosed;
            ViewUtil.SetAlpha(_door, open ? 0.45f : 1f);
        }
    }

    /// <summary>Exit or key marker on a node.</summary>
    public sealed class PropView : MonoBehaviour
    {
        public enum Kind { Exit, Key }
        public Kind PropKind { get; private set; }
        private SpriteRenderer _sprite;

        public void Setup(Kind kind, BoardTheme theme, float spacing)
        {
            PropKind = kind;
            if (_sprite == null) _sprite = ViewUtil.MakeSprite(transform, "Sprite", SpriteFactory.Circle(), Color.white, 1f, BoardSorting.Prop);
            _sprite.transform.localScale = Vector3.one * theme.PropSize * spacing;
            switch (kind)
            {
                case Kind.Exit: _sprite.sprite = theme.ExitOrDefault; _sprite.color = theme.Exit; _sprite.transform.localScale = Vector3.one * theme.NodeRadius * 2.4f * spacing; break;
                case Kind.Key: _sprite.sprite = theme.KeyOrDefault; _sprite.color = theme.Key; break;
            }
        }

        public void SetCollected(bool collected) => _sprite.gameObject.SetActive(!collected);
    }

    /// <summary>
    /// Player or guard token. Guards show their current facing arrow, a cyan NEXT step marker (moving guards) or a
    /// rotation marker (rotating guards). Purely visual; poses come from <see cref="GuardTrack"/>.
    /// </summary>
    public sealed class CharacterView : MonoBehaviour
    {
        public bool IsPlayer { get; private set; }
        public int GuardIndex { get; private set; } = -1;
        public GridCoord Coord { get; private set; }
        public Direction Facing { get; private set; }

        private SpriteRenderer _body, _outline, _arrow, _next, _nextRing, _rotation, _rotationArrow;
        private Color _baseColor;
        private float _baseAlpha = 1f;
        private float _spacing = 1f;

        public void SetupPlayer(BoardTheme theme, float spacing)
        {
            IsPlayer = true;
            Build(theme, spacing, theme.PlayerOrDefault, theme.Player, false);
            // The player is the one token without a facing arrow: larger, bright body, dark outline so it never blends with path rings.
            _body.transform.localScale = Vector3.one * theme.PlayerSize * spacing;
            _outline.transform.localScale = Vector3.one * theme.PlayerSize * theme.CharacterOutline * spacing;
            _outline.color = theme.PlayerOutline;
        }

        public void SetupGuard(int index, GuardSpec spec, BoardTheme theme, float spacing, bool ghost)
        {
            IsPlayer = false; GuardIndex = index;
            Build(theme, spacing, theme.GuardOrDefault, theme.GuardColor(spec.Kind), true);
            _rotation.gameObject.SetActive(!ghost && spec.Kind == GuardKind.Rotating);
            _rotationArrow.gameObject.SetActive(!ghost && spec.Kind == GuardKind.Rotating);
            _rotationArrow.flipX = !spec.Clockwise;
            if (ghost)
            {
                _baseAlpha = theme.GhostAlpha;
                ViewUtil.SetAlpha(_body, _baseAlpha); ViewUtil.SetAlpha(_arrow, _baseAlpha);
                _outline.gameObject.SetActive(false);
                _body.sortingOrder = BoardSorting.Ghost; _arrow.sortingOrder = BoardSorting.Ghost + 1;
            }
        }

        private void Build(BoardTheme theme, float spacing, Sprite body, Color color, bool arrow)
        {
            _baseColor = color; _spacing = spacing;
            if (_body == null)
            {
                _outline = ViewUtil.MakeSprite(transform, "Outline", body, theme.GuardOutline, 1f, BoardSorting.Character - 1);
                _body = ViewUtil.MakeSprite(transform, "Body", body, color, 1f, BoardSorting.Character);
                _arrow = ViewUtil.MakeSprite(transform, "Facing", theme.ArrowOrDefault, theme.FacingArrow, 1f, BoardSorting.Arrow);
                _nextRing = ViewUtil.MakeSprite(transform, "NextRing", SpriteFactory.Circle(), theme.NextMarker, 1f, BoardSorting.Marker);
                _next = ViewUtil.MakeSprite(_nextRing.transform, "NextArrow", SpriteFactory.Arrow(), Color.white, 0.7f, BoardSorting.Marker + 1);
                _rotation = ViewUtil.MakeSprite(transform, "RotationRing", SpriteFactory.Ring(), theme.RotationMarker, 1f, BoardSorting.Marker);
                _rotationArrow = ViewUtil.MakeSprite(_rotation.transform, "RotationArrow", SpriteFactory.Arrow(), theme.RotationMarker, 0.55f, BoardSorting.Marker + 1);
                _rotationArrow.transform.localPosition = new Vector3(0.45f, 0.45f, 0f);
                _rotationArrow.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }
            _body.sprite = body; _body.color = color;
            _body.transform.localScale = Vector3.one * theme.CharacterSize * spacing;
            _outline.sprite = body; _outline.color = theme.GuardOutline;
            _outline.transform.localScale = Vector3.one * theme.CharacterSize * theme.CharacterOutline * spacing;
            _arrow.gameObject.SetActive(arrow);
            _arrow.transform.localScale = Vector3.one * theme.FacingArrowSize * spacing;
            _nextRing.gameObject.SetActive(false);
            _nextRing.transform.localScale = Vector3.one * theme.MarkerSize * spacing;
            _rotation.gameObject.SetActive(false);
            _rotation.transform.localScale = Vector3.one * theme.MarkerSize * spacing;
            _rotation.transform.localPosition = new Vector3(-0.30f * spacing, 0.30f * spacing, 0f);
            _rotationArrow.gameObject.SetActive(false);
        }

        public void SetCoord(GridCoord c, BoardLayout layout) { Coord = c; transform.position = layout.WorldPosition(c); }

        public void SetFacing(Direction d)
        {
            Facing = d;
            _arrow.transform.localRotation = BoardLayout.RotationFor(d);
            _arrow.transform.localPosition = ViewUtil.WorldDir(d) * (_body.transform.localScale.x * 0.5f);
        }

        /// <summary>Cyan NEXT marker: the guard's actual next step, offset to the top-right so the facing arrow stays visible.</summary>
        public void SetNextStep(Direction? next)
        {
            _nextRing.gameObject.SetActive(next.HasValue);
            if (!next.HasValue) return;
            _nextRing.transform.localPosition = new Vector3(0.30f * _spacing, 0.30f * _spacing, 0f);
            _next.transform.localRotation = BoardLayout.RotationFor(next.Value);
        }

        public void SetTint(Color? tint) { _body.color = tint ?? _baseColor; ViewUtil.SetAlpha(_body, _baseAlpha); }

        public Transform ArrowTransform => _arrow.transform;
        public SpriteRenderer Body => _body;
    }

    /// <summary>
    /// Wall-mounted camera: bracket on the wall face, lens arrow for the current facing, rotation/sweep marker,
    /// disabled badge with remaining protected actions, highlight while EMP targeting.
    /// </summary>
    public sealed class CameraView : MonoBehaviour
    {
        public int CameraIndex { get; private set; }
        public CameraSpec Spec { get; private set; }
        public Direction Facing { get; private set; }
        private SpriteRenderer _body, _lens, _marker, _badge, _highlight;
        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();
        private BoardTheme _theme;
        private float _spacing;

        public void Setup(int index, CameraSpec spec, BoardTheme theme, BoardLayout layout)
        {
            CameraIndex = index; Spec = spec; _theme = theme; _spacing = layout.NodeSpacing;
            transform.position = layout.WorldPosition(spec.WallCell) + ViewUtil.WorldDir(spec.MountFace) * (0.5f * _spacing);
            if (_body == null)
            {
                _highlight = ViewUtil.MakeSprite(transform, "Highlight", SpriteFactory.Circle(), theme.CameraHighlight, 1f, BoardSorting.Camera - 1);
                _body = ViewUtil.MakeSprite(transform, "Body", theme.CameraOrDefault, theme.CameraBody, 1f, BoardSorting.Camera);
                _lens = ViewUtil.MakeSprite(transform, "Lens", theme.ArrowOrDefault, theme.CameraLens, 1f, BoardSorting.Camera + 1);
                _marker = ViewUtil.MakeSprite(transform, "Marker", SpriteFactory.Ring(), theme.RotationMarker, 1f, BoardSorting.Camera + 1);
                _badge = ViewUtil.MakeSprite(transform, "Disabled", SpriteFactory.Cross(), theme.CameraDisabled, 1f, BoardSorting.Badge);
                for (int i = 0; i < 2; i++) _pips.Add(ViewUtil.MakeSprite(transform, "Pip" + i, SpriteFactory.Circle(), theme.CameraDisabled, 1f, BoardSorting.Badge));
            }
            _body.transform.localScale = Vector3.one * theme.CameraBodySize * _spacing;
            _highlight.transform.localScale = Vector3.one * theme.CameraBodySize * 1.9f * _spacing;
            _highlight.gameObject.SetActive(false);
            _lens.transform.localScale = Vector3.one * theme.FacingArrowSize * _spacing;
            _marker.transform.localScale = Vector3.one * theme.MarkerSize * 0.8f * _spacing;
            _marker.transform.localPosition = new Vector3(-0.22f * _spacing, 0.22f * _spacing, 0f);
            _marker.gameObject.SetActive(spec.Kind != CameraKind.Fixed);
            _marker.color = spec.Kind == CameraKind.Sweeping ? theme.NextMarker : theme.RotationMarker;
            _badge.transform.localScale = Vector3.one * theme.MarkerSize * 1.2f * _spacing;
            for (int i = 0; i < _pips.Count; i++) { _pips[i].transform.localScale = Vector3.one * 0.07f * _spacing; _pips[i].transform.localPosition = new Vector3((i - 0.5f) * 0.14f * _spacing, -0.26f * _spacing, 0f); }
            SetDisabled(0);
            SetFacing(spec.Facing);
        }

        public void SetFacing(Direction d)
        {
            Facing = d;
            _lens.transform.localRotation = BoardLayout.RotationFor(d);
            _lens.transform.localPosition = ViewUtil.WorldDir(d) * (_body.transform.localScale.x * 0.55f);
        }

        /// <summary>0 = active; otherwise the remaining protected actions shown as pips.</summary>
        public void SetDisabled(int actionsLeft)
        {
            _badge.gameObject.SetActive(actionsLeft > 0);
            for (int i = 0; i < _pips.Count; i++) _pips[i].gameObject.SetActive(i < actionsLeft);
            _lens.color = actionsLeft > 0 ? _theme.CameraDisabled : _theme.CameraLens;
        }

        public void SetHighlighted(bool on) => _highlight.gameObject.SetActive(on);
        public Transform LensTransform => _lens.transform;
    }

    /// <summary>A stationary smoke cloud with pips for its remaining protected actions.</summary>
    public sealed class CloudView : MonoBehaviour
    {
        public GridCoord Coord { get; private set; }
        private SpriteRenderer _cloud;
        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();

        public void Setup(BoardTheme theme, float spacing)
        {
            if (_cloud == null)
            {
                _cloud = ViewUtil.MakeSprite(transform, "Cloud", theme.CloudOrDefault, theme.SmokeCloud, 1f, BoardSorting.Cloud);
                for (int i = 0; i < 2; i++) _pips.Add(ViewUtil.MakeSprite(transform, "Pip" + i, SpriteFactory.Circle(), Color.white, 1f, BoardSorting.Badge));
            }
            _cloud.transform.localScale = Vector3.one * theme.CloudSize * spacing;
            for (int i = 0; i < _pips.Count; i++) { _pips[i].transform.localScale = Vector3.one * 0.08f * spacing; _pips[i].transform.localPosition = new Vector3((i - 0.5f) * 0.16f * spacing, -0.30f * spacing, 0f); }
        }

        public void Show(GridCoord c, int actionsLeft, BoardLayout layout)
        {
            Coord = c;
            transform.position = layout.WorldPosition(c);
            gameObject.SetActive(true);
            for (int i = 0; i < _pips.Count; i++) _pips[i].gameObject.SetActive(i < actionsLeft);
        }
    }

    /// <summary>Pooled cells (fill + border) showing exactly the nodes a detector reports. Border and fill colours are set per Show call.</summary>
    public sealed class VisionOverlay : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _fills = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _borders = new List<SpriteRenderer>();
        private readonly List<GridCoord> _shown = new List<GridCoord>();
        private BoardTheme _theme;
        private BoardLayout _layout;
        private int _order;
        private float _scale = 0.96f;

        public IReadOnlyList<GridCoord> ShownCells => _shown;

        public void Setup(BoardTheme theme, BoardLayout layout, int sortingOrder, float scale)
        {
            _theme = theme; _layout = layout; _order = sortingOrder; _scale = scale;
            Clear();
        }

        public void Show(IEnumerable<GridCoord> cells, Color fill, Color border)
        {
            Clear();
            foreach (var c in cells)
            {
                int i = _shown.Count;
                if (i >= _fills.Count)
                {
                    _fills.Add(ViewUtil.MakeSprite(transform, "Fill", _theme.VisionCellOrDefault, fill, 1f, _order));
                    _borders.Add(ViewUtil.MakeSprite(transform, "Border", SpriteFactory.Ring(), border, 1f, _order + 1));
                }
                var f = _fills[i]; var b = _borders[i];
                f.color = fill; b.color = border;
                f.transform.position = _layout.WorldPosition(c, 0f); b.transform.position = f.transform.position;
                f.transform.localScale = Vector3.one * _layout.NodeSpacing * _scale;
                b.transform.localScale = Vector3.one * _layout.NodeSpacing * _scale;
                f.gameObject.SetActive(true); b.gameObject.SetActive(true);
                _shown.Add(c);
            }
        }

        public void Clear()
        {
            foreach (var sr in _fills) sr.gameObject.SetActive(false);
            foreach (var sr in _borders) sr.gameObject.SetActive(false);
            _shown.Clear();
        }
    }
}
