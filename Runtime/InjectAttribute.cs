using System;

namespace LightweightDI
{
    /// <summary>
    /// Marks a field or property to be resolved by <see cref="Injector"/>.
    /// Works on private and inherited members.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
    }
}
