using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using AdhocLinq.Tests.Helpers.Entities;

using Microsoft.EntityFrameworkCore;

using NUnit.Framework;

namespace AdhocLinq.Tests
{
    /// <summary>
    /// Summary description for EntitiesTests
    /// </summary>
    [TestFixture]
    public class EntitiesTests
    {
        #region Entities Test Support

        public static BlogContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<BlogContext>()
                .UseInMemoryDatabase("MyBlogs")
                .Options;

            var context = new BlogContext(options);

            context.Database.EnsureCreated();

            return context;
        }

        public static void Initialize(BlogContext context, int blogCount = 25, int postCount = 10)
        {
            var rnd = new Random(1);

            for (int i = 1; i <= blogCount; i++)
            {
                var blog = new Blog { Name = $"Blog{i}" };

                context.Blogs.Add(blog);

                for (int j = 1; j <= postCount; j++)
                {
                    var post = new Post
                    {
                        Blog = blog,
                        Content = "My Content",
                        NumberOfReads = rnd.Next(0, 5000),
                        PostDate = DateTime.Today.AddDays(-rnd.Next(0, 100)).AddSeconds(rnd.Next(0, 30000)),
                        Title = $"Blog {blog.BlogId} - Post {j}",
                    };

                    context.Posts.Add(post);
                }
            }

            context.SaveChanges();
        }

        public static void DisposeContext(ref BlogContext context)
        {
            context?.Database.EnsureDeleted();
            context?.Dispose();
            context = null;
        }

        #endregion

        BlogContext _context;

        [OneTimeSetUp]
        public void BeforeEverything() => Initialize(_context = CreateContext(), 5, 15);

        [OneTimeTearDown]
        public void AfterEverything() => DisposeContext(ref _context);


        #region Select Tests

        [Test]
        public void Entities_Where_SimplePredicate()
        {
            //Arrange
            Blog[] expected = _context.Blogs.Where(blog => blog.BlogId % 3 == 0 || blog.BlogId == 5).ToArray();

            //Act
            Blog[] actual = _context.Blogs.Where($"{nameof(Blog.BlogId)} % 3 == 0 || {nameof(Blog.BlogId)} == 5")
                .Cast<Blog>().ToArray();

            //Assert
            Assert.That(actual, Is.EquivalentTo(expected).Using(Blog.BlogComparer));
        }

        [Test]
        public void Entities_Select_SingleColumn()
        {
            //Arrange
            int[] expected = _context.Blogs.Select(x => x.BlogId).ToArray();

            //Act
            int[] actual = _context.Blogs.Select($"{nameof(Blog.BlogId)}").Cast<int>().ToArray();

            //Assert
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void Entities_Select_MultipleColumn()
        {
            //Arrange
            var expected = _context.Blogs.Select(blog => new { blog.BlogId, blog.Name }).ToArray();

            //Act
            var actual = _context.Blogs.Select($"new ({nameof(Blog.BlogId)}, {nameof(Blog.Name)})").ToDynamicArray()
                .Select(blog => new { BlogId = (int)blog.BlogId, Name = (string)blog.Name }).ToArray()
                ;

            //Assert
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void Entities_Select_BlogPosts()
        {
            //Arrange
            int[] expected = _context.Blogs.Where(blog => 2 <= blog.BlogId && blog.BlogId <= 3).SelectMany(blog => blog.Posts).Select(post => post.PostId).ToArray();

            //Act
            int[] actual = _context.Blogs.Where($"2 <= {nameof(Blog.BlogId)} && {nameof(Blog.BlogId)} <= 3")
                .SelectMany(nameof(Blog.Posts)).Select(nameof(Post.PostId)).Cast<int>().ToArray();

            //Assert
            Assert.That(actual, Is.EquivalentTo(expected));
        }

        [Test]
        public void Entities_Select_BlogAndPosts()
        {
            //Arrange
            var expected = _context.Blogs.Select(x => new { x.BlogId, x.Name, x.Posts }).ToArray();

            //Act
            var actuals = _context.Blogs.Select("new (BlogId, Name, Posts)").ToDynamicArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];
                var actual = actuals[i];

                Assert.AreEqual(expectedRow.BlogId, actual.BlogId);
                Assert.AreEqual(expectedRow.Name, actual.Name);

                Assert.That((ICollection<Post>)actual.Posts, Is.EquivalentTo(expectedRow.Posts).Using(Post.PostComparer));
            }
        }

        #endregion

        #region GroupBy Tests

        //TODO add support for grouping by multiple keys. EF Core does not support groupping by multiple keys the way it used to

        [Test]
        public void Entities_GroupBy_SingleKey()
        {
            //Arrange
            var expected = _context.Posts.AsEnumerable<Post>().GroupBy(x => x.BlogId).ToArray();

            //Act
            var actuals = _context.Posts.ToList<Post>().AsQueryable().GroupBy(nameof(Blog.BlogId)).Cast<IGrouping<int, Post>>().ToArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];

                var actual = actuals[i];

                Assert.That(actual.Key, Is.EqualTo(expectedRow.Key));

                Assert.That(actual.ToArray(), Is.EquivalentTo(expectedRow.ToArray()).Using(Post.PostComparer));
            }
        }

        [Test]
        public void Entities_GroupBy_MultiKey()
        {
            //Arrange
            var expected = _context.Posts.AsEnumerable<Post>().GroupBy(x => new { x.BlogId, x.PostDate }).ToArray();

            //Act
            var actuals = _context.Posts.ToList<Post>().AsQueryable().GroupBy("new (BlogId, PostDate)").ToDynamicArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];

                //For some reason, the DynamicBinder doesn't allow us to access values of the Group object, so we have to cast first
                var actual = (IGrouping<DynamicClass, Post>)actuals[i];

                Assert.AreEqual(expectedRow.Key.BlogId, ((dynamic)actual.Key).BlogId);
                Assert.AreEqual(expectedRow.Key.PostDate, ((dynamic)actual.Key).PostDate);

                Assert.That(actual.ToArray(), Is.EquivalentTo(expectedRow.ToArray()).Using(Post.PostComparer));
            }
        }

        [Test]
        public void Entities_GroupBy_SingleKey_SingleResult()
        {
            //Arrange
            IGrouping<DateTime, string>[] expected = _context.Posts.AsEnumerable<Post>().GroupBy(x => x.PostDate, x => x.Title).ToArray();

            //Act
            var actuals = _context.Posts.ToList<Post>().AsQueryable().GroupBy("PostDate", "Title").Cast<IGrouping<DateTime, string>>().ToArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];

                //For some reason, the DynamicBinder doesn't allow us to access values of the Group object, so we have to cast first
                var actual = actuals[i];

                Assert.AreEqual(expectedRow.Key, actual.Key);
                Assert.That(actual.ToArray(), Is.EquivalentTo(expectedRow.ToArray()));
            }
        }

        [Test]
        public void Entities_GroupBy_SingleKey_MultiResult()
        {
            //Arrange
            var expected = _context.Posts.AsEnumerable<Post>().GroupBy(x => x.PostDate, x => new { x.Title, x.Content }).ToArray();

            //Act
            var actuals = _context.Posts.ToList<Post>().AsQueryable().GroupBy("PostDate", "new (Title, Content)").ToDynamicArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];

                //For some reason, the DynamicBinder doesn't allow us to access values of the Group object, so we have to cast first
                var actual = (IGrouping<DateTime, DynamicClass>)actuals[i];

                Assert.AreEqual(expectedRow.Key, actual.Key);
                CollectionAssert.AreEqual(
                    expectedRow.ToArray(),
                    actual.Cast<dynamic>().Select(x => new { Title = (string)x.Title, Content = (string)x.Content }).ToArray());
            }
        }

        [Test]
        public void Entities_GroupBy_SingleKey_Count()
        {
            //Arrange
            var expected = _context.Posts.GroupBy(x => x.PostDate).Select(x => new { x.Key, Count = x.Count() }).ToArray();

            //Act
            var actuals = _context.Posts.GroupBy("PostDate").Select("new(Key, Count() AS Count)").ToDynamicArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];
                var actual = actuals[i];

                Assert.AreEqual(expectedRow.Key, actual.Key);
                Assert.AreEqual(expectedRow.Count, actual.Count);
            }
        }

        [Test]
        public void Entities_GroupBy_SingleKey_Sum()
        {
            //Arrange
            var expected = _context.Posts.GroupBy(x => x.PostDate).Select(x => new { x.Key, Reads = x.Sum(y => y.NumberOfReads) }).ToArray();

            //Act
            var actuals = _context.Posts.GroupBy("PostDate").Select("new(Key, Sum(NumberOfReads) AS Reads)").ToDynamicArray();

            //Assert
            Assert.AreEqual(expected.Length, actuals.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                var expectedRow = expected[i];
                var actual = actuals[i];

                Assert.AreEqual(expectedRow.Key, actual.Key);
                Assert.AreEqual(expectedRow.Reads, actual.Reads);
            }
        }

        #endregion
    }
}
