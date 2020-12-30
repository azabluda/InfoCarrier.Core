// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class Model
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddEntityFrameworkInfoCarrierClient()
            .BuildServiceProvider();

        private readonly LinkedList<Func<Task<string>>> steps = new LinkedList<Func<Task<string>>>();
        private LinkedListNode<Func<Task<string>>> nextStep;

        public Model()
        {
            BloggingContext aliceContext = null;
            BloggingContext bobContext = null;
            IDbContextTransaction tr = null;
            Post myBlogPost = null;

            this.steps = new LinkedList<Func<Task<string>>>(
                new Func<Task<string>>[]
                {
                    async () =>
                    {
                        aliceContext = CreateContext();
                        bobContext = CreateContext();
                        bobContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                        myBlogPost = await (
                            from blog in aliceContext.Blogs
                            from post in blog.Posts
                            join author in aliceContext.Authors on blog.AuthorId equals author.Id
                            where author.Name == "hi-its-me"
                            where post.Title == "my-blog-post"
                            select post).SingleAsync();

                        return $"Blog post '{myBlogPost.Title}' from '{myBlogPost.CreationDate}' is retrieved by Alice.";
                    },
                    async () =>
                    {
                        tr = await aliceContext.Database.BeginTransactionAsync();
                        return $"Alice started the transaction." +
                            Environment.NewLine +
                            "Quickly press Next to continue before Alice's transaction expires.";
                    },
                    async () =>
                    {
                        myBlogPost.CreationDate = DateTime.Now;
                        await aliceContext.SaveChangesAsync();
                        return $"CreationDate is set to '{myBlogPost.CreationDate}' by Alice." +
                            Environment.NewLine +
                            "Quickly press Next to continue before Alice's transaction expires.";
                    },
                    async () =>
                    {
                        Post checkBlogPost1 = await bobContext.Blogs.SelectMany(b => b.Posts).SingleAsync(p => p.Id == myBlogPost.Id);
                        return $"Blog post '{myBlogPost.Title}' retrieved by Bob, but its CreationDate is still '{checkBlogPost1.CreationDate}'." +
                            Environment.NewLine +
                            "Quickly press Next to continue before Alice's transaction expires.";
                    },
                    async () =>
                    {
                        await tr.CommitAsync();
                        await tr.DisposeAsync();
                        tr = null;
                        return "Alice committed the transaction.";
                    },
                    async () =>
                    {
                        Post checkBlogPost2 = await bobContext.Blogs.SelectMany(b => b.Posts).SingleAsync(p => p.Id == myBlogPost.Id);
                        return $"Now Bob can see the new CreationDate '{checkBlogPost2.CreationDate}'.";
                    },
                    async () =>
                    {
                        if (tr != null)
                        {
                            await tr.DisposeAsync();
                            tr = null;
                        }

                        if (aliceContext != null)
                        {
                            await aliceContext.DisposeAsync();
                            aliceContext = null;
                        }

                        if (bobContext != null)
                        {
                            await bobContext.DisposeAsync();
                            bobContext = null;
                        }

                        return "Finished. Reload the page to restart.";
                    },
                });

            this.nextStep = this.steps.First;
        }

        public List<(DateTime Date, string Content)> Output { get; } = new List<(DateTime, string)>();

        public bool IsBusy { get; private set; }

        public string NextStepName { get; private set; } = "Start";

        public async Task NextStepAsync()
        {
            this.IsBusy = true;

            string result;
            try
            {
                result = await this.nextStep.Value.Invoke();
            }
            catch (Exception ex)
            {
                result = ex.ToString();
                this.nextStep = this.steps.Last.Previous;
            }

            this.Output.Add((DateTime.Now, result));
            this.nextStep = this.nextStep.Next;
            if (this.nextStep != null)
            {
                this.NextStepName = "Next";
                this.IsBusy = false;
            }
            else
            {
                this.NextStepName = "Finished";
            }
        }

        private static BloggingContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder()
                .UseInternalServiceProvider(ServiceProvider)
                .EnableSensitiveDataLogging()
                .UseInfoCarrierClient(new WebApiInfoCarrierClientImpl());

            return new BloggingContext(optionsBuilder.Options);
        }
    }
}
