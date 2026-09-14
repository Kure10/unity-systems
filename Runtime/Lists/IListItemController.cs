namespace LightweightDI.Lists
{
    public interface IListItemController<TData> where TData : class
    {
        /// <summary>Index into the data set this item currently displays.</summary>
        int DataIndex { get; set; }

        /// <summary>Index of this controller in the list's pool of controllers.</summary>
        int Index { get; set; }

        void Initialize();
        void Refresh();
        void SetData(TData data);
        void SetSelected(bool selected);
        void SetActive(bool active);
        bool IsActive();
        void OnControllerEnabled();
    }
}
