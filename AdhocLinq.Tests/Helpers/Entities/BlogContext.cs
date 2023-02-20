using System.Data.Entity;

namespace AdhocLinq.Tests.Helpers.Entities;

public interface IBlogContext
{
    public IDbSet<Blog> Blogs { get; }

    public IDbSet<Post> Posts { get; }

    int SaveChanges();
}

public class BlogContext : DbContext, IBlogContext
{
    public IDbSet<Blog> Blogs { get; set; }

    public IDbSet<Post> Posts { get; set; }
}

public class FakeBlogContext : IBlogContext
{
    public IDbSet<Blog> Blogs { get; }

    public IDbSet<Post> Posts { get; }

    public FakeBlogContext()
    {
        Blogs = new FakeBlogSet();
        Posts = new FakePostSet();
    }

    public int SaveChanges() => 0;

    class FakeBlogSet : FakeDbSet<Blog>
    {
        public override Blog Find(params object[] keyValues) =>
            Local.SingleOrDefault(b => b.BlogId == (int)keyValues.Single());
    }

    class FakePostSet : FakeDbSet<Post>
    {
        public override Post Find(params object[] keyValues) =>
            Local.SingleOrDefault(b => b.PostId == (int)keyValues.Single());
    }
}


public class Blog : IEquatable<Blog>
{
    public int BlogId { get; set; }
    public string Name { get; set; }

    public Blog(int blogId, string name)
    {
        BlogId = blogId;
        Name = name;
    }

    public virtual List<Post> Posts { get; set; } = new();

    private sealed class BlogRelationalComparer : IComparer<Blog>
    {
        public int Compare(Blog x, Blog y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return 1;
            if (x is null) return -1;
            var blogIdComparison = x.BlogId.CompareTo(y.BlogId);
            if (blogIdComparison != 0) return blogIdComparison;
            return string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }

    public static IComparer<Blog> BlogComparer { get; } = new BlogRelationalComparer();

    public override string ToString() => $"[{BlogId}=>{Name}] #Posts: {Posts.Count}";

    public override bool Equals(object obj) => Equals(obj as Blog);

    public bool Equals(Blog other) => other is not null && BlogId == other.BlogId;

    public override int GetHashCode() => BlogId.GetHashCode();
}

public class Post : IEquatable<Post>
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public virtual Blog Blog { get; set; }
    public int BlogId => Blog.BlogId;

    public DateTime PostDate { get; set; }

    public int NumberOfReads { get; set; }

    public Post(int postId, string title, string content, Blog blog, DateTime postDate, int numberOfReads)
    {
        PostId = postId;
        Title = title;
        Content = content;
        Blog = blog;
        PostDate = postDate;
        NumberOfReads = numberOfReads;
    }

    private sealed class PostRelationalComparer : IComparer<Post>
    {
        public int Compare(Post x, Post y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (y is null) return 1;
            if (x is null) return -1;
            var postIdComparison = x.PostId.CompareTo(y.PostId);
            if (postIdComparison != 0) return postIdComparison;
            var titleComparison = string.Compare(x.Title, y.Title, StringComparison.Ordinal);
            if (titleComparison != 0) return titleComparison;
            var contentComparison = string.Compare(x.Content, y.Content, StringComparison.Ordinal);
            if (contentComparison != 0) return contentComparison;
            var postDateComparison = x.PostDate.CompareTo(y.PostDate);
            if (postDateComparison != 0) return postDateComparison;
            return x.NumberOfReads.CompareTo(y.NumberOfReads);
        }
    }

    public static IComparer<Post> PostComparer { get; } = new PostRelationalComparer();

    public override string ToString() => $"[{PostId}=>'{Title}'] @Blog {BlogId}, Posted: {PostDate:o}, read {NumberOfReads}";

    public override bool Equals(object obj) => Equals(obj as Post);

    public bool Equals(Post other) => other is not null && PostId == other.PostId;

    public override int GetHashCode() => PostId.GetHashCode();
}
