using UnityEngine;

namespace LightweightDI.Samples
{
    public interface IExampleAnalytics
    {
        void Track(string eventName, string value);
    }

    /// <summary>
    /// Bound to IExampleAnalytics through MapSingletonOf. Swapping this for a real backend is
    /// a one-line change in the composition root - nothing that consumes the interface has to
    /// know, and nothing else has to be recompiled against a concrete type.
    /// </summary>
    public class ExampleConsoleAnalytics : IExampleAnalytics
    {
        public void Track(string eventName, string value)
        {
            Debug.Log($"[analytics] {eventName}: {value}");
        }
    }
}
