namespace LightweightDI.Lists
{
    /// <summary>
    /// Optional layout strategy on a list. When a component implementing this sits on the
    /// same GameObject as a <see cref="ListController{TItemController,TData}"/>, the list
    /// initialises it before its own children, so the layout is ready when items appear.
    /// </summary>
    public interface ILayout
    {
        void Initialize();
    }
}
