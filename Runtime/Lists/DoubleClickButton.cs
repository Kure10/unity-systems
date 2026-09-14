using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LightweightDI.Lists
{
    /// <summary>
    /// Detects a double click on a UI element. Optional on list items - assign it in the
    /// inspector when a row needs a second action.
    /// </summary>
    public class DoubleClickButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private float _interval = 0.3f;

        public Action OnDoubleClick;

        private float _lastClickTime = float.NegativeInfinity;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - _lastClickTime <= _interval)
            {
                // Reset, so a third click does not register as another double click.
                _lastClickTime = float.NegativeInfinity;
                OnDoubleClick?.Invoke();
                return;
            }

            _lastClickTime = Time.unscaledTime;
        }
    }
}
