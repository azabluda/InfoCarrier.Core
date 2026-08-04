// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ComplexTypesTrackingTestBase</c> on ADR-009 Tier A, mirroring EF's own
///     <c>ComplexTypesTrackingInMemoryTest</c>.
/// </summary>
/// <remarks>
///     The corpus for complex types on the wire. A complex property is not in
///     <c>IEntityType.GetProperties()</c>, so before this base was adopted nothing in the suite
///     could tell whether one travelled at all — and the single failure that had been blamed on
///     complex types turned out to be a model that did not map any (A31).
/// </remarks>
public class ComplexTypesTrackingInfoCarrierTest(ComplexTypesTrackingInfoCarrierTest.InfoCarrierFixture fixture)
    : ComplexTypesTrackingTestBase<ComplexTypesTrackingInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<DbContext, Task> testOperation,
        Func<DbContext, Task> nestedTestOperation1 = null,
        Func<DbContext, Task> nestedTestOperation2 = null)
    {
        try
        {
            await base.ExecuteWithStrategyInTransactionAsync(testOperation, nestedTestOperation1, nestedTestOperation2);
        }
        finally
        {
            await Fixture.ReseedAsync();
        }
    }

    public class InfoCarrierFixture : FixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "ComplexTypesTrackingInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: builder => builder.ConfigureWarnings(
                    w => w.Log(InMemoryEventId.TransactionIgnoredWarning)),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning));

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.UsePropertyAccessMode(PropertyAccessMode.PreferProperty);
            base.OnModelCreating(modelBuilder, context);
        }
    }
}
