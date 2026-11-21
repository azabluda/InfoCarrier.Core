// Licensed under the MIT license. See license.txt file in the project root for license information.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

// Official EF Core 11 DefaultKeyValuesTest at 9e685c7a52d1a344d133a3d370e458db97417b3f.
public class DefaultKeyValuesTest(DefaultKeyValuesTest.CosmosDefaultKeyValuesTestFixture fixture)
    : IClassFixture<DefaultKeyValuesTest.CosmosDefaultKeyValuesTestFixture>
{
    protected CosmosDefaultKeyValuesTestFixture Fixture { get; } = fixture;

    [Fact]
    public async Task Single_key_value_with_single_partition_key_must_have_key_set()
    {
        using var context = CreateContext();

        context.Add(new SingleKeySinglePartitionKey());
        await AssertKeyValueNotSet(context, nameof(SingleKeySinglePartitionKey), nameof(SingleKeySinglePartitionKey.Id));

        context.Add(new SingleKeySinglePartitionKey { PartitionKey = 1 });
        await AssertKeyValueNotSet(context, nameof(SingleKeySinglePartitionKey), nameof(SingleKeySinglePartitionKey.Id));

        context.Add(new SingleKeySinglePartitionKey { Id = 1 });
        await AssertSaves(context);

        context.Add(new SingleKeySinglePartitionKey { Id = 1, PartitionKey = 1 });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Composite_key_value_with_single_partition_key_must_have_key_set()
    {
        using var context = CreateContext();

        context.Add(new CompositeKeySinglePartitionKey());
        await AssertKeyValueNotSet(context, nameof(CompositeKeySinglePartitionKey), nameof(CompositeKeySinglePartitionKey.Id3));

        context.Add(new CompositeKeySinglePartitionKey { PartitionKey = 1 });
        await AssertKeyValueNotSet(context, nameof(CompositeKeySinglePartitionKey), nameof(CompositeKeySinglePartitionKey.Id3));

        context.Add(new CompositeKeySinglePartitionKey { Id1 = 1 });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id3 = true });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id1 = 1, Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id1 = 1, Id3 = true });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id2 = Guid.NewGuid(), Id3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                Id3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id1 = 1, PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id2 = Guid.NewGuid(), PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new CompositeKeySinglePartitionKey { Id3 = true, PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(
            new CompositeKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeySinglePartitionKey
            {
                Id1 = 1,
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeySinglePartitionKey
            {
                Id2 = Guid.NewGuid(),
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Single_key_value_with_composite_partition_key_must_have_key_set()
    {
        using var context = CreateContext();

        context.Add(new SingleKeyCompositePartitionKey());
        await AssertKeyValueNotSet(context, nameof(SingleKeyCompositePartitionKey), nameof(SingleKeyCompositePartitionKey.Id));

        context.Add(new SingleKeyCompositePartitionKey { PartitionKey1 = 1 });
        await AssertKeyValueNotSet(context, nameof(SingleKeyCompositePartitionKey), nameof(SingleKeyCompositePartitionKey.Id));

        context.Add(new SingleKeyCompositePartitionKey { PartitionKey2 = Guid.NewGuid() });
        await AssertKeyValueNotSet(context, nameof(SingleKeyCompositePartitionKey), nameof(SingleKeyCompositePartitionKey.Id));

        context.Add(new SingleKeyCompositePartitionKey { PartitionKey3 = true });
        await AssertKeyValueNotSet(context, nameof(SingleKeyCompositePartitionKey), nameof(SingleKeyCompositePartitionKey.Id));

        context.Add(
            new SingleKeyCompositePartitionKey
            {
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertKeyValueNotSet(context, nameof(SingleKeyCompositePartitionKey), nameof(SingleKeyCompositePartitionKey.Id));

        context.Add(new SingleKeyCompositePartitionKey { Id = 1 });
        await AssertSaves(context);

        context.Add(
            new SingleKeyCompositePartitionKey
            {
                Id = 1,
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new SingleKeyCompositePartitionKey
            {
                Id = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new SingleKeyCompositePartitionKey { Id = 1, PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Composite_key_value_with_composite_partition_key_must_have_key_set()
    {
        using var context = CreateContext();

        context.Add(new CompositeKeyCompositePartitionKey());
        await AssertKeyValueNotSet(context, nameof(CompositeKeyCompositePartitionKey), nameof(CompositeKeyCompositePartitionKey.Id3));

        context.Add(new CompositeKeyCompositePartitionKey { PartitionKey1 = 1 });
        await AssertKeyValueNotSet(context, nameof(CompositeKeyCompositePartitionKey), nameof(CompositeKeyCompositePartitionKey.Id3));

        context.Add(new CompositeKeyCompositePartitionKey { PartitionKey2 = Guid.NewGuid() });
        await AssertKeyValueNotSet(context, nameof(CompositeKeyCompositePartitionKey), nameof(CompositeKeyCompositePartitionKey.Id3));

        context.Add(new CompositeKeyCompositePartitionKey { PartitionKey3 = true });
        await AssertKeyValueNotSet(context, nameof(CompositeKeyCompositePartitionKey), nameof(CompositeKeyCompositePartitionKey.Id3));

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertKeyValueNotSet(context, nameof(CompositeKeyCompositePartitionKey), nameof(CompositeKeyCompositePartitionKey.Id3));

        context.Add(new CompositeKeyCompositePartitionKey { Id1 = 1 });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id1 = 1,
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeKeyCompositePartitionKey { Id1 = 1, PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeKeyCompositePartitionKey { Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id2 = Guid.NewGuid(),
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id2 = Guid.NewGuid(),
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeKeyCompositePartitionKey { Id2 = Guid.NewGuid(), PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeKeyCompositePartitionKey { Id3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id3 = true,
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeKeyCompositePartitionKey
            {
                Id3 = true,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeKeyCompositePartitionKey { Id3 = true, PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);
    }

    [InfoCarrierDesign(Decisions.Architecture, Decisions.ClientServices,
        Justification = "Match Cosmos key-generation semantics explicitly because the client uses relational conventions.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Assert the convention-only client OnAdd and native Cosmos Never metadata. Explicit ValueGeneratedNever then gives both models the same missing-key contract.")]
    [Fact]
    public async Task Single_same_key_and_partition_key_must_have_key_set()
    {
        using var conventionClient = new ConventionOnlyKeyContext(
            ((InfoCarrierTestStore)Fixture.TestStore).AddProviderOptions(new DbContextOptionsBuilder()).Options);
        using var conventionDirect = new ConventionOnlyKeyContext(
            CosmosEmulator.Configure(new DbContextOptionsBuilder(), nameof(ConventionOnlyKeyContext)).Options);
        Assert.Equal(ValueGenerated.OnAdd, conventionClient.Model.FindEntityType(typeof(SingleSameKeyAndPartitionKey))!
            .FindProperty(nameof(SingleSameKeyAndPartitionKey.Id))!.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, conventionDirect.Model.FindEntityType(typeof(SingleSameKeyAndPartitionKey))!
            .FindProperty(nameof(SingleSameKeyAndPartitionKey.Id))!.ValueGenerated);

        using var context = CreateContext();

        context.Add(new SingleSameKeyAndPartitionKey());
        await AssertKeyValueNotSet(context, nameof(SingleSameKeyAndPartitionKey), nameof(SingleSameKeyAndPartitionKey.Id));

        context.Add(new SingleSameKeyAndPartitionKey { Id = 1 });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Client_temporary_keys_are_rejected_when_the_backend_never_generates_them()
    {
        var store = (InfoCarrierTestStore)Fixture.TestStore;
        using var direct = store.Backend.CreateDbContext();
        int before = await direct.Set<SingleSameKeyAndPartitionKey>().CountAsync(TestContext.Current.CancellationToken);
        using var client = new ConventionOnlyKeyContext(store.AddProviderOptions(new DbContextOptionsBuilder()).Options);
        var entry = client.Add(new SingleSameKeyAndPartitionKey());
        Assert.True(entry.Property(e => e.Id).IsTemporary);

        var failure = await Record.ExceptionAsync(() => client.SaveChangesAsync(TestContext.Current.CancellationToken));
        var keys = await direct.Set<SingleSameKeyAndPartitionKey>().Select(e => e.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.True(failure is InvalidOperationException,
            $"Expected temporary-key rejection; actual={failure?.GetType().FullName ?? "success"}; "
            + $"stored count before={before}, after={keys.Count}; keys=[{string.Join(",", keys)}].");
        Assert.Contains("temporary", failure!.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(SingleSameKeyAndPartitionKey.Id), failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, keys.Count);

        // An explicitly supplied negative key is a real key, not a temporary-value heuristic.
        using var explicitClient = new ConventionOnlyKeyContext(store.AddProviderOptions(new DbContextOptionsBuilder()).Options);
        var explicitEntry = explicitClient.Add(new SingleSameKeyAndPartitionKey { Id = -12345 });
        Assert.False(explicitEntry.Property(e => e.Id).IsTemporary);
        await explicitClient.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(-12345, await direct.Set<SingleSameKeyAndPartitionKey>().Where(e => e.Id == -12345)
            .Select(e => e.Id).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Composite_same_key_and_partition_key_must_have_key_set()
    {
        using var context = CreateContext();

        context.Add(new CompositeSameKeyAndPartitionKey());
        await AssertKeyValueNotSet(context, nameof(CompositeSameKeyAndPartitionKey), nameof(CompositeSameKeyAndPartitionKey.Key1));

        context.Add(new CompositeSameKeyAndPartitionKey { Key1 = 1 });
        await AssertSaves(context);

        context.Add(new CompositeSameKeyAndPartitionKey { Key2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeSameKeyAndPartitionKey { Key3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeSameKeyAndPartitionKey
            {
                Key1 = 1,
                Key2 = Guid.NewGuid(),
                Key3 = true
            });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Single_key_value_with_single_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new SingleGeneratedKeySinglePartitionKey());
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeySinglePartitionKey { PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeySinglePartitionKey { Id = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeySinglePartitionKey { Id = Guid.NewGuid(), PartitionKey = 1 });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Composite_key_value_with_single_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new CompositeGeneratedKeySinglePartitionKey());
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id1 = 1 });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id3 = true });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id1 = 1, Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id1 = 1, Id3 = true });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id2 = Guid.NewGuid(), Id3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                Id3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id1 = 1, PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id2 = Guid.NewGuid(), PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeySinglePartitionKey { Id3 = true, PartitionKey = 1 });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeySinglePartitionKey
            {
                Id1 = 1,
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeySinglePartitionKey
            {
                Id2 = Guid.NewGuid(),
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeySinglePartitionKey
            {
                Id1 = 1,
                Id2 = Guid.NewGuid(),
                Id3 = true,
                PartitionKey = 1
            });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Single_key_value_with_composite_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new SingleGeneratedKeyCompositePartitionKey());
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeyCompositePartitionKey { PartitionKey1 = 1 });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeyCompositePartitionKey { PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeyCompositePartitionKey { PartitionKey3 = true });
        await AssertSaves(context);

        context.Add(
            new SingleGeneratedKeyCompositePartitionKey
            {
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeyCompositePartitionKey { Id = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(
            new SingleGeneratedKeyCompositePartitionKey
            {
                Id = Guid.NewGuid(),
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new SingleGeneratedKeyCompositePartitionKey
            {
                Id = Guid.NewGuid(),
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new SingleGeneratedKeyCompositePartitionKey { Id = Guid.NewGuid(), PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Composite_key_value_with_composite_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new CompositeGeneratedKeyCompositePartitionKey());
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { PartitionKey1 = 1 });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { PartitionKey3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id1 = 1 });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id1 = 1,
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id1 = 1, PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id2 = Guid.NewGuid(),
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id2 = Guid.NewGuid(),
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id2 = Guid.NewGuid(), PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id3 = true,
                PartitionKey1 = 1,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(
            new CompositeGeneratedKeyCompositePartitionKey
            {
                Id3 = true,
                PartitionKey2 = Guid.NewGuid(),
                PartitionKey3 = true
            });
        await AssertSaves(context);

        context.Add(new CompositeGeneratedKeyCompositePartitionKey { Id3 = true, PartitionKey2 = Guid.NewGuid() });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Single_same_key_and_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new SingleSameGeneratedKeyAndPartitionKey());
        await AssertSaves(context);

        context.Add(new SingleSameGeneratedKeyAndPartitionKey { Id = Guid.NewGuid() });
        await AssertSaves(context);
    }

    [Fact]
    public async Task Composite_same_key_and_partition_key_can_use_generated_value()
    {
        using var context = CreateContext();

        context.Add(new CompositeSameGeneratedKeyAndPartitionKey());
        await AssertSaves(context);

        context.Add(new CompositeSameGeneratedKeyAndPartitionKey { Key1 = 1 });
        await AssertSaves(context);

        context.Add(new CompositeSameGeneratedKeyAndPartitionKey { Key2 = Guid.NewGuid() });
        await AssertSaves(context);

        context.Add(new CompositeSameGeneratedKeyAndPartitionKey { Key3 = true });
        await AssertSaves(context);

        context.Add(
            new CompositeSameGeneratedKeyAndPartitionKey
            {
                Key1 = 1,
                Key2 = Guid.NewGuid(),
                Key3 = true
            });
        await AssertSaves(context);
    }

    private static async Task AssertSaves(DefaultKeyValuesTestContext context)
    {
        var entry = context.ChangeTracker.Entries().Single();

        Assert.Equal(EntityState.Added, entry.State);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(EntityState.Unchanged, entry.State);

        context.ChangeTracker.Clear();
    }

    internal static string ExpectedMissingKeyWarning(string entityTypeName, string propertyName)
        => CoreStrings.WarningAsErrorTemplate(
            CosmosEventId.PrimaryKeyValueNotSet.ToString(),
            $"The key property '{entityTypeName}.{propertyName}' is not configured to generate values and has the CLR default or sentinel value while saving a new entity to the database. The Azure Cosmos DB database provider for EF Core does not generate key values by default. This means key values must be explicitly set before saving new entities. See https://aka.ms/ef-cosmos-keys for more information.",
            "CosmosEventId.PrimaryKeyValueNotSet");

    private static async Task AssertKeyValueNotSet(
        DefaultKeyValuesTestContext context,
        string entityTypeName,
        string propertyName)
    {
        object entity = context.ChangeTracker.Entries().Single().Entity;
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        // The client re-raises update errors with its own entries (wire requirement W5).
        Assert.Same(entity, Assert.Single(failure.Entries).Entity);
        var serverFailure = Assert.IsType<DbUpdateException>(failure.InnerException);
        Assert.Equal(serverFailure.Message, failure.Message);
        var warning = Assert.IsType<InvalidOperationException>(serverFailure.InnerException);
        Assert.Equal(ExpectedMissingKeyWarning(entityTypeName, propertyName), warning.Message);
        context.ChangeTracker.Clear();
    }

    protected DefaultKeyValuesTestContext CreateContext()
        => Fixture.CreateContext();

    private sealed class ConventionOnlyKeyContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<SingleSameKeyAndPartitionKey>(b =>
            {
                b.HasKey(e => e.Id);
                b.HasPartitionKey(e => e.Id);
            });
    }

    public class CosmosDefaultKeyValuesTestFixture : SharedStoreFixtureBase<DefaultKeyValuesTestContext>
    {
        protected override string StoreName
            => nameof(DefaultKeyValuesTest);

        protected override bool UsePooling
            => false;

        private ITestStoreFactory? _factory;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, null, onAddOptions: AddOptions);



    }

    public class DefaultKeyValuesTestContext(DbContextOptions dbContextOptions) : DbContext(dbContextOptions)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SingleKeySinglePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.PartitionKey);
                b.HasKey(e => new { e.Id, e.PartitionKey });
            });

            modelBuilder.Entity<CompositeKeySinglePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.PartitionKey);
                b.HasKey(e => new
                {
                    e.Id1,
                    e.Id2,
                    e.Id3,
                    e.PartitionKey
                });
            });

            modelBuilder.Entity<SingleKeyCompositePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.HasKey(e => new
                {
                    e.Id,
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
            });

            modelBuilder.Entity<CompositeKeyCompositePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.HasKey(e => new
                {
                    e.Id1,
                    e.Id2,
                    e.Id3,
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
            });

            modelBuilder.Entity<SingleSameKeyAndPartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.Id);
                b.HasKey(e => e.Id);
                // The InfoCarrier client uses relational conventions. State the backend's
                // non-generated integer key explicitly so both models have the same contract.
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<CompositeSameKeyAndPartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.Key1,
                    e.Key2,
                    e.Key3
                });
                b.HasKey(e => new
                {
                    e.Key1,
                    e.Key2,
                    e.Key3
                });
            });

            modelBuilder.Entity<SingleGeneratedKeySinglePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.PartitionKey);
                b.HasKey(e => new { e.Id, e.PartitionKey });
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<CompositeGeneratedKeySinglePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.PartitionKey);
                b.HasKey(e => new
                {
                    e.Id1,
                    e.Id2,
                    e.Id3,
                    e.PartitionKey
                });
                b.Property(e => e.Id2).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<SingleGeneratedKeyCompositePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.HasKey(e => new
                {
                    e.Id,
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<CompositeGeneratedKeyCompositePartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.HasKey(e => new
                {
                    e.Id1,
                    e.Id2,
                    e.Id3,
                    e.PartitionKey1,
                    e.PartitionKey2,
                    e.PartitionKey3
                });
                b.Property(e => e.Id2).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<SingleSameGeneratedKeyAndPartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => e.Id);
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<CompositeSameGeneratedKeyAndPartitionKey>(b =>
            {
                b.ToContainer(b.Metadata.ClrType.Name);
                b.HasPartitionKey(e => new
                {
                    e.Key1,
                    e.Key2,
                    e.Key3
                });
                b.HasKey(e => new
                {
                    e.Key1,
                    e.Key2,
                    e.Key3
                });
                b.Property(e => e.Key2).ValueGeneratedOnAdd();
            });
        }
    }

    public class SingleKeySinglePartitionKey
    {
        public int Id { get; set; }
        public int PartitionKey { get; set; }
    }

    protected class CompositeKeySinglePartitionKey
    {
        public int Id1 { get; set; }
        public Guid Id2 { get; set; }
        public bool Id3 { get; set; }
        public int PartitionKey { get; set; }
    }

    protected class SingleKeyCompositePartitionKey
    {
        public int Id { get; set; }
        public int PartitionKey1 { get; set; }
        public Guid PartitionKey2 { get; set; }
        public bool PartitionKey3 { get; set; }
    }

    protected class CompositeKeyCompositePartitionKey
    {
        public int Id1 { get; set; }
        public Guid Id2 { get; set; }
        public bool Id3 { get; set; }
        public int PartitionKey1 { get; set; }
        public Guid PartitionKey2 { get; set; }
        public bool PartitionKey3 { get; set; }
    }

    public class SingleSameKeyAndPartitionKey
    {
        public int Id { get; set; }
    }

    protected class CompositeSameKeyAndPartitionKey
    {
        public int Key1 { get; set; }
        public Guid Key2 { get; set; }
        public bool Key3 { get; set; }
    }

    public class SingleGeneratedKeySinglePartitionKey
    {
        public Guid Id { get; set; }
        public int PartitionKey { get; set; }
    }

    protected class CompositeGeneratedKeySinglePartitionKey
    {
        public int Id1 { get; set; }
        public Guid Id2 { get; set; }
        public bool Id3 { get; set; }
        public int PartitionKey { get; set; }
    }

    protected class SingleGeneratedKeyCompositePartitionKey
    {
        public Guid Id { get; set; }
        public int PartitionKey1 { get; set; }
        public Guid PartitionKey2 { get; set; }
        public bool PartitionKey3 { get; set; }
    }

    protected class CompositeGeneratedKeyCompositePartitionKey
    {
        public int Id1 { get; set; }
        public Guid Id2 { get; set; }
        public bool Id3 { get; set; }
        public int PartitionKey1 { get; set; }
        public Guid PartitionKey2 { get; set; }
        public bool PartitionKey3 { get; set; }
    }

    protected class SingleSameGeneratedKeyAndPartitionKey
    {
        public Guid Id { get; set; }
    }

    protected class CompositeSameGeneratedKeyAndPartitionKey
    {
        public int Key1 { get; set; }
        public Guid Key2 { get; set; }
        public bool Key3 { get; set; }
    }
}

[WireFreeControl]
public class DefaultKeyControlTest(DefaultKeyValuesTest.CosmosDefaultKeyValuesTestFixture fixture)
    : IClassFixture<DefaultKeyValuesTest.CosmosDefaultKeyValuesTestFixture>
{
    [Fact]
    public async Task Missing_keys_are_refused_and_generated_keys_are_readable_in_the_direct_store()
    {
        // Confirm the backend convention without the explicit client/server model normalization.
        using (var convention = new KeyConventionContext(CosmosEmulator.Configure(new DbContextOptionsBuilder(), "model-only").Options))
        {
            Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never,
                convention.Model.FindEntityType(typeof(ConventionKey))!.FindProperty(nameof(ConventionKey.Id))!.ValueGenerated);
        }

        using var context = (DefaultKeyValuesTest.DefaultKeyValuesTestContext)
            ((InfoCarrierTestStore)fixture.TestStore).Backend.CreateDbContext();
        context.Add(new DefaultKeyValuesTest.SingleKeySinglePartitionKey());
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        var warning = Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal(DefaultKeyValuesTest.ExpectedMissingKeyWarning(
            nameof(DefaultKeyValuesTest.SingleKeySinglePartitionKey), nameof(DefaultKeyValuesTest.SingleKeySinglePartitionKey.Id)), warning.Message);
        context.ChangeTracker.Clear();
        context.Add(new DefaultKeyValuesTest.SingleSameKeyAndPartitionKey());
        failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        warning = Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal(DefaultKeyValuesTest.ExpectedMissingKeyWarning(
            nameof(DefaultKeyValuesTest.SingleSameKeyAndPartitionKey), nameof(DefaultKeyValuesTest.SingleSameKeyAndPartitionKey.Id)), warning.Message);
        context.ChangeTracker.Clear();
        var generated = new DefaultKeyValuesTest.SingleGeneratedKeySinglePartitionKey();
        context.Add(generated);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, generated.Id);
        using var read = ((InfoCarrierTestStore)fixture.TestStore).Backend.CreateDbContext();
        var stored = await read.Set<DefaultKeyValuesTest.SingleGeneratedKeySinglePartitionKey>()
            .SingleAsync(e => e.Id == generated.Id, TestContext.Current.CancellationToken);
        Assert.Equal(generated.Id, stored.Id);
        Assert.Equal(0, stored.PartitionKey);
    }
    private sealed class ConventionKey
    {
        public int Id { get; set; }
    }

    private sealed class KeyConventionContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConventionKey>().HasPartitionKey(e => e.Id);
    }

}
