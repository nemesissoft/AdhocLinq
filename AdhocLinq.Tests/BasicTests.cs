using AdhocLinq.Tests.Helpers;
using NUnit.Framework;

namespace AdhocLinq.Tests;

[TestFixture]
public class BasicTests
{
    #region Aggregates

    [Test]
    public void Any()
    {
        //Arrange
        IQueryable testListFull = User.GenerateSampleModels(100).AsQueryable();
        IQueryable testListOne = User.GenerateSampleModels(1).AsQueryable();
        IQueryable testListNone = User.GenerateSampleModels(0).AsQueryable();

        //Act
        var resultFull = testListFull.Any();
        var resultOne = testListOne.Any();
        var resultNone = testListNone.Any();

        //Assert
        Assert.IsTrue(resultFull);
        Assert.IsTrue(resultOne);
        Assert.IsFalse(resultNone);
    }

    [Test]
    public void Contains()
    {
        //Arrange
        var baseQuery = User.GenerateSampleModels(100).AsQueryable();
        var containsList = new List<string>() { "User1", "User5", "User10" };


        //Act
        var realQuery = baseQuery.Where(x => containsList.Contains(x.UserName)).Select(x => x.Id);
        var testQuery = baseQuery.Where("@0.Contains(UserName)", containsList).Select("Id");

        //Assert
        CollectionAssert.AreEqual(realQuery.ToArray(), testQuery.Cast<Guid>().ToArray());
    }

    [Test]
    public void Count()
    {
        //Arrange
        IQueryable testListFull = User.GenerateSampleModels(100).AsQueryable();
        IQueryable testListOne = User.GenerateSampleModels(1).AsQueryable();
        IQueryable testListNone = User.GenerateSampleModels(0).AsQueryable();

        //Act
        var resultFull = testListFull.Count();
        var resultOne = testListOne.Count();
        var resultNone = testListNone.Count();

        //Assert
        Assert.AreEqual(100, resultFull);
        Assert.AreEqual(1, resultOne);
        Assert.AreEqual(0, resultNone);
    }

    [Test]
    public void In()
    {
        //Arrange
        var testRange = Enumerable.Range(1, 100).ToArray();
        var testModels = User.GenerateSampleModels(10);
        var testModelByUsername =
            $"Username in (\"{testModels[0].UserName}\",\"{testModels[1].UserName}\",\"{testModels[2].UserName}\")";
        var testInExpression = new[] { 2, 4, 6, 8 };

        //Act
        var result1 = testRange.AsQueryable().Where("it in (2,4,6,8)").ToArray();
        var result2 = testModels.AsQueryable().Where(testModelByUsername).ToArray();
        var result3 = testModels.AsQueryable().Where("Id in (@0, @1, @2)", testModels[0].Id, testModels[1].Id, testModels[2].Id).ToArray();
        var result4 = testRange.AsQueryable().Where("it in @0", testInExpression).ToArray();

        //Assert
        CollectionAssert.AreEqual(new[] { 2, 4, 6, 8 }, result1);
        CollectionAssert.AreEqual(testModels.Take(3).ToArray(), result2);
        CollectionAssert.AreEqual(testModels.Take(3).ToArray(), result3);
        CollectionAssert.AreEqual(new[] { 2, 4, 6, 8 }, result4);
    }

    #endregion

    #region Adjustors

    [Test]
    public void Skip()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var resultFull = testListQry.Skip(0);
        var resultMinus1 = testListQry.Skip(1);
        var resultHalf = testListQry.Skip(50);
        var resultNone = testListQry.Skip(100);

        //Assert
        CollectionAssert.AreEqual(testList.Skip(0).ToArray(), resultFull.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Skip(1).ToArray(), resultMinus1.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Skip(50).ToArray(), resultHalf.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Skip(100).ToArray(), resultNone.Cast<User>().ToArray());
    }

    [Test]
    public void Take()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var resultFull = testListQry.Take(100);
        var resultMinus1 = testListQry.Take(99);
        var resultHalf = testListQry.Take(50);
        var resultOne = testListQry.Take(1);

        //Assert
        CollectionAssert.AreEqual(testList.Take(100).ToArray(), resultFull.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Take(99).ToArray(), resultMinus1.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Take(50).ToArray(), resultHalf.Cast<User>().ToArray());
        CollectionAssert.AreEqual(testList.Take(1).ToArray(), resultOne.Cast<User>().ToArray());
    }

    [Test]
    public void Reverse()
    {
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var result = BasicQueryable.Reverse(testListQry);

        //Assert
        CollectionAssert.AreEqual(Enumerable.Reverse(testList).ToArray(), result.Cast<User>().ToArray());
    }

    #endregion

    #region Executors

    [Test]
    public void Single()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var result = testListQry.Take(1).Single();

        //Assert
        Assert.AreEqual(testList[0].Id, result.Id);
    }

    [Test]
    public void SingleOrDefault()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var singleResult = testListQry.Take(1).SingleOrDefault();
        var defaultResult = Enumerable.Empty<User>().AsQueryable().SingleOrDefault();

        //Assert
        Assert.AreEqual(testList[0].Id, singleResult.Id);
        Assert.IsNull(defaultResult);
    }

    [Test]
    public void First()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var result = testListQry.First();

        //Assert
        Assert.AreEqual(testList[0].Id, result.Id);
    }

    [Test]
    public void FirstOrDefault()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var singleResult = testListQry.FirstOrDefault();
        // ReSharper disable once RedundantCast
        var defaultResult = ((IQueryable)Enumerable.Empty<User>().AsQueryable()).FirstOrDefault();

        //Assert
        Assert.AreEqual(testList[0].Id, singleResult.Id);
        Assert.IsNull(defaultResult);
    }


    [Test]
    public void First_AsStringExpression()
    {
        //Arrange
        var testList = User.GenerateSampleModels(100);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        Guid[] realResult = testList.OrderBy(x => x.Roles.First().Name).Select(x => x.Id).ToArray();
        IQueryable testResult = testListQry.OrderBy("Roles.First().Name").Select("Id");


        //Assert
        CollectionAssert.AreEqual(realResult, testResult.ToDynamicArray());
    }

    [Test]
    public void Single_AsStringExpression()
    {
        //Arrange
        var testList = User.GenerateSampleModels(1);
        while (testList[0].Roles.Count > 1) testList[0].Roles.RemoveAt(0);
        IQueryable testListQry = testList.AsQueryable();

        //Act
        var realResult = testList.OrderBy(x => x.Roles.Single().Name).Select(x => x.Id).ToArray();
        var testResult = testListQry.OrderBy("Roles.Single().Name").Select("Id");

        //Assert
        CollectionAssert.AreEqual(realResult, testResult.ToDynamicArray());
    }


    #endregion
}
