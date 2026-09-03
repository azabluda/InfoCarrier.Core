// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The InfoCarrier Northwind query fixture (v1's <c>NorthwindQueryInfoCarrierFixture</c>,
///     rebuilt for EF Core 10). Overrides only <see cref="TestStoreFactory" /> — the client
///     context remotes through the InfoCarrier backend store (InMemory first).
/// </summary>
/// <typeparam name="TModelCustomizer">The model customizer.</typeparam>
/// <remarks>
///     <b>It no longer implements <c>ITestSqlLoggerFactory</c>, and losing it is the point of the
///     project split.</b> That interface and its <c>TestSqlLoggerFactory</c> property live in
///     <c>EFCore.Relational.Specification.Tests</c>, which this tier does not reference. They were
///     here to satisfy <c>RelationalComplianceTestBase</c>'s second assertion (R54), and Tier A is
///     now checked by the plain <c>ComplianceTestBase</c>, which does not ask. What the property
///     returned was the <em>client's</em> log anyway, and this client has no database and emits no
///     SQL; <c>ServerSqlLog</c> is where the server's statements can actually be read. Tier B's
///     <c>NorthwindQueryInfoCarrierSqliteFixture</c> still implements it, because there the
///     relational compliance base does ask.
/// </remarks>
public class NorthwindQueryInfoCarrierFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    /// <inheritdoc />
    /// <remarks>
    ///     Both sides build from ONE <c>OnModelCreating</c>, which is what version 1 of this
    ///     provider did and what an application does. See the Tier B Northwind fixture.
    /// </remarks>
    protected override Type ContextType
        => typeof(NorthwindInfoCarrierServerContext);

    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server),
            // The server adds the InMemory defining queries for the keyless entity types; the
            // client model has the types but not the store's means of producing their rows.
            serverContextType: typeof(NorthwindInfoCarrierServerContext),
            configureConventions: ConfigureConventions);

    /// <summary>
    ///     Enables Query-category logging, as EF Core's own <c>NorthwindQueryInMemoryFixture</c>
    ///     does.
    /// </summary>
    /// <remarks>
    ///     Without this the fixture's <c>ListLoggerFactory.Log</c> is always empty, so every
    ///     spec test that asserts on a logged event — <c>ToListAsync_with_canceled_token</c>
    ///     looks for <c>CoreEventId.QueryCanceled</c> — fails against an empty collection no
    ///     matter what the provider does.
    /// </remarks>
    protected override bool ShouldLogCategory(string logCategory)
        => logCategory == DbLoggerCategory.Query.Name;

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
