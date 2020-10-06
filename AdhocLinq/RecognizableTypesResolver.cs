using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AdhocLinq
{
    /// <summary>
    /// Indicates to Dynamic Linq to consider the Type as a valid dynamic linq type. Only <see cref="DeclarativelyMarkedTypesResolver"/> should use this attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, Inherited = false)]
    public sealed class TypeIsRecognizableByDynamicLinqAttribute : Attribute { }

    /// <summary>
    /// The default <see cref="IRecognizableTypesResolver"/>. Scans the current <see cref="AppDomain"/> for all types marked with 
    /// <see cref="TypeIsRecognizableByDynamicLinqAttribute"/>, and adds them as custom Dynamic Link types.
    /// </summary>
    public class DeclarativelyMarkedTypesResolver : IRecognizableTypesResolver
    {
        HashSet<Type> _customTypes;

        static IEnumerable<Type> FindTypesMarkedWithAttribute()
            => AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetTypes)
                .Where(type => type.IsDefined(typeof(TypeIsRecognizableByDynamicLinqAttribute), false));

        private static IEnumerable<Type> GetTypes(Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types;
            }
        }
        /// <summary>
        /// Get collection of recognizable types
        /// </summary>
        public IEnumerable<Type> CustomTypes => _customTypes ?? (_customTypes = new HashSet<Type>(FindTypesMarkedWithAttribute()));

        /// <summary>
        /// Determines whether given types should be handled
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsTypeRecognizable(Type type) => CustomTypes.Contains(type);
    }

    /// <summary>
    ///  Type resolver that recognizes any type and can load type definition on the fly 
    /// </summary>
    public class DynamicTypesResolver : IRecognizableTypesResolver
    {
        /// <summary>
        /// ctor. No type will be registered. Resolver would still try to load type definition on the fly 
        /// </summary>
        public DynamicTypesResolver() : this(null) { }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="customTypes">Types that this resolver would recognize</param>
        public DynamicTypesResolver(IEnumerable<Type> customTypes) => CustomTypes = customTypes ?? new Type[0];

        /// <summary>
        /// Get collection of recognizable types
        /// </summary>
        public IEnumerable<Type> CustomTypes { get; }

        /// <summary>
        /// Determines whether given types should be handled
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsTypeRecognizable(Type type) => true;
    }

    /// <summary>
    /// Type resolver that does not recognize any additional type 
    /// </summary>
    public class EmptyTypesResolver : IRecognizableTypesResolver
    {
        /// <summary>
        /// Cached instance of <see cref="EmptyTypesResolver"/>
        /// </summary>
        public static IRecognizableTypesResolver Instance { get; } = new EmptyTypesResolver();

        /// <summary>
        /// Get collection of recognizable types
        /// </summary>
        public IEnumerable<Type> CustomTypes { get; } = new Type[0];

        /// <summary>
        /// Determines whether given types should be handled
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool IsTypeRecognizable(Type type) => false;
    }

    /// <summary>
    /// Interface for providing custom types for Dynamic Linq.
    /// </summary>
    public interface IRecognizableTypesResolver
    {
        /// <summary>
        /// Returns a list of custom types that Dynamic Linq will understand.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
        IEnumerable<Type> CustomTypes { get; }

        /// <summary>
        /// Determines whether given types should be handled 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        bool IsTypeRecognizable(Type type);
    }

}
