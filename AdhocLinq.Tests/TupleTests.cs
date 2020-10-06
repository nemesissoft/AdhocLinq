using System;
using System.Linq;
using System.Linq.Expressions;
using AdhocLinq.Tests.Helpers;
using NUnit.Framework;

namespace AdhocLinq.Tests
{
    [TestFixture]
    class TupleTests
    {
        [Test]
        public void ValueTuple_CreationSupport()
        {
            //Arrange
            var sut = DynamicExpressionFactory.DefaultFactory.Create();
            var tupleExpression = sut.Parse(typeof(string), "tuple(1, \"2\", 3, 44, 55, 66, 777, 888.8, 999.9, 1000).Item1 + tuple(\"ABC\").Item1");

            //Act
            var result = Expression.Lambda(tupleExpression).Compile().DynamicInvoke();

            //Assert
            Assert.That(result, Is.EqualTo("1ABC"));
        }

        [Test]
        public void ValueTuple_ReturnSupport()
        {
            //Arrange
            var testList = User.GenerateSampleModels(2);
            var testListQry = testList.AsQueryable();

            //Act
            var actual = testListQry
                    .Select("tuple(UserName, Income, Profile.FirstName)")
                    .Where("it == tuple(\"User1\", 100, \"FirstName1\")")
                    .Cast<(string, int, string)>().ToList().Single()
                ;
            var expected = testListQry
                .Where(user =>
                    new Tuple<string, int, string>(user.UserName, user.Income, user.Profile.FirstName).Equals(
                        new Tuple<string, int, string>("User1", 100, "FirstName1")))
                .Select(user => new Tuple<string, int, string>(user.UserName, user.Income, user.Profile.FirstName))
                .ToList()
                .Single();


            //Assert
            Assert.That(actual.Item1, Is.EqualTo(expected.Item1));
            Assert.That(actual.Item2, Is.EqualTo(expected.Item2));
            Assert.That(actual.Item3, Is.EqualTo(expected.Item3));
        }

        [Test]
        public void ValueTuple_NotEqual()
        {
            //Arrange
            var testList = User.GenerateSampleModels(2);
            var testListQry = testList.AsQueryable();

            //Act
            var actual = testListQry
                    .Select("tuple(UserName, Income)")
                    .Where("it != tuple(\"User1\", 100)")
                    .Cast<(string, int)>().ToList().Single()
                ;
            
            //Assert
            Assert.That(actual.Item1, Is.EqualTo("User0"));
            Assert.That(actual.Item2, Is.EqualTo(0));
        }

        [TestCase(">=", 300, 300)]
        [TestCase(">", 299, 300)]
        [TestCase("<=", 99, 0)]
        [TestCase("<", 100, 0)]
        public void ValueTuple_Comparisons(string @operator, int operand, int expectedIncome)
        {
            //Arrange
            var testList = User.GenerateSampleModels(4);//Incomes: 0, 100, 200, 300
            var testListQry = testList.AsQueryable();

            //Act
            var actual = testListQry
                    .Select("tuple(Income)")
                    .Where($"it {@operator} tuple({operand})")
                    .Cast<ValueTuple<int>>().ToList().Single()
                ;
            
            //Assert
            Assert.That(actual.Item1, Is.EqualTo(expectedIncome));
            
        }
    }
}
