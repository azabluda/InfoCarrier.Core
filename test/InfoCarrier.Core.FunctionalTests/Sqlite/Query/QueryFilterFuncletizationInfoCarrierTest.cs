// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>QueryFilterFuncletizationTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         A query filter closes over the <c>DbContext</c>, so every one of these queries carries a
///         captured instance field into the tree. That is precisely what ADR-010's boundary rule reads
///         — a surviving closure field access pushes the boundary in — and
///         `SubstituteParametersExpressionVisitor` is what has to have replaced it first.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Base and fixture follow
///         EF's own SQLite class.
///     </para>
/// </remarks>
public class QueryFilterFuncletizationInfoCarrierTest(QueryFilterFuncletizationInfoCarrierTest.InfoCarrierFixture fixture)
    : QueryFilterFuncletizationTestBase<QueryFilterFuncletizationInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : QueryFilterFuncletizationRelationalFixture
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                copyDbContextParameters: (client, server) => Copy(
                    (QueryFilterFuncletizationContext)client, (QueryFilterFuncletizationContext)server),
                configureConventions: ConfigureConventions);

        /// <summary>
        ///     Carries the context state every filter in this model closes over to the server.
        /// </summary>
        /// <remarks>
        ///     A query filter is part of the <em>model</em>, so both sides build it and both sides
        ///     apply it — the client funcletizes its own value into the shipped tree, and the
        ///     server then applies its filter again with whatever <c>Field</c>, <c>Property</c> or
        ///     <c>Tenant</c> its own instance happens to hold. These tests exist to mutate exactly
        ///     those members between two queries, so with nothing copied the server keeps
        ///     filtering on the initial value and the second query answers like the first. This is
        ///     the same mechanism the Northwind fixture uses for <c>TenantPrefix</c>.
        /// </remarks>
        private static void Copy(QueryFilterFuncletizationContext client, QueryFilterFuncletizationContext server)
        {
            server.Field = client.Field;
            server.Property = client.Property;
            server.IsModerated = client.IsModerated;
            server.Tenant = client.Tenant;
            server.TenantIds = client.TenantIds;
            server.IndirectionFlag = client.IndirectionFlag;
        }
    }
}
