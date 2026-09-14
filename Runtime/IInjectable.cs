namespace LightweightDI
{
    /// <summary>
    /// Tracks whether injection has already been performed on an object.
    ///
    /// Reflection is expensive, and in a UI-heavy game the same objects are handed to the
    /// container from several places. This flag makes a second attempt a no-op, so callers
    /// do not have to keep track of who injected what.
    /// </summary>
    public interface IInjectable
    {
        bool Injected { get; set; }
    }
}
