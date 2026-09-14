using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace LightweightDI.Lists
{
    /// <summary>
    /// A list that spawns, injects and owns its item controllers.
    ///
    /// This is the non-virtualised base: one controller per data entry, created on demand and
    /// reused when the data set shrinks and grows again. <see cref="DynamicListScroller{T,TData}"/>
    /// builds on it and adds recycling.
    /// </summary>
    /// <typeparam name="TItemController">Controller that renders one row.</typeparam>
    /// <typeparam name="TData">Data for one row.</typeparam>
    public class ListController<TItemController, TData> : ControllerContainer, IListController
        where TItemController : ListItemController<TData>
        where TData : class
    {
        [Inject] protected ControllerFactory _controllerFactory;

        [SerializeField] protected GameObject _itemPrefab;
        public GameObject ItemPrefab => _itemPrefab;

#if ODIN_INSPECTOR
        [Required("Provide parent of items!")]
#endif
        [SerializeField] protected RectTransform _contentsParent;
        public RectTransform ContentsParent => _contentsParent;

        [Header("Selection")]
        /// <summary>False disables input on the list and on every row in it.</summary>
        [field: SerializeField] public bool Interactable { get; private set; } = true;

        /// <summary>True if more than one row can be selected at a time.</summary>
        [SerializeField] protected bool _multiselect;

        /// <summary>Upper bound for multiselect. -1 means no limit.</summary>
        [SerializeField] protected int _maxSelectedItems = -1;

        /// <summary>True if clicking a selected row deselects it.</summary>
        [SerializeField] protected bool _unselectable;

        /// <summary>True if a selected row can be selected again (re-firing the event).</summary>
        [SerializeField] protected bool _canSelectSelected;

        protected List<int> _selectedItemIndexes = new List<int>();

        protected List<TItemController> _itemControllers = new List<TItemController>();

        public IEnumerable<TItemController> Controllers => _itemControllers;

        protected List<TData> _data = new List<TData>();

        public List<TData> Data => _data;

        public Action<int> OnItemSelected;
        public Action<int> OnItemUnselected;

        public Action<int> OnControllerEnabled { get; set; }
        public Action<int> OnControllerHidden { get; set; }
        public Action OnDataSet { get; set; }

        public ILayout Layout { get; private set; }

        /// <summary>Number of spawned controllers, visible or not.</summary>
        public int Count => _itemControllers.Count;

        public int DataCount => _data.Count;

        public int SelectedItemsCount => _selectedItemIndexes.Count;

        public int VisibleControllersCount
        {
            get
            {
                int count = 0;
                foreach (TItemController controller in _itemControllers)
                {
                    if (controller.gameObject.activeInHierarchy)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public override void Initialize()
        {
            if (_initialized)
            {
                Debug.LogError($"Initialize() called twice on list: {name}");
                return;
            }

            // The layout has to be ready before any item exists.
            Layout = GetComponent<ILayout>();
            Layout?.Initialize();

            base.Initialize();

            AdoptItemControllersInScene();
        }

        /// <summary>
        /// Picks up rows that were placed in the scene by hand, so a designer can lay out a
        /// short list in the editor and still have it behave like a spawned one.
        /// </summary>
        protected void AdoptItemControllersInScene()
        {
            foreach (Transform child in _contentsParent)
            {
                TItemController itemController = child.GetComponent<TItemController>();
                if (itemController != null)
                {
                    AddNewController(itemController);
                }
            }
        }

        #region Data

        public virtual void SetData(List<TData> data)
        {
            _data = data;

            for (int i = 0; i < _data.Count; i++)
            {
                GetOrCreateController(i, i);
            }

            HideUnusedControllers();

            OnDataSet?.Invoke();
        }

        protected virtual void HideUnusedControllers()
        {
            for (int i = _data.Count; i < _itemControllers.Count; i++)
            {
                if (_itemControllers[i].gameObject.activeSelf)
                {
                    _itemControllers[i].SetActive(false);
                    OnControllerHidden?.Invoke(i);
                }
            }
        }

        public virtual TData GetData(int index)
        {
            if (index < 0 || index >= _data.Count)
            {
                Debug.LogError($"Data index {index} out of range.");
                return null;
            }

            return _data[index];
        }

        public int FindItemIndex(TData data)
        {
            return _data.IndexOf(data);
        }

        public virtual void RemoveDataProvider(TData data)
        {
            int index = _data.IndexOf(data);
            if (index < 0)
            {
                return;
            }

            _data.Remove(data);

            // Reindex what is left and switch off whatever no longer has data.
            for (int i = index; i < _itemControllers.Count; i++)
            {
                if (i < _data.Count)
                {
                    GetController(i).Index = i;
                }
                else
                {
                    GetController(i).SetActive(false);
                }
            }
        }

        public virtual void RemoveItemAt(int index)
        {
            TData data = GetData(index);
            if (data != null)
            {
                RemoveDataProvider(data);
            }
        }

        public void Clear()
        {
            foreach (TItemController item in _itemControllers)
            {
                Destroy(item.gameObject);
            }

            _selectedItemIndexes.Clear();
            _itemControllers.Clear();
            _data.Clear();
        }

        #endregion

        #region Controllers

        protected virtual TItemController GetOrCreateController(int index, int dataIndex)
        {
            if (index < 0)
            {
                return null;
            }

            TItemController controller;

            if (index < _itemControllers.Count)
            {
                controller = _itemControllers[index];

                bool wasHidden = !controller.IsActive();
                controller.SetActive(true);

                if (wasHidden)
                {
                    OnControllerEnabled?.Invoke(index);
                    controller.OnControllerEnabled();
                }
            }
            else
            {
                controller = _controllerFactory.Create<TItemController>(_itemPrefab, _contentsParent);
                AddNewController(controller);

                OnControllerEnabled?.Invoke(index);
                controller.OnControllerEnabled();
            }

            controller.DataIndex = dataIndex;
            controller.SetData(_data[dataIndex]);
            controller.Refresh();
            controller.Interactable = Interactable;

            return controller;
        }

        protected virtual void AddNewController(TItemController controller)
        {
            controller.SetControllerParent(this);
            controller.Index = _itemControllers.Count;
            controller.OnClick += HandleItemClicked;
            controller.OnDoubleClick += HandleItemDoubleClicked;
            controller.SetParentListController(this);

            _itemControllers.Add(controller);

            OnNewControllerCreated(controller);
        }

        protected virtual void OnNewControllerCreated(TItemController controller)
        {
        }

        public virtual TItemController GetController(int index)
        {
            return index >= 0 && index < _itemControllers.Count ? _itemControllers[index] : null;
        }

        /// <summary>
        /// Controller currently showing the given data index. In this base list the two
        /// indexes are the same; a virtualised list maps them.
        /// </summary>
        public virtual TItemController GetControllerForDataIndex(int dataIndex)
        {
            return GetController(dataIndex);
        }

        public virtual TItemController GetController(TData data)
        {
            int index = _data.IndexOf(data);
            return index >= 0 ? _itemControllers[index] : null;
        }

        public TItemController FindItemControllerMatching(Func<TData, bool> condition)
        {
            for (int i = 0; i < _data.Count; i++)
            {
                if (condition(_data[i]))
                {
                    return GetControllerForDataIndex(i);
                }
            }

            return null;
        }

        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;

            foreach (TItemController controller in _itemControllers)
            {
                controller.Interactable = interactable;
            }
        }

        protected void AfterSetData(int index, int dataIndex)
        {
            if (index < _itemControllers.Count)
            {
                _itemControllers[index].AfterSetData();
            }
        }

        #endregion

        #region Selection

        public virtual void SelectItem(int dataIndex)
        {
            if (!_canSelectSelected && _selectedItemIndexes.Contains(dataIndex))
            {
                return;
            }

            if (_multiselect && MaxSelectedItemsReached())
            {
                return;
            }

            // Single-select: the previous selection goes away first.
            if (!_multiselect && _selectedItemIndexes.Count > 0)
            {
                UnselectAll();
            }

            ApplyItemSelection(dataIndex);

            OnItemSelected?.Invoke(dataIndex);
        }

        /// <summary>
        /// Deselects from code. Does not raise the selection events - use it to reflect a
        /// state change that did not come from the user.
        /// </summary>
        public void UnselectItem(int dataIndex)
        {
            if (!_selectedItemIndexes.Contains(dataIndex))
            {
                return;
            }

            ApplyItemUnselection(dataIndex);

            OnItemUnselected?.Invoke(dataIndex);
        }

        public virtual void UnselectAll()
        {
            foreach (int index in _selectedItemIndexes)
            {
                GetControllerForDataIndex(index)?.SetSelected(false);
            }

            _selectedItemIndexes.Clear();
        }

        public virtual void SelectAll()
        {
            for (int i = 0; i < _data.Count; i++)
            {
                SelectItem(i);
            }
        }

        protected virtual void ApplyItemSelection(int dataIndex)
        {
            _selectedItemIndexes.Add(dataIndex);

            TItemController controller = GetControllerForDataIndex(dataIndex);
            if (controller == null)
            {
                // Expected in a virtualised list - the row is simply not on screen.
                return;
            }

            controller.SetSelected(true);
        }

        protected virtual void ApplyItemUnselection(int dataIndex)
        {
            _selectedItemIndexes.Remove(dataIndex);

            GetControllerForDataIndex(dataIndex)?.SetSelected(false);
        }

        public TData GetSelectedData()
        {
            if (_selectedItemIndexes.Count == 0)
            {
                return null;
            }

            int index = _selectedItemIndexes[0];
            return index >= 0 && index < _data.Count ? _data[index] : null;
        }

        public List<TData> GetSelectedItems()
        {
            List<TData> selected = new List<TData>();
            foreach (int index in _selectedItemIndexes)
            {
                selected.Add(_data[index]);
            }
            return selected;
        }

        public int? GetSelectedIndex()
        {
            return _selectedItemIndexes.Count > 0 ? _selectedItemIndexes[0] : (int?)null;
        }

        protected bool MaxSelectedItemsReached()
        {
            return _maxSelectedItems != -1 && _selectedItemIndexes.Count >= _maxSelectedItems;
        }

        private void HandleItemClicked(int dataIndex)
        {
            if (!Interactable)
            {
                return;
            }

            bool alreadySelected = _selectedItemIndexes.Contains(dataIndex);

            if (!alreadySelected || _canSelectSelected)
            {
                SelectItem(dataIndex);
            }
            else if (_unselectable)
            {
                UnselectItem(dataIndex);
            }

            OnListItemClicked(dataIndex);
        }

        private void HandleItemDoubleClicked(int dataIndex)
        {
            if (Interactable)
            {
                OnListItemDoubleClicked(dataIndex);
            }
        }

        protected virtual void OnListItemClicked(int dataIndex)
        {
        }

        protected virtual void OnListItemDoubleClicked(int dataIndex)
        {
        }

        #endregion

        #region Scrolling

        protected virtual void ScrollToIndex(int index)
        {
            _contentsParent.anchoredPosition = new Vector2(0, GetItemScrollOffset(index));
        }

        protected virtual void DOScrollToIndex(int index, float duration)
        {
            _contentsParent.DOAnchorPosY(GetItemScrollOffset(index), duration);
        }

        /// <summary>
        /// Vertical offset of the given row. The base list reads it straight off the
        /// transform; a virtualised list has to compute it, because the row may not exist.
        /// </summary>
        public virtual float GetItemScrollOffset(int index)
        {
            TItemController controller = GetController(index);
            if (controller == null)
            {
                return 0f;
            }

            float offset = ((RectTransform)controller.transform).anchoredPosition.y;

            if (Mathf.Approximately(offset, 0f))
            {
                offset = TryGetGridCellHeight();
            }

            return -offset;
        }

        private float TryGetGridCellHeight()
        {
            return _contentsParent.TryGetComponent(out GridLayoutGroup grid) ? grid.cellSize.y : 0f;
        }

        #endregion

        /// <summary>
        /// A row changed its height. The base list lets Unity's layout handle it; the
        /// virtualised list overrides this to update its height cache.
        /// </summary>
        public virtual void OnItemSizeUpdated(int viewIndex)
        {
        }
    }
}
