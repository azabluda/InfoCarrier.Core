// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Which tier registers <c>InfoCarrier.Core.Relational</c> on the <em>client</em>, and which
///     one must never (#97 level 2, <c>architecture.md</c> section 6a <b>D3</b>).
/// </summary>
/// <remarks>
///     <para>
///         <b>This test exists because the change it pins moved no count.</b> Turning the
///         relational client on for the whole of Tier B left <c>failed</c>, the failing-test names
///         and the failure reasons all byte-identical. <c>CLAUDE.md</c> is explicit that a matcher
///         which never fired and a change which did not help look the same from outside, and that
///         the code must be established to have RUN before anything is concluded from a count that
///         did not move. This class is that evidence, and it is cheaper and more durable than a
///         probe: it reads the registration straight out of the collection each tier builds.
///     </para>
///     <para>
///         <b>The second assertion is a guardrail rather than a duplicate.</b> Tier A's backing
///         store is EF's InMemory provider, which is not a relational store, and a relational
///         client over it is exactly the disagreement the seam exists to prevent. Nothing today
///         could produce one -- the override lives in this project and
///         <c>InfoCarrierTier.AddClientServices</c> adds nothing by default -- and this pins that
///         it stays so.
///     </para>
/// </remarks>
public class RelationalClientTierPinTest
{
    /// <summary>
    ///     Tier B registers the relational client for <b>every</b> fixture, not only the ones that
    ///     asked for raw SQL.
    /// </summary>
    /// <remarks>
    ///     <c>arbitrarySqlExecution</c> is deliberately left at its default here. That flag used to
    ///     gate this registration, because the only thing the package bought was the
    ///     <c>Database.SqlQuery&lt;T&gt;</c> facade shim; it is a permission, and whether the
    ///     backing store is relational is not.
    /// </remarks>
    [ConditionalFact]
    public void Tier_B_registers_the_relational_client_even_without_the_raw_SQL_grant()
    {
        IServiceCollection services = ProviderServicesFor(SqliteInfoCarrierTier.Instance);

        Assert.Contains(services, d => d.ServiceType == typeof(IRelationalDatabaseFacadeDependencies));
    }

    /// <summary>
    ///     Tier A registers nothing relational, because InMemory is not a relational store.
    /// </summary>
    [ConditionalFact]
    public void Tier_A_registers_nothing_relational_on_the_client()
    {
        IServiceCollection services = ProviderServicesFor(InfoCarrierTestStoreFactory.InMemory);

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IRelationalDatabaseFacadeDependencies));
    }

    /// <summary>
    ///     The client provider services a fixture on this tier would be built with.
    /// </summary>
    /// <remarks>
    ///     Read out of the collection rather than out of a built provider: what is being pinned is
    ///     which tier makes the registration, and a resolved instance would answer a different
    ///     question and need a context to resolve it from.
    /// </remarks>
    private static IServiceCollection ProviderServicesFor(InfoCarrierTier tier)
        => ((InfoCarrierTestStoreFactory)InfoCarrierTestStoreFactory.Create(
                tier,
                typeof(DbContext),
                onModelCreating: (_, _) => { }))
            .AddProviderServices(new ServiceCollection());
}
