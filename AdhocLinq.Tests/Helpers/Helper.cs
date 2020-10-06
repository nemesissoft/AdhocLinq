using System;

namespace AdhocLinq.Tests.Helpers
{
    static class Helper
    {
        public static T GetDynamicProperty<T>(this object obj, string propertyName)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var type = obj.GetType();
            var propInfo = type.GetProperty(propertyName);

            return (T)propInfo.GetValue(obj, null);
        }
    }
}
