// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Captures fixture state (context type, model customization, options) so the
///     parameterless <see cref="Microsoft.EntityFrameworkCore.TestUtilities.ITestStoreFactory" />
///     members can build correctly-configured contexts on both sides of the wire (v1 pattern).
/// </summary>
public struct SharedTestStoreProperties
{
    /// <summary>
    ///     The <see cref="DbContext" /> type, used by BOTH sides.
    /// </summary>
    /// <remarks>
    ///     <b>One type, one model, both halves</b> — version 1 of this provider did exactly this,
    ///     and an application does it too: <c>samples/Northwind.Client</c> and
    ///     <c>samples/Northwind.Server</c> share one <c>NorthwindContext</c>. This harness carried a
    ///     second <c>ServerContextType</c> until R144, so that the server could hold store shape the
    ///     client had no way to express. Making every client relational removed that need, and
    ///     converging every fixture was measured to change no answer anywhere in the suite.
    /// </remarks>
    public Type ContextType;

    /// <summary>
    ///     The fixture's model customization.
    /// </summary>
    public Action<ModelBuilder, DbContext>? OnModelCreating;

    /// <summary>
    ///     The fixture's convention configuration — the other half of its model.
    /// </summary>
    /// <remarks>
    ///     <see cref="OnModelCreating" /> is not all a fixture says about its model.
    ///     <c>ConfigureConventions</c> is where a type-wide rule goes: every
    ///     <c>NorthwindQueryFixtureBase</c> routes its model customizer through it,
    ///     <c>LazyLoadProxyTestBase</c> declares five complex types there, and
    ///     <c>StoreGeneratedFixtureBase</c> registers three dozen value converters. The server
    ///     builds <em>the same model as the client</em> or the wire has nothing to agree on, so it
    ///     needs both halves — <c>TestModelSource.GetFactory</c> has taken this since EF wrote it,
    ///     and it was simply never passed.
    /// </remarks>
    public Action<ModelConfigurationBuilder>? ConfigureConventions;

    /// <summary>
    ///     Additional options configuration applied to the server context.
    /// </summary>
    public Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? OnAddOptions;

    /// <summary>
    ///     Copies per-request parameters from the client context to the server context
    ///     (e.g. tenant prefix), invoked server-side per request.
    /// </summary>
    public Action<DbContext, DbContext>? CopyDbContextParameters;

    /// <summary>
    ///     The lifetime of the server context's <see cref="DbContextOptions" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <see cref="ServiceLifetime.Singleton" /> for every fixture but one, because the
    ///         options never change: the connection string and the model are fixed for the store's
    ///         lifetime, and building them once is what the whole suite wants.
    ///     </para>
    ///     <para>
    ///         <b><see cref="ServiceLifetime.Transient" /> is for a fixture whose server options
    ///         differ per REQUEST.</b> Only <c>NullSemanticsQueryInfoCarrierTest</c> needs it. Its
    ///         base calls <c>CreateContext(useRelationalNulls: true)</c> in some tests and
    ///         <c>false</c> in others, and <c>UseRelationalNulls</c> is a provider option that
    ///         belongs to the server. Singleton options cannot express "this request wants
    ///         relational nulls", so that fixture asks for transient ones and reads an ambient flag
    ///         in its <see cref="OnAddOptions" />.
    ///     </para>
    ///     <para>
    ///         Opt-in rather than the default, because transient options are rebuilt on every
    ///         server context resolution — about 29,000 times in a full run — and no other fixture
    ///         gets anything for the cost.
    ///     </para>
    /// </remarks>
    public ServiceLifetime? ServerOptionsLifetime;

    /// <summary>
    ///     Extra services the <em>server</em> provider needs.
    /// </summary>
    /// <remarks>
    ///     A fixture's <c>AddServices</c> configures the client provider, which is usually all
    ///     that matters. It is not enough when the fixture's own seed depends on those services:
    ///     <c>PropertyValuesFixtureBase</c> registers a materialization interceptor and then
    ///     asserts in <c>SeedAsync</c> that it ran, and the seed executes against the server.
    /// </remarks>
    public Func<IServiceCollection, IServiceCollection>? OnAddServices;

    /// <summary>
    ///     Whether this fixture's client and server both grant raw SQL execution (#60).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Per fixture, and default off, for the same reason
    ///         <c>relationalClientStore</c> is</b> - the opt-in list is the record of which bases
    ///         need it. A fixture that sets this gets
    ///         <c>services.AddInfoCarrierArbitrarySqlExecution()</c> on the server and
    ///         <c>o.AllowArbitrarySqlExecution()</c> on the client, which are the two halves of the
    ///         seam and only the first of which is a security boundary.
    ///     </para>
    ///     <para>
    ///         <b>Not granted suite-wide</b>, deliberately. The default refusal is what every other
    ///         fixture exercises, and two of them assert it directly through
    ///         <see cref="FromSqlAssertions" />. Granting globally would delete that coverage and
    ///         claim, of every fixture in the suite, that its deployment had made a security
    ///         decision it has not.
    ///     </para>
    /// </remarks>
    public bool ArbitrarySqlExecution;

    /// <summary>
    ///     Whether this fixture's server sends the log events it raises back to the client
    ///     (<c>IInfoCarrierServerLogForwarding</c>, R172).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Per fixture and default off, like the raw-SQL grant above, and for a sharper
    ///         reason.</b> A forwarded event lands in the client's own logger, which is the same
    ///         <c>TestSqlLoggerFactory</c> a spec base reads with <c>Assert.Single</c> and
    ///         <c>Assert.Empty</c>. Granting it suite-wide would put the server's warnings into
    ///         every such assertion at once.
    ///     </para>
    ///     <para>
    ///         The fixtures that set it get the <b>sensitive</b> grant, because the base that
    ///         needs forwarding at all — <c>TableSplittingTestBase</c> — has one test that asserts
    ///         the sensitive wording and one that asserts the plain one, and which it gets is the
    ///         context's own <c>EnableSensitiveDataLogging</c> rather than the grant's.
    ///     </para>
    /// </remarks>
    public bool ServerLogForwarding;

    /// <summary>
    ///     CLR types this fixture's queries name that its model does not imply (ADR-008
    ///     constraint 2).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A projection DTO is the case, and <c>SqlQueryTestBase</c> is the base that
    ///         made it real.</b> <c>Database.SqlQuery&lt;UnmappedCustomer&gt;</c> makes EF build an
    ///         <em>ad-hoc</em> entity type, which lives outside <c>IModel.GetEntityTypes()</c>, so
    ///         <c>TypeAllowlist.ForModel</c> cannot infer it and the boundary refuses the query
    ///         root. That is the allowlist doing its job: the type is not in the model, and
    ///         nothing about the model implies it.
    ///     </para>
    ///     <para>
    ///         <b>An application declares such a type explicitly, and the harness is an
    ///         application.</b> The seam is <c>InfoCarrierDbContextOptionsBuilder.AllowTypes</c> on
    ///         the client and <c>AddInfoCarrierAllowedTypes</c> on the server, and
    ///         <see cref="Expressions.IInfoCarrierAllowedTypes" /> requires <b>both</b> halves —
    ///         one alone fails asymmetrically. A fixture setting this gets both.
    ///     </para>
    ///     <para>
    ///         <b>Not gated on <see cref="ArbitrarySqlExecution" />, unlike the store's parameter
    ///         type.</b> A <c>DbParameter</c> can only appear in a raw-SQL payload; a projection
    ///         DTO is independent of raw SQL, and gating it would state a dependency that is not
    ///         there.
    ///     </para>
    /// </remarks>
    public Type[]? AllowedTypes;
}
