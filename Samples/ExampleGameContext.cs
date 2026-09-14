using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// The composition root: the single place that decides what goes into the container.
    ///
    /// Runs before everything else and survives scene changes, so managers registered here
    /// stay alive and stay registered for the lifetime of the game.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ExampleGameContext : MonoBehaviour
    {
        public static ExampleGameContext Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private ExampleSettings _settings;

        [Header("Managers")]
        [SerializeField] private ExampleAudioManager _audioManager;
        [SerializeField] private ExampleSceneManager _sceneManager;

        private void Awake()
        {
            if (Instance != null)
            {
                // A reloaded scene brought a second context with it.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Injector injector = Injector.Instance;

            // The container registers itself, so anything can request an Injector.
            injector.MapAndInjectInto(injector);

            // A value the container does not own.
            injector.MapValue<ExampleSettings>(_settings);

            // Scene components. TryMapManager also calls Initialize() on first registration,
            // and discards the duplicate when this scene is loaded again.
            _audioManager = injector.TryMapManager(_audioManager);
            _sceneManager = injector.TryMapManager(_sceneManager);

            // Plain C# services - no scene presence, built by the container.
            injector.MapOrGetSingleton<ExampleSaveService>();

            // Needed by anything that spawns controllers at runtime - lists, popups, pools.
            injector.MapOrGetSingleton<ControllerFactory>();

            // Interface bound to an implementation.
            injector.MapSingletonOf<IExampleAnalytics, ExampleConsoleAnalytics>();
        }
    }
}
