using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// Base class for MonoBehaviours that are created outside the scene bootstrap - spawned
    /// prefabs, pooled objects, anything Instantiate() produces at runtime.
    ///
    /// Scene controllers do not need this: the bootstrap walks the scene and injects into
    /// them. An object that appears later has to ask for itself, and Awake() is the earliest
    /// point where it can.
    /// </summary>
    public abstract class InjectableMonoBehaviour : MonoBehaviour, IInjectable
    {
        public bool Injected { get; set; }

        protected virtual void Awake()
        {
            Injector.Instance.InjectInto(this);
        }
    }
}
