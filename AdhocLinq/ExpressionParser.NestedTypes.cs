using System;
using JetBrains.Annotations;

namespace AdhocLinq
{
    partial class ExpressionParser
    {
        struct Token { public TokenId Id; public string Text; public int Pos; }

        enum TokenId
        {
            [UsedImplicitly]
            Unknown = 0,
            End,
            Identifier,
            StringLiteral,
            IntegerLiteral,
            RealLiteral,
            Exclamation,
            Percent,
            Amphersand,
            OpenParen,
            CloseParen,
            Asterisk,
            Plus,
            Comma,
            Minus,
            Dot,
            Slash,
            Colon,
            LessThan,
            Equal,
            GreaterThan,
            Question,
            OpenBracket,
            CloseBracket,
            Bar,
            ExclamationEqual,
            DoubleAmphersand,
            LessThanEqual,
            LessGreater,
            DoubleEqual,
            GreaterThanEqual,
            DoubleBar,
            RightShiftOp,
            LeftShiftOp,
        }

        interface ILogicalSignatures
        {
            [UsedImplicitly] void F(bool x, bool y);
            [UsedImplicitly] void F(bool? x, bool? y);
        }

        interface IArithmeticSignatures
        {
            [UsedImplicitly] void F(int x, int y);
            [UsedImplicitly] void F(uint x, uint y);
            [UsedImplicitly] void F(long x, long y);
            [UsedImplicitly] void F(ulong x, ulong y);
            [UsedImplicitly] void F(float x, float y);
            [UsedImplicitly] void F(double x, double y);
            [UsedImplicitly] void F(decimal x, decimal y);
            [UsedImplicitly] void F(int? x, int? y);
            [UsedImplicitly] void F(uint? x, uint? y);
            [UsedImplicitly] void F(long? x, long? y);
            [UsedImplicitly] void F(ulong? x, ulong? y);
            [UsedImplicitly] void F(float? x, float? y);
            [UsedImplicitly] void F(double? x, double? y);
            [UsedImplicitly] void F(decimal? x, decimal? y);
        }

        interface IRelationalSignatures : IArithmeticSignatures
        {
            [UsedImplicitly] void F(string x, string y);
            [UsedImplicitly] void F(char x, char y);
            [UsedImplicitly] void F(DateTime x, DateTime y);
            [UsedImplicitly] void F(DateTimeOffset x, DateTimeOffset y);
            [UsedImplicitly] void F(TimeSpan x, TimeSpan y);
            [UsedImplicitly] void F(char? x, char? y);
            [UsedImplicitly] void F(DateTime? x, DateTime? y);
            [UsedImplicitly] void F(DateTimeOffset? x, DateTimeOffset? y);
            [UsedImplicitly] void F(TimeSpan? x, TimeSpan? y);
        }

        interface IEqualitySignatures : IRelationalSignatures
        {
            [UsedImplicitly] void F(bool x, bool y);
            [UsedImplicitly] void F(bool? x, bool? y);
            [UsedImplicitly] void F(Guid x, Guid y);
            [UsedImplicitly] void F(Guid? x, Guid? y);
            [UsedImplicitly] void F(Guid x, string y);
            [UsedImplicitly] void F(Guid? x, string y);
            [UsedImplicitly] void F(string x, Guid y);
            [UsedImplicitly] void F(string x, Guid? y);
        }

        interface IAddSignatures : IArithmeticSignatures
        {
            [UsedImplicitly] void F(DateTime x, TimeSpan y);
            [UsedImplicitly] void F(TimeSpan x, TimeSpan y);
            [UsedImplicitly] void F(DateTime? x, TimeSpan? y);
            [UsedImplicitly] void F(TimeSpan? x, TimeSpan? y);
        }

        interface ISubtractSignatures : IAddSignatures
        {
            [UsedImplicitly] void F(DateTime x, DateTime y);
            [UsedImplicitly] void F(DateTime? x, DateTime? y);
        }

        interface INegationSignatures
        {
            [UsedImplicitly] void F(int x);
            [UsedImplicitly] void F(long x);
            [UsedImplicitly] void F(float x);
            [UsedImplicitly] void F(double x);
            [UsedImplicitly] void F(decimal x);
            [UsedImplicitly] void F(int? x);
            [UsedImplicitly] void F(long? x);
            [UsedImplicitly] void F(float? x);
            [UsedImplicitly] void F(double? x);
            [UsedImplicitly] void F(decimal? x);
        }

        interface INotSignatures
        {
            [UsedImplicitly] void F(bool x);
            [UsedImplicitly] void F(bool? x);
        }

        interface IEnumerableSignatures
        {
            [UsedImplicitly] void Where(bool predicate);
            [UsedImplicitly] void Any();
            [UsedImplicitly] void Any(bool predicate);
            [UsedImplicitly] void First(bool predicate);
            [UsedImplicitly] void FirstOrDefault(bool predicate);
            [UsedImplicitly] void Single(bool predicate);
            [UsedImplicitly] void SingleOrDefault(bool predicate);
            [UsedImplicitly] void Last(bool predicate);
            [UsedImplicitly] void LastOrDefault(bool predicate);
            [UsedImplicitly] void All(bool predicate);
            [UsedImplicitly] void Count();
            [UsedImplicitly] void Count(bool predicate);
            [UsedImplicitly] void Min(object selector);
            [UsedImplicitly] void Max(object selector);
            [UsedImplicitly] void Sum(int selector);
            [UsedImplicitly] void Sum(int? selector);
            [UsedImplicitly] void Sum(long selector);
            [UsedImplicitly] void Sum(long? selector);
            [UsedImplicitly] void Sum(float selector);
            [UsedImplicitly] void Sum(float? selector);
            [UsedImplicitly] void Sum(double selector);
            [UsedImplicitly] void Sum(double? selector);
            [UsedImplicitly] void Sum(decimal selector);
            [UsedImplicitly] void Sum(decimal? selector);
            [UsedImplicitly] void Average(int selector);
            [UsedImplicitly] void Average(int? selector);
            [UsedImplicitly] void Average(long selector);
            [UsedImplicitly] void Average(long? selector);
            [UsedImplicitly] void Average(float selector);
            [UsedImplicitly] void Average(float? selector);
            [UsedImplicitly] void Average(double selector);
            [UsedImplicitly] void Average(double? selector);
            [UsedImplicitly] void Average(decimal selector);
            [UsedImplicitly] void Average(decimal? selector);
            [UsedImplicitly] void Select(object selector);
            [UsedImplicitly] void OrderBy(object selector);
            [UsedImplicitly] void OrderByDescending(object selector);
            [UsedImplicitly] void Contains(object selector);

            //Executors
            [UsedImplicitly] void Single();
            [UsedImplicitly] void SingleOrDefault();
            [UsedImplicitly] void First();
            [UsedImplicitly] void FirstOrDefault();
            [UsedImplicitly] void Last();
            [UsedImplicitly] void LastOrDefault();
        }
    }
}
