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
                .SelectMany(a => a.GetTypesSafe())
                .Where(type => type.IsDefined(typeof(TypeIsRecognizableByDynamicLinqAttribute), false));

        public IEnumerable<Type> CustomTypes => _customTypes ??= new HashSet<Type>(FindTypesMarkedWithAttribute());

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
        public DynamicTypesResolver(IEnumerable<Type> customTypes) => CustomTypes = customTypes ?? Array.Empty<Type>();

        public IEnumerable<Type> CustomTypes { get; }

        public bool IsTypeRecognizable(Type type) => true;
    }

    /// <summary>
    /// Type resolver that does not recognize any additional type 
    /// </summary>
    public class EmptyTypesResolver : IRecognizableTypesResolver
    {
        public static IRecognizableTypesResolver Instance { get; } = new EmptyTypesResolver();

        public IEnumerable<Type> CustomTypes { get; } = Array.Empty<Type>();

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
        IEnumerable<Type> CustomTypes { get; }

        /// <summary>
        /// Determines whether given types should be handled 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        bool IsTypeRecognizable(Type type);
    }

}
