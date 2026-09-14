using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A manager that lives as a component in the scene. The one [Inject] line is the whole
    /// wiring - no Find(), no singleton, no serialized reference to drag in.
    /// </summary>
    public class ExampleAudioManager : MonoBehaviour, IManager
    {
        [Inject] private ExampleSettings _settings;

        private float _volume;

        /// <summary>
        /// Called by TryMapManager() right after injection completes, so _settings is
        /// guaranteed to be set here. This is what replaces Awake() for injected state.
        /// </summary>
        public void Initialize()
        {
            _volume = _settings.MasterVolume;
        }

        public void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(clip, Vector3.zero, _volume);
        }
    }
}
