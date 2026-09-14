using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// One bootstrap per scene. This is everything a scene needs to take part - the base
    /// class injects into this object, then builds and initialises the controller tree.
    /// </summary>
    public class ExampleSceneBootstrap : SceneBootstrap
    {
        [Inject] private ExampleSceneManager _sceneManager;

        protected override void OnAfterControllers()
        {
            Debug.Log($"Scene ready: {_sceneManager.CurrentScene}");
        }
    }
}
