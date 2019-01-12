// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Android.Content;
    using global::Android.App;
    using global::Android.OS;
    using global::Android.Widget;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;

    [Activity(Label = "WcfSample.Android", MainLauncher = true, Icon = "@mipmap/icon")]
    public class MainActivity : Activity
    {
        private const string PropServerUrl = @"serverUrl";

        private readonly ISharedPreferences sharedPreferences =
            Application.Context.GetSharedPreferences(Application.Context.PackageName, FileCreationMode.Private);

        private TextView editServerUrl;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Set our view from the "main" layout resource
            this.SetContentView(Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            Button button = this.FindViewById<Button>(Resource.Id.myButton);

            button.Click += this.Button_Click;

            this.editServerUrl = this.FindViewById<TextView>(Resource.Id.editServerUrl);
            this.editServerUrl.Text = this.sharedPreferences.GetString(PropServerUrl, this.editServerUrl.Text);
        }

        private async void Button_Click(object sender, EventArgs e)
        {
            var consoleTextView = this.FindViewById<TextView>(Resource.Id.textConsole);
            void ConsoleWriteLine(string line)
            {
                consoleTextView.Text += $"{line}\n";
            }

            try
            {
                consoleTextView.Text = string.Empty;

                string serverUrl = this.editServerUrl.Text;
                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder.UseInfoCarrierClient(new WcfInfoCarrierClientXamarinImpl(serverUrl));
                var options = optionsBuilder.Options;

                using (var contextEdit = this.sharedPreferences.Edit())
                {
                    contextEdit.PutString(PropServerUrl, serverUrl);
                    contextEdit.Commit();
                }

                // Select and update
                using (var context = new BloggingContext(options))
                {
                    Post myBlogPost = await (
                        from blog in context.Blogs
                        from post in blog.Posts
                        join author in context.Authors on blog.AuthorId equals author.Id
                        where author.Name == "hi-its-me"
                        where post.Title == "my-blog-post"
                        select post).SingleAsync();

                    ConsoleWriteLine($@"Blog post '{myBlogPost.Title}' is retrieved.");

                    myBlogPost.CreationDate = DateTime.Now;
                    await context.SaveChangesAsync();
                    ConsoleWriteLine($@"CreationDate is set to '{myBlogPost.CreationDate}'.");
                }
            }
            catch (Exception exception)
            {
                ConsoleWriteLine(exception.ToString());
            }
        }

        // Xamarin.Android implementation of asynchronous WCF method calls is broken in a very
        // weird way (throws no exception but just never executes the continuation after await).
        // We have to simulate asynchrony by calling synchronous methods on the thread pool.
        private class WcfInfoCarrierClientXamarinImpl : WcfInfoCarrierClientImpl
        {
            public WcfInfoCarrierClientXamarinImpl(string serverUrl)
                : base(serverUrl)
            {
            }

            public override async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
                => await Task.Run(() => this.QueryData(request, dbContext), cancellationToken);

            public override async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
                => await Task.Run(() => this.SaveChanges(request), cancellationToken);
        }
    }
}
