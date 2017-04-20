namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class SqlServerTestStore : RelationalTestStore
    {
        public SqlServerTestStore(
            string databaseName,
            IServiceProvider seriveProvider,
            InfoCarrierTestHelper<SqlServerTestStore> helper)
        {
            string connectionString = CreateConnectionString(databaseName, true);
            var connection = new SqlConnection(connectionString);

            this.ConnectionString = connectionString;
            this.Connection = connection;
            this.SqlServerOptions = new DbContextOptionsBuilder()
                .UseSqlServer(connection)
                .UseInternalServiceProvider(seriveProvider)
                .Options;
            this.InfoCarrierOptions = helper.BuildInfoCarrierOptions(this);
        }

        public DbContextOptions SqlServerOptions { get; }

        public DbContextOptions InfoCarrierOptions { get; }

        public override string ConnectionString { get; }

        public override DbConnection Connection { get; }

        public override DbTransaction Transaction => null;

        private static string CreateConnectionString(string name, bool multipleActiveResultSets)
        {
            var builder = new SqlConnectionStringBuilder("Data Source=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;Connect Timeout=30")
            {
                MultipleActiveResultSets = multipleActiveResultSets,
                InitialCatalog = name
            };

            return builder.ToString();
        }
    }
}
