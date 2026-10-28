using System.Collections.Generic;
using ShadowSteps.Core;
using UnityEngine;

namespace ShadowSteps.Runtime
{
    /// <summary>Sorting layers used by the board. All on the default sorting layer; order decides stacking.</summary>
    public static class BoardSorting
    {
        public const int Edge = 0;
        public const int Node = 10;
        public const int Cone = 20;
        public const int RouteDot = 25;
        public const int Prop = 30;
        public const int Ghost = 35;
        public const int Character = 40;
        public const int Arrow = 41;
        public const int Overlay = 50;
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

        public static void SetAlpha(SpriteRenderer sr, float a)
        {
            var c = sr.color; c.a = a; sr.color = c;
        }
    }

    /// <summary>One existing node: floor disc, wall block or cover marker.</summary>
    public sealed class NodeView : MonoBehaviour
    {
        public GridCoord Coord { get; private set; }
        public NodeInfo Info { get; private set; }
        private SpriteRenderer _sprite;
        private SpriteRenderer _outline;

        public void Setup(GridCoord coord, NodeInfo info, BoardTheme theme, float spacing)
        {
            Coord = coord;
            Info = info;
            if (_sprite == null)
            {
                _outline = ViewUtil.MakeSprite(transform, "Outline", SpriteFactory.RoundedSquare(), theme.WallOutline, 1f, BoardSorting.Node - 1);
                _sprite = ViewUtil.MakeSprite(transform, "Sprite", SpriteFactory.Circle(), theme.Node, 1f, BoardSorting.Node);
            }
            if (!info.Traversable)
            {
                _outline.gameObject.SetActive(true);
                _outline.transform.localScale = Vector3.one * theme.WallSize * spacing;
                _sprite.sprite = info.BlocksSight && Info.Equals(NodeInfo.Wall) ? theme.WallOrDefault : theme.CoverOrDefault;
                _sprite.color = theme.Wall;
                _sprite.transform.localScale = Vector3.one * (theme.WallSize - 0.08f) * spacing;
            }
            else if (info.BlocksSight)
            {
                _outline.gameObject.SetActive(false);
                _sprite.sprite = theme.CoverOrDefault;
                _sprite.color = theme.NodeCover;
                _sprite.transform.localScale = Vector3.one * theme.CoverSize * spacing;
            }
            else
            {
                _outline.gameObject.SetActive(false);
                _sprite.sprite = theme.NodeOrDefault;
                _sprite.color = theme.Node;
                _sprite.transform.localScale = Vector3.one * theme.NodeRadius * 2f * spacing;
            }
        }
    }

    /// <summary>A traversable connection drawn between two node centres; one-way edges get a chevron, locked edges a padlock tint.</summary>
    public sealed class EdgeView : MonoBehaviour
    {
        public Edge Edge { get; private set; }
        public bool Bidirectional { get; private set; }
        private SpriteRenderer _bar;
        private SpriteRenderer _marker;

        public void Setup(Edge edge, bool bidirectional, BoardTheme theme, BoardLayout layout)
        {
            Edge = edge;
            Bidirectional = bidirectional;
            var a = layout.WorldPosition(edge.From);
            var b = layout.WorldPosition(edge.To);
            transform.position = (a + b) * 0.5f;
            var dir = (b - a).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (_bar == null)
            {
                _bar = ViewUtil.MakeSprite(transform, "Bar", SpriteFactory.Pixel(), theme.Edge, 1f, BoardSorting.Edge);
                _marker = ViewUtil.MakeSprite(transform, "Marker", SpriteFactory.Chevron(), theme.EdgeOneWay, 1f, BoardSorting.Edge + 1);
            }
            float length = Vector3.Distance(a, b) - theme.NodeRadius * 2f * layout.NodeSpacing * 0.9f;
            _bar.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _bar.transform.localScale = new Vector3(Mathf.Max(0.05f, length), theme.EdgeWidth * layout.NodeSpacing, 1f);
            _bar.color = edge.Type == EdgeType.Locked ? theme.EdgeLocked : bidirectional ? theme.Edge : theme.EdgeOneWay;
            _marker.gameObject.SetActive(!bidirectional);
            _marker.transform.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            _marker.transform.localScale = Vector3.one * theme.EdgeWidth * 3f * layout.NodeSpacing;
            _marker.color = theme.EdgeOneWay;
        }

        public void SetLockedVisual(bool locked, BoardTheme theme)
        {
            if (Edge.Type != EdgeType.Locked) return;
            _bar.color = locked ? theme.EdgeLocked : theme.DoorOpen;
        }
    }

    /// <summary>Exit, key or door marker on a node.</summary>
    public sealed class PropView : MonoBehaviour
    {
        public enum Kind { Exit, Key, Door }
        public Kind PropKind { get; private set; }
        private SpriteRenderer _sprite;
        private BoardTheme _theme;

        public void Setup(Kind kind, BoardTheme theme, float spacing)
        {
            PropKind = kind;
            _theme = theme;
            if (_sprite == null) _sprite = ViewUtil.MakeSprite(transform, "Sprite", SpriteFactory.Circle(), Color.white, 1f, BoardSorting.Prop);
            _sprite.transform.localScale = Vector3.one * theme.PropSize * spacing;
            switch (kind)
            {
                case Kind.Exit: _sprite.sprite = theme.ExitOrDefault; _sprite.color = theme.Exit; _sprite.transform.localScale = Vector3.one * theme.NodeRadius * 2.4f * spacing; break;
                case Kind.Key: _sprite.sprite = theme.KeyOrDefault; _sprite.color = theme.Key; break;
                case Kind.Door: _sprite.sprite = theme.DoorOrDefault; _sprite.color = theme.DoorLocked; break;
            }
        }

        public void SetCollected(bool collected) => _sprite.gameObject.SetActive(!collected);

        public void SetDoorOpen(bool open)
        {
            _sprite.color = open ? _theme.DoorOpen : _theme.DoorLocked;
            ViewUtil.SetAlpha(_sprite, open ? 0.45f : 1f);
        }
    }

    /// <summary>Player or guard token: body, facing arrow, optional status badges (frozen, concealed). Purely visual.</summary>
    public sealed class CharacterView : MonoBehaviour
    {
        public bool IsPlayer { get; private set; }
        public int GuardIndex { get; private set; } = -1;
        public GridCoord Coord { get; private set; }
        public Direction Facing { get; private set; }

        private SpriteRenderer _body;
        private SpriteRenderer _arrow;
        private SpriteRenderer _badge;
        private SpriteRenderer _smoke;
        private Color _baseColor;
        private float _baseAlpha = 1f;

        public void SetupPlayer(BoardTheme theme, float spacing)
        {
            IsPlayer = true;
            Build(theme, spacing, theme.PlayerOrDefault, theme.Player, false);
        }

        public void SetupGuard(int index, GuardDefinition def, BoardTheme theme, float spacing, bool ghost)
        {
            IsPlayer = false;
            GuardIndex = index;
            Build(theme, spacing, theme.GuardOrDefault, theme.GuardColor(def.Mode), true);
            if (ghost)
            {
                _baseAlpha = theme.GhostAlpha;
                ViewUtil.SetAlpha(_body, _baseAlpha);
                ViewUtil.SetAlpha(_arrow, _baseAlpha);
                _body.sortingOrder = BoardSorting.Ghost;
                _arrow.sortingOrder = BoardSorting.Ghost + 1;
            }
        }

        private void Build(BoardTheme theme, float spacing, Sprite body, Color color, bool arrow)
        {
            _baseColor = color;
            if (_body == null)
            {
                _body = ViewUtil.MakeSprite(transform, "Body", body, color, 1f, BoardSorting.Character);
                _arrow = ViewUtil.MakeSprite(transform, "Facing", theme.ArrowOrDefault, theme.FacingArrow, 1f, BoardSorting.Arrow);
                _badge = ViewUtil.MakeSprite(transform, "Badge", SpriteFactory.Cross(), theme.EmpPulse, 1f, BoardSorting.Arrow);
                _smoke = ViewUtil.MakeSprite(transform, "Smoke", SpriteFactory.Circle(), theme.Smoke, 1f, BoardSorting.Overlay);
            }
            _body.sprite = body;
            _body.color = color;
            _body.transform.localScale = Vector3.one * theme.CharacterSize * spacing;
            _arrow.gameObject.SetActive(arrow);
            _arrow.transform.localScale = Vector3.one * theme.FacingArrowSize * spacing;
            _badge.gameObject.SetActive(false);
            _badge.transform.localScale = Vector3.one * theme.FacingArrowSize * spacing;
            _badge.transform.localPosition = new Vector3(0.28f * spacing, 0.28f * spacing, 0f);
            _smoke.gameObject.SetActive(false);
            _smoke.transform.localScale = Vector3.one * spacing * 1.1f;
        }

        public void SetCoord(GridCoord c, BoardLayout layout)
        {
            Coord = c;
            transform.position = layout.WorldPosition(c);
        }

        public void SetFacing(Direction d)
        {
            Facing = d;
            _arrow.transform.localRotation = BoardLayout.RotationFor(d);
            // Arrow sits on the body edge in the facing direction.
            var offset = new Vector3(DirectionUtil.DeltaX(d), -DirectionUtil.DeltaY(d), 0f) * (_body.transform.localScale.x * 0.5f);
            _arrow.transform.localPosition = offset;
        }

        public void SetFrozen(bool frozen) => _badge.gameObject.SetActive(frozen);
        public void SetConcealed(bool concealed) => _smoke.gameObject.SetActive(concealed);

        public void SetTint(Color? tint)
        {
            _body.color = tint ?? _baseColor;
            ViewUtil.SetAlpha(_body, _baseAlpha);
        }

        public Transform ArrowTransform => _arrow.transform;
        public SpriteRenderer Body => _body;
    }

    /// <summary>Pooled translucent cells showing exactly the nodes <see cref="VisionSystem"/> reports as seen.</summary>
    public sealed class VisionOverlay : MonoBehaviour
    {
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private readonly List<GridCoord> _shown = new List<GridCoord>();
        private BoardTheme _theme;
        private BoardLayout _layout;

        public IReadOnlyList<GridCoord> ShownCells => _shown;

        public void Setup(BoardTheme theme, BoardLayout layout)
        {
            _theme = theme;
            _layout = layout;
            Clear();
        }

        private Sprite _sprite;

        /// <summary>Changes the cell glyph (e.g. hatched cross for high-contrast mode). Applies to subsequent Show calls.</summary>
        public void SetSprite(Sprite sprite)
        {
            _sprite = sprite;
            foreach (var sr in _pool) sr.sprite = sprite;
        }

        public void Show(IEnumerable<GridCoord> cells, Color color)
        {
            Clear();
            foreach (var c in cells)
            {
                var sr = Acquire();
                sr.color = color;
                sr.transform.position = _layout.WorldPosition(c, 0f);
                sr.transform.localScale = Vector3.one * _layout.NodeSpacing * 0.96f;
                sr.gameObject.SetActive(true);
                _shown.Add(c);
            }
        }

        public void Clear()
        {
            foreach (var sr in _pool) sr.gameObject.SetActive(false);
            _shown.Clear();
        }

        private SpriteRenderer Acquire()
        {
            foreach (var sr in _pool) if (!sr.gameObject.activeSelf) return sr;
            var n = ViewUtil.MakeSprite(transform, "Cell", _sprite != null ? _sprite : _theme.ConeCellOrDefault, _theme.Cone, 1f, BoardSorting.Cone);
            _pool.Add(n);
            return n;
        }
    }
}
