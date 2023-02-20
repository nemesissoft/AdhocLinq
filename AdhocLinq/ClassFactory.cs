using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace AdhocLinq
{
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "There is only ever one instance of this class, and it should never be destroyed except on AppDomain termination.")]
    internal class ClassFactory
    {
        public static readonly ClassFactory Instance = new();

        readonly ModuleBuilder _module;
        readonly Dictionary<Signature, Type> _classes;
        int _classCount;
        readonly ReaderWriterLockSlim _rwLock;

        private ClassFactory()
        {
            AssemblyName name = new($"{typeof(ClassFactory).Namespace}.DynamicClasses");
            AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#if ENABLE_LINQ_PARTIAL_TRUST
            new ReflectionPermission(PermissionState.Unrestricted).Assert();
#endif
            try
            {
                _module = builder.DefineDynamicModule($"{typeof(ClassFactory).Namespace}.DynamicClasses");
            }
            // ReSharper disable once RedundantEmptyFinallyBlock
            finally
            {
#if ENABLE_LINQ_PARTIAL_TRUST
                PermissionSet.RevertAssert();
#endif
            }
            _classes = new Dictionary<Signature, Type>();

            _rwLock = new ReaderWriterLockSlim();
        }

        public Type GetDynamicClass(IEnumerable<DynamicProperty> properties)
        {
            Signature signature = new(properties);

            _rwLock.EnterReadLock();

            try
            {
                if (_classes.TryGetValue(signature, out var type)) return type;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            return CreateDynamicClass(signature);
        }

        Type CreateDynamicClass(Signature signature)
        {
            _rwLock.EnterWriteLock();

            try
            {
                //do a final check to make sure the type hasn't been generated.
                if (_classes.TryGetValue(signature, out var type)) return type;


                string typeName = "DynamicClass" + (_classCount + 1);
#if ENABLE_LINQ_PARTIAL_TRUST
                new ReflectionPermission(PermissionState.Unrestricted).Assert();
#endif
                try
                {
                    TypeBuilder tb = _module.DefineType(typeName, TypeAttributes.Class |
                        TypeAttributes.Public, typeof(DynamicClass));
                    FieldInfo[] fields = GenerateProperties(tb, signature.Properties);
                    GenerateEquals(tb, fields);
                    GenerateGetHashCode(tb, fields);

                    Type result = tb.CreateType();
                    _classCount++;

                    _classes.Add(signature, result);

                    return result;
                }
                // ReSharper disable once RedundantEmptyFinallyBlock
                finally
                {
#if ENABLE_LINQ_PARTIAL_TRUST
                    PermissionSet.RevertAssert();
#endif
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        static FieldInfo[] GenerateProperties(TypeBuilder tb, DynamicProperty[] properties)
        {
            var fields = new FieldInfo[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                DynamicProperty dp = properties[i];
                FieldBuilder fb = tb.DefineField("_" + dp.PropertyName, dp.PropertyType, FieldAttributes.Private);
                PropertyBuilder pb = tb.DefineProperty(dp.PropertyName, PropertyAttributes.HasDefault, dp.PropertyType, null);
                MethodBuilder mbGet = tb.DefineMethod("get_" + dp.PropertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    dp.PropertyType, Type.EmptyTypes);
                ILGenerator genGet = mbGet.GetILGenerator();
                genGet.Emit(OpCodes.Ldarg_0);
                genGet.Emit(OpCodes.Ldfld, fb);
                genGet.Emit(OpCodes.Ret);
                MethodBuilder mbSet = tb.DefineMethod("set_" + dp.PropertyName,
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, new[] { dp.PropertyType });
                ILGenerator genSet = mbSet.GetILGenerator();
                genSet.Emit(OpCodes.Ldarg_0);
                genSet.Emit(OpCodes.Ldarg_1);
                genSet.Emit(OpCodes.Stfld, fb);
                genSet.Emit(OpCodes.Ret);
                pb.SetGetMethod(mbGet);
                pb.SetSetMethod(mbSet);
                fields[i] = fb;
            }
            return fields;
        }

        static void GenerateEquals(TypeBuilder tb, FieldInfo[] fields)
        {
            MethodBuilder mb = tb.DefineMethod("Equals",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(bool), new[] { typeof(object) });
            ILGenerator gen = mb.GetILGenerator();
            LocalBuilder other = gen.DeclareLocal(tb);
            Label next = gen.DefineLabel();
            gen.Emit(OpCodes.Ldarg_1);
            gen.Emit(OpCodes.Isinst, tb);
            gen.Emit(OpCodes.Stloc, other);
            gen.Emit(OpCodes.Ldloc, other);
            gen.Emit(OpCodes.Brtrue_S, next);
            gen.Emit(OpCodes.Ldc_I4_0);
            gen.Emit(OpCodes.Ret);
            gen.MarkLabel(next);
            foreach (FieldInfo field in fields)
            {
                Type ft = field.FieldType;
                Type ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                next = gen.DefineLabel();
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default") ?? throw new InvalidOperationException("No get_Default method"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.Emit(OpCodes.Ldloc, other);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("Equals", new[] { ft, ft }) ?? throw new InvalidOperationException("No Equals method"), null);
                gen.Emit(OpCodes.Brtrue_S, next);
                gen.Emit(OpCodes.Ldc_I4_0);
                gen.Emit(OpCodes.Ret);
                gen.MarkLabel(next);
            }
            gen.Emit(OpCodes.Ldc_I4_1);
            gen.Emit(OpCodes.Ret);
        }

        static void GenerateGetHashCode(TypeBuilder tb, FieldInfo[] fields)
        {
            MethodBuilder mb = tb.DefineMethod("GetHashCode",
                MethodAttributes.Public | MethodAttributes.ReuseSlot |
                MethodAttributes.Virtual | MethodAttributes.HideBySig,
                typeof(int), Type.EmptyTypes);
            ILGenerator gen = mb.GetILGenerator();
            gen.Emit(OpCodes.Ldc_I4_0);
            foreach (FieldInfo field in fields)
            {
                Type ft = field.FieldType;
                Type ct = typeof(EqualityComparer<>).MakeGenericType(ft);
                gen.EmitCall(OpCodes.Call, ct.GetMethod("get_Default") ?? throw new InvalidOperationException("No get_Default method"), null);
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Ldfld, field);
                gen.EmitCall(OpCodes.Callvirt, ct.GetMethod("GetHashCode", new[] { ft }) ?? throw new InvalidOperationException("No GetHashCode method"), null);
                gen.Emit(OpCodes.Xor);
            }
            gen.Emit(OpCodes.Ret);
        }


        class Signature : IEquatable<Signature>
        {
            public readonly DynamicProperty[] Properties;
            private readonly int _hashCode;

            public Signature(IEnumerable<DynamicProperty> properties)
            {
                Properties = properties.ToArray();
                _hashCode = 0;
                foreach (DynamicProperty p in Properties)
                    _hashCode ^= p.PropertyName.GetHashCode() ^ p.PropertyType.GetHashCode();
            }

            public override int GetHashCode() => _hashCode;

            public override bool Equals(object other) => other is Signature otherSignature && Equals(otherSignature);

            public bool Equals(Signature other)
            {
                if (Properties.Length != other?.Properties.Length) return false;

                return !Properties.Where((prop, i) => prop.PropertyName != other.Properties[i].PropertyName || prop.PropertyType != other.Properties[i].PropertyType).Any();
            }
        }
    }

}
