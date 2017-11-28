// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine(@"Wait until the server is ready, then press any key to start.");
            Console.ReadKey();

            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInfoCarrierBackend(new WebApiBackendImpl());
            var options = optionsBuilder.Options;

            // Activate console logging
            using (var context = new BloggingContext(options))
            {
                context.GetService<ILoggerFactory>().AddConsole((msg, level) => true);
            }

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

                Console.WriteLine($@"Blog post '{myBlogPost.Title}' is retrieved.");
                Console.WriteLine(@"Press any key to continue.");
                Console.ReadKey();

                myBlogPost.CreationDate = DateTime.Now;
                await context.SaveChangesAsync();
                Console.WriteLine($@"CreationDate is set to '{myBlogPost.CreationDate}'.");
            }

            Console.WriteLine(@"Press any key to exit.");
            Console.ReadKey();
        }
    }
}
