using System;
using UnityEngine;
using UnityEngine.UI;

namespace LightweightDI.Lists
{
    /// <summary>
    /// Base class for a single row. It is a <see cref="ControllerContainer"/>, so a row can
    /// own its own controllers and they get injected and initialised with it.
    ///
    /// A row is recycled: the same instance is handed different data as the list scrolls, so
    /// everything visual has to be rebuilt in <see cref="Refresh"/> and nothing may be
    /// assumed to persist between two data sets.
    /// </summary>
    public abstract class ListItemController<TData> : ControllerContainer,
        IListItemController<TData> where TData : class
    {
        [SerializeField] protected Button _selectButton;
        [SerializeField] protected DoubleClickButton _doubleClickButton;

        public Action<int> OnClick;
        public Action<int> OnDoubleClick;

        protected IListController _parentList;

        protected TData _data;

        public TData Data => _data;

        protected bool _selected;

        public bool Selected => _selected;

        /// <summary>True while the parent list accepts input.</summary>
        public bool Interactable { get; set; }

        protected int _dataIndex;

        /// <summary>Index into the data set this row currently displays.</summary>
        public int DataIndex
        {
            get => _dataIndex;
            set => _dataIndex = value;
        }

        protected int _index;

        /// <summary>
        /// Index of this controller in the list's pool. In a virtualised list this is not the
        /// same as <see cref="DataIndex"/> - the pool is smaller than the data set.
        /// </summary>
        public int Index
        {
            get => _index;
            set => _index = value;
        }

        public override void Initialize()
        {
            base.Initialize();

            if (_selectButton != null)
            {
                _selectButton.onClick.AddListener(HandleClick);
            }

            if (_doubleClickButton != null)
            {
                _doubleClickButton.OnDoubleClick += HandleDoubleClick;
            }
        }

        public void SetParentListController(IListController parent)
        {
            _parentList = parent;
        }

        public virtual void SetData(TData data)
        {
            _data = data;
        }

        /// <summary>
        /// Rebuild the row from <see cref="Data"/>. Called every time the row is reused, so
        /// it must set every visual it owns - including back to its default state.
        /// </summary>
        public override void Refresh()
        {
        }

        public virtual void SetSelected(bool selected)
        {
            _selected = selected;
        }

        public virtual void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public virtual bool IsActive()
        {
            return gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Resizes the row without telling the list. Use when the list recalculates its
        /// layout anyway - during Refresh, for instance.
        /// </summary>
        public virtual void UpdateSize(float newHeight)
        {
            RectTransform.sizeDelta = new Vector2(RectTransform.sizeDelta.x, newHeight);
        }

        /// <summary>
        /// Resizes the row and tells the list about it, so the height cache and the positions
        /// of every row below are updated. This is the entry point for expanding rows.
        ///
        /// Call it from a coroutine to expand gradually.
        /// </summary>
        public virtual void ExpandItem(float newHeight)
        {
            UpdateSize(newHeight);

            _parentList?.OnItemSizeUpdated(_index);
        }

        public virtual void EnableSelectButton(bool state)
        {
            if (_selectButton != null)
            {
                _selectButton.enabled = state;
            }
        }

        /// <summary>
        /// Called when this controller is taken from the pool and made visible. Use it for
        /// work that must happen per appearance rather than per data change.
        /// </summary>
        public virtual void OnControllerEnabled()
        {
        }

        /// <summary>
        /// Called after the whole data set has been applied to every visible row - the point
        /// where a row may look at its neighbours.
        /// </summary>
        public virtual void AfterSetData()
        {
        }

        private void HandleClick()
        {
            if (Interactable)
            {
                OnClick?.Invoke(_dataIndex);
            }
        }

        private void HandleDoubleClick()
        {
            if (Interactable)
            {
                OnDoubleClick?.Invoke(_dataIndex);
            }
        }
    }
}
