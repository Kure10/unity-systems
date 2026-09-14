using System;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightweightDI.Lists
{
    /// <summary>
    /// ScrollRect that exposes drag and scroll as plain callbacks.
    ///
    /// Unity's ScrollRect only raises onValueChanged, which fires for programmatic changes
    /// too. Distinguishing "the user is dragging" from "the content moved" needs the pointer
    /// events, and those are only reachable by overriding.
    /// </summary>
    public class DynamicScrollRect : ScrollRect
    {
        public Action<PointerEventData> OnScrollAction;
        public Action<PointerEventData> OnDragAction;
        public Action<PointerEventData> OnBeginDragAction;

        public override void OnScroll(PointerEventData data)
        {
            base.OnScroll(data);

            OnScrollAction?.Invoke(data);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            base.OnDrag(eventData);

            OnDragAction?.Invoke(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            // Raised before the base call, so a listener can cancel inertia or snapping
            // before ScrollRect starts acting on the drag.
            OnBeginDragAction?.Invoke(eventData);

            base.OnBeginDrag(eventData);
        }
    }
}
