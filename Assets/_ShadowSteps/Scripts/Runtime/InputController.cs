using System;
using ShadowSteps.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Turns touch/mouse swipes, taps on adjacent nodes and desktop keys into <see cref="GameAction"/> requests.
    /// One gesture produces at most one action; input over UI elements is ignored. Uses the legacy Input Manager
    /// (the project setting), so no Input System package is required.
    /// Keys: arrows/WASD move, Space = WAIT, Q = SMOKE, E = EMP, Z = UNDO, R = RESTART.
    /// </summary>
    public sealed class InputController : MonoBehaviour
    {
        public GameSessionController Session;
        public Camera BoardCamera;
        [Tooltip("Swipe length that counts as a move, in inches (DPI independent).")]
        public float SwipeInches = 0.18f;
        [Tooltip("Max tap travel in inches.")]
        public float TapInches = 0.08f;
        public bool KeyboardEnabled = true;

        public event Action<GameAction> ActionRequested;
        public event Action UndoRequested;
        public event Action RestartRequested;

        private bool _pointerDown;
        private bool _gestureConsumed;
        private bool _pointerStartedOverUi;
        private Vector2 _pointerStart;

        private float Dpi => Screen.dpi > 0 ? Screen.dpi : 160f;

        private void Update()
        {
            if (Session == null) return;
            HandlePointer();
            if (KeyboardEnabled) HandleKeyboard();
        }

        private void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Request(GameAction.North);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Request(GameAction.East);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Request(GameAction.South);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Request(GameAction.West);
            else if (Input.GetKeyDown(KeyCode.Space)) Request(GameAction.Wait);
            else if (Input.GetKeyDown(KeyCode.Q)) Request(GameAction.Smoke);
            else if (Input.GetKeyDown(KeyCode.E)) Request(GameAction.Emp);
            else if (Input.GetKeyDown(KeyCode.Z)) UndoRequested?.Invoke();
            else if (Input.GetKeyDown(KeyCode.R)) RestartRequested?.Invoke();
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
                _pointerDown = true;
                _gestureConsumed = false;
                _pointerStart = pos;
                _pointerStartedOverUi = IsOverUi();
                return;
            }
            if (!_pointerDown) return;

            var delta = pos - _pointerStart;
            float swipePx = SwipeInches * Dpi;
            if (held && !_gestureConsumed && !_pointerStartedOverUi && delta.magnitude >= swipePx)
            {
                _gestureConsumed = true;
                Request(DirectionFor(delta));
            }
            if (up)
            {
                _pointerDown = false;
                if (!_gestureConsumed && !_pointerStartedOverUi && delta.magnitude <= TapInches * Dpi) HandleTap(pos);
                _gestureConsumed = false;
            }
        }

        private void HandleTap(Vector2 screenPos)
        {
            if (BoardCamera == null || Session.Board == null || !Session.Board.IsBuilt || Session.State == null) return;
            var world = BoardCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -BoardCamera.transform.position.z));
            var coord = Session.Board.Layout.NearestCoord(world);
            var player = Session.State.PlayerPosition;
            var d = DirectionUtil.FromDelta(coord.X - player.X, coord.Y - player.Y);
            if (d == null)
            {
                if (coord == player) Request(GameAction.Wait); // tapping yourself waits
                return;
            }
            Request(GameActionUtil.FromDirection(d.Value));
        }

        private static GameAction DirectionFor(Vector2 delta)
        {
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x > 0 ? GameAction.East : GameAction.West;
            return delta.y > 0 ? GameAction.North : GameAction.South;
        }

        private void Request(GameAction a) => ActionRequested?.Invoke(a);

        private static bool IsOverUi()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
