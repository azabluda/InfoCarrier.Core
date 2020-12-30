// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public static class SqlServerShared
    {
        private static readonly ServiceProvider ServiceProvider = new ServiceCollection()
            .AddLogging(loggingBuilder => loggingBuilder.AddConsole())
            .AddEntityFrameworkSqlServer()
            .AddInfoCarrierServer()
            .BuildServiceProvider();

        public static string MasterConnectionString { get; } =
            Environment.GetEnvironmentVariable(@"SqlServer__DefaultConnection")
            ?? @"Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Connect Timeout=30";

        public static string SampleDbName => "InfoCarrierSample";

        public static string ConnectionString { get; } =
            new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = SampleDbName }.ToString();

        public static void RecreateDatabase()
        {
            // Drop database if exists
            using (var master = new SqlConnection(MasterConnectionString))
            {
                master.Open();
                using (var cmd = master.CreateCommand())
                {
                    cmd.CommandText = $@"
                        IF EXISTS (SELECT * FROM sys.databases WHERE name = N'{SampleDbName}')
                        BEGIN
                            ALTER DATABASE[{SampleDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE[{SampleDbName}];
                        END";
                    cmd.ExecuteNonQuery();
                }
            }

            SqlConnection.ClearAllPools();

            // Create and seed database
            using (var ctx = CreateDbContext())
            {
                ctx.Database.EnsureCreated();

                ctx.Blogs.Add(
                    new Blog
                    {
                        Author = new Author { Name = "hi-its-me" },
                        Posts = new List<Post>
                        {
                            new Post { Title = "my-blog-post" },
                        },
                    });

                ctx.SaveChanges();
            }
        }

        public static BloggingContext CreateDbContext(DbConnection dbConnection = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseInternalServiceProvider(ServiceProvider);
            if (dbConnection != null)
            {
                optionsBuilder.UseSqlServer(dbConnection);
            }
            else
            {
                optionsBuilder.UseSqlServer(ConnectionString);
            }

            var context = new BloggingContext(optionsBuilder.Options);
            return context;
        }

        public static IInfoCarrierServer CreateInfoCarrierServer()
            => ServiceProvider.GetRequiredService<IInfoCarrierServer>();
    }
}
