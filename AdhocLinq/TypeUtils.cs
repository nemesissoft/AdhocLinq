using System;

namespace AdhocLinq
{
    static class TypeUtils
    {
        public static Type FindGenericType(this Type generic, Type type)
        {
            while (type != null && type != typeof(object))
            {
                Type found;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == generic) return type;
                if (generic.IsInterface)
                    foreach (Type intfType in type.GetInterfaces())
                        if ((found = FindGenericType(generic, intfType)) != null) return found;
                type = type.BaseType;
            }
            return null;
        }

        public static bool IsNullableType(this Type type)=> type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

        public static Type GetNonNullableType(this Type type) => IsNullableType(type) ? type.GetGenericArguments()[0] : type;

        public static string GetTypeName(this Type type)
        {
            Type baseType = GetNonNullableType(type);
            string s = baseType.Name;
            if (type != baseType) s += '?';
            return s;
        }

        public static bool IsNumericType(this Type type) => GetNumericTypeKind(type) != NumberKind.None;
        public static bool IsSignedIntegralType(this Type type) => GetNumericTypeKind(type) == NumberKind.Signed;
        public static bool IsUnsignedIntegralType(this Type type) => GetNumericTypeKind(type) == NumberKind.Unsigned;

        public static NumberKind GetNumericTypeKind(Type type)
        {
            type = GetNonNullableType(type);
            if (type.IsEnum) return 0;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return NumberKind.Floating;
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return NumberKind.Signed;
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return NumberKind.Unsigned;
                case TypeCode.Char:
                    return NumberKind.Ordinal;
                default:
                    return NumberKind.None;
            }
        }
        public enum NumberKind : byte
        {
            None = 0,
            Floating = 1,
            Signed = 2,
            Unsigned = 3,
            Ordinal = 4,
        }
        public static bool IsEnumType(this Type type) => GetNonNullableType(type).IsEnum;
    }
}
