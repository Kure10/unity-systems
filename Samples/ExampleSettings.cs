using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A value object the container hands out but does not own. Registered with MapValue.
    /// </summary>
    [CreateAssetMenu(menuName = "LightweightDI/Example Settings")]
    public class ExampleSettings : ScriptableObject
    {
        public float MasterVolume = 1f;
        public string StartSceneName = "Main";
    }
}
