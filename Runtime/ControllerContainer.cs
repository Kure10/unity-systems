using System.Collections.Generic;
using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// A Controller that owns other controllers.
    ///
    /// This class is where the bootstrap becomes recursive: the scene initializer injects
    /// into the roots only, and every container repeats those two steps - inject, then
    /// initialise - for its own direct children. Refresh and teardown propagate the same way.
    ///
    /// Note: Controller and ControllerContainer are deliberately separate types. Merging them
    /// means a GameObject carrying two controllers gets Refresh() and Initialize() called once
    /// per component, which is a source of duplicated work that is hard to see.
    /// </summary>
    public class ControllerContainer : Controller
    {
        private List<Controller> _children = new List<Controller>();

        public List<Controller> Children
        {
            get => _children;
            set => _children = value;
        }

        protected RectTransform _rectTransform;

        /// <summary>
        /// Cached RectTransform. UI containers need it constantly, and the cast is not free.
        /// </summary>
        public RectTransform RectTransform => _rectTransform;

        public override void Initialize()
        {
            if (_initialized)
            {
                Debug.LogError($"Initialize() called twice on container: {name}");
                return;
            }

            if (!Injected)
            {
                Debug.LogError($"Initialize() on {name} was called before it was injected into.");
            }

            _rectTransform = transform as RectTransform;

            foreach (Controller child in _children)
            {
                if (child == null)
                {
                    continue;
                }

                _injector.InjectInto(child);
                child.Initialize();
            }

            _initialized = true;
        }

        public override void Refresh()
        {
            foreach (Controller child in _children)
            {
                if (child != null && child.gameObject != null)
                {
                    child.Refresh();
                }
            }
        }

        protected override void OnControllerDestroy()
        {
            base.OnControllerDestroy();

            // Walk backwards: children remove themselves from this list as they are destroyed.
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (_children[i] != null)
                {
                    _children[i].Destroy();
                }
            }
        }
    }
}
