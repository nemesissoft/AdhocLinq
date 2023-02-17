using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AdhocLinq;

internal partial class ExpressionParser
{
    static readonly IReadOnlyCollection<Type> _predefinedTypes = new HashSet<Type>
    {
        typeof(object),
        typeof(bool),
        typeof(char), typeof(string),
        typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(float), typeof(double),typeof(decimal),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
        typeof(Guid),
        typeof(Math), typeof(Convert), typeof(Uri), typeof(CultureInfo),
        //TODO: ADD EF later 
        //typeof(System.Data.Objects.EntityFunctions) => typeof(System.Data.Entity.DbFunctions),
    };

    const string KEYWORD_IT = "it";
    const string KEYWORD_PARENT = "parent";
    const string KEYWORD_ROOT = "root";
    const string SYMBOL_IT = "$";
    const string SYMBOL_PARENT = "^";
    const string SYMBOL_ROOT = "~";
    const string KEYWORD_IIF = "iif";
    const string KEYWORD_NEW = "new";
    const string KEYWORD_TUPLE = "tuple";

    static readonly Dictionary<string, object> _keywords = CreateKeywords();

    readonly Dictionary<string, object> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private IDictionary<string, object> _externals;

    private readonly LambdaStore _λStore = new();

    private ParameterExpression _it, _parent, _root;
    private readonly string _text;
    private int _textPos;
    private readonly int _textLen;
    private char _ch;
    private Token _token;

    private readonly IRecognizableTypesResolver _customTypeResolver;
    private readonly INumberParserHandler _numberParserHandler;

    public ExpressionParser(ParameterExpression[] parameters, string expression, object[] values, IRecognizableTypesResolver customTypeResolver, INumberParserHandler numberParserHandler)
    {
        _customTypeResolver = customTypeResolver;
        _numberParserHandler = numberParserHandler;

        if (parameters != null) ProcessParameters(parameters);
        if (values != null) ProcessValues(values);
        _text = expression;
        _textLen = _text.Length;

        SetTextPos(0);
        NextToken();
    }

    void ProcessParameters(IReadOnlyList<ParameterExpression> parameters)
    {
        foreach (var pe in parameters)
            if (!string.IsNullOrEmpty(pe.Name))
                AddSymbol(pe.Name, pe);
        if (parameters.Count == 1 && string.IsNullOrEmpty(parameters[0].Name))
        {
            _parent = _it;
            _it = parameters[0];
            _root ??= _it;
        }
    }

    void ProcessValues(IReadOnlyList<object> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            object value = values[i];

            if (i == values.Count - 1 && value is IDictionary<string, object> externals)
                _externals = externals;
            else
                AddSymbol($"@{i.ToString(CultureInfo.InvariantCulture)}", value);
        }
    }

    void AddSymbol(string name, object value)
    {
        if (_symbols.TryGetValue(name, out var oldValue))
            throw ParsingError(_token.Pos, $"The identifier '{name}' was defined before with value '{oldValue}'");
        _symbols.Add(name, value);
    }

    public Expression Parse(Type resultType)
    {
        int exprPos = _token.Pos;
        Expression expr = ParseExpression();
        if (resultType != null)
            if (false == _λStore.TryPromoteExpression(expr, resultType, true, out expr))
                throw ParsingError(exprPos, $"Expression of type '{resultType.GetTypeName()}' expected");
        ValidateToken(TokenId.End);
        return expr;
    }

    public IEnumerable<DynamicOrdering> ParseOrdering()
    {
        var orderings = new List<DynamicOrdering>();
        while (true)
        {
            Expression expr = ParseExpression();
            bool ascending = true;
            if (TokenIdentifierIs("asc") || TokenIdentifierIs("ascending"))
                NextToken();
            else if (TokenIdentifierIs("desc") || TokenIdentifierIs("descending"))
            {
                NextToken();
                ascending = false;
            }
            orderings.Add(new DynamicOrdering { Selector = expr, Ascending = ascending });
            if (_token.Id != TokenId.Comma) break;
            NextToken();
        }
        ValidateToken(TokenId.End);
        return orderings;
    }

    // ?: operator
    Expression ParseExpression()
    {
        int errorPos = _token.Pos;
        Expression expr = ParseConditionalOr();
        if (_token.Id == TokenId.Question)
        {
            NextToken();
            Expression ifTrue = ParseExpression();
            ValidateToken(TokenId.Colon, "':' expected");
            NextToken();
            Expression ifFalse = ParseExpression();
            expr = GenerateConditional(expr, ifTrue, ifFalse, errorPos);
        }
        return expr;
    }

    // ||, or operator
    Expression ParseConditionalOr()
    {
        Expression left = ParseConditionalAnd();
        while (_token.Id == TokenId.DoubleBar || TokenIdentifierIs("or"))
        {
            Token op = _token;
            NextToken();
            Expression right = ParseConditionalAnd();
            CheckAndPromoteOperands(typeof(ILogicalSignatures), op.Text, ref left, ref right, op.Pos);
            left = Expression.OrElse(left, right);
        }
        return left;
    }

    // &&, and operator
    Expression ParseConditionalAnd()
    {
        Expression left = ParseIn();
        while (_token.Id == TokenId.DoubleAmphersand || TokenIdentifierIs("and"))
        {
            Token op = _token;
            NextToken();
            Expression right = ParseComparison();
            CheckAndPromoteOperands(typeof(ILogicalSignatures), op.Text, ref left, ref right, op.Pos);
            left = Expression.AndAlso(left, right);
        }
        return left;
    }

    // in operator for literals - example: "x in (1,2,3,4)"
    // in operator to mimic contains - example: "x in @0", compare to @0.contains(x)
    // Adapted from ticket submitted by github user mlewis9548 
    Expression ParseIn()
    {
        Expression left = ParseLogicalAndOr();
        Expression accumulate = left;

        while (TokenIdentifierIs("in"))
        {
            var op = _token;

            NextToken();
            if (_token.Id == TokenId.OpenParen) //literals (or other inline list)
            {
                Expression identitifer = left;
                while (_token.Id != TokenId.CloseParen)
                {
                    NextToken();
                    Expression right = ParsePrimary();

                    if (identitifer.Type != right.Type)
                        throw ParsingError(op.Pos, $"Expression of type '{identitifer.Type}' expected");

                    CheckAndPromoteOperands(typeof(IEqualitySignatures), "==", ref identitifer, ref right, op.Pos);

                    accumulate = accumulate.Type != typeof(bool)
                        ? GenerateEqual(identitifer, right)
                        : Expression.OrElse(accumulate, GenerateEqual(identitifer, right));

                    if (_token.Id == TokenId.End) throw ParsingError(op.Pos, "')' or ',' expected");
                }
            }
            else if (_token.Id == TokenId.Identifier) //a single argument
            {
                Expression right = ParsePrimary();

                if (!typeof(IEnumerable).IsAssignableFrom(right.Type))
                    throw ParsingError(_token.Pos, $"Identifier implementing interface '{typeof(IEnumerable).FullName}' expected");

                var args = new[] { left };

                if (FindMethod(typeof(IEnumerableSignatures), "Contains", false, args, out var containsSignature) != 1)
                    throw ParsingError(op.Pos, "No applicable aggregate method 'Contains' exists");

                var typeArgs = new[] { left.Type };
                args = new[] { right, left };

                accumulate = Expression.Call(typeof(Enumerable), containsSignature.Name, typeArgs, args);
            }
            else
                throw ParsingError(op.Pos, "'(' or Identifier expected");

            NextToken();
        }

        return accumulate;
    }

    // &, | bitwise operators
    Expression ParseLogicalAndOr()
    {
        Expression left = ParseComparison();
        while (_token.Id == TokenId.Amphersand || _token.Id == TokenId.Bar)
        {
            Token op = _token;
            NextToken();
            Expression right = ParseComparison();

            if (left.Type.IsEnum)
            {
                left = Expression.Convert(left, Enum.GetUnderlyingType(left.Type));
            }

            if (right.Type.IsEnum)
            {
                right = Expression.Convert(right, Enum.GetUnderlyingType(right.Type));
            }

            switch (op.Id)
            {
                case TokenId.Amphersand:
                    left = Expression.And(left, right);
                    break;
                case TokenId.Bar:
                    left = Expression.Or(left, right);
                    break;
            }
        }
        return left;
    }

    // =, ==, !=, <>, >, >=, <, <= operators
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
    Expression ParseComparison()
    {
        Expression left = ParseShift();
        while (_token.Id == TokenId.Equal || _token.Id == TokenId.DoubleEqual ||
               _token.Id == TokenId.ExclamationEqual || _token.Id == TokenId.LessGreater ||
               _token.Id == TokenId.GreaterThan || _token.Id == TokenId.GreaterThanEqual ||
               _token.Id == TokenId.LessThan || _token.Id == TokenId.LessThanEqual)
        {
            Token op = _token;
            NextToken();
            Expression right = ParseShift();
            bool isEquality = op.Id == TokenId.Equal || op.Id == TokenId.DoubleEqual ||
                              op.Id == TokenId.ExclamationEqual || op.Id == TokenId.LessGreater;
            if (isEquality &&
                (
                !left.Type.IsValueType && !right.Type.IsValueType
                ||
                left.Type == typeof(Guid) && right.Type == typeof(Guid)
                )
                )
            {
                if (left.Type != right.Type)
                {
                    if (left.Type.IsAssignableFrom(right.Type))
                        right = Expression.Convert(right, left.Type);
                    else if (right.Type.IsAssignableFrom(left.Type))
                        left = Expression.Convert(left, right.Type);
                    else
                        throw IncompatibleOperandsError(op.Text, left, right, op.Pos);
                }
            }
            else if (left.Type.IsEnumType() || right.Type.IsEnumType())
            {
                if (left.Type != right.Type)
                {
                    if (_λStore.TryPromoteExpression(right, left.Type, true, out var e1))
                        right = e1;
                    else if (_λStore.TryPromoteExpression(left, right.Type, true, out var e2))
                        left = e2;
                    else if (left.Type.IsEnumType() && right is ConstantExpression constantExpr1)
                        right = Expression.Constant(Enum.ToObject(left.Type, constantExpr1.Value), left.Type);
                    else if (right.Type.IsEnumType() && left is ConstantExpression constantExpr2)
                        left = Expression.Constant(Enum.ToObject(right.Type, constantExpr2.Value), right.Type);
                    else
                        throw IncompatibleOperandsError(op.Text, left, right, op.Pos);
                }
            }
            else if (typeof(ITuple).IsAssignableFrom(left.Type) && typeof(ITuple).IsAssignableFrom(right.Type))
            {
                switch (op.Id)
                {
                    case TokenId.Equal:
                    case TokenId.DoubleEqual:
                    case TokenId.ExclamationEqual:
                    case TokenId.LessGreater:
                        var equalsMethod = left.Type.GetMethods().Single(m => m.Name == nameof(Equals) && m.GetParameters() is var @params && @params.Length == 1 && @params[0].ParameterType == typeof(object));
                        var equals = Expression.Call(left, equalsMethod, Expression.Convert(right, typeof(object)));
                        return op.Id == TokenId.Equal || op.Id == TokenId.DoubleEqual
                            ? (Expression)@equals
                            : Expression.Not(@equals);

                    case TokenId.GreaterThan:
                    case TokenId.GreaterThanEqual:
                    case TokenId.LessThan:
                    case TokenId.LessThanEqual:
                        var compareToMethod = typeof(IComparable).GetMethod(nameof(IComparable.CompareTo));
                        var compare = Expression.Call(left, compareToMethod, Expression.Convert(right, typeof(object)));
                        var zeroOperand = Expression.Constant(0);
                        var @operator = op.Id switch
                        {
                            TokenId.GreaterThan => ExpressionType.GreaterThan,
                            TokenId.GreaterThanEqual => ExpressionType.GreaterThanOrEqual,
                            TokenId.LessThan => ExpressionType.LessThan,
                            TokenId.LessThanEqual => ExpressionType.LessThanOrEqual,
                            _ => throw new NotSupportedException(),
                        };
                        return Expression.MakeBinary(@operator, compare, zeroOperand);
                    default:
                        throw ParsingError(op.Pos, $"Operator '{op.Text}' is not supported for value tuple type");
                }
            }
            else
            {
                CheckAndPromoteOperands(isEquality ? typeof(IEqualitySignatures) : typeof(IRelationalSignatures),
                    op.Text, ref left, ref right, op.Pos);
            }
            switch (op.Id)
            {
                case TokenId.Equal:
                case TokenId.DoubleEqual:
                    left = GenerateEqual(left, right);
                    break;
                case TokenId.ExclamationEqual:
                case TokenId.LessGreater:
                    left = GenerateNotEqual(left, right);
                    break;
                case TokenId.GreaterThan:
                    left = GenerateComparison(left, right, ExpressionType.GreaterThan);
                    break;
                case TokenId.GreaterThanEqual:
                    left = GenerateComparison(left, right, ExpressionType.GreaterThanOrEqual);
                    break;
                case TokenId.LessThan:
                    left = GenerateComparison(left, right, ExpressionType.LessThan);
                    break;
                case TokenId.LessThanEqual:
                    left = GenerateComparison(left, right, ExpressionType.LessThanOrEqual);
                    break;
            }
        }
        return left;
    }

    // <<, >> operators
    Expression ParseShift()
    {
        Expression left = ParseAdditive();
        while (_token.Id == TokenId.LeftShiftOp || _token.Id == TokenId.RightShiftOp)
        {
            Token op = _token;
            NextToken();
            Expression right = ParseAdditive();
            switch (op.Id)
            {
                case TokenId.LeftShiftOp:
                    CheckAndPromoteOperands(typeof(IArithmeticSignatures), op.Text, ref left, ref right, op.Pos);
                    left = Expression.LeftShift(left, right);
                    break;
                case TokenId.RightShiftOp:
                    CheckAndPromoteOperands(typeof(IArithmeticSignatures), op.Text, ref left, ref right, op.Pos);
                    left = Expression.RightShift(left, right);
                    break;
            }
        }
        return left;
    }

    // +, - operators
    Expression ParseAdditive()
    {
        Expression left = ParseMultiplicative();
        while (_token.Id == TokenId.Plus || _token.Id == TokenId.Minus)
        {
            Token op = _token;
            NextToken();
            Expression right = ParseMultiplicative();
            switch (op.Id)
            {
                case TokenId.Plus:
                    if (left.Type == typeof(string) || right.Type == typeof(string))
                    {
                        left = GenerateStringConcat(left, right);
                    }
                    else
                    {
                        CheckAndPromoteOperands(typeof(IAddSignatures), op.Text, ref left, ref right, op.Pos);
                        left = GenerateAdd(left, right);
                    }
                    break;
                case TokenId.Minus:
                    CheckAndPromoteOperands(typeof(ISubtractSignatures), op.Text, ref left, ref right, op.Pos);
                    left = Expression.Subtract(left, right);
                    break;
            }
        }
        return left;
    }

    // *, /, %, mod operators
    Expression ParseMultiplicative()
    {
        Expression left = ParseUnary();
        while (_token.Id == TokenId.Asterisk || _token.Id == TokenId.Slash ||
               _token.Id == TokenId.Percent || TokenIdentifierIs("mod"))
        {
            Token op = _token;
            NextToken();
            Expression right = ParseUnary();
            CheckAndPromoteOperands(typeof(IArithmeticSignatures), op.Text, ref left, ref right, op.Pos);
            switch (op.Id)
            {
                case TokenId.Asterisk:
                    left = Expression.Multiply(left, right);
                    break;
                case TokenId.Slash:
                    left = Expression.Divide(left, right);
                    break;
                case TokenId.Percent:
                case TokenId.Identifier:
                    left = Expression.Modulo(left, right);
                    break;
            }
        }
        return left;
    }

    // -, !, not unary operators
    Expression ParseUnary()
    {
        if (_token.Id == TokenId.Minus || _token.Id == TokenId.Exclamation ||
            TokenIdentifierIs("not"))
        {
            Token op = _token;
            NextToken();
            if (op.Id == TokenId.Minus && (_token.Id == TokenId.IntegerLiteral ||
                                           _token.Id == TokenId.RealLiteral))
            {
                _token.Text = "-" + _token.Text;
                _token.Pos = op.Pos;
                return ParsePrimary();
            }
            Expression expr = ParseUnary();
            if (op.Id == TokenId.Minus)
            {
                CheckAndPromoteOperand(typeof(INegationSignatures), op.Text, ref expr, op.Pos);
                expr = Expression.Negate(expr);
            }
            else
            {
                CheckAndPromoteOperand(typeof(INotSignatures), op.Text, ref expr, op.Pos);
                expr = Expression.Not(expr);
            }
            return expr;
        }
        return ParsePrimary();
    }

    Expression ParsePrimary()
    {
        Expression expr = ParsePrimaryStart();
        while (true)
        {
            if (_token.Id == TokenId.Dot)
            {
                NextToken();
                expr = ParseMemberAccess(null, expr);
            }
            else if (_token.Id == TokenId.OpenBracket)
            {
                expr = ParseElementAccess(expr);
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    Expression ParsePrimaryStart()
    {
        return _token.Id switch
        {
            TokenId.Identifier => ParseIdentifier(),
            TokenId.StringLiteral => ParseStringLiteral(),
            TokenId.IntegerLiteral => ParseIntegerLiteral(),
            TokenId.RealLiteral => ParseRealLiteral(),
            TokenId.OpenParen => ParseParenExpression(),
            _ => throw ParsingError(_token.Pos, "Expression expected"),
        };
    }

    Expression ParseStringLiteral()
    {
        ValidateToken(TokenId.StringLiteral);
        char quote = _token.Text[0];
        string s = _token.Text[1..^1];
        int start = 0;
        while (true)
        {
            int i = s.IndexOf(quote, start);
            if (i < 0) break;
            s = s.Remove(i, 1);
            start = i + 1;
        }
        if (quote == '\'')
        {
            if (s.Length != 1)
                throw ParsingError(_token.Pos, "Character literal must contain exactly one character");
            NextToken();
            return _λStore.CreateLiteral(s[0], s);
        }
        NextToken();
        return _λStore.CreateLiteral(s, s);
    }

    Expression ParseIntegerLiteral()
    {
        ValidateToken(TokenId.IntegerLiteral);
        string text = _token.Text;
        if (!_numberParserHandler.TryParse(typeof(IIntegerNumberParser), text, out var number) || number == null)
            throw ParsingError(_token.Pos, $"Invalid integer literal '{text}'");

        NextToken();
        return _λStore.CreateLiteral(number, text);
    }

    Expression ParseRealLiteral()
    {
        ValidateToken(TokenId.RealLiteral);
        string text = _token.Text;

        if (!_numberParserHandler.TryParse(typeof(IRealNumberParser), text, out var number) || number == null)
            throw ParsingError(_token.Pos, $"Invalid real literal '{text}'");
        NextToken();
        return _λStore.CreateLiteral(number, text);
    }

    Expression ParseParenExpression()
    {
        ValidateToken(TokenId.OpenParen, "'(' expected");
        NextToken();
        Expression e = ParseExpression();
        ValidateToken(TokenId.CloseParen, "')' or operator expected");
        NextToken();
        return e;
    }

    Expression ParseIdentifier()
    {
        ValidateToken(TokenId.Identifier);
        if (_keywords.TryGetValue(_token.Text, out var value))
        {
            if (value is Type typeValue) return ParseTypeAccess(typeValue);

            if (value is string text) return ParseTextExpression(text);

            NextToken();
            return (Expression)value;
        }
        if (_customTypeResolver.CustomTypes.FirstOrDefault(t => t.Name == _token.Text) is Type customType)
        {
            return ParseTypeAccess(customType);
        }
        if (_symbols.TryGetValue(_token.Text, out value) ||
            _externals != null && _externals.TryGetValue(_token.Text, out value))
        {
            if (value is LambdaExpression lambda) return ParseLambdaInvocation(lambda);

            if (value is not Expression expr)
                expr = Expression.Constant(value);

            NextToken();
            return expr;
        }
        if (_it != null) return ParseMemberAccess(null, _it);
        throw ParsingError(_token.Pos, $"Unknown identifier '{_token.Text}'");
    }

    private Expression ParseTextExpression(string text)
    {
        return text switch
        {
            KEYWORD_IT => ParseIt(),
            KEYWORD_PARENT => ParseParent(),
            KEYWORD_ROOT => ParseRoot(),
            SYMBOL_IT => ParseIt(),
            SYMBOL_PARENT => ParseParent(),
            SYMBOL_ROOT => ParseRoot(),
            KEYWORD_IIF => ParseIif(),
            KEYWORD_NEW => ParseNew(),
            KEYWORD_TUPLE => ParseTuple(),
            _ => throw ParsingError(_token.Pos, $"'{text}' is not supported as textual entry"),
        };
    }

    Expression ParseIt()
    {
        if (_it == null) throw ParsingError(_token.Pos, "No 'it' is in scope");
        NextToken();
        return _it;
    }

    Expression ParseParent()
    {
        if (_parent == null) throw ParsingError(_token.Pos, "No 'parent' is in scope");
        NextToken();
        return _parent;
    }

    Expression ParseRoot()
    {
        if (_root == null)
            throw ParsingError(_token.Pos, "No 'root' is in scope");
        NextToken();
        return _root;
    }

    Expression ParseIif()
    {
        int errorPos = _token.Pos;
        NextToken();
        Expression[] args = ParseArgumentList();
        if (args.Length != 3)
            throw ParsingError(errorPos, "The 'iif' function requires three arguments");
        return GenerateConditional(args[0], args[1], args[2], errorPos);
    }

    Expression GenerateConditional(Expression test, Expression ifTrue, Expression ifFalse, int errorPos)
    {
        if (test.Type != typeof(bool))
            throw ParsingError(errorPos, "The first expression must be of type 'Boolean'");
        if (ifTrue.Type != ifFalse.Type)
        {
            //TODO:fix that
            Expression expr1As2 = ifFalse != LambdaStore.NullLiteral ? _λStore.PromoteExpression(ifTrue, ifFalse.Type, true) : null;
            Expression expr2As1 = ifTrue != LambdaStore.NullLiteral ? _λStore.PromoteExpression(ifFalse, ifTrue.Type, true) : null;
            if (expr1As2 != null && expr2As1 == null)
                ifTrue = expr1As2;
            else if (expr1As2 == null && expr2As1 != null)
                ifFalse = expr2As1;
            else
            {
                string type1 = ifTrue != LambdaStore.NullLiteral ? ifTrue.Type.Name : "null";
                string type2 = ifFalse != LambdaStore.NullLiteral ? ifFalse.Type.Name : "null";
                if (expr1As2 != null)
                    throw ParsingError(errorPos, $"Both of the types '{type1}' and '{type2}' convert to each other");
                throw ParsingError(errorPos, $"Neither of the types '{type1}' and '{type2}' converts to the other");
            }
        }
        return Expression.Condition(test, ifTrue, ifFalse);
    }

    Expression ParseNew()
    {
        NextToken();
        ValidateToken(TokenId.OpenParen, "'(' expected");
        NextToken();
        List<DynamicProperty> properties = new();
        List<Expression> expressions = new();
        while (true)
        {
            int exprPos = _token.Pos;
            Expression expr = ParseExpression();
            string propName;
            if (TokenIdentifierIs("as"))
            {
                NextToken();
                propName = GetIdentifier();
                NextToken();
            }
            else
            {
                if (!LambdaStore.TryGetMemberName(expr, out propName))
                    throw ParsingError(exprPos, "Expression is missing an 'as' clause");
            }

            expressions.Add(expr);
            properties.Add(new DynamicProperty(propName, expr.Type));
            if (_token.Id != TokenId.Comma) break;
            NextToken();
        }
        ValidateToken(TokenId.CloseParen, "')' or ',' expected");
        NextToken();
        Type type = ClassFactory.Instance.GetDynamicClass(properties);
        MemberBinding[] bindings = new MemberBinding[properties.Count];
        for (int i = 0; i < bindings.Length; i++)
            bindings[i] = Expression.Bind(type.GetProperty(properties[i].PropertyName) ?? throw new MissingFieldException($"Property '{properties[i].PropertyName}' does not exist in {type.Name}"),
                expressions[i]);
        return Expression.MemberInit(Expression.New(type), bindings);
    }

    Expression ParseTuple()
    {
        NextToken();
        ValidateToken(TokenId.OpenParen, "'(' expected");
        NextToken();

        var expressions = new List<Expression>();
        while (true)
        {
            expressions.Add(ParseExpression());
            if (_token.Id != TokenId.Comma) break;
            NextToken();
        }
        ValidateToken(TokenId.CloseParen, "')' or ',' expected");
        NextToken();

        if (expressions.Count == 0) throw ParsingError(_token.Pos, "Tuple should have at least one field/parameter");

        return GenerateValueTupleCompountCtor(expressions);

        Expression GenerateValueTupleCompountCtor(List<Expression> ctorParams)
        {
            while (ctorParams.Count > 8)
            {
                var lastPartitionSize = ctorParams.Count % 7;
                var rest = ctorParams.GetRange(ctorParams.Count - lastPartitionSize, lastPartitionSize);
                ctorParams.RemoveRange(ctorParams.Count - lastPartitionSize, lastPartitionSize);
                ctorParams.Add(GenerateValueTupleCompountCtor(rest));
            }

            var factoryMethod = typeof(ValueTuple).GetMethods().Single(m => m.Name == nameof(ValueTuple.Create) && m.GetParameters().Length == ctorParams.Count);
            factoryMethod = factoryMethod.MakeGenericMethod(ctorParams.Select(p => p.Type).ToArray());

            return Expression.Call(factoryMethod, ctorParams.AsEnumerable());
        }
    }

    Expression ParseLambdaInvocation(LambdaExpression lambda)
    {
        NextToken();
        Expression[] args = ParseArgumentList();
        return FindMethod(lambda.Type, "Invoke", false, args, out _) != 1
            ? throw ParsingError(_token.Pos, "Argument list incompatible with lambda expression")
            : Expression.Invoke(lambda, args);
    }

    Expression ParseTypeAccess(Type type)
    {
        int errorPos = _token.Pos;
        NextToken();
        if (_token.Id == TokenId.Question)
        {
            if (!type.IsValueType || type.IsNullableType())
                throw ParsingError(errorPos, $"Type '{type.GetTypeName()}' has no nullable form");
            type = typeof(Nullable<>).MakeGenericType(type);
            NextToken();
        }
        if (_token.Id == TokenId.OpenParen)
        {
            Expression[] args = ParseArgumentList();
            switch (_λStore.FindBestMethod(type.GetConstructors(), args, out var method))
            {
                case 0:
                    if (args.Length == 1)
                        return GenerateConversion(args[0], type, errorPos);
                    throw ParsingError(errorPos, $"No matching constructor in type '{type.GetTypeName()}'");
                case 1:
                    return Expression.New((ConstructorInfo)method, args);
                default:
                    throw ParsingError(errorPos, $"Ambiguous invocation of '{type.GetTypeName()}' constructor");
            }
        }
        ValidateToken(TokenId.Dot, "'.' or '(' expected");
        NextToken();
        return ParseMemberAccess(type, null);
    }

    static Expression GenerateConversion(Expression expr, Type type, int errorPos)
    {
        Type exprType = expr.Type;
        if (exprType == type) return expr;
        if (exprType.IsValueType && type.IsValueType)
        {
            if ((exprType.IsNullableType() || type.IsNullableType()) && exprType.GetNonNullableType() == type.GetNonNullableType())
                return Expression.Convert(expr, type);
            if ((exprType.IsNumericType() || exprType.IsEnumType()) &&
                type.IsNumericType() || type.IsEnumType())
                return Expression.ConvertChecked(expr, type);
        }
        if (exprType.IsAssignableFrom(type) || type.IsAssignableFrom(exprType) ||
            exprType.IsInterface || type.IsInterface)
            return Expression.Convert(expr, type);
        throw ParsingError(errorPos, $"A value of type '{exprType.GetTypeName()}' cannot be converted to type '{type.GetTypeName()}'");
    }

    Expression ParseMemberAccess(Type type, Expression instance)
    {
        if (instance != null) type = instance.Type;
        int errorPos = _token.Pos;
        string id = GetIdentifier();
        NextToken();
        if (_token.Id == TokenId.OpenParen)
        {
            if (instance != null && type != typeof(string))
            {
                Type enumerableType = typeof(IEnumerable<>).FindGenericType(type);
                if (enumerableType != null)
                {
                    Type elementType = enumerableType.GetGenericArguments()[0];
                    return ParseAggregate(instance, elementType, id, errorPos);
                }
            }
            Expression[] args = ParseArgumentList();
            switch (FindMethod(type, id, instance == null, args, out var mb))
            {
                case 0:
                    throw ParsingError(errorPos, $"No applicable method '{id}' exists in type '{type.GetTypeName()}'");
                case 1:
                    MethodInfo method = (MethodInfo)mb;

                    if (!IsTypeRecognizable(method.DeclaringType))
                        throw ParsingError(errorPos, $"Methods on type '{method.DeclaringType.GetTypeName()}' are not accessible");

                    /*if (method.ReturnType == typeof(void))
                        throw ParsingError(errorPos, $"Method '{id}' in type '{method.DeclaringType.GetTypeName()}' does not return a value");*/
                    return Expression.Call(instance, method, args);
                default:
                    throw ParsingError(errorPos, $"Ambiguous invocation of method '{id}' in type '{type.GetTypeName()}'");
            }
        }
        else
        {
            if (type.IsEnum)
                return Expression.Constant(Enum.Parse(type, id, true));

            var member = FindPropertyOrField(type, id, instance == null);
            return member switch
            {
                null => throw ParsingError(errorPos, $"No property or field '{id}' exists in type '{type.GetTypeName()}'"),
                PropertyInfo property => Expression.Property(instance, property),
                FieldInfo field => Expression.Field(instance, field),
                _ => throw new NotSupportedException($"{type}.{id} member (of type {member.GetType().Name}) is not supported"),
            };
        }
    }

    Expression ParseAggregate(Expression instance, Type elementType, string methodName, int errorPos)
    {
        var oldParent = _parent;

        ParameterExpression outerIt = _it;
        ParameterExpression innerIt = Expression.Parameter(elementType, "");

        _parent = _it;

        //for any method that acts on the parent element type, we need to specify the outerIt as scope.
        _it = methodName == "Contains" ? outerIt : innerIt;

        Expression[] args = ParseArgumentList();

        _it = outerIt;
        _parent = oldParent;

        if (FindMethod(typeof(IEnumerableSignatures), methodName, false, args, out var signature) != 1)
            throw ParsingError(errorPos, $"No applicable aggregate method '{methodName}' exists");
        Type[] typeArgs;
        if (
            signature.Name == "Min" ||
            signature.Name == "Max" ||
            signature.Name == "Select" ||
            signature.Name == "OrderBy" ||
            signature.Name == "OrderByDescending"
            )
        {
            typeArgs = new[] { elementType, args[0].Type };
        }
        else
        {
            typeArgs = new[] { elementType };
        }

        if (signature.Name == "Contains")
        {
            args = new[] { instance, args[0] };
        }
        else if (args.Length == 0)
        {
            args = new[] { instance };
        }
        else
        {
            args = new[] { instance, Expression.Lambda(args[0], innerIt) };
        }

        return Expression.Call(typeof(Enumerable), signature.Name, typeArgs, args);
    }

    Expression[] ParseArgumentList()
    {
        ValidateToken(TokenId.OpenParen, "'(' expected");
        NextToken();
        Expression[] args = _token.Id != TokenId.CloseParen ? ParseArguments() : Array.Empty<Expression>();
        ValidateToken(TokenId.CloseParen, "')' or ',' expected");
        NextToken();
        return args;
    }

    Expression[] ParseArguments()
    {
        List<Expression> argList = new();
        while (true)
        {
            argList.Add(ParseExpression());
            if (_token.Id != TokenId.Comma) break;
            NextToken();
        }
        return argList.ToArray();
    }

    Expression ParseElementAccess(Expression expr)
    {
        int errorPos = _token.Pos;
        ValidateToken(TokenId.OpenBracket, "'[' expected");
        NextToken();
        Expression[] args = ParseArguments();
        ValidateToken(TokenId.CloseBracket, "']' or ',' expected");
        NextToken();
        if (expr.Type.IsArray)
        {
            if (expr.Type.GetArrayRank() != 1 || args.Length != 1)
                throw ParsingError(errorPos, "Indexing of multi-dimensional arrays is not supported");

            return _λStore.TryPromoteExpression(args[0], typeof(int), true, out var index) ?
                Expression.ArrayIndex(expr, index) :
                throw ParsingError(errorPos, "Array index must be an integer expression or be convertible to it");
        }
        else
        {
            return FindIndexer(expr.Type, args, out var mb) switch
            {
                0 => throw ParsingError(errorPos, $"No applicable indexer exists in type '{expr.Type.GetTypeName()}'"),
                1 => Expression.Call(expr, (MethodInfo)mb, args),
                _ => throw ParsingError(errorPos, $"Ambiguous invocation of indexer in type '{expr.Type.GetTypeName()}'"),
            };
        }
    }

    private bool IsTypeRecognizable(Type type) => _predefinedTypes.Contains(type) || _customTypeResolver.IsTypeRecognizable(type) || typeof(ITuple).IsAssignableFrom(type);

    void CheckAndPromoteOperand(Type signatures, string opName, ref Expression expr, int errorPos)
    {
        var args = new[] { expr };
        if (FindMethod(signatures, "F", false, args, out _) != 1)
            throw ParsingError(errorPos, $"Operator '{opName}' incompatible with operand type '{args[0].Type.GetTypeName()}'");
        expr = args[0];
    }

    void CheckAndPromoteOperands(Type signatures, string opName, ref Expression left, ref Expression right,
        int errorPos)
    {
        var args = new[] { left, right };
        if (FindMethod(signatures, "F", false, args, out _) != 1)
            throw IncompatibleOperandsError(opName, left, right, errorPos);
        left = args[0];
        right = args[1];
    }

    static Exception IncompatibleOperandsError(string opName, Expression left, Expression right, int pos)
    {
        return ParsingError(pos, $"Operator '{opName}' incompatible with operand types '{left.Type.GetTypeName()}' and '{right.Type.GetTypeName()}'");
    }

    static MemberInfo FindPropertyOrField(Type type, string memberName, bool staticAccess)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly |
                             (staticAccess ? BindingFlags.Static : BindingFlags.Instance);
        foreach (Type t in SelfAndBaseTypes(type))
        {
            MemberInfo[] members = t.FindMembers(MemberTypes.Property | MemberTypes.Field,
                flags, Type.FilterNameIgnoreCase, memberName);
            if (members.Length != 0) return members[0];
        }
        return null;
    }

    int FindMethod(Type type, string methodName, bool staticAccess, Expression[] args, out MethodBase method)
    {
        BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly |
                             (staticAccess ? BindingFlags.Static : BindingFlags.Instance);
        foreach (Type t in SelfAndBaseTypes(type))
        {
            MemberInfo[] members = t.FindMembers(MemberTypes.Method,
                flags, Type.FilterNameIgnoreCase, methodName);
            int count = _λStore.FindBestMethod(members.Cast<MethodBase>(), args, out method);
            if (count != 0) return count;
        }
        method = null;
        return 0;
    }

    int FindIndexer(Type type, Expression[] args, out MethodBase method)
    {
        foreach (Type t in SelfAndBaseTypes(type))
        {
            MemberInfo[] members = t.GetDefaultMembers();
            if (members.Length != 0)
            {
                IEnumerable<MethodBase> methods = members.
                    OfType<PropertyInfo>().
                    Select(p => (MethodBase)p.GetGetMethod()).
                    Where(m => m != null);
                int count = _λStore.FindBestMethod(methods, args, out method);
                if (count != 0) return count;
            }
        }
        method = null;
        return 0;
    }

    static IEnumerable<Type> SelfAndBaseTypes(Type type)
    {
        IEnumerable<Type> SelfAndBaseClasses()
        {
            var t = type;
            while (t != null)
            {
                yield return t; t = t.BaseType;
            }
        }

        void AddInterface(ISet<Type> types, Type currentType)
        {
            if (!types.Contains(currentType))
            {
                types.Add(currentType);
                foreach (Type t in currentType.GetInterfaces()) AddInterface(types, t);
            }
        }

        if (type.IsInterface)
        {
            var types = new HashSet<Type>();
            AddInterface(types, type);
            return types;
        }
        else return SelfAndBaseClasses();
    }

    static Expression GenerateEqual(Expression left, Expression right)
    {
        PromoteToGuid(ref left, ref right);
        return Expression.Equal(left, right);
    }
    static Expression GenerateNotEqual(Expression left, Expression right)
    {
        PromoteToGuid(ref left, ref right);
        return Expression.NotEqual(left, right);
    }
    //TODO: combine GenerateComparison and GenerateEqual methods, rework PromoteExpression in lambda store

    private static readonly MethodInfo _parseMethod = typeof(Guid).GetMethod(nameof(Guid.Parse), new[] { typeof(string) })
        ?? throw new MissingMethodException($"{nameof(Guid.Parse)} method cannot be found");
    private static void PromoteToGuid(ref Expression left, ref Expression right)
    {
        if ((left.Type == typeof(Guid) || left.Type == typeof(Guid?)) && right.Type == typeof(string))
            right = Expression.Call(_parseMethod, right);
        else if ((right.Type == typeof(Guid) || right.Type == typeof(Guid?)) && left.Type == typeof(string))
            left = Expression.Call(_parseMethod, left);
    }

    static Expression GenerateComparison(Expression left, Expression right, ExpressionType comparisonType)
    {
        if (left.Type == typeof(string) || right.Type == typeof(string))
        {
            left = left.Type == typeof(string) ? left : Expression.Convert(left, typeof(string));
            right = right.Type == typeof(string) ? right : Expression.Convert(right, typeof(string));

            var comparerExp = Expression.Property(null, typeof(StringComparer), nameof(StringComparer.Ordinal));

            left = Expression.Call(comparerExp,
                typeof(IComparer<string>).GetMethod(nameof(IComparer<string>.Compare)) ?? throw new InvalidOperationException("Compare method does not exist"),
                new[] { left, right });

            right = Expression.Constant(0);
        }
        else if (left.Type.IsEnum || right.Type.IsEnum)
        {
            left = left.Type.IsEnum ? Expression.Convert(left, Enum.GetUnderlyingType(left.Type)) : left;
            right = right.Type.IsEnum ? Expression.Convert(right, Enum.GetUnderlyingType(right.Type)) : right;
        }
        return Expression.MakeBinary(comparisonType, left, right);
    }

    static Expression GenerateAdd(Expression left, Expression right)
    {
        return left.Type == typeof(string) && right.Type == typeof(string)
            ? GenerateStringConcat(left, right)
            : Expression.Add(left, right);
    }

    static Expression GenerateStringConcat(Expression left, Expression right)
    {
        var concatMethod = typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(object), typeof(object) }) ?? throw new MissingMethodException($"{nameof(string.Concat)} method cannot be found");
        var toStringMethod = typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes) ?? throw new MissingMethodException($"{nameof(ToString)} method cannot be found");

        return Expression.Call(null,
            concatMethod,
            new[]
            {
                left.Type.IsValueType ? Expression.Call(left, toStringMethod) : left,
                right.Type.IsValueType ? Expression.Call(right, toStringMethod) : right
            });
    }

    void SetTextPos(int pos)
    {
        _textPos = pos;
        _ch = _textPos < _textLen ? _text[_textPos] : '\0';
    }

    void NextChar()
    {
        if (_textPos < _textLen) _textPos++;
        _ch = _textPos < _textLen ? _text[_textPos] : '\0';
    }

    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
    void NextToken()
    {
        while (char.IsWhiteSpace(_ch)) NextChar();
        TokenId t;
        int tokenPos = _textPos;
        switch (_ch)
        {
            case '!':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    t = TokenId.ExclamationEqual;
                }
                else
                {
                    t = TokenId.Exclamation;
                }
                break;
            case '%':
                NextChar();
                t = TokenId.Percent;
                break;
            case '&':
                NextChar();
                if (_ch == '&')
                {
                    NextChar();
                    t = TokenId.DoubleAmphersand;
                }
                else
                {
                    t = TokenId.Amphersand;
                }
                break;
            case '(':
                NextChar();
                t = TokenId.OpenParen;
                break;
            case ')':
                NextChar();
                t = TokenId.CloseParen;
                break;
            case '*':
                NextChar();
                t = TokenId.Asterisk;
                break;
            case '+':
                NextChar();
                t = TokenId.Plus;
                break;
            case ',':
                NextChar();
                t = TokenId.Comma;
                break;
            case '-':
                NextChar();
                t = TokenId.Minus;
                break;
            case '.':
                NextChar();
                t = TokenId.Dot;
                break;
            case '/':
                NextChar();
                t = TokenId.Slash;
                break;
            case ':':
                NextChar();
                t = TokenId.Colon;
                break;
            case '<':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    t = TokenId.LessThanEqual;
                }
                else if (_ch == '>')
                {
                    NextChar();
                    t = TokenId.LessGreater;
                }
                else if (_ch == '<')
                {
                    NextChar();
                    t = TokenId.LeftShiftOp;
                }
                else
                {
                    t = TokenId.LessThan;
                }
                break;
            case '=':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    t = TokenId.DoubleEqual;
                }
                else
                {
                    t = TokenId.Equal;
                }
                break;
            case '>':
                NextChar();
                if (_ch == '=')
                {
                    NextChar();
                    t = TokenId.GreaterThanEqual;
                }
                else if (_ch == '>')
                {
                    NextChar();
                    t = TokenId.RightShiftOp;
                }
                else
                {
                    t = TokenId.GreaterThan;
                }
                break;
            case '?':
                NextChar();
                t = TokenId.Question;
                break;
            case '[':
                NextChar();
                t = TokenId.OpenBracket;
                break;
            case ']':
                NextChar();
                t = TokenId.CloseBracket;
                break;
            case '|':
                NextChar();
                if (_ch == '|')
                {
                    NextChar();
                    t = TokenId.DoubleBar;
                }
                else
                {
                    t = TokenId.Bar;
                }
                break;
            case '"':
            case '\'':
                char quote = _ch;
                do
                {
                    NextChar();
                    while (_textPos < _textLen && _ch != quote) NextChar();
                    if (_textPos == _textLen)
                        throw ParsingError(_textPos, "Unterminated string literal");
                    NextChar();
                } while (_ch == quote);
                t = TokenId.StringLiteral;
                break;
            default:
                if (Char.IsLetter(_ch) || _ch == '@' || _ch == '_' || _ch == '$' || _ch == '^' || _ch == '~')
                {
                    do
                    {
                        NextChar();
                    } while (Char.IsLetterOrDigit(_ch) || _ch == '_');
                    t = TokenId.Identifier;
                    break;
                }
                if (Char.IsDigit(_ch))
                {
                    t = TokenId.IntegerLiteral;
                    do
                    {
                        NextChar();
                    } while (char.IsDigit(_ch));
                    if (_ch == '.')
                    {
                        t = TokenId.RealLiteral;
                        NextChar();
                        ValidateDigit();
                        do
                        {
                            NextChar();
                        } while (char.IsDigit(_ch));
                    }
                    if (_ch == 'E' || _ch == 'e')
                    {
                        t = TokenId.RealLiteral;
                        NextChar();
                        if (_ch == '+' || _ch == '-') NextChar();
                        ValidateDigit();
                        do
                        {
                            NextChar();
                        } while (char.IsDigit(_ch));
                    }

                    while (_numberParserHandler.Suffixes.Contains(_ch)) NextChar();

                    break;
                }
                if (_textPos == _textLen)
                {
                    t = TokenId.End;
                    break;
                }
                throw ParsingError(_textPos, $"Syntax error '{_ch}'");
        }
        _token.Id = t;
        _token.Text = _text[tokenPos.._textPos];
        _token.Pos = tokenPos;
    }

    bool TokenIdentifierIs(string id)
    {
        return _token.Id == TokenId.Identifier && string.Equals(id, _token.Text, StringComparison.OrdinalIgnoreCase);
    }

    string GetIdentifier()
    {
        ValidateToken(TokenId.Identifier, "Identifier expected");
        string id = _token.Text;
        if (id.Length > 1 && id[0] == '@') id = id[1..];
        return id;
    }

    void ValidateDigit()
    {
        if (!char.IsDigit(_ch)) throw ParsingError(_textPos, "Digit expected");
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    void ValidateToken(TokenId t, string errorMessage = "Syntax error")
    {
        if (_token.Id != t) throw ParsingError(_token.Pos, errorMessage);
    }
    static Exception ParsingError(int pos, string message) => new ParsingException(message, pos);

    static Dictionary<string, object> CreateKeywords()
    {
        var keywords = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["true"] = Expression.Constant(true),
            ["false"] = Expression.Constant(false),
            ["null"] = LambdaStore.NullLiteral,
            [KEYWORD_IT] = KEYWORD_IT,
            [KEYWORD_PARENT] = KEYWORD_PARENT,
            [KEYWORD_ROOT] = KEYWORD_ROOT,
            [SYMBOL_IT] = SYMBOL_IT,
            [SYMBOL_PARENT] = SYMBOL_PARENT,
            [SYMBOL_ROOT] = SYMBOL_ROOT,
            [KEYWORD_IIF] = KEYWORD_IIF,
            [KEYWORD_NEW] = KEYWORD_NEW,
            [KEYWORD_TUPLE] = KEYWORD_TUPLE
        };

        foreach (Type type in _predefinedTypes) keywords.Add(type.Name, type);

        return keywords;
    }
}