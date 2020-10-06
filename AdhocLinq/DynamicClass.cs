using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AdhocLinq
{
    /// <summary>
    /// Provides a base class for dynamic objects created by using the <see cref="DynamicQueryable.Select(IQueryable,string,object[])"/>  method. For internal use only.
    /// </summary>
    public abstract class DynamicClass
    {
        /// <summary>
        /// Get textual representation of dynamic class in JSON like syntax 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
             => GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Aggregate(
                    new StringBuilder("{" + Environment.NewLine, 32),
                    (sb, prop) => sb.AppendLine($"\t{prop.Name} = {prop.GetValue(this, null)}"),
                    sb => sb.Append("}").ToString()
                );

    }
}
