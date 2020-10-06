using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;

namespace AdhocLinq.Tests.Helpers.Entities
{

    public class BlogContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }

        public DbSet<Post> Posts { get; set; }

        [UsedImplicitly]
        public BlogContext() { }

        public BlogContext([NotNull] DbContextOptions options) : base(options) { }
    }


    public class Blog 
    {
        public int BlogId { get; set; }
        public string Name { get; set; }

        public virtual ICollection<Post> Posts { get; set; }

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
    }

    public class Post 
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public virtual Blog Blog { get; set; }
        public int BlogId => Blog.BlogId;

        public DateTime PostDate { get; set; }

        public int NumberOfReads { get; set; }

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
    }
}
