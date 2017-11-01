// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace WcfSample
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.ServiceModel;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.Logging;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class MyRemoteService : IMyRemoteService
    {
        private static readonly string MasterConnectionString =
            Environment.GetEnvironmentVariable(@"SqlServer__DefaultConnection")
            ?? @"Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Connect Timeout=30";

        private static readonly string SampleDbName = "InfoCarrierWcfSample";

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
                        Owner = new User { Name = "hi-its-me" },
                        Posts = new List<Post>
                        {
                            new Post { Title = "my-blog-post" },
                        },
                    });

                ctx.SaveChanges();
            }
        }

        public static BloggingContext CreateDbContext()
        {
            var connectionString =
                new SqlConnectionStringBuilder(MasterConnectionString) { InitialCatalog = SampleDbName }.ToString();

            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseSqlServer(connectionString);
            var context = new BloggingContext(optionsBuilder.Options);
            context.GetService<ILoggerFactory>().AddConsole((msg, level) => true);
            return context;
        }

        public QueryDataResult ProcessQueryDataRequest(QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(CreateDbContext, request))
            {
                return helper.QueryData();
            }
        }

        public async Task<QueryDataResult> ProcessQueryDataRequestAsync(QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(CreateDbContext, request))
            {
                return await helper.QueryDataAsync();
            }
        }

        public SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(CreateDbContext, request))
            {
                return helper.SaveChanges();
            }
        }

        public async Task<SaveChangesResult> ProcessSaveChangesRequestAsync(SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(CreateDbContext, request))
            {
                return await helper.SaveChangesAsync();
            }
        }
    }
}