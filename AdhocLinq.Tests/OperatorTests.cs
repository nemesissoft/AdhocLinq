using System;
using System.Linq;
using System.Linq.Expressions;
using AdhocLinq.Tests.Helpers;
using NUnit.Framework;

namespace AdhocLinq.Tests
{
    [TestFixture]
    public class OperatorTests
    {
        [Test]
        public void Operator_Multiplication_Single_Float_ParseException()
        {
            //Arrange
            var models = new[] { new SimpleValuesModel() }.AsQueryable();

            //Act
            Assert.Throws<ParsingException>(() => models.Select("FloatValue * DecimalValue"));
        }

        [Test]
        public void Operator_Multiplication_Single_Float_Cast()
        {
            //Arrange
            var models = new[] { new SimpleValuesModel { FloatValue = 2, DecimalValue = 3 } }.AsQueryable();

            //Act
            var result = models.Select("Decimal(FloatValue) * DecimalValue").First();

            //Assert
            Assert.AreEqual(6.0m, result);
        }

        [TestCase("true == true", true)]
        [TestCase("false == false", true)]
        [TestCase("true != false", true)]
        [TestCase("false != true", true)]
        [TestCase("true == false", false)]

        [TestCase("15 + 10", 25)]
        [TestCase("15 - 10", 5)]
        [TestCase("15 * 10", 150)]
        [TestCase("15 / 10", 1)]
        [TestCase("15.0 / 10.0", 1.5)]
        [TestCase("80 >> 3", 10)]
        [TestCase("80 << 3", 640)]

        [TestCase("\"15\" + \"10\"", "1510")]
        [TestCase("\"15\" + 10", "1510")]
        [TestCase("10 + \"Abc\"", "10Abc")]
        [TestCase("10 + String(null)", "10")]
        [TestCase("String(null) + \"ABC\"", "ABC")]


        [TestCase("\"22222222-7651-4045-962A-3D44DEE71398\" == Guid.Parse(\"{0x22222222,0x7651,0x4045,{0x96,0x2a,0x3d,0x44,0xde,0xe7,0x13,0x98}}\")", true)]
        [TestCase("\"22222222-7651-4045-962A-3D44DEE71398\" == Guid.Parse(\"{0x22222222,0x7651,0x4045,{0x96,0x2a,0x3d,0x44,0xde,0xe7,0x13,0x97}}\")", false)]


        [TestCase("15 > 14", true)]
        [TestCase("15 > 15", false)]
        [TestCase("15 < 16", true)]
        [TestCase("15 < 14", false)]
        [TestCase("15 >= 15", true)]
        [TestCase("15 >= 16", false)]
        [TestCase("15 <= 15", true)]
        [TestCase("15 <= 14", false)]

        [TestCase("\"D\" > \"C\"", true)]
        [TestCase("\"D\" < \"E\"", true)]
        [TestCase("\"D\" <= \"D\"", true)]
        [TestCase("\"D\" >= \"D\"", true)]

        [TestCase("TestEnumOperator.Var3 > TestEnumOperator.Var2", true)]
        [TestCase("TestEnumOperator.Var3 > 1", true)]
        [TestCase("TestEnumOperator.Var3 <= TestEnumOperator.Var4", true)]
        [TestCase("TestEnumOperator.Var3 <= 4", true)]

        [TestCase("4 > 2 ? 10 : 20", 10)]
        [TestCase("4 > 2 ? (10+5.5) : 20", 15.5)]
        //[TestCase("4 > -2 ? Int16(10) : 2000", 10)]//TODO
        [TestCase("4 > -2 ? Int16(10) : 9223372036854775807", 10L)]
        //[TestCase("4 > -2 ? Int16(10) : Int32(2000)", 10)]//TODO
        public void Operator_BinaryOperationTests(string expression, object expectedResult)
        {
            //Arrange
            //var testExpression = Expression.Lambda(Expression.MakeBinary(linqExpType, Expression.Constant(left), Expression.Constant(right)));
            var sut = new DynamicExpression(new DeclarativelyMarkedTypesResolver(), NumberParserHandler.FromAssembly()).Parse(expectedResult.GetType(), expression);

            //Act
            var result = Expression.Lambda(sut).Compile().DynamicInvoke();
            //var linqExpressionResult = testExpression.Compile().DynamicInvoke();

            //Assert
            Assert.That(result, Is.EqualTo(expectedResult));
            //Assert.That(Format(linqExpressionResult), Is.EqualTo(Format(expectedResult)));
            Console.WriteLine($"{expression} -->> {result}");
        }

        [TypeIsRecognizableByDynamicLinq]
        public enum TestEnumOperator { Var1 = 0, Var2 = 1, Var3 = 2, Var4 = 4, Var5 = 8, Var6 = 16, }
        
        /*public static string Format(object obj)
        {
            //var expression = $"{Format(left)} {@operator} {Format(right)}";
            if (obj == null) return "null";
            else
                switch (Type.GetTypeCode(obj?.GetType()))
                {
                    case TypeCode.DBNull:
                    case TypeCode.Empty: return "null";

                    case TypeCode.Boolean: return (bool)obj ? "true" : "false";

                    case TypeCode.Char: return $"\'{obj}\'";

                    case TypeCode.SByte:
                    case TypeCode.Byte:
                    case TypeCode.Int16:
                    case TypeCode.UInt16:
                    case TypeCode.Int32:
                    case TypeCode.UInt32:
                    case TypeCode.Int64:
                    case TypeCode.UInt64:
                        return ((IFormattable)obj).ToString(null, CultureInfo.InvariantCulture);

                    case TypeCode.Single:
                    case TypeCode.Double:
                    case TypeCode.Decimal:
                        return ((IFormattable)obj).ToString("0.0", CultureInfo.InvariantCulture);

                    case TypeCode.DateTime:
                        return ((DateTime)obj).ToString("o", CultureInfo.InvariantCulture);

                    case TypeCode.String: return $"\"{obj}\"";

                    case TypeCode.Object:
                        return obj is IFormattable ifor
                            ? ifor.ToString(null, CultureInfo.InvariantCulture)
                            : obj.ToString();
                    default:
                        throw new ArgumentOutOfRangeException();
                }
        }*/
    }
}
