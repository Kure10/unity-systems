using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A plain C# service - no MonoBehaviour, no scene presence. The container constructs it
    /// and resolves its dependencies in a single MapOrGetSingleton call.
    /// </summary>
    public class ExampleSaveService
    {
        [Inject] private ExampleSettings _settings;

        public void Save()
        {
            PlayerPrefs.SetFloat("volume", _settings.MasterVolume);
            PlayerPrefs.Save();
        }
    }
}
