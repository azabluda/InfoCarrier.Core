// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using InfoCarrier.Core.Relational;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     What a client that is <em>half</em> configured for a relational store does (#97 level 2).
/// </summary>
/// <remarks>
///     <para>
///         <b>The answer must name the fix, and until R130 it was silence.</b>
///         R128 moved the relational model conventions out of <c>InfoCarrier.Core</c> and into
///         <c>InfoCarrier.Core.Relational</c>. A client that registers
///         <c>AddInfoCarrierRelational()</c> — so it has said its store IS relational — and not
///         <c>AddInfoCarrierRelationalClient()</c> then builds a model EF's relational conventions
///         never touched. Nothing raised anything: a TPT hierarchy simply kept the discriminator
///         the server's model had dropped, and the two models disagreed quietly. That is the
///         failure class <c>CLAUDE.md</c> opens with.
///     </para>
///     <para>
///         <b>It warns rather than refuses, and that was measured rather than chosen.</b> A version
///         that threw broke two legitimate patterns — <c>External_model_builder_uses_validation</c>
///         and <c>F1FixtureBase</c>, both of which build a model externally, which no convention set
///         can stamp. A diagnostic that refuses a legitimate model is worse than the silence it
///         replaces. See <see cref="InfoCarrierModelValidator" />.
///     </para>
///     <para>
///         <b>What this class does NOT claim, stated so a green here is not read as more than it
///         is.</b> A client that says nothing at all is not caught, and cannot be: nothing on it
///         knows the server is relational, and finding out needs the model handshake
///         <c>architecture.md</c> §6a <b>D2</b> describes and this repository has not built. The
///         third test below pins that a plain client still builds, because a guard that fired
///         there would break every ADR-009 Tier A model.
///     </para>
/// </remarks>
public class HalfConfiguredRelationalClientTest
{
    /// <summary>
    ///     Said the store is relational, did not register the conventions: warned, with the fix
    ///     named.
    /// </summary>
    [ConditionalFact]
    public void A_client_that_registers_only_the_shared_half_is_warned_and_told_what_to_call()
    {
        ListLoggerFactory logger = new(_ => true);

        using SimpleContext context = CreateContext(
            services => InfoCarrierRelationalServiceCollectionExtensions.AddInfoCarrierRelational(services),
            logger);

        Assert.NotNull(context.Model);

        Assert.Contains(
            logger.Log,
            entry => entry.Message?.Contains(
                InfoCarrierModelValidator.HalfConfiguredMessage, StringComparison.Ordinal) == true);
    }

    /// <summary>
    ///     Both halves registered: no warning.
    /// </summary>
    /// <remarks>
    ///     The control for the test above. Without it, a warning emitted for every relational
    ///     client would look identical from the assertion's side.
    /// </remarks>
    [ConditionalFact]
    public void A_client_that_registers_the_client_half_is_not_warned()
    {
        ListLoggerFactory logger = new(_ => true);

        using SimpleContext context = CreateContext(
            InfoCarrierRelationalServiceCollectionExtensions.AddInfoCarrierRelationalClient,
            logger);

        Assert.NotNull(context.Model);

        Assert.DoesNotContain(
            logger.Log,
            entry => entry.Message?.Contains(
                InfoCarrierModelValidator.HalfConfiguredMessage, StringComparison.Ordinal) == true);
    }

    /// <summary>
    ///     A client that has said nothing is not warned, and that is deliberate.
    /// </summary>
    /// <remarks>
    ///     ADR-009 Tier A is exactly this shape and its store is not relational, so a model with
    ///     <c>ToTable</c> on it is ordinary rather than suspicious. The trigger reads the client's
    ///     own configuration first, and never the shape of the model alone.
    /// </remarks>
    [ConditionalFact]
    public void A_plain_client_that_has_said_nothing_is_not_warned()
    {
        ListLoggerFactory logger = new(_ => true);

        using SimpleContext context = CreateContext(services => services, logger);

        Assert.NotNull(context.Model);

        Assert.DoesNotContain(
            logger.Log,
            entry => entry.Message?.Contains(
                InfoCarrierModelValidator.HalfConfiguredMessage, StringComparison.Ordinal) == true);
    }

    /// <remarks>
    ///     The transport stub is the shared harness's, through
    ///     <c>InfoCarrierTestHelpers.UseProviderOptions</c>, rather than a second one written here:
    ///     these tests build models and reach no server, and that helper already owns the "client
    ///     that is never called" shape.
    /// </remarks>
    private static SimpleContext CreateContext(
        Func<IServiceCollection, IServiceCollection> addRelational,
        ListLoggerFactory logger)
        => new((DbContextOptions<SimpleContext>)InfoCarrierTestHelpers.Instance
            .UseProviderOptions(new DbContextOptionsBuilder<SimpleContext>())
            .UseInternalServiceProvider(
                addRelational(new ServiceCollection().AddEntityFrameworkInfoCarrier())
                    .AddSingleton<ILoggerFactory>(logger)
                    .BuildServiceProvider(validateScopes: false))
            .Options);

    /// <summary>
    ///     A TPT model, which is the shape the missing conventions would have changed.
    /// </summary>
    private class SimpleContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>().ToTable("Animals");
            modelBuilder.Entity<Bird>().ToTable("Birds");
        }
    }

    private class Animal
    {
        public int Id { get; set; }
    }

    private class Bird : Animal
    {
        public string? Wingspan { get; set; }
    }
}
