using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Dynamic;
using System.Globalization;

namespace AdhocLinq
{
    class LambdaStore
    {
        readonly Dictionary<Expression, string> _literals = new();

        //TODO:remove that and make sure that Conditional parsing conversion is done right 
        internal static readonly Expression NullLiteral = Expression.Constant(null);

        public Expression CreateLiteral(object value, string text)
        {
            ConstantExpression expr = Expression.Constant(value);
            _literals.Add(expr, text);
            return expr;
        }

        public static bool TryGetMemberName(Expression expression, out string memberName)
        {
            memberName = null;
            if (expression is MemberExpression memberExpression)
            {
                memberName = memberExpression.Member.Name;
                return true;
            }
            else if (expression is System.Linq.Expressions.DynamicExpression dynamicExpression)
            {
                memberName = ((GetMemberBinder)dynamicExpression.Binder).Name;
                return true;
            }
            else return false;
        }
        
        public int FindBestMethod(IEnumerable<MethodBase> methods, Expression[] args, out MethodBase method)
        {
            MethodData[] applicable = methods.
                Select(m => new MethodData { MethodBase = m, Parameters = m.GetParameters() }).
                Where(m => IsApplicable(m, args)).
                ToArray();
            if (applicable.Length > 1)
            {
                applicable = applicable.
                    Where(m => applicable.All(n => m == n || IsBetterThan(args, m, n))).
                    ToArray();
            }
            if (applicable.Length == 1)
            {
                MethodData md = applicable[0];
                for (int i = 0; i < args.Length; i++) args[i] = md.Args[i];
                method = md.MethodBase;
            }
            else
            {
                method = null;
            }
            return applicable.Length;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public bool TryPromoteExpression(Expression expr, Type type, bool exact, out Expression result)
        {
            try
            {
                result = PromoteExpression(expr, type, exact);
                return result != null;
            }
            catch (Exception)
            {
                result = null;
                return false;
            }
        }

        public Expression PromoteExpression(Expression expr, Type targetType, bool exact)
        {
            if (expr.Type == targetType) return expr;

            if (expr is ConstantExpression ce)
            {
                if (ce == NullLiteral)
                {
                    if (!targetType.IsValueType || targetType.IsNullableType())
                        return Expression.Constant(null, targetType);
                }
                else
                {
                    if (_literals.TryGetValue(ce, out var text))
                    {
                        Type target = targetType.GetNonNullableType();
                        Object value = null;
                        switch (Type.GetTypeCode(ce.Type))
                        {
                            case TypeCode.Int32:
                            case TypeCode.UInt32:
                            case TypeCode.Int64:
                            case TypeCode.UInt64:
                                value = ParseNumber(text, target);
                                break;
                            case TypeCode.Double:
                                if (target == typeof(decimal)) value = ParseNumber(text, target);
                                break;
                            case TypeCode.String:
                                value = ParseEnum(text, target);
                                break;
                        }
                        if (value != null)
                            return Expression.Constant(value, targetType);
                    }
                }
            }
            if (IsCompatibleWith(expr.Type, targetType))
            {
                return targetType.IsValueType || exact ? Expression.Convert(expr, targetType) : expr;
            }
            return null;
        }

        bool IsApplicable(MethodData method, Expression[] args)
        {
            if (method.Parameters.Length != args.Length) return false;
            Expression[] promotedArgs = new Expression[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                ParameterInfo pi = method.Parameters[i];
                if (pi.IsOut) return false;
                
                if(false == TryPromoteExpression(args[i], pi.ParameterType, false, out var promoted))
                    return false;
                promotedArgs[i] = promoted;
            }
            method.Args = promotedArgs;
            return true;
        }
        
        static bool IsBetterThan(Expression[] args, MethodData m1, MethodData m2)
        {
            bool better = false;
            for (int i = 0; i < args.Length; i++)
            {
                int c = CompareConversions(args[i].Type,
                    m1.Parameters[i].ParameterType,
                    m2.Parameters[i].ParameterType);
                if (c < 0) return false;
                if (c > 0) better = true;
            }
            return better;
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        static bool IsCompatibleWith(Type source, Type target)
        {
            if (source == target) return true;
            if (!target.IsValueType) return target.IsAssignableFrom(source);
            Type st = source.GetNonNullableType();
            Type tt = target.GetNonNullableType();
            if (st != source && tt == target) return false;
            TypeCode sc = st.IsEnum ? TypeCode.Object : Type.GetTypeCode(st);
            TypeCode tc = tt.IsEnum ? TypeCode.Object : Type.GetTypeCode(tt);
            switch (sc)
            {
                case TypeCode.SByte:
                    switch (tc)
                    {
                        case TypeCode.SByte:
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.Byte:
                    switch (tc)
                    {
                        case TypeCode.Byte:
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.Int16:
                    switch (tc)
                    {
                        case TypeCode.Int16:
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.UInt16:
                    switch (tc)
                    {
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.Int32:
                    switch (tc)
                    {
                        case TypeCode.Int32:
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.UInt32:
                    switch (tc)
                    {
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.Int64:
                    switch (tc)
                    {
                        case TypeCode.Int64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.UInt64:
                    switch (tc)
                    {
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                        case TypeCode.Decimal:
                            return true;
                    }
                    break;
                case TypeCode.Single:
                    switch (tc)
                    {
                        case TypeCode.Single:
                        case TypeCode.Double:
                            return true;
                    }
                    break;
                default:
                    if (st == tt) return true;
                    break;
            }
            return false;
        }

        static object ParseNumber(string text, Type type)
        {
            switch (Type.GetTypeCode(type.GetNonNullableType()))
            {
                case TypeCode.SByte:
                    if (sbyte.TryParse(text, out var sb)) return sb;
                    break;
                case TypeCode.Byte:
                    if (byte.TryParse(text, out var b)) return b;
                    break;
                case TypeCode.Int16:
                    if (short.TryParse(text, out var s)) return s;
                    break;
                case TypeCode.UInt16:
                    if (ushort.TryParse(text, out var us)) return us;
                    break;
                case TypeCode.Int32:
                    if (int.TryParse(text, out var i)) return i;
                    break;
                case TypeCode.UInt32:
                    if (uint.TryParse(text, out var ui)) return ui;
                    break;
                case TypeCode.Int64:
                    if (long.TryParse(text, out var l)) return l;
                    break;
                case TypeCode.UInt64:
                    if (ulong.TryParse(text, out var ul)) return ul;
                    break;
                case TypeCode.Single:
                    if (float.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var f)) return f;
                    break;
                case TypeCode.Double:
                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
                    break;
                case TypeCode.Decimal:
                    if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var m)) return m;
                    break;
            }
            return null;
        }

        static object ParseEnum(string name, Type type)
        {
            if (type.IsEnum)
            {
                MemberInfo[] memberInfos = type.FindMembers(MemberTypes.Field,
                    BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Static,
                    Type.FilterNameIgnoreCase, name);
                if (memberInfos.Length != 0) return ((FieldInfo)memberInfos[0]).GetValue(null);
            }
            return null;
        }

        // Return 1 if s -> t1 is a better conversion than s -> t2
        // Return -1 if s -> t2 is a better conversion than s -> t1
        // Return 0 if neither conversion is better
        static int CompareConversions(Type s, Type t1, Type t2)
        {
            if (t1 == t2) return 0;
            if (s == t1) return 1;
            if (s == t2) return -1;
            bool t1T2 = IsCompatibleWith(t1, t2);
            bool t2T1 = IsCompatibleWith(t2, t1);
            if (t1T2 && !t2T1) return 1;
            if (t2T1 && !t1T2) return -1;
            if (t1.IsSignedIntegralType() && t2.IsUnsignedIntegralType()) return 1;
            if (t2.IsSignedIntegralType() && t1.IsUnsignedIntegralType()) return -1;
            return 0;
        }

        class MethodData
        {
            public MethodBase MethodBase; public ParameterInfo[] Parameters; public Expression[] Args;
        }
    }
}
