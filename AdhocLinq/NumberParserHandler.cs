using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace AdhocLinq
{
    /// <summary>
    /// Collection of contracts that every number parser must implement 
    /// </summary>
    interface INumberParser
    {
        /// <summary>
        /// Determines whether given parser should be used for text handling
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        bool CanHandle(string text);

        /// <summary>
        /// Perform parsing action
        /// </summary>
        /// <param name="text"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        bool TryParse(string text, out object value);

        /// <summary>
        /// List of suffixes that mark given type 
        /// </summary>
        IEnumerable<char> Suffixes { get; }

        /// <summary>
        /// Order of precedence for chain of command pattern 
        /// </summary>
        byte Priority { get; }
    }

    interface IRealNumberParser : INumberParser { }

    interface IIntegerNumberParser : INumberParser { }

    class FloatParser : IRealNumberParser
    {
        private const char SUFFIX = 'F';
        bool INumberParser.CanHandle(string text) => NumberParserHandler.IsLastEqual(text, SUFFIX);

        bool INumberParser.TryParse(string text, out object value)
        {
            if (float.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out var f))
            {
                value = f;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<char> Suffixes { get; } = new[] { SUFFIX };
        public byte Priority => 1;
    }
    class DecimalParser : IRealNumberParser
    {
        private const char SUFFIX = 'M';
        bool INumberParser.CanHandle(string text) => NumberParserHandler.IsLastEqual(text, SUFFIX);

        bool INumberParser.TryParse(string text, out object value)
        {
            if (decimal.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            {
                value = m;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<char> Suffixes { get; } = new[] { SUFFIX };

        public byte Priority => 2;
    }

    class DoubleParser : IRealNumberParser
    {
        private const char SUFFIX = 'D';
        bool INumberParser.CanHandle(string text) => NumberParserHandler.IsLastEqual(text, SUFFIX);

        bool INumberParser.TryParse(string text, out object value)
        {
            if (double.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                value = d;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<char> Suffixes { get; } = new[] { SUFFIX };

        public byte Priority => 3;
    }

    class FallbackRealParser : IRealNumberParser
    {
        bool INumberParser.CanHandle(string text) => true;

        bool INumberParser.TryParse(string text, out object value)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                value = d;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        public IEnumerable<char> Suffixes { get; } = new char[0];

        public byte Priority => 4;
    }

    class UnsignedNumberParser : IIntegerNumberParser
    {
        bool INumberParser.CanHandle(string text) =>
            NumberParserHandler.IsPrevLastEqual(text, 'U') && (NumberParserHandler.IsLastEqual(text, 'S') || NumberParserHandler.IsLastEqual(text, 'I') || NumberParserHandler.IsLastEqual(text, 'L'))
            ||
            NumberParserHandler.IsPrevLastNotEqual(text, 'S') && NumberParserHandler.IsLastEqual(text, 'B')
            ;

        bool INumberParser.TryParse(string text, out object value)
        {
            if (NumberParserHandler.IsLastEqual(text, 'B') && byte.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out var b)) value = b;
            else if (NumberParserHandler.IsLastEqual(text, 'S') && ushort.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var us)) value = us;
            else if (NumberParserHandler.IsLastEqual(text, 'I') && uint.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var ui)) value = ui;
            else if (NumberParserHandler.IsLastEqual(text, 'L') && ulong.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var ul)) value = ul;
            else
            {
                value = null;
                return false;
            }

            return true;
        }

        public IEnumerable<char> Suffixes { get; } = new[] { 'U', 'S', 'I', 'L', 'B' };

        public byte Priority => 10;
    }

    class SignedNumberParser : IIntegerNumberParser
    {
        bool INumberParser.CanHandle(string text) =>
            NumberParserHandler.IsPrevLastNotEqual(text, 'U') && (NumberParserHandler.IsLastEqual(text, 'S') || NumberParserHandler.IsLastEqual(text, 'I') || NumberParserHandler.IsLastEqual(text, 'L'))
            ||
            NumberParserHandler.IsPrevLastEqual(text, 'S') && NumberParserHandler.IsLastEqual(text, 'B')
            ;

        bool INumberParser.TryParse(string text, out object value)
        {
            if (NumberParserHandler.IsLastEqual(text, 'B') && sbyte.TryParse(text.Substring(0, text.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out var sb)) value = sb;
            else if (NumberParserHandler.IsLastEqual(text, 'S') && short.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) value = s;
            else if (NumberParserHandler.IsLastEqual(text, 'I') && int.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var i)) value = i;
            else if (NumberParserHandler.IsLastEqual(text, 'L') && long.TryParse(text.Substring(0, text.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out var l)) value = l;
            else
            {
                value = null;
                return false;
            }

            return true;
        }

        public IEnumerable<char> Suffixes { get; } = new[] { 'S', 'I', 'L', 'B' };

        public byte Priority => 11;
    }

    class FallbackIntegerParser : IIntegerNumberParser
    {
        bool INumberParser.CanHandle(string text) => true;

        bool INumberParser.TryParse(string text, out object value)
        {
            value = null;
            if (text[0] != '-') //positive number
            {
                if (!ulong.TryParse(text, out var number)) return false;


                /*if (number <= (ulong)sbyte.MaxValue) value = (sbyte)number;
                else if (number <= byte.MaxValue) value = (byte)number;
                else if (number <= (ulong)short.MaxValue) value = (short)number;
                else if (number <= ushort.MaxValue) value = (ushort)number;
                else */
                if (number <= int.MaxValue) value = (int)number;
                else if (number <= uint.MaxValue) value = (uint)number;
                else if (number <= long.MaxValue) value = (long)number;
                else value = number;

                return true;
            }
            else
            {
                if (!long.TryParse(text, out var number)) return false;

                /*if (number >= sbyte.MinValue && number <= sbyte.MaxValue) value = (sbyte)number;
                else if (number >= short.MinValue && number <= short.MaxValue) value = (short)number;
                else */
                if (number >= int.MinValue && number <= int.MaxValue) value = (int)number;
                else value = number;

                return true;
            }
        }

        public IEnumerable<char> Suffixes { get; } = new char[0];

        public byte Priority => 12;
    }

    /// <summary>
    ///  Collection of contracts that every number parser handler must implement 
    /// </summary>
    public interface INumberParserHandler
    {
        /// <summary>
        /// List of all possible suffixes that mark given type
        /// </summary>
        ReadOnlyCollection<char> Suffixes { get; }

        /// <summary>
        /// Perform parsing action
        /// </summary>
        /// <param name="expectedParserType"></param>
        /// <param name="text"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1007:UseGenericsWhereAppropriate")]
        bool TryParse(Type expectedParserType, string text, out object value);
    }

    /// <summary>
    /// Standard implementation of number parser handler 
    /// </summary>
    public class NumberParserHandler : INumberParserHandler
    {
        internal static bool IsLastEqual(string text, char c) => char.ToUpperInvariant(text.Length >= 1 ? text[text.Length - 1] : '\0') == char.ToUpperInvariant(c);
        internal static bool IsPrevLastEqual(string text, char c) => char.ToUpperInvariant(text.Length >= 2 ? text[text.Length - 2] : '\0') == char.ToUpperInvariant(c);
        internal static bool IsPrevLastNotEqual(string text, char c) => char.ToUpperInvariant(text.Length >= 2 ? text[text.Length - 2] : '\0') != char.ToUpperInvariant(c);

        private readonly IEnumerable<INumberParser> _parsers;
        private static readonly ReadOnlyCollection<INumberParser> _defaultParsers = GetParsersFromAssembly(Assembly.GetExecutingAssembly());

        private static ReadOnlyCollection<INumberParser> GetParsersFromAssembly(Assembly assembly)
        {
            IEnumerable<Type> GetTypes(Assembly ass)
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

            return GetTypes(assembly)
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && typeof(INumberParser).IsAssignableFrom(t))
                .Select(Activator.CreateInstance).Cast<INumberParser>().OrderBy(np => np.Priority).ToList().AsReadOnly();
        }

        /// <summary>
        /// List of all possible suffixes that mark given type
        /// </summary>
        public ReadOnlyCollection<char> Suffixes { get; }

        /// <summary>
        /// Instantiates <see cref="NumberParserHandler"/> type
        /// </summary>
        /// <param name="parsers">Parsers used for chain of command pattern</param>
        private NumberParserHandler(IEnumerable<INumberParser> parsers)
        {
            _parsers = parsers;
            Suffixes = _parsers.SelectMany(p => p.Suffixes).Distinct().ToList().AsReadOnly();
        }

        /// <summary>
        /// Create chain of command pattern invoker from current assembly
        /// </summary>
        /// <returns></returns>
        public static NumberParserHandler FromAssembly() => FromAssembly(null);

        /// <summary>
        /// Create chain of command pattern invoker from given assembly
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public static NumberParserHandler FromAssembly(Assembly assembly) => new NumberParserHandler(assembly == null ? _defaultParsers : GetParsersFromAssembly(assembly));

        /// <summary>
        /// Perform parsing action across all registered parsers
        /// </summary>
        /// <param name="expectedParserType"></param>
        /// <param name="text"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryParse(Type expectedParserType, string text, out object value)
        {
            if (expectedParserType == null || !typeof(INumberParser).IsAssignableFrom(expectedParserType))
                throw new ArgumentException($"{nameof(expectedParserType)} needs to be type that extends {nameof(INumberParser)}", nameof(expectedParserType));

            if (string.IsNullOrEmpty(text)) throw new ArgumentException("Cannot parse a valid numbe from empty text", nameof(text));

            var parsers = _parsers.Where(expectedParserType.IsInstanceOfType).ToList();
            if (parsers.Count == 0) throw new InvalidOperationException($"No parser found for template {expectedParserType.FullName}");

            var parser = parsers.FirstOrDefault(p => p.CanHandle(text));

            value = null;
            if (parser == null) return false;

            return parser.TryParse(text, out value);
        }
    }
}
