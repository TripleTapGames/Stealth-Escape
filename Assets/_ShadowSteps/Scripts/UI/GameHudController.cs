using ShadowSteps.Core;
using ShadowSteps.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowSteps.UI
{
    /// <summary>
    /// Gameplay HUD: level title, turn counter, key state, hint/message line and the action buttons
    /// (WAIT, SMOKE, EMP, UNDO, RESTART). It only reads <see cref="GameSessionController"/> state and forwards taps;
    /// buttons disable while a turn animates or when the action is invalid. Build once with <see cref="BuildHierarchy"/>
    /// (used both by the prefab generator and at runtime when no prefab exists).
    /// </summary>
    public sealed class GameHudController : MonoBehaviour, IGameHud
    {
        [Header("Wired by BuildHierarchy or the prefab")]
        public UiText TitleText;
        public UiText TurnText;
        public UiText KeyText;
        public UiText MessageText;
        public UiButton WaitButton;
        public UiButton SmokeButton;
        public UiButton EmpButton;
        public UiButton UndoButton;
        public UiButton RestartButton;
        public UiButton PauseButton;
        public RectTransform TopBar;
        public RectTransform BottomBar;

        private GameSessionController _session;
        private float _messageUntil;
        private string _hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterFactory()
        {
            GameHudFactory.Default = composer =>
            {
                var canvas = UiBuilder.CreateCanvas("GameHud", 10);
                var hud = canvas.gameObject.AddComponent<GameHudController>();
                hud.BuildHierarchy();
                return hud;
            };
        }

        /// <summary>Creates the child hierarchy and wires the serialized fields. Safe to call on an empty object with a Canvas.</summary>
        public void BuildHierarchy()
        {
            var safe = UiBuilder.CreateRect("SafeArea", transform);
            UiBuilder.Anchor(safe, 0f, 0f, 1f, 1f);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            TopBar = UiBuilder.CreatePanel("TopBar", safe, UiBuilder.PanelColor);
            UiBuilder.Anchor(TopBar, 0f, 0.88f, 1f, 1f);
            TitleText = UiBuilder.CreateText("Title", TopBar, "Level", 44f, TextAnchor.MiddleLeft, true);
            UiBuilder.Anchor(TitleText.Rect, 0.03f, 0.5f, 0.6f, 1f);
            TurnText = UiBuilder.CreateText("Turns", TopBar, "Turn 0", 40f, TextAnchor.MiddleRight);
            UiBuilder.Anchor(TurnText.Rect, 0.5f, 0.5f, 0.84f, 1f);
            PauseButton = UiBuilder.CreateButton("Pause", TopBar, "II", UiBuilder.ButtonColor, 40f);
            UiBuilder.RemoveComponent(PauseButton.GetComponent<LayoutElement>());
            UiBuilder.Anchor((RectTransform)PauseButton.transform, 0.86f, 0.52f, 0.98f, 0.98f);
            KeyText = UiBuilder.CreateText("Key", TopBar, "", 32f, TextAnchor.MiddleLeft);
            UiBuilder.Anchor(KeyText.Rect, 0.03f, 0f, 0.5f, 0.5f);
            MessageText = UiBuilder.CreateText("Message", TopBar, "", 32f, TextAnchor.MiddleRight);
            UiBuilder.Anchor(MessageText.Rect, 0.4f, 0f, 0.97f, 0.5f);

            BottomBar = UiBuilder.CreatePanel("BottomBar", safe, UiBuilder.PanelColor);
            UiBuilder.Anchor(BottomBar, 0f, 0f, 1f, 0.2f);
            UiBuilder.VerticalStack(BottomBar, 14f, 20f);

            var row1 = UiBuilder.CreateRect("ActionsRow", BottomBar);
            UiBuilder.HorizontalRow(row1, 14f, 0f);
            WaitButton = UiBuilder.CreateButton("Wait", row1, "WAIT", UiBuilder.ButtonColor);
            SmokeButton = UiBuilder.CreateButton("Smoke", row1, "SMOKE", UiBuilder.ButtonAccent);
            EmpButton = UiBuilder.CreateButton("Emp", row1, "EMP", UiBuilder.ButtonAccent);

            var row2 = UiBuilder.CreateRect("SystemRow", BottomBar);
            UiBuilder.HorizontalRow(row2, 14f, 0f);
            UndoButton = UiBuilder.CreateButton("Undo", row2, "UNDO", UiBuilder.ButtonColor);
            RestartButton = UiBuilder.CreateButton("Restart", row2, "RESTART", UiBuilder.ButtonDanger);
            WaitButton.SetLabel("WAIT"); SmokeButton.SetLabel("SMOKE"); EmpButton.SetLabel("EMP");
            UndoButton.SetLabel("UNDO"); RestartButton.SetLabel("RESTART");
        }

        /// <summary>Raised when the player taps the pause button; the flow controller decides what to show.</summary>
        public event System.Action PauseRequested;

        public void Bind(GameSessionController session)
        {
            if (_session != null) Unbind();
            _session = session;
            _session.StateChanged += OnStateChanged;
            _session.LevelLoaded += OnLevelLoaded;
            _session.TurnStarted += OnTurnStarted;
            _session.ActionRejected += OnActionRejected;
            _session.LevelWon += OnLevelWon;
            _session.PlayerCaptured += OnCaptured;
            WaitButton.Button.onClick.AddListener(() => _session.TrySubmit(GameAction.Wait));
            SmokeButton.Button.onClick.AddListener(() => _session.TrySubmit(GameAction.Smoke));
            EmpButton.Button.onClick.AddListener(() => _session.TrySubmit(GameAction.Emp));
            UndoButton.Button.onClick.AddListener(() => _session.Undo());
            RestartButton.Button.onClick.AddListener(() => _session.Restart());
            PauseButton.Button.onClick.AddListener(() => PauseRequested?.Invoke());
            if (_session.HasLevel) { OnLevelLoaded(_session.Level); OnStateChanged(_session.State); }
        }

        private void Unbind()
        {
            _session.StateChanged -= OnStateChanged;
            _session.LevelLoaded -= OnLevelLoaded;
            _session.TurnStarted -= OnTurnStarted;
            _session.ActionRejected -= OnActionRejected;
            _session.LevelWon -= OnLevelWon;
            _session.PlayerCaptured -= OnCaptured;
            foreach (var b in new[] { WaitButton, SmokeButton, EmpButton, UndoButton, RestartButton, PauseButton }) b.Button.onClick.RemoveAllListeners();
        }

        private void OnDestroy() { if (_session != null) Unbind(); }

        private void OnLevelLoaded(LevelDefinition level)
        {
            TitleText.Text = level.Id + ". " + level.Name;
            _hint = level.Hint;
            ShowMessage(_hint ?? string.Empty, 0f);
        }

        private void OnTurnStarted(TurnResult r) => SetButtons(false);

        private void OnStateChanged(GameState state)
        {
            TurnText.Text = "Turn " + state.TurnCount;
            var level = _session.Level;
            KeyText.Text = level.HasKey ? (state.HasKey ? "Key: collected" : "Key: needed") : string.Empty;
            SmokeButton.SetCount(state.SmokeCharges);
            EmpButton.SetCount(state.EmpCharges);
            SetButtons(true);
        }

        private void SetButtons(bool enabled)
        {
            var state = _session.State;
            var level = _session.Level;
            bool playing = enabled && state != null && !state.IsTerminal;
            WaitButton.Interactable = playing && !level.NoWait;
            SmokeButton.Interactable = playing && state.SmokeCharges > 0;
            EmpButton.Interactable = playing && state.EmpCharges > 0;
            UndoButton.Interactable = enabled && _session.Session != null && _session.Session.CanUndo && !level.NoUndo;
            RestartButton.Interactable = enabled;
        }

        private void OnActionRejected(GameAction action, InvalidActionReason reason)
        {
            switch (reason)
            {
                case InvalidActionReason.NoEdge: ShowMessage("No path that way"); break;
                case InvalidActionReason.Wall: ShowMessage("Blocked"); break;
                case InvalidActionReason.OutOfBounds: ShowMessage("Nothing there"); break;
                case InvalidActionReason.LockedDoor: ShowMessage("Locked: find the key"); break;
                case InvalidActionReason.NoSmokeCharge: ShowMessage("No smoke left"); break;
                case InvalidActionReason.NoEmpCharge: ShowMessage("No EMP left"); break;
                case InvalidActionReason.WaitDisabled: ShowMessage("Waiting is disabled here"); break;
                case InvalidActionReason.GameOver: ShowMessage("Undo or restart"); break;
            }
        }

        private void OnLevelWon(TurnResult r) => ShowMessage("Extracted in " + r.StateAfter.TurnCount + " turns", 0f);
        private void OnCaptured(TurnResult r) => ShowMessage(r.Capture == CaptureKind.Seen ? "Spotted! Undo or restart" : "Caught! Undo or restart", 0f);

        public void ShowMessage(string message) => ShowMessage(message, 1.8f);

        public void ShowMessage(string message, float seconds)
        {
            MessageText.Text = message;
            _messageUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
        }

        private void Update()
        {
            if (_messageUntil != float.MaxValue && Time.unscaledTime > _messageUntil)
            {
                MessageText.Text = _hint ?? string.Empty;
                _messageUntil = float.MaxValue;
            }
        }
    }
}
