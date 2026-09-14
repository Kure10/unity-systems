using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// Creates controllers from prefabs at runtime and gives them the same treatment the
    /// scene bootstrap gives scene controllers: build the controller tree, inject, initialise.
    ///
    /// Register it once in the composition root:
    /// <code>injector.MapOrGetSingleton&lt;ControllerFactory&gt;();</code>
    /// </summary>
    public class ControllerFactory
    {
        [Inject] private Injector _injector;

        public T Create<T>(GameObject prefab, Transform parent) where T : Controller
        {
            if (prefab == null)
            {
                Debug.LogError("Cannot create a controller from a null prefab.");
                return null;
            }

            GameObject instance = Object.Instantiate(prefab, parent);

            T controller = instance.GetComponent<T>();
            if (controller == null)
            {
                Debug.LogError($"Prefab {prefab.name} has no {typeof(T).Name} component.");
                Object.Destroy(instance);
                return null;
            }

            // A runtime prefab has no controller tree yet - it has to be built before
            // anything can be injected into it.
            ControllerUtils.InitializeControllerHierarchy(instance);

            _injector.InjectInto(controller);
            controller.Initialize();

            return controller;
        }
    }
}
