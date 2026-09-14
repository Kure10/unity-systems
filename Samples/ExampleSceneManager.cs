using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A manager that depends on both a value and an interface binding. Registration order
    /// in the composition root matters here, because Initialize() runs immediately - anything
    /// this manager reads during Initialize must already be registered.
    /// </summary>
    public class ExampleSceneManager : MonoBehaviour, IManager
    {
        [Inject] private ExampleSettings _settings;
        [Inject] private IExampleAnalytics _analytics;

        public string CurrentScene { get; private set; }

        public void Initialize()
        {
            CurrentScene = _settings.StartSceneName;
        }

        public void Load(string sceneName)
        {
            _analytics?.Track("scene_load", sceneName);
            CurrentScene = sceneName;
        }
    }
}
