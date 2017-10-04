namespace WcfSample
{
    using System;
    using System.Collections.Generic;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using System.Linq;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@"Wait until the server is ready, then press any key to start.");
            Console.ReadKey();

            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInfoCarrierBackend(new WcfBackendImpl());
            var options = optionsBuilder.Options;

            // Activate console logging
            using (var context = new BloggingContext(options))
                context.GetService<ILoggerFactory>().AddConsole((_, __) => true);

            // Seed database
            using (var context = new BloggingContext(options))
            {
                context.Blogs.Add(
                    new Blog
                    {
                        Owner = new User { Name = "hi-its-me" },
                        Posts = new List<Post>
                        {
                            new Post { Title = "my-blog-post" }
                        }
                    });

                context.SaveChanges();
            }

            Console.WriteLine(@"Database is seeded. Press any key to continue.");
            Console.ReadKey();

            // Select and update
            using (var context = new BloggingContext(options))
            {
                Post myBlogPost = (
                    from blog in context.Blogs
                    from post in blog.Posts
                    join owner in context.Users on blog.OwnerId equals owner.Id
                    where owner.Name == "hi-its-me"
                    where post.Title == "my-blog-post"
                    select post).Single();

                Console.WriteLine($@"Blog post '{myBlogPost.Title}' is retrieved. Press any key to continue.");
                Console.ReadKey();

                myBlogPost.CreationDate = DateTime.Now;
                context.SaveChanges();
                Console.WriteLine($@"CreationDate is set to '{myBlogPost.CreationDate}'.");
            }

            Console.WriteLine(@"Press any key to exit.");
            Console.ReadKey();
        }
    }
}
