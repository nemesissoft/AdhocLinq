using System;

namespace AdhocLinq
{
    /// <summary>
    /// Represents dynami property that dynamic class dan use 
    /// </summary>
    public sealed class DynamicProperty
    {
        /// <summary>
        /// Instantiates a <see cref="DynamicProperty"/> class
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyType"></param>
        public DynamicProperty(string propertyName, Type propertyType)
        {
            PropertyName = propertyName;
            PropertyType = propertyType;
        }

        /// <summary>
        /// Property name
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Property type
        /// </summary>
        public Type PropertyType { get; }
    }
}
