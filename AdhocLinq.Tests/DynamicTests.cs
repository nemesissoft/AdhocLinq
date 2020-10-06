using System;
using NUnit.Framework;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading.Tasks;
using AdhocLinq.Tests.Helpers;

namespace AdhocLinq.Tests
{
    [TestFixture]
    public class DynamicTests
    {
        private static readonly DynamicExpression _dynamicExpression = DynamicExpressionFactory.DefaultFactory.Create();

        [Test]
        public void Parse_ParameterExpressionMethodCall_ReturnsIntExpression()
        {
            var expression = _dynamicExpression.ParseExpression(
                Expression.Parameter(typeof(int), "x"),
                typeof(int),
                "x + 1");
            Assert.AreEqual(typeof(int), expression.Type);
        }

        [Test]
        public void Parse_TupleToStringMethodCall_ReturnsStringLambdaExpression()
        {
            var expression = _dynamicExpression.ParseLambda(
                typeof(Tuple<int>),
                typeof(string),
                "it.ToString()");
            Assert.AreEqual(typeof(string), expression.ReturnType);
        }

        [Test]
        public void Parse_ValueTuple_ReturnsStringLambdaExpression()
        {
            var expression = _dynamicExpression.ParseLambda(
                typeof(ValueTuple<int, string>),
                typeof(string),
                "it.Item1 + it.Item2");
            Assert.AreEqual(typeof(string), expression.ReturnType);

            var text = expression.Compile().DynamicInvoke(new ValueTuple<int, string>(10, "ABC"));
            Assert.That(text, Is.EqualTo("10ABC"));
        }

        [Test]
        public void ParseLambda_DelegateTypeMethodCall_ReturnsEventHandlerLambdaExpression()
        {
            var λ = _dynamicExpression.ParseLambda(
                new[] { Expression.Parameter(typeof(object), "sender"),
                    Expression.Parameter(typeof(EventArgs), "e") },
                null,
                "sender.ToString()");

            Assert.AreEqual(typeof(string), λ.ReturnType);
            Assert.AreEqual(typeof(object), λ.Parameters[0].Type);
            Assert.AreEqual(typeof(EventArgs), λ.Parameters[1].Type);
        }

        [Test]
        public void ParseLambda_VoidMethodCall_ReturnsActionDelegate()
        {
            var expression = new DynamicExpression(new DynamicTypesResolver(), NumberParserHandler.FromAssembly()).ParseLambda(
                typeof(System.IO.MemoryStream),
                typeof(void),
                "it.Close()");

            Assert.AreEqual(typeof(void), expression.ReturnType);
            Assert.AreEqual(typeof(Action<System.IO.MemoryStream>), expression.Type);

            var closeDelegate = expression.Compile();
            using (var stream =new System.IO.MemoryStream())
            {
                stream.WriteByte(15);
                closeDelegate.DynamicInvoke(stream);
                Assert.Throws<ObjectDisposedException>(() => stream.WriteByte(15));
            }
        }

        [Test]
        public void CreateClass_TheadSafe()
        {
            const int NUM_OF_TASKS = 15;

            var properties = new[] { new DynamicProperty("prop1", typeof(string)) };

            var tasks = new List<Task>(NUM_OF_TASKS);

            for (var i = 0; i < NUM_OF_TASKS; i++)
            {
                tasks.Add(Task.Factory.StartNew(() => ClassFactory.Instance.GetDynamicClass(properties)));
            }

            Task.WaitAll(tasks.ToArray());
        }

        [Test]
        public void Where()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100, allowNullableProfiles: true);
            var qry = testList.AsQueryable();


            //Act
            var userById = qry.Where("Id=@0", testList[10].Id);
            var userByUserName = qry.Where("UserName=\"User5\"");
            var nullProfileCount = qry.Where("Profile=null");
            var userByFirstName = qry.Where("Profile!=null && Profile.FirstName=@0", testList[1].Profile.FirstName);


            //Assert
            Assert.AreEqual(testList[10], userById.Single());
            Assert.AreEqual(testList[5], userByUserName.Single());
            // ReSharper disable once ReplaceWithSingleCallToCount
            Assert.AreEqual(testList.Where(x => x.Profile == null).Count(), nullProfileCount.Count());
            Assert.AreEqual(testList[1], userByFirstName.Single());
        }

        [Test]
        public void Where_Exceptions()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100, allowNullableProfiles: true);
            var qry = testList.AsQueryable();

            //Act
            Assert.Throws<ParsingException>(() => qry.Where("Id"));
            Assert.Throws<ParsingException>(() => qry.Where("Bad=3"));
            Assert.Throws<ParsingException>(() => qry.Where("Id=123"));

            Assert.Throws<ArgumentNullException>(() => DynamicQueryable.Where(null, "Id=1"));
            Assert.Throws<ArgumentException>(() => qry.Where(null));
            Assert.Throws<ArgumentException>(() => qry.Where(""));
            Assert.Throws<ArgumentException>(() => qry.Where(" "));
        }

        [Test]
        public void OrderBy()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100);
            var qry = testList.AsQueryable();


            //Act
            var orderById = qry.OrderBy("Id");
            var orderByIdDesc = qry.OrderBy("Id DESC");
            var orderByAge = qry.OrderBy("Profile.Age");
            var orderByAgeDesc = qry.OrderBy("Profile.Age DESC");
            var orderByComplex = qry.OrderBy("Profile.Age, Id");
            var orderByComplex2 = qry.OrderBy("Profile.Age DESC, Id");


            //Assert
            CollectionAssert.AreEqual(testList.OrderBy(x => x.Id).ToArray(), orderById.ToArray());
            CollectionAssert.AreEqual(testList.OrderByDescending(x => x.Id).ToArray(), orderByIdDesc.ToArray());

            CollectionAssert.AreEqual(testList.OrderBy(x => x.Profile.Age).ToArray(), orderByAge.ToArray());
            CollectionAssert.AreEqual(testList.OrderByDescending(x => x.Profile.Age).ToArray(), orderByAgeDesc.ToArray());

            CollectionAssert.AreEqual(testList.OrderBy(x => x.Profile.Age).ThenBy(x => x.Id).ToArray(), orderByComplex.ToArray());
            CollectionAssert.AreEqual(testList.OrderByDescending(x => x.Profile.Age).ThenBy(x => x.Id).ToArray(), orderByComplex2.ToArray());
        }

        [Test]
        public void OrderBy_AsStringExpression()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100);
            var qry = testList.AsQueryable();

            //Act
            var orderById = qry.SelectMany("Roles.OrderBy(Name)").Select("Name");
            var expected = qry.SelectMany(x => x.Roles.OrderBy(y => y.Name)).Select(x => x.Name);

            var orderByIdDesc = qry.SelectMany("Roles.OrderByDescending(Name)").Select("Name");
            var expectedDesc = qry.SelectMany(x => x.Roles.OrderByDescending(y => y.Name)).Select(x => x.Name);


            //Assert
            CollectionAssert.AreEqual(expected.ToArray(), orderById.Cast<string>().ToArray());
            CollectionAssert.AreEqual(expectedDesc.ToArray(), orderByIdDesc.Cast<string>().ToArray());
        }

        [Test]
        public void OrderBy_Exceptions()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100, allowNullableProfiles: true);
            var qry = testList.AsQueryable();

            //Act
            Assert.Throws<ParsingException>(() => qry.OrderBy("Bad=3"));
            Assert.Throws<ParsingException>(() => qry.Where("Id=123"));

            Assert.Throws<ArgumentNullException>(() => DynamicQueryable.OrderBy(null, "Id"));
            Assert.Throws<ArgumentException>(() => qry.OrderBy(null));
            Assert.Throws<ArgumentException>(() => qry.OrderBy(""));
            Assert.Throws<ArgumentException>(() => qry.OrderBy(" "));
        }

        [Test]
        public void Select()
        {
            //Arrange
            List<int> range = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            var testList = User.GenerateSampleModels(100);
            var qry = testList.AsQueryable();

            //Act
            IEnumerable rangeResult = range.AsQueryable().Select("it * it");
            var userNames = qry.Select("UserName");
            var userFirstName = qry.Select("new (UserName, Profile.FirstName as MyFirstName)");
            var userRoles = qry.Select("new (UserName, Roles.Select(Id) AS RoleIds)");


            //Assert
            CollectionAssert.AreEqual(range.Select(x => x * x).ToArray(), rangeResult.Cast<int>().ToArray());

            CollectionAssert.AreEqual(testList.Select(x => x.UserName).ToArray(), userNames.ToDynamicArray());
            CollectionAssert.AreEqual(
                testList.Select(x => $"{{\r\n\tUserName = {x.UserName}\r\n\tMyFirstName = {x.Profile.FirstName}\r\n}}").ToArray(),
                userFirstName.AsEnumerable().Select(x => x.ToString()).ToArray()
                );
            CollectionAssert.AreEqual(testList[0].Roles.Select(x => x.Id).ToArray(), Enumerable.ToArray(userRoles.First().RoleIds));
        }

        [Test]
        public void Select_Exceptions()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100, allowNullableProfiles: true);
            var qry = testList.AsQueryable();

            //Act
            Assert.Throws<ParsingException>(() => qry.Select("Bad"));
            Assert.Throws<ParsingException>(() => qry.Select("Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.Select("new Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.Select("new (Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.Select("new (Id, UserName, Bad)"));

            Assert.Throws<ArgumentNullException>(() => DynamicQueryable.Select(null, "Id"));
            Assert.Throws<ArgumentException>(() => qry.Select(null));
            Assert.Throws<ArgumentException>(() => qry.Select(""));
            Assert.Throws<ArgumentException>(() => qry.Select(" "));
        }

        [Test]
        public void GroupBy()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100);
            var qry = testList.AsQueryable();

            //Act
            var byAgeReturnUserName = qry.GroupBy("Profile.Age", "UserName");
            var byAgeReturnAll = qry.GroupBy("Profile.Age");

            //Assert
            Assert.AreEqual(testList.GroupBy(x => x.Profile.Age).Count(), byAgeReturnUserName.Count());
            Assert.AreEqual(testList.GroupBy(x => x.Profile.Age).Count(), byAgeReturnAll.Count());
        }

        [Test]
        public void GroupBy_Exceptions()
        {
            //Arrange
            var testList = User.GenerateSampleModels(100, allowNullableProfiles: true);
            var qry = testList.AsQueryable();

            //Act
            Assert.Throws<ParsingException>(() => qry.GroupBy("Bad"));
            Assert.Throws<ParsingException>(() => qry.GroupBy("Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.GroupBy("new Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.GroupBy("new (Id, UserName"));
            Assert.Throws<ParsingException>(() => qry.GroupBy("new (Id, UserName, Bad)"));

            // ReSharper disable once InvokeAsExtensionMethod
            // ReSharper disable once RedundantCast
            Assert.Throws<ArgumentNullException>(() => DynamicQueryable.GroupBy((IQueryable<string>)null, "Id"));
            Assert.Throws<ArgumentException>(() => qry.GroupBy(null));
            Assert.Throws<ArgumentException>(() => qry.GroupBy(""));
            Assert.Throws<ArgumentException>(() => qry.GroupBy(" "));

            Assert.Throws<ArgumentException>(() => qry.GroupBy("Id", (string)null));
            Assert.Throws<ArgumentException>(() => qry.GroupBy("Id", ""));
            Assert.Throws<ArgumentException>(() => qry.GroupBy("Id", " "));
        }

        [Test]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public void GroupByMany_StringExpressions()
        {
            var lst = new List<System.Tuple<int, int, int>>()
            {
                new System.Tuple<int, int, int>(1, 1, 1),
                new System.Tuple<int, int, int>(1, 1, 2),
                new System.Tuple<int, int, int>(1, 1, 3),
                new System.Tuple<int, int, int>(2, 2, 4),
                new System.Tuple<int, int, int>(2, 2, 5),
                new System.Tuple<int, int, int>(2, 2, 6),
                new System.Tuple<int, int, int>(2, 3, 7)
            };

            // ReSharper disable once PossibleUnintendedQueryableAsEnumerable
            var sel = lst.AsQueryable().GroupByMany("Item1", "Item2");

            Assert.AreEqual(sel.Count(), 2);
            Assert.AreEqual(sel.First().Subgroups.Count(), 1);
            Assert.AreEqual(sel.Skip(1).First().Subgroups.Count(), 2);
        }

        [Test]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        public void GroupByMany_LambdaExpressions()
        {
            var lst = new List<(int, int, int)>()
            {
                (1, 1, 1),
                (1, 1, 2),
                (1, 1, 3),
                (2, 2, 4),
                (2, 2, 5),
                (2, 2, 6),
                (2, 3, 7)
            };

            // ReSharper disable once PossibleUnintendedQueryableAsEnumerable
            var sel = lst.AsQueryable().GroupByMany(x => x.Item1, x => x.Item2);

            Assert.AreEqual(sel.Count(), 2);
            Assert.AreEqual(sel.First().Subgroups.Count(), 1);
            Assert.AreEqual(sel.Skip(1).First().Subgroups.Count(), 2);
        }

        class Person
        {
            public string Name { get; set; }
        }

        class Pet
        {
            public string Name { get; set; }
            public Person Owner { get; set; }
        }

        [Test]
        public void Join()
        {
            //Arrange
            Person magnus = new Person { Name = "Hedlund, Magnus" };
            Person terry = new Person { Name = "Adams, Terry" };
            Person charlotte = new Person { Name = "Weiss, Charlotte" };

            Pet barley = new Pet { Name = "Barley", Owner = terry };
            Pet boots = new Pet { Name = "Boots", Owner = terry };
            Pet whiskers = new Pet { Name = "Whiskers", Owner = charlotte };
            Pet daisy = new Pet { Name = "Daisy", Owner = magnus };

            List<Person> people = new List<Person> { magnus, terry, charlotte };
            List<Pet> pets = new List<Pet> { barley, boots, whiskers, daisy };


            //Act
            var realQuery = people.AsQueryable().Join(
                pets,
                person => person,
                pet => pet.Owner,
                (person, pet) =>
                new { OwnerName = person.Name, Pet = pet.Name });

            var dynamicQuery = people.AsQueryable().Join(
                pets,
                "it",
                "Owner",
                "new(outer.Name as OwnerName, inner.Name as Pet)");

            //Assert
            var realResult = realQuery.ToArray();

            var dynamicResult = dynamicQuery.ToDynamicArray();

            Assert.AreEqual(realResult.Length, dynamicResult.Length);
            for (int i = 0; i < realResult.Length; i++)
            {
                Assert.AreEqual(realResult[i].OwnerName, dynamicResult[i].OwnerName);
                Assert.AreEqual(realResult[i].Pet, dynamicResult[i].Pet);
            }
        }
    }
}
