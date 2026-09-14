using LightweightDI.Lists;
using TMPro;
using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// One row of the friends list.
    ///
    /// The row is recycled, so Refresh() has to set every visual it owns - including back to
    /// its default. Anything left over from the previous data entry would show up on a
    /// completely unrelated friend.
    /// </summary>
    public class ExampleFriendRow : ListItemController<ExampleFriendData>
    {
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private GameObject _onlineBadge;
        [SerializeField] private GameObject _detailsPanel;

        [Header("Heights")]
        [SerializeField] private float _collapsedHeight = 90f;
        [SerializeField] private float _expandedHeight = 180f;

        public override void Refresh()
        {
            if (_data == null)
            {
                return;
            }

            _nameLabel.text = _data.Name;
            _levelLabel.text = $"Lv {_data.Level}";
            _onlineBadge.SetActive(_data.Online);

            // Expanded state lives in the data, not in the row - the row that displayed this
            // friend a moment ago may already be showing someone else.
            _detailsPanel.SetActive(_data.DetailsOpen);

            // Setting the height here rather than through ExpandItem: the list measures the
            // row right after Refresh, so it picks this up without a second layout pass.
            UpdateSize(_data.DetailsOpen ? _expandedHeight : _collapsedHeight);
        }

        /// <summary>
        /// Wire this to a button on the row. Unlike Refresh, this happens after the list has
        /// already laid everything out, so it has to tell the list to re-run the layout.
        /// </summary>
        public void ToggleDetails()
        {
            _data.DetailsOpen = !_data.DetailsOpen;

            _detailsPanel.SetActive(_data.DetailsOpen);

            ExpandItem(_data.DetailsOpen ? _expandedHeight : _collapsedHeight);
        }
    }
}
