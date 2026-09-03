using System;
using UnityEngine;

namespace CloudWhale.Game
{
    /// <summary>
    /// Starts idle production without requiring a scene or prefab reference. UI may observe GameSession later,
    /// but all resource changes continue to pass through the state and storage boundary.
    /// </summary>
    public sealed class OpenGameProductionRuntime : MonoBehaviour
    {
        private static OpenGameProductionRuntime instance;
        private GameSession session;
        private OpenGameProductionController controller;

        public GameSession Session => session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartAutomatically()
        {
            if (instance != null) return;

            var runtimeObject = new GameObject(nameof(OpenGameProductionRuntime));
            DontDestroyOnLoad(runtimeObject);
            instance = runtimeObject.AddComponent<OpenGameProductionRuntime>();
        }

        private void Start()
        {
            if (session == null)
            {
                Initialize(GameStorageFactory.CreateDefault(), new SystemClock(), ProductionSettings.Default);
            }
        }

        private void Update()
        {
            controller?.Tick();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) session?.SaveCurrentProgress();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) session?.SaveCurrentProgress();
        }

        private void OnApplicationQuit()
        {
            session?.SaveCurrentProgress();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Kept public so EditMode/PlayMode tests and a future composition root can provide deterministic dependencies.
        public void Initialize(IStateStorage storage, IClock clock, ProductionSettings production)
        {
            if (session != null) throw new InvalidOperationException("Open-game production has already been initialized.");
            session = new GameSession(storage, clock, production);
            session.Load();
            controller = new OpenGameProductionController(session, clock);
        }

        public bool TickNow()
        {
            if (controller == null) throw new InvalidOperationException("Open-game production has not been initialized.");
            return controller.Tick();
        }
    }
}
