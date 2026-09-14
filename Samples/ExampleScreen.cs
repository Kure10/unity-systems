using UnityEngine;

namespace LightweightDI.Samples
{
    /// <summary>
    /// A container controller. Its children are injected into and initialised by the base
    /// Initialize() below, so the tree resolves itself without this class knowing what is
    /// underneath it.
    /// </summary>
    public class ExampleScreen : ControllerContainer
    {
        [Inject] private ExampleSaveService _saveService;

        public override void Initialize()
        {
            // Children are handled by the base call.
            base.Initialize();

            Refresh();
        }

        public void OnCloseClicked()
        {
            _saveService.Save();
            Hide();
        }
    }
}
