using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AdhocLinq
{
    /// <summary>
    /// Provides a set of static (Shared in Visual Basic) methods for querying data structures that implement <see cref="IQueryable"/>.
    /// It adds basic methods to <see cref="IQueryable"/> that would normally require <see cref="IQueryable{T}"/>.
    /// </summary>
    public static class BasicQueryable
    {
        #region IQueryable Adjustors

        /// <summary>
        /// Returns a specified number of contiguous elements from the start of a sequence.
        /// </summary>
        /// <param name="source">The sequence to return elements from.</param>
        /// <param name="count">The number of elements to return.</param>
        /// <returns>A <see cref="IQueryable"/> that contains the specified number of elements from the start of source.</returns>
        public static IQueryable Take(this IQueryable source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));

            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable), nameof(Queryable.Take),
                    new[] { source.ElementType },
                    source.Expression, Expression.Constant(count)));
        }

        /// <summary>
        /// Bypasses a specified number of elements in a sequence and then returns the remaining elements.
        /// </summary>
        /// <param name="source">A <see cref="IQueryable"/> to return elements from.</param>
        /// <param name="count">The number of elements to skip before returning the remaining elements.</param>
        /// <returns>A <see cref="IQueryable"/> that contains elements that occur after the specified index in the input sequence.</returns>
        public static IQueryable Skip(this IQueryable source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            //no need to skip if count is zero
            if (count == 0) return source;

            return source.Provider.CreateQuery(
                Expression.Call(
                    typeof(Queryable), nameof(Queryable.Skip),
                    new[] { source.ElementType },
                    source.Expression, Expression.Constant(count)));
        }


        /// <summary>
        /// Inverts the order of the elements in a sequence.
        /// </summary>
        /// <param name="source">A sequence of values to reverse.</param>
        /// <returns>A <see cref="IQueryable"/> whose elements correspond to those of the input sequence in reverse order.</returns>
        public static IQueryable Reverse(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.CreateQuery(Expression.Call(
                typeof(Queryable), nameof(Queryable.Reverse),
                new[] { source.ElementType }, source.Expression));
        }

        #endregion

        #region Aggregates
        /// <summary>
        /// Determines whether a sequence contains any elements.
        /// </summary>
        /// <param name="source">A sequence to check for being empty.</param>
        /// <returns>true if the source sequence contains any elements; otherwise, false.</returns>
        public static bool Any(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return (bool)source.Provider.Execute(
                Expression.Call(
                    typeof(Queryable), nameof(Queryable.Any),
                    new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Returns the number of elements in a sequence.
        /// </summary>
        /// <param name="source">The <see cref="IQueryable"/> that contains the elements to be counted.</param>
        /// <returns>The number of elements in the input sequence.</returns>
        public static int Count(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return (int)source.Provider.Execute(
                Expression.Call(
                    typeof(Queryable), nameof(Queryable.Count),
                    new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Computes the sum of a sequence of numeric values.
        /// </summary>
        /// <param name="source">A sequence of numeric values to calculate the sum of.</param>
        /// <returns>The sum of the values in the sequence.</returns>
        public static object Sum(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(
                Expression.Call(
                typeof(Queryable), nameof(Queryable.Sum),
                null,
                source.Expression));
        }

        #endregion

        #region Executors

        /// <summary>
        /// Returns the only element of a sequence, and throws an exception if there
        /// is not exactly one element in the sequence.
        /// </summary>
        /// <param name="source">A <see cref="IQueryable"/> to return the single element of.</param>
        /// <returns>The single element of the input sequence.</returns>
        public static dynamic Single(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.Single),
                new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Returns the only element of a sequence, or a default value if the sequence
        /// is empty; this method throws an exception if there is more than one element
        /// in the sequence.
        /// </summary>
        /// <param name="source">A <see cref="IQueryable"/> to return the single element of.</param>
        /// <returns>The single element of the input sequence, or default(TSource) if the sequence contains no elements.</returns>
        public static dynamic SingleOrDefault(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.SingleOrDefault),
                new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Returns the first element of a sequence.
        /// </summary>
        /// <param name="source">The <see cref="IQueryable"/> to return the first element of.</param>
        /// <returns>The first element in source.</returns>
        public static dynamic First(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.First),
                new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Returns the first element of a sequence, or a default value if the sequence contains no elements.
        /// </summary>
        /// <param name="source">The <see cref="IQueryable"/> to return the first element of.</param>
        /// <returns>default(TSource) if source is empty; otherwise, the first element in source.</returns>
        public static dynamic FirstOrDefault(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.FirstOrDefault),
                new[] { source.ElementType }, source.Expression));
        }


        /// <summary>
        /// Returns the last element of a sequence.
        /// </summary>
        /// <param name="source">The <see cref="IQueryable"/> to return the last element of.</param>
        /// <returns>The last element in source.</returns>
        public static dynamic Last(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.Last),
                new[] { source.ElementType }, source.Expression));
        }


        /// <summary>
        /// Returns the last element of a sequence, or a default value if the sequence contains no elements.
        /// </summary>
        /// <param name="source">The <see cref="IQueryable"/> to return the last element of.</param>
        /// <returns>default(TSource) if source is empty; otherwise, the last element in source.</returns>
        public static dynamic LastOrDefault(this IQueryable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Provider.Execute(Expression.Call(
                typeof(Queryable), nameof(Queryable.LastOrDefault),
                new[] { source.ElementType }, source.Expression));
        }

        /// <summary>
        /// Returns the input typed as <see cref="IEnumerable{T}"/> of dynamic.
        /// </summary>
        /// <param name="source">The sequence to type as <see cref="IEnumerable{T}"/> of dynamic.</param>
        /// <returns>The input typed as <see cref="IEnumerable{T}"/> of dynamic.</returns>
        public static IEnumerable<dynamic> AsEnumerable(this IQueryable source)
        {
            foreach (var obj in source)
                yield return obj;
        }



        /// <summary>
        /// Creates an array of dynamic objects from a <see cref="IEnumerable"/>.
        /// </summary>
        /// <param name="source">A <see cref="IEnumerable"/> to create an array from.</param>
        /// <returns>An array that contains the elements from the input sequence.</returns>
        public static dynamic[] ToDynamicArray(this IEnumerable source) => source.Cast<object>().ToArray();


        /* TODO: add tuple support
         public static IEnumerable<TTuple> ToTuples<TTuple>(this IQueryable source) where TTuple : ITuple
        {
            foreach (var obj in source)
                yield return obj;
        }*/

        #endregion


    }
}
