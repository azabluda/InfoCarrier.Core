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
            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            Console.WriteLine($"[{DateTime.Now}] Wait until the server is ready, then press Enter to start.");
            Console.ReadKey();

            BloggingContext aliceContext = CreateContext();
            BloggingContext bobContext = CreateContext();
            bobContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            Post myBlogPost = (
                from blog in aliceContext.Blogs
                from post in blog.Posts
                join author in aliceContext.Authors on blog.AuthorId equals author.Id
                where author.Name == "hi-its-me"
                where post.Title == "my-blog-post"
                select post).Single();

            Console.WriteLine($"[{DateTime.Now}] Blog post '{myBlogPost.Title}' from '{myBlogPost.CreationDate}' is retrieved by Alice.");
            Console.WriteLine("Press Enter to continue.");
            Console.ReadKey();

            using (await aliceContext.Database.BeginTransactionAsync())
            {
                Console.WriteLine($"[{DateTime.Now}] Alice started a transaction.");
                Console.WriteLine("Quickly press Enter to continue before Alice's transaction expires.");
                Console.ReadKey();

                myBlogPost.CreationDate = DateTime.Now;
                await aliceContext.SaveChangesAsync();
                Console.WriteLine($"[{DateTime.Now}] CreationDate is set to '{myBlogPost.CreationDate}' by Alice.");
                Console.WriteLine("Quickly press Enter to continue before Alice's transaction expires.");
                Console.ReadKey();

                Post checkBlogPost1 = bobContext.Blogs.SelectMany(b => b.Posts).Single(p => p.Id == myBlogPost.Id);
                Console.WriteLine($"[{DateTime.Now}] Blog post '{myBlogPost.Title}' retrieved by Bob, but its CreationDate is still '{checkBlogPost1.CreationDate}'.");
                Console.WriteLine("Quickly press Enter to continue before Alice's transaction expires.");
                Console.ReadKey();

                aliceContext.Database.CommitTransaction();
                Console.WriteLine($"[{DateTime.Now}] Alice commits the transaction.");
                Console.WriteLine("Press Enter to continue.");
                Console.ReadKey();

                Post checkBlogPost2 = bobContext.Blogs.SelectMany(b => b.Posts).Single(p => p.Id == myBlogPost.Id);
                Console.WriteLine($"[{DateTime.Now}] Now Bob can see the new CreationDate '{checkBlogPost2.CreationDate}'.");
                Console.WriteLine("Press Enter to exit.");
                Console.ReadKey();
            }

            aliceContext.Dispose();
            bobContext.Dispose();
        }

        private static BloggingContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInfoCarrierClient(new WebApiInfoCarrierClientImpl());
            var options = optionsBuilder.Options;

            BloggingContext context = new BloggingContext(options);
            context.GetService<ILoggerFactory>().AddConsole((msg, level) => true);
            return context;
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ExceptionObject.ToString());
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
            Environment.Exit(1);
        }
    }
}
