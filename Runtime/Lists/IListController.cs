using System;
using UnityEngine;

namespace LightweightDI.Lists
{
    /// <summary>
    /// The non-generic face of a list. Item controllers talk to their parent through this,
    /// so they do not need to know the list's data type.
    /// </summary>
    public interface IListController
    {
        /// <summary>Raised when a controller is enabled and filled with data.</summary>
        Action<int> OnControllerEnabled { get; set; }

        /// <summary>Raised when a controller is hidden because it has no data to show.</summary>
        Action<int> OnControllerHidden { get; set; }

        /// <summary>Raised after a new data set has been applied.</summary>
        Action OnDataSet { get; set; }

        GameObject ItemPrefab { get; }
        RectTransform ContentsParent { get; }

        int Count { get; }
        int DataCount { get; }
        int VisibleControllersCount { get; }
        int SelectedItemsCount { get; }

        /// <summary>
        /// Called by an item that changed its own height, so the list can update its height
        /// cache and re-run the layout. This is what makes expandable rows possible.
        /// </summary>
        void OnItemSizeUpdated(int viewIndex);
    }
}
