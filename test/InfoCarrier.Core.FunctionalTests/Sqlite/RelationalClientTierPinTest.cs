// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     Every InfoCarrier client gets the relational half, whatever the backing store is
///     (#97 level 2, <c>architecture.md</c> section 6a <b>D3</b>).
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
///         <b>It asserted the opposite of this until D3 was reverted.</b> While the relational half
///         was a separate package, a client only had it after an <c>AddInfoCarrierRelationalClient()</c>
///         call, and Tier A was pinned to have nothing relational at all. One package with one
///         registration path makes both tiers the same here, which is the point of the revert: a
///         consumer cannot save anything by leaving the relational half out, so there is nothing to
///         leave out and no half-configured client to diagnose.
///     </para>
/// </remarks>
public class RelationalClientTierPinTest
{
    /// <summary>
    ///     Tier B's client resolves EF's relational facade dependencies, which is what
    ///     <c>Database.SqlQuery&lt;T&gt;</c> tests for before it builds anything.
    /// </summary>
    [ConditionalFact]
    public void Tier_B_registers_the_relational_facade_dependencies()
    {
        IServiceCollection services = ProviderServicesFor(SqliteInfoCarrierTier.Instance);

        Assert.Contains(services, d => d.ServiceType == typeof(IRelationalDatabaseFacadeDependencies));
    }

    /// <summary>
    ///     Tier A's client resolves them too, because the package carries them unconditionally.
    /// </summary>
    /// <remarks>
    ///     A client over a non-relational backend never produces a relational query root, so the
    ///     registration answers nothing and costs nothing. What it removes is the half-configured
    ///     state: there is no way to ask for InfoCarrier and get only part of it.
    /// </remarks>
    [ConditionalFact]
    public void Tier_A_registers_them_as_well()
    {
        IServiceCollection services = ProviderServicesFor(InfoCarrierTestStoreFactory.InMemory);

        Assert.Contains(services, d => d.ServiceType == typeof(IRelationalDatabaseFacadeDependencies));
    }

    /// <summary>
    ///     There is <b>one</b> convention set builder and it is the relational subclass, on both
    ///     tiers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>#97 level 2, and this is a REPLACEMENT rather than a second registration.</b>
    ///         The subclass calls the base and adds EF's own
    ///         <c>EntityTypeHierarchyMappingConvention</c>. Two builders, or two hierarchy
    ///         conventions, would be the duplication the one-package layout exists to remove.
    ///     </para>
    ///     <para>
    ///         Asserting the count as well as the type is the half that matters: a registration
    ///         that stopped replacing would leave both present and the last one would silently win.
    ///     </para>
    /// </remarks>
    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void One_convention_set_builder_and_it_is_the_relational_one(bool relationalTier)
    {
        IServiceCollection services = ProviderServicesFor(
            relationalTier ? SqliteInfoCarrierTier.Instance : InfoCarrierTestStoreFactory.InMemory);

        ServiceDescriptor descriptor = Assert.Single(
            services, d => d.ServiceType == typeof(IProviderConventionSetBuilder));

        Assert.Equal(
            typeof(InfoCarrier.Core.Relational.InfoCarrierRelationalConventionSetBuilder),
            descriptor.ImplementationType);
    }

    /// <summary>
    ///     The client provider services a fixture on this tier would be built with.
    /// </summary>
    /// <remarks>
    ///     Read out of the collection rather than out of a built provider: what is being pinned is
    ///     which registration is made, and a resolved instance would answer a different question
    ///     and need a context to resolve it from.
    /// </remarks>
    private static IServiceCollection ProviderServicesFor(InfoCarrierTier tier)
        => ((InfoCarrierTestStoreFactory)InfoCarrierTestStoreFactory.Create(
                tier,
                typeof(DbContext),
                onModelCreating: (_, _) => { }))
            .AddProviderServices(new ServiceCollection());
}
