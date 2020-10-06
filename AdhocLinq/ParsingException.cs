using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace AdhocLinq
{
    /// <summary>
    /// Represents errors that occur while parsing dynamic linq string expressions.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors")]
    [Serializable]
    public sealed class ParsingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ParsingException"/> class with a specified error message and position.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="position">The location in the parsed string that produced the <see cref="ParsingException"/></param>
        public ParsingException(string message, int position): base(message) => Position = position;

        /// <summary>
        /// The location in the parsed string that produced the <see cref="ParsingException"/>.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// Creates and returns a string representation of the current exception.
        /// </summary>
        /// <returns>A string representation of the current exception.</returns>
        public override string ToString() => FormattableString.Invariant($"Error at {Position}: {Message}");

        private ParsingException(SerializationInfo info, StreamingContext context): base(info, context) => Position = (int)info.GetValue(nameof(Position), typeof(int));

        /// <summary>
        /// Supports Serialization
        /// </summary>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Position), Position);
        }
    }
}
