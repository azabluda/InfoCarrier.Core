// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

#pragma warning disable SA1402 // FileMayOnlyContainASingleType
#pragma warning disable SA1649 // FileNameMustMatchTypeName

namespace InfoCarrierSample
{
    using System;
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore;

    public class Blog
    {
        public long Id { get; set; }

        public long AuthorId { get; set; }

        public Author Author { get; set; }

        public IList<Post> Posts { get; set; }
    }

    public class Post
    {
        public long Id { get; set; }

        public long BlogId { get; set; }

        public DateTime CreationDate { get; set; }

        public string Title { get; set; }

        public Blog Blog { get; set; }
    }

    public class Author
    {
        public long Id { get; set; }

        public string Name { get; set; }
    }

    public class BloggingContext : DbContext
    {
        public BloggingContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Blog> Blogs { get; set; }

        public DbSet<Author> Authors { get; set; }
    }
}
