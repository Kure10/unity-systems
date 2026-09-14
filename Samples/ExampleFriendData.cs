namespace LightweightDI.Samples
{
    /// <summary>
    /// Data for one row. A list is generic over this type and over the controller that
    /// renders it, so the pair is checked at compile time - a row can never be handed data
    /// it does not understand.
    /// </summary>
    public class ExampleFriendData
    {
        public string Name;
        public int Level;
        public bool Online;

        /// <summary>Set by the row when it is expanded, so the state survives recycling.</summary>
        public bool DetailsOpen;
    }
}
