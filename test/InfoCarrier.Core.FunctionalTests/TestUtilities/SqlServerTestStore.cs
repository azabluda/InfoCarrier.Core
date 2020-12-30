// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class SqlServerTestStore : InfoCarrierBackendTestStore
    {
        private readonly SqlConnection connection;
        private SqlTransaction transaction;

        public SqlServerTestStore(string name, bool shared, SharedTestStoreProperties testStoreProperties)
            : base(name, shared, testStoreProperties)
        {
            this.connection = new SqlConnection(CreateConnectionString(this.Name, true));
        }

        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
            => serviceCollection.AddEntityFrameworkSqlServer();

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => base.AddProviderOptions(builder).UseSqlServer(this.connection);

        public override DbContext CreateDbContext()
        {
            DbContext context = base.CreateDbContext();
            context.Database.UseTransaction(this.transaction);
            return context;
        }

        public override void BeginTransaction()
        {
            this.connection.Open();
            this.transaction = this.connection.BeginTransaction();
        }

        public override async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            await this.connection.OpenAsync(cancellationToken);
            this.transaction = this.connection.BeginTransaction();
        }

        public override void CommitTransaction()
        {
            this.transaction.Commit();
            this.transaction = null;
            this.connection.Close();
        }

        public override async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            await this.transaction.CommitAsync(cancellationToken);
            this.transaction = null;
            await this.connection.CloseAsync();
        }

        public override void RollbackTransaction()
        {
            this.transaction.Rollback();
            this.transaction = null;
            this.connection.Close();
        }

        public override async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            await this.transaction.RollbackAsync(cancellationToken);
            this.transaction = null;
            await this.connection.CloseAsync();
        }

        public override void Clean(DbContext context)
        {
            EnsureDatabaseDeleted(this.Name);
            context.Database.EnsureCreated();
        }

        public override void Dispose()
        {
            this.transaction?.Dispose();
            this.connection.Dispose();
            base.Dispose();
        }

        private static void EnsureDatabaseDeleted(string databaseName)
        {
            using (var master = new SqlConnection(CreateConnectionString("master", false)))
            {
                master.Open();
                using (var cmd = master.CreateCommand())
                {
                    cmd.CommandText = $@"
                        IF EXISTS (SELECT * FROM sys.databases WHERE name = N'{databaseName}')
                        BEGIN
                            ALTER DATABASE[{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE[{databaseName}];
                        END";
                    cmd.ExecuteNonQuery();
                }

                SqlConnection.ClearAllPools();
            }
        }

        private static string CreateConnectionString(string name, bool multipleActiveResultSets)
        {
            string connectionString =
                Environment.GetEnvironmentVariable(@"Test__SqlServer__DefaultConnection")
                ?? @"Data Source=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Connect Timeout=30";

            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                MultipleActiveResultSets = multipleActiveResultSets,
                InitialCatalog = name,
            };

            return builder.ToString();
        }
    }
}
