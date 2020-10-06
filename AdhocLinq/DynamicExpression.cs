using System;
using System.Linq.Expressions;

namespace AdhocLinq
{
    /// <summary>
    /// Façade around dynamic LINQ parsing library - single point of entry 
    /// </summary>
    public class DynamicExpression
    {
        private readonly IRecognizableTypesResolver _customTypeResolver;
        private readonly INumberParserHandler _numberParserHandler;

        /// <summary>
        /// create façade
        /// </summary>
        /// <param name="customTypeResolver"></param>
        /// <param name="numberParserHandler"></param>
        public DynamicExpression(IRecognizableTypesResolver customTypeResolver, INumberParserHandler numberParserHandler)
        {
            _customTypeResolver = customTypeResolver;
            _numberParserHandler = numberParserHandler;
        }

        /// <summary>
        /// Parse expression with no parameter 
        /// </summary>
        /// <param name="resultType"></param>
        /// <param name="expression"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public Expression Parse(Type resultType, string expression, params object[] values)
            => new ExpressionParser(null, expression, values, _customTypeResolver, _numberParserHandler).Parse(resultType);

        /// <summary>
        /// Parse expression with parameter type 
        /// </summary>
        /// <param name="itParameter"></param>
        /// <param name="resultType"></param>
        /// <param name="expression"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public Expression ParseExpression(ParameterExpression itParameter, Type resultType, string expression, params object[] values)
            => new ExpressionParser(new[] { itParameter }, expression, values, _customTypeResolver, _numberParserHandler).Parse(resultType);

        /// <summary>
        /// Parse lambda of given type 
        /// </summary>
        /// <param name="itType"></param>
        /// <param name="resultType"></param>
        /// <param name="expression"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public LambdaExpression ParseLambda(Type itType, Type resultType, string expression, params object[] values)
            => ParseLambda(new[] { Expression.Parameter(itType, "") }, resultType, expression, values);

        /// <summary>
        /// Parse lambda with multiple parameters 
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="resultType"></param>
        /// <param name="expression"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public LambdaExpression ParseLambda(ParameterExpression[] parameters, Type resultType, string expression, params object[] values)
            => Expression.Lambda(new ExpressionParser(parameters, expression, values, _customTypeResolver, _numberParserHandler).Parse(resultType), parameters);

        /// <summary>
        /// Parse strongly typed lambda expression 
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="expression"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public Expression<Func<TEntity, TResult>> ParseLambda<TEntity, TResult>(string expression, params object[] values)
             => (Expression<Func<TEntity, TResult>>)ParseLambda(typeof(TEntity), typeof(TResult), expression, values);
    }

    /// <summary>
    /// Expression Parser façade factory 
    /// </summary>
    public class DynamicExpressionFactory
    {
        private readonly IRecognizableTypesResolver _customTypeResolver;
        private readonly INumberParserHandler _numberParserHandler;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="customTypeResolver"></param>
        /// <param name="numberParserHandler"></param>
        public DynamicExpressionFactory(IRecognizableTypesResolver customTypeResolver, INumberParserHandler numberParserHandler)
        {
            _customTypeResolver = customTypeResolver;
            _numberParserHandler = numberParserHandler;
        }

        /// <summary>
        /// Create façade based on current factory settings
        /// </summary>
        /// <returns></returns>
        public DynamicExpression Create() => new DynamicExpression(_customTypeResolver, _numberParserHandler);

        /// <summary>
        /// Get cached version of default factory
        /// </summary>
        public static DynamicExpressionFactory DefaultFactory { get; } = new DynamicExpressionFactory(new DeclarativelyMarkedTypesResolver(), NumberParserHandler.FromAssembly());
    }
}
