// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class Model : IAsyncDisposable
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddEntityFrameworkInfoCarrierClient()
            .BuildServiceProvider();

        private readonly HttpClient httpClient;
        private readonly IAsyncEnumerator<string> enumerator;

        public Model(HttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.enumerator = this.EntityFrameworkDemo().GetAsyncEnumerator();
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
                await this.enumerator.MoveNextAsync();
                result = this.enumerator.Current;
                if (result == null)
                {
                    this.Output.Add((DateTime.Now, "That's all folks. Reload the page to restart."));
                    this.NextStepName = "Finished";
                }
                else
                {
                    this.Output.Add((DateTime.Now, result));
                    this.NextStepName = "Next";
                    this.IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                this.Output.Add((DateTime.Now, ex.ToString()));
                this.Output.Add((DateTime.Now, "Oops... Alice's transaction expired. Reload the page to restart."));
                this.NextStepName = "Finished";
            }
        }

        public ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        private async IAsyncEnumerable<string> EntityFrameworkDemo()
        {
            using BloggingContext aliceContext = this.CreateContext();
            using BloggingContext bobContext = this.CreateContext();
            bobContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            Post myBlogPost = await (
                from blog in aliceContext.Blogs
                from post in blog.Posts
                join author in aliceContext.Authors on blog.AuthorId equals author.Id
                where author.Name == "hi-its-me"
                where post.Title == "my-blog-post"
                select post).SingleAsync();

            yield return $"Blog post '{myBlogPost.Title}' from '{myBlogPost.CreationDate}' is retrieved by Alice.";

            await using (var tr = await aliceContext.Database.BeginTransactionAsync())
            {
                yield return $"Alice started the transaction." +
                    Environment.NewLine +
                    "Quickly press Next to continue before Alice's transaction expires.";

                myBlogPost.CreationDate = DateTime.Now;
                await aliceContext.SaveChangesAsync();
                yield return $"CreationDate is set to '{myBlogPost.CreationDate}' by Alice." +
                    Environment.NewLine +
                    "Quickly press Next to continue before Alice's transaction expires.";

                Post checkBlogPost1 = await bobContext.Blogs.SelectMany(b => b.Posts).SingleAsync(p => p.Id == myBlogPost.Id);
                yield return $"Blog post '{myBlogPost.Title}' retrieved by Bob, but its CreationDate is still '{checkBlogPost1.CreationDate}'." +
                    Environment.NewLine +
                    "Quickly press Next to continue before Alice's transaction expires.";

                await tr.CommitAsync();
                yield return "Alice committed the transaction.";

                Post checkBlogPost2 = await bobContext.Blogs.SelectMany(b => b.Posts).SingleAsync(p => p.Id == myBlogPost.Id);
                yield return $"Now Bob can see the new CreationDate '{checkBlogPost2.CreationDate}'.";
            }

            yield return null;
        }

        private BloggingContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder()
                .UseInternalServiceProvider(ServiceProvider)
                .EnableSensitiveDataLogging()
                .UseInfoCarrierClient(new WebApiInfoCarrierClientImpl(this.httpClient));

            return new BloggingContext(optionsBuilder.Options);
        }
    }
}
