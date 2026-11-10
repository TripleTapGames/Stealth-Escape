using System.Collections;
using ShadowSteps.Data;
using ShadowSteps.V3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShadowSteps.Runtime
{
    /// <summary>
    /// Composition root of the Game scene. The scene file only needs this component on one object; everything else
    /// (camera, board, session, input, HUD) is created or found here so the scene cannot drift out of sync with code.
    /// Designers can still place a BoardTheme or a HUD prefab in the serialized fields to override the defaults.
    /// </summary>
    public sealed class GameSceneComposer : MonoBehaviour
    {
        [Header("Optional overrides")]
        public BoardTheme Theme;
        [Tooltip("HUD prefab (must contain a component implementing IGameHud). Leave empty to build the default HUD at runtime.")]
        public GameObject HudPrefab;
        [Tooltip("Optional UiPrefabSet asset (ScriptableObject from the UI assembly) with screen/HUD prefabs.")]
        public ScriptableObject UiPrefabs;
        [Tooltip("Stage id to open when the scene is started directly in the editor and no stage was requested (0 = first unfinished).")]
        public int DebugStageId = 0;
        /// <summary>SessionState key the editor-only stage editor uses to pin the next stage (consumed once).</summary>
        public const string EditorPlayStageKey = "ShadowSteps.EditorPlayStageId";
        [Header("Presentation")]
        [Tooltip("Drive the board with the 3D art layer (ShadowSteps.Art3D) instead of the 2D sprite board. The rules engine is identical either way.")]
        public bool Use3DPresentation = false;
        [Tooltip("SS3DArtPalette asset (from the Art3D assembly) used when Use3DPresentation is on.")]
        public ScriptableObject Art3DPalette;
        [Tooltip("Drive the board with the 2D sprite art layer (ShadowSteps.Art2D: generated sprites, captured character frames) instead of the placeholder sprite board. Ignored when Use3DPresentation is on.")]
        public bool Use2DArtPresentation = false;
        [Tooltip("SS2DSpriteLibrary asset (from the Art2D assembly) used when Use2DArtPresentation is on.")]
        public ScriptableObject Art2DLibrary;

        /// <summary>Optional presentation hook: called after a win is recorded with the outcome and a continuation (the results screen). Return true to take over and call the continuation later (or advance to the next stage yourself).</summary>
        public System.Func<ShadowSteps.V3.ActionOutcome, System.Action, bool> WinTransition;
        /// <summary>Presentation hook: highlight guard i (stable index) for a tutorial card, -1 = clear. Set by the active presentation layer.</summary>
        public System.Action<int> GuardHighlight;
        /// <summary>Presentation hooks for the spotlight tutorial (D-077): world position of a node / camera head and the drawn node spacing. Set by the active presentation layer; the placeholder board is the fallback.</summary>
        public System.Func<ShadowSteps.Core.GridCoord, Vector3> NodeWorldPosition;
        public System.Func<int, Vector3?> CameraWorldPosition;
        public System.Func<float> NodeSpacingWorld;
        /// <summary>
        /// Optional presentation hook for a capture or lockdown: called before the failure screen; return true to own the
        /// timing (finish the guard's step/turn, play the capture beat) and invoke the continuation when the screen may appear.
        /// </summary>
        public System.Func<ShadowSteps.V3.ActionOutcome, System.Action, bool> CaptureTransition;

        public Camera BoardCamera { get; private set; }
        public BoardView Board { get; private set; }
        public GameSessionController Session { get; private set; }
        public InputController Input { get; private set; }
        public TurnAnimationCoordinator Animator { get; private set; }
        public IGameHud Hud { get; private set; }
        public bool Ready { get; private set; }

        private void Awake() { BuildScene(); }

        private IEnumerator Start()
        {
            if (!AppServices.IsInitialized && !AppServices.TryInitializeSync())
            {
                bool ok = false;
                yield return AppServices.InitializeAsync(r => ok = r);
                if (!ok) { Hud?.ShowMessage("Could not load stages: " + AppServices.LastError); yield break; }
            }
            int id = AppServices.PendingStageId;
#if UNITY_EDITOR
            // The stage editor's Play button (D-072) pins the stage for this one play session; direct scene previews may pin one via the inspector.
            if (id <= 0) { id = UnityEditor.SessionState.GetInt(EditorPlayStageKey, 0); UnityEditor.SessionState.EraseInt(EditorPlayStageKey); }
            // Installed builds always follow saved progress.
            if (id <= 0) id = DebugStageId;
#endif
            AppServices.PendingStageId = 0;
            var stage = (id > 0 ? AppServices.Stages.ById(id) : null) ?? StageProgression.ResumeStage(AppServices.Stages, AppServices.Save);
            OpenStage(stage);
        }

        public void OpenStage(Stage stage)
        {
            if (!Session.HasStage) Session.PresetRulesMode(StageModeUtil.Default); // Knockout only (D-043): no player-facing mode selection
            Session.LoadStage(stage);
            if (AppServices.Save != null) { AppServices.Save.LastPlayedLevelId = stage.Id; AppServices.PersistSave(); }
            Ready = true;
        }

        /// <summary>Creates all scene objects. Idempotent: existing objects with the same roles are reused.</summary>
        public void BuildScene()
        {
            BoardCamera = Camera.main;
            if (BoardCamera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                BoardCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            BoardCamera.orthographic = true;
            BoardCamera.clearFlags = CameraClearFlags.SolidColor;
            BoardCamera.transform.position = new Vector3(0f, 0f, -10f);
            BoardCamera.transform.rotation = Quaternion.identity;

            var boardGo = new GameObject("Board");
            Board = boardGo.AddComponent<BoardView>();
            Board.Theme = Theme != null ? Theme : BoardTheme.CreateRuntimeDefault();
            BoardCamera.backgroundColor = Board.Theme.Background;

            var fitter = BoardCamera.GetComponent<CameraFitter>() ?? BoardCamera.gameObject.AddComponent<CameraFitter>();
            fitter.Board = Board;

            var sessionGo = new GameObject("GameSession");
            Animator = sessionGo.AddComponent<TurnAnimationCoordinator>();
            Animator.ReducedMotion = AppServices.Save != null && AppServices.Save.Settings.ReducedMotion;
            Session = sessionGo.AddComponent<GameSessionController>();
            Session.Board = Board;
            Session.Animator = Animator;
            Session.RulesModeChanged += mode => { if (AppServices.Save != null) { AppServices.Save.SelectedMode = mode; AppServices.PersistSave(); } };

            Input = sessionGo.AddComponent<InputController>();
            Input.Session = Session;
            Input.BoardCamera = BoardCamera;
            Input.RetryRequested += () => Session.Retry();
            Input.UndoRequested += () => Session.DebugUndo();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            GameObject hudGo = HudPrefab != null ? Instantiate(HudPrefab) : null;
            Hud = hudGo != null ? hudGo.GetComponentInChildren<IGameHud>() : null;
            if (Hud == null)
            {
                var factory = GameHudFactory.Default;
                if (factory != null) Hud = factory(this);
            }
            Hud?.Bind(Session);
            GameSceneHooks.OnComposed?.Invoke(this);
        }
    }

    /// <summary>Lets higher-level assemblies (UI flow, 3D art) attach to every composed Game scene without a Runtime→UI reference. Subscribe with +=.</summary>
    public static class GameSceneHooks
    {
        public static System.Action<GameSceneComposer> OnComposed;
    }

    /// <summary>What the Game scene needs from any HUD implementation. The UI assembly registers a factory for the default HUD.</summary>
    public interface IGameHud
    {
        void Bind(GameSessionController session);
        void ShowMessage(string message);
    }

    /// <summary>Optional: a HUD that owns a pause button raises this so the flow controller can open the pause screen.</summary>
    public interface IGameHudPause
    {
        event System.Action PauseRequested;
    }

    /// <summary>Optional: a HUD with gadget buttons can accent one while its first-encounter card (D-075) is open and pin the two-step guide text (D-076).</summary>
    public interface IGameHudGadgetHighlight
    {
        void HighlightGadget(ShadowSteps.Data.GadgetKind kind, bool on);
        /// <summary>Pins a guide line in the message pill until <see cref="ClearGuide"/> or the next guide line.</summary>
        void ShowGuide(string message);
        void ClearGuide();
        /// <summary>Screen-space rectangle (pixels, origin bottom-left) of the gadget's button, for the spotlight cut-out (D-077).</summary>
        bool TryGetGadgetScreenRect(ShadowSteps.Data.GadgetKind kind, out Rect screenRect);
    }

    /// <summary>Lets the UI assembly (which Runtime cannot reference) provide the default HUD without a hard dependency.</summary>
    public static class GameHudFactory
    {
        public delegate IGameHud Factory(GameSceneComposer composer);
        public static Factory Default;
    }
}
