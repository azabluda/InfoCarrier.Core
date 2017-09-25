namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class SqlServerTestStore<TDbContext> : TestStoreImplBase<TDbContext>
        where TDbContext : DbContext
    {
        private readonly Func<TestStoreBase> fromShared;
        private readonly DbConnection connection;
        private DbTransaction transaction;

        private SqlServerTestStore(
            Func<DbContextOptions, TDbContext, TDbContext> createContext,
            Action<IServiceCollection> configureStoreService,
            Action<TDbContext> initializeDatabase,
            string databaseName)
            : base(
                  createContext,
                  c =>
                  {
                      if (initializeDatabase != null)
                      {
                          EnsureDatabaseDeleted(databaseName);
                          c.Database.EnsureCreated();
                          initializeDatabase(c);
                      }
                  })
        {
            this.connection = new SqlConnection(CreateConnectionString(databaseName, true));

            var serviceCollection = new ServiceCollection().AddEntityFrameworkSqlServer();
            configureStoreService(serviceCollection);

            this.DbContextOptions = new DbContextOptionsBuilder()
                .UseSqlServer(this.connection)
                .UseInternalServiceProvider(serviceCollection.BuildServiceProvider())
                .Options;

            this.fromShared = () => new SqlServerTestStore<TDbContext>(createContext, configureStoreService, null, databaseName);
        }

        protected override DbContextOptions DbContextOptions { get; }

        public override TDbContext CreateStoreContext(TDbContext clientDbContext)
        {
            var context = base.CreateStoreContext(clientDbContext);
            context.Database.UseTransaction(this.transaction);
            return context;
        }

        public override TestStoreBase FromShared()
        {
            this.EnsureInitialized();
            return this.fromShared();
        }

        public override void BeginTransaction()
        {
            this.EnsureInitialized();
            this.connection.Open();
            this.transaction = this.connection.BeginTransaction();
        }

        public override async Task BeginTransactionAsync()
        {
            this.EnsureInitialized();
            await this.connection.OpenAsync();
            this.transaction = this.connection.BeginTransaction();
        }

        public override void CommitTransaction()
        {
            this.transaction.Commit();
            this.transaction = null;
            this.connection.Close();
        }

        public override void RollbackTransaction()
        {
            this.transaction.Rollback();
            this.transaction = null;
            this.connection.Close();
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

        public static InfoCarrierTestHelper<TDbContext> CreateHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, TDbContext> createContext,
            Action<TDbContext> initializeDatabase,
            bool useSharedStore,
            string databaseName)
            => CreateTestHelper(
                onModelCreating,
                () => new SqlServerTestStore<TDbContext>(
                    (o, _) => createContext(o),
                    MakeStoreServiceConfigurator(onModelCreating),
                    initializeDatabase,
                    databaseName),
                useSharedStore);

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
    }
}
