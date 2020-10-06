using System.Linq.Expressions;

namespace AdhocLinq
{
    internal class DynamicOrdering
    {
        public Expression Selector;
        public bool Ascending;
    }
}
