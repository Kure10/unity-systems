using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// Created at runtime, so the scene bootstrap never sees it. It asks for injection itself
    /// in Awake through InjectableMonoBehaviour - the one case where a script talks to the
    /// container directly, because nothing else can reach it in time.
    /// </summary>
    public class ExampleSpawnedPopup : InjectableMonoBehaviour
    {
        [Inject] private ExampleAudioManager _audioManager;

        [SerializeField] private AudioClip _openSound;

        protected override void Awake()
        {
            base.Awake();

            _audioManager.Play(_openSound);
        }
    }
}
