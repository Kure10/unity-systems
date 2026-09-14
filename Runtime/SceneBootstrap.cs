using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// Entry point for a scene. Derive one per scene and put it on a GameObject there.
    ///
    /// Runs before other scripts, resolves its own dependencies first, then builds the
    /// controller tree of the scene, injects into the roots and initialises them. Everything
    /// below the roots is reached by the containers themselves.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public abstract class SceneBootstrap : MonoBehaviour
    {
        protected Injector _injector;

        private bool _initialized;

        protected virtual void Awake()
        {
            Run();
        }

        /// <summary>
        /// Safe to call more than once - a scene loaded through a transition manager may be
        /// bootstrapped explicitly before Awake gets the chance.
        /// </summary>
        public void Run()
        {
            if (_initialized)
            {
                return;
            }

            _injector = Injector.Instance;

            // Self-injection first: everything below depends on these members being set.
            _injector.InjectInto(this);

            _initialized = true;

            OnBeforeControllers();

            InitializeControllers();

            OnAfterControllers();
        }

        /// <summary>
        /// Runs after this object has its dependencies but before any controller is touched.
        /// The place to hand scene-level references to long-lived services.
        /// </summary>
        protected virtual void OnBeforeControllers()
        {
        }

        /// <summary>
        /// Runs once the whole scene is initialised.
        /// </summary>
        protected virtual void OnAfterControllers()
        {
        }

        protected void InitializeControllers()
        {
            var rootControllers = ControllerUtils.InitializeSceneHierarchy(gameObject.scene);

            // Only the roots are injected here. Every ControllerContainer injects into its
            // own children from Initialize(), so the pass continues by itself.
            foreach (Controller controller in rootControllers)
            {
                _injector.InjectInto(controller);
            }

            foreach (Controller controller in rootControllers)
            {
                controller.Initialize();
            }
        }

        /// <summary>
        /// Call when leaving the scene, so controllers that were never activated are still
        /// torn down.
        /// </summary>
        public virtual void OnSceneExit()
        {
            ControllerUtils.DestroyAllControllersInScene(gameObject.scene);
        }
    }
}
