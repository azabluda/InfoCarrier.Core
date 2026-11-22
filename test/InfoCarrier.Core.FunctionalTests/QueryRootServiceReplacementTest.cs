// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using InfoCarrier.Core.Metadata;
using InfoCarrier.Core.Relational;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests;

public class QueryRootServiceReplacementTest
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task Query_execution_uses_the_registered_root_adapter(bool async)
    {
        var services = new ServiceCollection();
        string database = Guid.NewGuid().ToString("N");
        services.AddDbContext<Context>(o => o.UseInMemoryDatabase(database));
        services.AddScoped<DbContext>(p => p.GetRequiredService<Context>());
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<Context>();
            seed.Add(new Row { Id = 1, Text = "expected" });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var server = new InProcessInfoCarrierServer(provider);
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelope = new InfoCarrierEnvelopeServer(server, serializer);
        var client = new TransportInfoCarrierClient(new TestUtilities.InProcessInfoCarrierTransport(
            envelope.Dispatch, envelope.DispatchAsync, serializer), serializer);
        var roots = new RecordingRoots();
        await using var clientServices = new ServiceCollection()
            .AddSingleton<IInfoCarrierRelationalQueryRoots>(roots).AddEntityFrameworkInfoCarrier().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<Context>().UseInfoCarrier(client)
            .UseInternalServiceProvider(clientServices).Options;
        await using var context = new Context(options);
        var query = context.Set<Row>().Where(r => r.Id == 1).Select(r => r.Text);
        var rows = async ? await query.ToListAsync(TestContext.Current.CancellationToken) : query.ToList();
        Assert.Equal("expected", Assert.Single(rows));
        Assert.True(roots.EntityReads > 0, "The registered root adapter was never used during query translation.");
    }

    private sealed class RecordingRoots : IInfoCarrierRelationalQueryRoots
    {
        private readonly InfoCarrierRelationalQueryRoots _inner = new();
        public int EntityReads { get; private set; }
        public bool IsRawSqlRoot(Expression node) => _inner.IsRawSqlRoot(node);
        public bool TryReadEntityRoot(Expression node, [NotNullWhen(true)] out string? sql,
            [NotNullWhen(true)] out Expression? argument)
        {
            EntityReads++;
            return _inner.TryReadEntityRoot(node, out sql, out argument);
        }
        public bool TryReadScalarRoot(Expression node, [NotNullWhen(true)] out string? sql,
            [NotNullWhen(true)] out Expression? argument) => _inner.TryReadScalarRoot(node, out sql, out argument);
        public Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument)
            => _inner.CreateEntityRoot(entityType, sql, argument);
        public Expression CreateScalarRoot(Type elementType, string sql, Expression argument)
            => _inner.CreateScalarRoot(elementType, sql, argument);
    }

    public class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Row>();
    }
    public class Row { public int Id { get; set; } public string Text { get; set; } = ""; }
}
