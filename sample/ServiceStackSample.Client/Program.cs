// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.Logging;
    using ServiceStack;
    using ServiceStack.Auth;

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.WriteLine(e.ExceptionObject.ToString());
                Console.WriteLine("Press Enter to exit.");
                Console.ReadLine();
                Environment.Exit(1);
            };

            Console.WriteLine($"[{DateTime.Now}] Wait until the server is ready, then press Enter to start.");
            Console.ReadKey();

            BloggingContext aliceContext = CreateContext("alice", "alice1");
            BloggingContext bobContext = CreateContext("bob", "bob2");
            bobContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            Post myBlogPost = (
                from blog in aliceContext.Blogs
                from post in blog.Posts
                join owner in aliceContext.Users on blog.OwnerId equals owner.Id
                where owner.Name == "hi-its-me"
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
                Console.WriteLine("Press Enter to continue.");
                Console.ReadKey();
            }

            aliceContext.Dispose();
            bobContext.Dispose();
        }

        private static BloggingContext CreateContext(string userName, string password)
        {
            var client = new JsonHttpClient(ServiceStackShared.BaseAddress)
            {
                HttpMessageHandler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true },
            };

            client.Post(new Authenticate
            {
                provider = CredentialsAuthProvider.Name,
                UserName = userName,
                Password = password,
                RememberMe = true,
            });

            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInfoCarrierBackend(new ServiceStackBackend(client));
            var options = optionsBuilder.Options;

            var context = new BloggingContext(options);
            context.GetService<ILoggerFactory>().AddConsole((msg, level) => true);
            return context;
        }
    }
}
