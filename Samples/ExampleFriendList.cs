using System.Collections.Generic;
using LightweightDI.Lists;
using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A virtualised friends list.
    ///
    /// Deriving from DynamicListScroller with the row controller and its data type is the
    /// whole setup - recycling, variable row heights and selection come from the base class.
    /// Ten thousand friends cost the same number of GameObjects as ten.
    /// </summary>
    public class ExampleFriendList : DynamicListScroller<ExampleFriendRow, ExampleFriendData>
    {
        [Inject] private IExampleAnalytics _analytics;

        public void Show(List<ExampleFriendData> friends)
        {
            SetData(friends);
        }

        protected override void OnListItemClicked(int dataIndex)
        {
            ExampleFriendData friend = GetData(dataIndex);
            if (friend == null)
            {
                return;
            }

            _analytics?.Track("friend_selected", friend.Name);
        }

        protected override void OnListItemDoubleClicked(int dataIndex)
        {
            // The row may be outside the pool if the list scrolled since the click - always
            // null-check what a virtualised list hands back.
            GetControllerForDataIndex(dataIndex)?.ToggleDetails();
        }
    }
}
