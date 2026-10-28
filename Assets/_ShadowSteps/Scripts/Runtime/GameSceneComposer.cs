using System.Collections;
using ShadowSteps.Core;
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
        [Tooltip("Level to open when the scene is started directly in the editor and no level was requested.")]
        public int DebugLevelId = 1;

        public Camera BoardCamera { get; private set; }
        public BoardView Board { get; private set; }
        public GameSessionController Session { get; private set; }
        public InputController Input { get; private set; }
        public TurnAnimationCoordinator Animator { get; private set; }
        public IGameHud Hud { get; private set; }
        public bool Ready { get; private set; }

        private void Awake()
        {
            BuildScene();
        }

        private IEnumerator Start()
        {
            if (!AppServices.IsInitialized && !AppServices.TryInitializeSync())
            {
                bool ok = false;
                yield return AppServices.InitializeAsync(r => ok = r);
                if (!ok)
                {
                    Hud?.ShowMessage("Could not load levels: " + AppServices.LastError);
                    yield break;
                }
            }
            int id = AppServices.PendingLevelId > 0 ? AppServices.PendingLevelId : (AppServices.Save != null && AppServices.Save.LastPlayedLevelId > 0 ? AppServices.Save.LastPlayedLevelId : DebugLevelId);
            AppServices.PendingLevelId = 0;
            var level = AppServices.Levels.ById(id) ?? AppServices.Levels.ByIndex(0);
            OpenLevel(level);
        }

        public void OpenLevel(LevelDefinition level)
        {
            Session.LoadLevel(level);
            if (AppServices.Save != null) { AppServices.Save.LastPlayedLevelId = level.Id; AppServices.PersistSave(); }
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

            Input = sessionGo.AddComponent<InputController>();
            Input.Session = Session;
            Input.BoardCamera = BoardCamera;
            Input.ActionRequested += a => Session.TrySubmit(a);
            Input.UndoRequested += () => Session.Undo();
            Input.RestartRequested += () => Session.Restart();

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
        }
    }

    /// <summary>What the Game scene needs from any HUD implementation. The UI assembly registers a factory for the default HUD.</summary>
    public interface IGameHud
    {
        void Bind(GameSessionController session);
        void ShowMessage(string message);
    }

    /// <summary>Lets the UI assembly (which Runtime cannot reference) provide the default HUD without a hard dependency.</summary>
    public static class GameHudFactory
    {
        public delegate IGameHud Factory(GameSceneComposer composer);
        public static Factory Default;
    }
}
