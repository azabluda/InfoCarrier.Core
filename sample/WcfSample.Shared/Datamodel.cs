// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

#pragma warning disable SA1402 // FileMayOnlyContainASingleType
#pragma warning disable SA1649 // FileNameMustMatchTypeName

namespace WcfSample
{
    using System;
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore;

    public class Blog
    {
        public decimal Id { get; set; }

        public decimal OwnerId { get; set; }

        public User Owner { get; set; }

        public IList<Post> Posts { get; set; }
    }

    public class Post
    {
        public decimal Id { get; set; }

        public decimal BlogId { get; set; }

        public DateTime CreationDate { get; set; }

        public string Title { get; set; }

        public Blog Blog { get; set; }
    }

    public class User
    {
        public decimal Id { get; set; }

        public string Name { get; set; }
    }

    public class BloggingContext : DbContext
    {
        public BloggingContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Blog> Blogs { get; set; }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Post>().ToTable("Posts");
        }
    }
}
