using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A leaf controller. It never asks the container for anything - by the time Initialize()
    /// or Refresh() runs, its parent container has already injected into it.
    /// </summary>
    public class ExampleVolumeLabel : Controller
    {
        [Inject] private ExampleSettings _settings;

        public override void Refresh()
        {
            Debug.Log($"Volume: {_settings.MasterVolume:P0}");
        }
    }
}
