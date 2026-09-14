using UnityEngine;

namespace LightweightDI
{
    /// <summary>
    /// Base class for scene objects that take part in the bootstrap.
    ///
    /// A Controller does not inject into itself. The bootstrap injects into the root
    /// controllers of a scene and calls Initialize(); every <see cref="ControllerContainer"/>
    /// then does the same for its own children, so the pass walks the tree top down and a
    /// parent is always ready before its children.
    /// </summary>
    public class Controller : MonoBehaviour, IInjectable
    {
        [Inject] protected Injector _injector;

        public bool Injected { get; set; }

        protected bool _initialized;

        private bool _destroyed;

        protected Controller _parent;

        public Controller Parent
        {
            get => _parent;
            set => _parent = value;
        }

        /// <summary>
        /// Called once, after injection. Override to set up the controller - at this point
        /// every [Inject] member is valid.
        /// </summary>
        public virtual void Initialize()
        {
            if (_initialized)
            {
                Debug.LogError($"Initialize() called twice on controller: {name}");
                return;
            }

            _initialized = true;
        }

        /// <summary>
        /// Override to rebuild the controller's view from current data.
        /// </summary>
        public virtual void Refresh()
        {
        }

        public virtual void Show()
        {
            if (this != null && gameObject != null)
            {
                gameObject.SetActive(true);
            }
        }

        public virtual void Hide()
        {
            if (this != null && gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Explicit teardown. Called from OnDestroy, and by a parent container when it is
        /// torn down - a controller that was never activated still gets cleaned up.
        /// </summary>
        public virtual void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            OnControllerDestroy();

            _destroyed = true;
        }

        protected virtual void OnControllerDestroy()
        {
            // Detach from the parent so the parent does not try to tear this down twice.
            if (_parent is ControllerContainer parentContainer)
            {
                parentContainer.Children.Remove(this);
            }

            if (this != null)
            {
                StopAllCoroutines();
            }
        }

        protected void OnDestroy()
        {
            Destroy();
        }

        /// <summary>
        /// Sets the parent found by the hierarchy walk. A ControllerContainer already set as
        /// the parent wins, so a Controller and a ControllerContainer can sit on the same
        /// GameObject without fighting over the link.
        /// </summary>
        public void SetControllerParent(Controller parent)
        {
            if (_parent == null || !(_parent is ControllerContainer))
            {
                _parent = parent;
            }
        }
    }
}
