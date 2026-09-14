using System;

namespace LightweightDI.Lists
{
    /// <summary>
    /// Spacing for a dynamic list. Separated so several lists can share one configuration
    /// asset instead of each carrying its own inspector fields.
    /// </summary>
    [Serializable]
    public class DynamicScrollerSettings
    {
        public float PaddingTop;
        public float PaddingBottom;
        public float SpacingBetweenItems;
    }
}
