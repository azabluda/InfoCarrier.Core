// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>DataAnnotationTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     Mostly a model-building suite — which annotation produces which metadata — and therefore a
///     direct check that the client model this provider builds is EF's, since the client is where
///     conventions actually run. The six overrides are EF's own
///     <c>DataAnnotationInMemoryTest</c>'s: each asserts a constraint the *store* would enforce,
///     and the backing store here is InMemory, which enforces none of them; EF replaces the
///     round-trip with the metadata assertion instead.
/// </remarks>
public class DataAnnotationInfoCarrierTest(DataAnnotationInfoCarrierTest.DataAnnotationInfoCarrierFixture fixture)
    : DataAnnotationTestBase<DataAnnotationInfoCarrierTest.DataAnnotationInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    protected override TestHelpers TestHelpers
        => InfoCarrierTestHelpers.Instance;

    /// <inheritdoc />
    /// <remarks>
    ///     Unlike InMemory's, which answers <see langword="false" />: this provider keeps EF's
    ///     `ForeignKeyIndexConvention`, so its model has the indexes (A47 measured what happens
    ///     when the flag says otherwise — 136 failures).
    /// </remarks>
    protected override bool HasForeignKeyIndexes
        => true;

    /// <inheritdoc />
    public override Task ConcurrencyCheckAttribute_throws_if_value_in_database_changed()
    {
        using DbContext context = CreateContext();
        Assert.True(context.Model.FindEntityType(typeof(One))!.FindProperty("RowVersion")!.IsConcurrencyToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length()
    {
        using DbContext context = CreateContext();
        Assert.Equal(10, context.Model.FindEntityType(typeof(One))!.FindProperty("MaxLengthProperty")!.GetMaxLength());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RequiredAttribute_for_navigation_throws_while_inserting_null_value()
    {
        using DbContext context = CreateContext();
        Assert.True(
            context.Model.FindEntityType(typeof(BookDetails))!
                .FindNavigation(nameof(BookDetails.AnotherBook))!.ForeignKey.IsRequired);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RequiredAttribute_for_property_throws_while_inserting_null_value()
    {
        using DbContext context = CreateContext();
        Assert.False(context.Model.FindEntityType(typeof(One))!.FindProperty("RequiredColumn")!.IsNullable);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task StringLengthAttribute_throws_while_inserting_value_longer_than_max_length()
    {
        using DbContext context = CreateContext();
        Assert.Equal(16, context.Model.FindEntityType(typeof(Two))!.FindProperty("Data")!.GetMaxLength());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task TimestampAttribute_throws_if_value_in_database_changed()
    {
        using DbContext context = CreateContext();
        Assert.True(context.Model.FindEntityType(typeof(Two))!.FindProperty("Timestamp")!.IsConcurrencyToken);
        return Task.CompletedTask;
    }

    public class DataAnnotationInfoCarrierFixture : DataAnnotationFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "DataAnnotationInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }
}
