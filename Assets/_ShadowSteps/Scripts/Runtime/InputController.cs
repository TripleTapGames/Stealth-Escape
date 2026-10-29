using System;
using ShadowSteps.Core;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Single gesture recogniser for the board (handover §8): a tap on an adjacent connected node = one Move; a
    /// cardinal swipe anywhere on the board = one Move; a long-press on an adjacent node = one-action preview that never
    /// moves on release; in Smoke/EMP targeting mode taps pick targets and swipes are ignored. Input over UI is ignored.
    /// Legacy Input Manager (project setting). Keys: arrows/WASD move, Space WAIT, Q smoke mode, E EMP mode, Esc cancel,
    /// R retry, Z debug undo.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        public GameSessionController Session;
        public Camera BoardCamera;
        [Tooltip("Swipe length that counts as a move, in inches (DPI independent).")]
        public float SwipeInches = 0.16f;
        [Tooltip("Max tap travel in inches.")]
        public float TapInches = 0.08f;
        [Tooltip("Hold time for the one-action preview, seconds.")]
        public float LongPressSeconds = 0.38f;
        public bool KeyboardEnabled = true;

        public event Action RetryRequested;
        public event Action UndoRequested;

        private bool _pointerDown, _gestureConsumed, _pointerStartedOverUi, _previewShown;
        private Vector2 _pointerStart;
        private float _pointerDownTime;
        private float _ignoreUntil;

        private float Dpi => Screen.dpi > 0 ? Screen.dpi : 160f;

        private void OnApplicationPause(bool paused) { if (!paused) ResetGesture(); }
        private void OnApplicationFocus(bool focus) { if (focus) ResetGesture(); }

        private void ResetGesture()
        {
            _ignoreUntil = Time.unscaledTime + 0.35f;
            _pointerDown = false; _gestureConsumed = false; _previewShown = false;
            Session?.HidePreview();
        }

        private void Update()
        {
            if (Session == null) return;
            if (Time.unscaledTime < _ignoreUntil) return;
            HandlePointer();
            if (KeyboardEnabled) HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(Direction.North);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Move(Direction.East);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(Direction.South);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Move(Direction.West);
            else if (Input.GetKeyDown(KeyCode.Space)) { if (Session.Mode == InputMode.Move) Session.TrySubmit(StageAction.Wait); }
            else if (Input.GetKeyDown(KeyCode.Q)) Session.ToggleMode(InputMode.SmokeTarget);
            else if (Input.GetKeyDown(KeyCode.E)) Session.ToggleMode(InputMode.EmpTarget);
            else if (Input.GetKeyDown(KeyCode.Escape)) Session.SetMode(InputMode.Move);
            else if (Input.GetKeyDown(KeyCode.Z)) UndoRequested?.Invoke();
            else if (Input.GetKeyDown(KeyCode.R)) RetryRequested?.Invoke();
        }

        private void Move(Direction d)
        {
            if (Session.Mode != InputMode.Move) return; // gadget modes never move the player
            Session.TrySubmit(StageAction.Move(d));
        }

        private void HandlePointer()
        {
            bool down, up, held;
            Vector2 pos;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !up;
            }
            else
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                up = Input.GetMouseButtonUp(0);
                held = Input.GetMouseButton(0);
            }

            if (down)
            {
                _pointerDown = true; _gestureConsumed = false; _previewShown = false;
                _pointerStart = pos; _pointerDownTime = Time.unscaledTime;
                _pointerStartedOverUi = IsOverUi();
                return;
            }
            if (!_pointerDown) return;

            var delta = pos - _pointerStart;
            float swipePx = SwipeInches * Dpi, tapPx = TapInches * Dpi;

            if (held && !_gestureConsumed && !_pointerStartedOverUi && !_previewShown && delta.magnitude >= swipePx)
            {
                _gestureConsumed = true;
                if (Session.Mode == InputMode.Move) Move(DirectionFor(delta));
                return;
            }

            if (held && !_gestureConsumed && !_previewShown && !_pointerStartedOverUi && Session.Mode == InputMode.Move
                && delta.magnitude <= tapPx && Time.unscaledTime - _pointerDownTime >= LongPressSeconds)
            {
                var adjacent = AdjacentDirectionAt(_pointerStart);
                if (adjacent.HasValue) { _previewShown = true; Session.ShowPreview(StageAction.Move(adjacent.Value)); }
            }

            if (up)
            {
                _pointerDown = false;
                if (_previewShown) { Session.HidePreview(); _previewShown = false; return; } // a completed preview never moves
                if (!_gestureConsumed && !_pointerStartedOverUi && delta.magnitude <= tapPx) HandleTap(pos);
                _gestureConsumed = false;
            }
        }

        private Vector3 WorldAt(Vector2 screenPos) => BoardCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -BoardCamera.transform.position.z));

        private Direction? AdjacentDirectionAt(Vector2 screenPos)
        {
            if (BoardCamera == null || Session.Board == null || !Session.Board.IsBuilt || Session.State == null) return null;
            var coord = Session.Board.Layout.NearestCoord(WorldAt(screenPos));
            var player = Session.State.Player;
            return DirectionUtil.FromDelta(coord.X - player.X, coord.Y - player.Y);
        }

        private void HandleTap(Vector2 screenPos)
        {
            if (BoardCamera == null || Session.Board == null || !Session.Board.IsBuilt || Session.State == null) return;
            var world = WorldAt(screenPos);
            if (Session.Mode == InputMode.EmpTarget)
            {
                int cam = Session.Board.CameraIndexNear(world);
                if (cam >= 0) Session.TapCamera(cam);
                return; // taps elsewhere keep the targeting mode
            }
            var coord = Session.Board.Layout.NearestCoord(world);
            if (!Session.Stage.Inside(coord)) return;
            Session.TapNode(coord);
        }

        private static Direction DirectionFor(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x > 0 ? Direction.East : Direction.West;
            return delta.y > 0 ? Direction.North : Direction.South;
        }

        private static bool IsOverUi()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
