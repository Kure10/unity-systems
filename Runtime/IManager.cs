namespace LightweightDI
{
    /// <summary>
    /// A long-lived service registered through <see cref="Injector.TryMapManager{T}"/>.
    ///
    /// Initialize() is called by the container immediately after injection completes, so
    /// every [Inject] member is guaranteed to be set by the time it runs. This is the hook
    /// that replaces Awake() for anything that depends on injected state.
    /// </summary>
    public interface IManager
    {
        void Initialize();
    }
}
