using System.Reflection;

namespace AdhocLinq;

static class TypeUtils
{
    public static IEnumerable<Type> GetTypesSafe(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }

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

    public static bool IsNullableType(this Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

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
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Single or TypeCode.Double or TypeCode.Decimal => NumberKind.Floating,
            TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 => NumberKind.Signed,
            TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => NumberKind.Unsigned,
            TypeCode.Char => NumberKind.Ordinal,
            _ => NumberKind.None,
        };
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
