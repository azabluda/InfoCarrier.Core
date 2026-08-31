// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>OwnedEntityQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         EF's owned-entity regression corpus: each test builds a model for one reported bug,
///         built per test rather than shared, which is why it needs
///         <see cref="NonSharedModelInfoCarrierHarness" /> (A49).
///     </para>
///     <para>
///         <b>Moved here from Tier A, and R41's reason for withholding the relational base has
///         gone.</b> That reason was two <c>AsSplitQuery</c> tests; R59 removes the hint before the
///         boundary analysis and both pass. The base needs no overrides at all — 37 tests, 37
///         green — and it also declares no <c>UseTransaction</c> and calls the transaction helper
///         zero times, both checked rather than assumed.
///     </para>
/// </remarks>
public class OwnedEntityQueryInfoCarrierTest(NonSharedFixture fixture)
    : OwnedEntityQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
