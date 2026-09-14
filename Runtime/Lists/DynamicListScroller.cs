using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace LightweightDI.Lists
{
    /// <summary>
    /// A vertically scrolling list that keeps a constant number of row instances no matter how
    /// large the data set is, and supports rows of different heights.
    ///
    /// How the recycling works: the pool holds just enough rows to cover the viewport plus a
    /// margin. As the content scrolls, the row that leaves the top is moved to the bottom and
    /// refilled with the next data entry, so a data index maps onto a pool index with a modulo.
    /// Nothing is instantiated or destroyed while scrolling.
    ///
    /// Variable heights are what makes this more than the usual recycling list. Every row's
    /// measured height is kept in <see cref="_itemsHeightCache"/>, and positions are derived
    /// from that cache rather than from a fixed row height.
    ///
    /// There are two ways a row can change its height:
    ///
    ///   From Refresh() - a row that resizes itself while being filled with data. The height
    ///   is read back right after Refresh and the cache is updated.
    ///
    ///   From ExpandItem(height) - a row that resizes later, in response to a tap. The call
    ///   goes back to this list through IListController, the cache is updated and the layout
    ///   re-runs. Call it from a coroutine to expand gradually.
    ///
    /// Known constraint: vertical only, and the content and viewport pivots are assumed to be
    /// at the top (0, 1).
    /// </summary>
    public class DynamicListScroller<TItemController, TData> : ListController<TItemController, TData>
        where TItemController : ListItemController<TData>
        where TData : class
    {
        [Header("Dynamic scroller")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _scrollRectTransform;

        [Space]
        [SerializeField] private float _paddingTop;
        [SerializeField] private float _paddingBottom;
        [SerializeField] private float _spacingBetweenItems;

        /// <summary>
        /// Extra rows beyond what the viewport needs. Raise it when rows are much taller than
        /// the prefab used to size the pool, or when a row animates outside its own bounds.
        /// </summary>
        [SerializeField] private int _extraRenderers;

        public delegate void ItemIndexChangedDelegate(int itemIndexIncrement);

        /// <summary>Raised when the window of visible data indexes shifts by one row.</summary>
        public event ItemIndexChangedDelegate ItemIndexChangedEvent;

        public Action<Vector2> OnScrollAction;

        public ScrollRect ScrollRect => _scrollRect;
        public float PaddingTop => _paddingTop;
        public float PaddingBottom => _paddingBottom;

        private RectTransform _content;

        private List<RectTransform> _items;

        /// <summary>Size of the row pool. Derived from the viewport and the prefab height.</summary>
        private int _numRenderers = 10;

        /// <summary>Data index of the first row currently held by the pool.</summary>
        private int _firstDataIndex;

        /// <summary>Data index of the last row currently held by the pool (inclusive).</summary>
        private int _lastDataIndex;

        /// <summary>Number of rows actually in use - min(pool size, data count).</summary>
        private int _viewCount;

        /// <summary>
        /// Measured height of every entry, including entries not currently rendered. Filled as
        /// rows pass through the pool; entries never rendered yet stay at zero and are
        /// approximated when a scroll offset has to be computed.
        /// </summary>
        protected float[] _itemsHeightCache;

        private bool _contentHasFixedSize;

        /// <summary>Rows excluded from the content height, counted from the end.</summary>
        protected int _excludedFromEnd;

        /// <summary>Rows excluded from the content height, counted from the start.</summary>
        protected int _excludedFromStart;

        private int FirstViewIndex => GetViewIndex(_firstDataIndex);

        public override void Initialize()
        {
            base.Initialize();

            if (_scrollRect.horizontal)
            {
                Debug.LogError($"{name}: DynamicListScroller supports vertical scrolling only.");
            }

            _scrollRect.onValueChanged.AddListener(HandleScroll);
            _scrollRectTransform.pivot = Vector2.up;

            _content = _scrollRect.content;
            _items = new List<RectTransform>();

            CollectItems();

            // Size the pool from the viewport and the prefab: enough rows to cover the visible
            // area, plus two so there is always one entering and one leaving.
            RectTransform itemRect = (RectTransform)_itemPrefab.transform;
            float itemHeight = itemRect.rect.height;
            _numRenderers = Mathf.Abs(Mathf.CeilToInt(
                _scrollRectTransform.rect.height / (itemHeight + _spacingBetweenItems))) + 2 + _extraRenderers;

            // An even pool keeps the modulo mapping symmetric in both scroll directions.
            if (_numRenderers % 2 != 0)
            {
                _numRenderers++;
            }

            _content.anchoredPosition = Vector2.zero;
        }

        #region Data

        public override void SetData(List<TData> data)
        {
            // Deliberately not calling base.SetData: the base class creates one controller per
            // data entry, which is exactly what this class exists to avoid.

            _data = data;

            _firstDataIndex = Math.Clamp(_firstDataIndex - 1, 0, Mathf.Max(0, data.Count - 1));
            _lastDataIndex = -1;

            ResizeHeightCache(data.Count);

            _viewCount = Mathf.Min(_numRenderers, _data.Count);
            _lastDataIndex = _firstDataIndex + _viewCount - 1;

            // _lastDataIndex is inclusive everywhere it is used, so it must never point past
            // the end - which it would after the data set shrank.
            if (_lastDataIndex >= _data.Count)
            {
                _lastDataIndex = _data.Count - 1;
            }

            for (int i = 0; i < _viewCount; i++)
            {
                GetOrCreateController(i, i);
            }

            HideUnusedControllers();

            CollectItems();

            ApplyLayout();

            for (int i = 0; i < _viewCount; i++)
            {
                AfterSetData(i, i);
            }

            for (int i = _firstDataIndex; i <= _lastDataIndex; i++)
            {
                if (i >= _data.Count)
                {
                    continue;
                }

                UpdateItemData(GetViewIndex(i), i);
            }

            OnDataSet?.Invoke();
        }

        /// <summary>
        /// Re-applies the current data without touching the pool. Only valid when the entries
        /// changed in place - not when their count or order changed.
        /// </summary>
        public void RefreshData(List<TData> data)
        {
            _data = data;
            Refresh();
        }

        public override void Refresh()
        {
            base.Refresh();

            if (_data == null)
            {
                Debug.LogError($"{name}: Refresh() called before any data was set.");
                return;
            }

            for (int i = _viewCount; i < _itemControllers.Count; i++)
            {
                _itemControllers[i].SetActive(false);
            }
        }

        /// <summary>
        /// Grows or shrinks the height cache, keeping heights already measured.
        /// </summary>
        private void ResizeHeightCache(int count)
        {
            if (_itemsHeightCache == null)
            {
                _itemsHeightCache = new float[count];
                return;
            }

            float[] previous = _itemsHeightCache;
            _itemsHeightCache = new float[count];

            int shared = Mathf.Min(_itemsHeightCache.Length, previous.Length);
            Array.Copy(previous, _itemsHeightCache, shared);
        }

        /// <summary>
        /// Re-measures every row currently in the pool. Use after something outside this list
        /// changed row heights - a font swap, a resolution change.
        /// </summary>
        public void RemeasureVisibleItems()
        {
            for (int i = 0; i < _itemControllers.Count && i < _itemsHeightCache.Length; i++)
            {
                _itemsHeightCache[i] = ((RectTransform)_itemControllers[i].transform).rect.height;
            }
        }

        private void UpdateItemData(int viewIndex, int dataIndex)
        {
            TItemController controller = GetOrCreateController(viewIndex, dataIndex);

            if (viewIndex >= _viewCount)
            {
                controller.SetActive(true);
            }

            controller.SetSelected(_selectedItemIndexes.Contains(dataIndex));
        }

        #endregion

        #region Controllers

        protected override TItemController GetOrCreateController(int viewIndex, int dataIndex)
        {
            TItemController controller = base.GetOrCreateController(viewIndex, dataIndex);

            // Refresh() has run by now, so this is the row's final height for this data entry.
            _itemsHeightCache[controller.DataIndex] =
                ((RectTransform)controller.transform).rect.height;

            return controller;
        }

        /// <summary>
        /// Only rows currently held by the pool can be returned - everything else is not
        /// instantiated. Callers must handle null.
        /// </summary>
        public override TItemController GetControllerForDataIndex(int dataIndex)
        {
            if (dataIndex < _firstDataIndex || dataIndex > _lastDataIndex)
            {
                return null;
            }

            int viewIndex = GetViewIndex(dataIndex);

            return viewIndex >= 0 && viewIndex < _itemControllers.Count
                ? _itemControllers[viewIndex]
                : null;
        }

        /// <summary>
        /// Maps a data index onto a pool index. The positive modulo is what makes the pool
        /// behave as a ring buffer while scrolling in either direction.
        /// </summary>
        public int GetViewIndex(int dataIndex)
        {
            if (_viewCount == 0)
            {
                return 0;
            }

            int result = dataIndex % _viewCount;
            return result < 0 ? result + _viewCount : result;
        }

        /// <summary>
        /// Picks up row transforms under the content object, keeping the order of the pool and
        /// ignoring anything that is not a row - headers, separators, decoration.
        /// </summary>
        private void CollectItems()
        {
            foreach (Transform child in _content)
            {
                if (_items.Contains(child))
                {
                    continue;
                }

                if (child.GetComponent<TItemController>() == null)
                {
                    continue;
                }

                RectTransform item = (RectTransform)child;

                // Rows are positioned from the top downwards, so their pivot and anchors have
                // to agree with that regardless of how the prefab was authored.
                item.pivot = new Vector2(item.pivot.x, 1f);
                item.anchorMin = new Vector2(0.5f, 1f);
                item.anchorMax = new Vector2(0.5f, 1f);

                _items.Add(item);
            }
        }

        #endregion

        #region Layout

        private void ApplyLayout()
        {
            for (int dataIndex = _firstDataIndex; dataIndex <= _lastDataIndex; dataIndex++)
            {
                UpdateItemPosition(dataIndex);
            }

            UpdateContentSize();

            _scrollRect.StopMovement();

            UpdateVisibleItems();
        }

        private void UpdateLayout()
        {
            for (int dataIndex = _firstDataIndex; dataIndex <= _lastDataIndex; dataIndex++)
            {
                UpdateItemPosition(dataIndex);
            }

            UpdateContentSize();
        }

        /// <summary>
        /// A row reported a new height. Update the cache and re-run the layout, so every row
        /// below it moves.
        /// </summary>
        public override void OnItemSizeUpdated(int viewIndex)
        {
            if (_itemsHeightCache == null || viewIndex >= _itemsHeightCache.Length)
            {
                return;
            }

            TItemController controller = GetController(viewIndex);
            if (controller == null)
            {
                return;
            }

            _itemsHeightCache[controller.DataIndex] =
                ((RectTransform)controller.transform).rect.height;

            UpdateLayout();
        }

        private void UpdateItemPosition(int dataIndex)
        {
            if (_items.Count == 0)
            {
                return;
            }

            int viewIndex = GetViewIndex(dataIndex);
            if (viewIndex < 0 || viewIndex >= _items.Count)
            {
                return;
            }

            RectTransform itemRect = _items[viewIndex];
            itemRect.SetParent(_content);

            Vector2 anchoredPosition = itemRect.anchoredPosition;
            anchoredPosition.y = -GetItemPosition(dataIndex);
            itemRect.anchoredPosition = anchoredPosition;
        }

        /// <summary>
        /// Distance from the top of the content to the top of the given row.
        ///
        /// Linear in the index: fine for the list sizes this was built for, but a prefix-sum
        /// array would make it constant time if a list ever got long enough to matter.
        /// </summary>
        public float GetItemPosition(int dataIndex)
        {
            float sum = 0f;

            // Never index past the cache - a stale or oversized index must not throw here.
            int count = Mathf.Min(dataIndex, _itemsHeightCache?.Length ?? 0);

            for (int i = 0; i < count; i++)
            {
                sum += _itemsHeightCache[i] + _spacingBetweenItems;
            }

            return _paddingTop + sum;
        }

        private void UpdateContentSize()
        {
            if (_contentHasFixedSize)
            {
                return;
            }

            float contentHeight = 0f;

            for (int i = 0; i < _itemsHeightCache.Length; i++)
            {
                if (i >= _itemsHeightCache.Length - _excludedFromEnd)
                {
                    break;
                }

                if (i < _excludedFromStart)
                {
                    continue;
                }

                contentHeight += _itemsHeightCache[i];
            }

            contentHeight += (_data.Count - 1 - _excludedFromEnd - _excludedFromStart)
                * _spacingBetweenItems;
            contentHeight += _paddingTop + _paddingBottom;

            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        }

        /// <summary>
        /// Sizes the content as if every row were as tall as the first one, and stops
        /// recalculating it afterwards.
        ///
        /// Only valid when all rows really are the same height. It exists because computing
        /// the true height requires every row to have been measured, which has not happened
        /// while most of them have never been rendered - so a list that must not change its
        /// scroll range while the user scrolls can opt out.
        /// </summary>
        public void FixContentSizeFromFirstItem()
        {
            if (_itemsHeightCache == null || _itemsHeightCache.Length == 0)
            {
                return;
            }

            float rowHeight = _itemsHeightCache[0];
            float contentHeight = rowHeight * _itemsHeightCache.Length;

            contentHeight += (_data.Count - 1 - _excludedFromEnd - _excludedFromStart)
                * _spacingBetweenItems;
            contentHeight += _paddingTop + _paddingBottom;

            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);

            _contentHasFixedSize = true;
        }

        public float GetBottomMaxPosition()
        {
            return _content.offsetMax.y - _paddingBottom - _paddingTop + _spacingBetweenItems;
        }

        #endregion

        #region Recycling

        /// <summary>
        /// The recycling step. While the first row has drifted far enough above the viewport,
        /// move it to the bottom and fill it with the next data entry - and the mirror case
        /// when scrolling back up.
        ///
        /// The two thresholds are deliberately far apart: the one for recycling a row down has
        /// to be more than a row height away from the one for recycling back up, otherwise a
        /// row would bounce between the two on every frame.
        /// </summary>
        public void UpdateVisibleItems()
        {
            if (_items.Count == 0)
            {
                return;
            }

            RectTransform firstItem = _items[FirstViewIndex];

            int guard = 0;
            while (guard < _data.Count)
            {
                float firstItemHeight = firstItem.rect.height;
                float relativeY = _scrollRect.transform.InverseTransformPoint(firstItem.position).y;

                bool scrolledPastTop = relativeY > 2.2f * firstItemHeight
                    && _lastDataIndex < _data.Count - 1;

                bool scrolledBackUp = relativeY < firstItemHeight / 4f
                    && _firstDataIndex > 0;

                if (scrolledPastTop)
                {
                    ShiftWindow(1);
                    UpdateItemPosition(_lastDataIndex);
                    UpdateItemData(GetViewIndex(_lastDataIndex), _lastDataIndex);
                    UpdateContentSize();
                }
                else if (scrolledBackUp)
                {
                    ShiftWindow(-1);
                    UpdateItemPosition(_firstDataIndex);
                    UpdateItemData(GetViewIndex(_firstDataIndex), _firstDataIndex);
                    UpdateContentSize();
                }
                else
                {
                    return;
                }

                firstItem = _items[FirstViewIndex];
                guard++;
            }
        }

        private void ShiftWindow(int change)
        {
            _firstDataIndex += change;
            _lastDataIndex = _firstDataIndex + _viewCount - 1;

            ItemIndexChangedEvent?.Invoke(change);
        }

        private void HandleScroll(Vector2 position)
        {
            UpdateVisibleItems();

            OnScrollAction?.Invoke(position);
        }

        #endregion

        #region Scrolling and selection

        protected override void ScrollToIndex(int index)
        {
            // Rebuilding the window is cheaper and more reliable than scrolling through
            // hundreds of rows that would have to be recycled on the way.
            _firstDataIndex = index;
            SetData(_data);
        }

        protected override void DOScrollToIndex(int viewIndex, float duration)
        {
            _contentsParent.DOAnchorPosY(GetItemScrollOffset(viewIndex), duration);
        }

        /// <summary>
        /// Offset of a row that may never have been rendered. Heights that were measured are
        /// used as they are; the rest are approximated with the average of what is known, so
        /// the result is exact for a uniform list and close enough to land in the right place
        /// for a ragged one.
        /// </summary>
        public override float GetItemScrollOffset(int index)
        {
            if (_itemsHeightCache == null || index >= _itemsHeightCache.Length)
            {
                return 0f;
            }

            float measuredTotal = 0f;
            int measuredCount = 0;

            foreach (float height in _itemsHeightCache)
            {
                if (height > 0f)
                {
                    measuredTotal += height;
                    measuredCount++;
                }
            }

            if (measuredCount == 0)
            {
                return 0f;
            }

            float averageHeight = measuredTotal / measuredCount;

            float offset = 0f;
            for (int i = 0; i < index; i++)
            {
                offset += _itemsHeightCache[i] > 0f ? _itemsHeightCache[i] : averageHeight;
            }

            return offset + _spacingBetweenItems * index;
        }

        public override void SelectItem(int dataIndex)
        {
            base.SelectItem(dataIndex);

            // Selection can change a row's height, so the layout has to follow.
            UpdateLayout();
        }

        public override void UnselectAll()
        {
            foreach (int index in _selectedItemIndexes)
            {
                // Null is expected - the row may be outside the pool right now.
                GetControllerForDataIndex(index)?.SetSelected(false);
            }

            _selectedItemIndexes.Clear();

            UpdateLayout();
        }

        public override void SelectAll()
        {
            for (int i = 0; i < _data.Count; i++)
            {
                SelectItem(i);
            }

            UpdateLayout();
        }

        #endregion
    }
}
