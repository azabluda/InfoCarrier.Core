// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class Program
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder => loggingBuilder.AddConsole().AddFilter((msg, level) => true))
            .AddEntityFrameworkInfoCarrierClient()
            .BuildServiceProvider();

        private static async Task Main(string[] args)
        {
            Console.WriteLine(@"Wait until the server is ready, then press any key to start.");
            Console.ReadKey();

            var optionsBuilder = new DbContextOptionsBuilder()
                .UseInternalServiceProvider(ServiceProvider)
                .EnableSensitiveDataLogging()
                .UseInfoCarrierClient(new WcfInfoCarrierClientImpl());

            // Select and update
            using (var context = new BloggingContext(optionsBuilder.Options))
            {
                Post myBlogPost = (
                    from blog in context.Blogs
                    from post in blog.Posts
                    join author in context.Authors on blog.AuthorId equals author.Id
                    where author.Name == "hi-its-me"
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
