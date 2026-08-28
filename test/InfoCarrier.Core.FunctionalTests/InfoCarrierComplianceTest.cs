// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The coverage scoreboard (ADR-004). Fails while any
///     <c>EFCore.Specification.Tests</c> base class has no InfoCarrier subclass, listing every
///     one that is missing.
/// </summary>
/// <remarks>
///     <para>
///         <strong>This test is expected to be red for a long time, and that is its job.</strong>
///         It converts "adopt the EF Core suite" from an unbounded intention into a generated,
///         auditable inventory: every base is either implemented or listed in
///         <see cref="IgnoredTestBases" /> with a stated reason. Nothing can be silently
///         forgotten.
///     </para>
///     <para>
///         Only bases that are <em>conceptually inapplicable to a remoting provider</em> belong
///         in <see cref="IgnoredTestBases" />. A base that is merely not built yet must stay
///         out of the list so this test keeps reporting it.
///     </para>
/// </remarks>
public class InfoCarrierComplianceTest : RelationalComplianceTestBase
{
    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(InfoCarrierComplianceTest).Assembly;

    /// <summary>
    ///     Bases that are conceptually inapplicable to InfoCarrier — each with the reason.
    ///     Seeded in M1-I3; the relational entries were added for #56.
    /// </summary>
    /// <remarks>
    ///     A base that is merely unadopted must NOT be listed here. Everything below needs a
    ///     service or an object that does not exist on this side of the wire, and no amount of
    ///     work in this repository would change that.
    /// </remarks>
    protected override ICollection<Type> IgnoredTestBases { get; } =
    [
        // Migrations run DDL against a database. The client has none. The server is an ordinary
        // EF application, and EF already tests migrations for the provider it references.
        typeof(MigrationsInfrastructureTestBase<>),
        typeof(MigrationsTestBase<>),

        // Asserts the SQL an IMigrationsSqlGenerator emits, resolved from the context's services.
        typeof(MigrationsSqlGeneratorTestBase),

        // Same shape: CreateSqlGenerator() must return an IUpdateSqlGenerator.
        typeof(UpdateSqlGeneratorTestBase),

        // Asserts that EntityFrameworkRelationalServicesBuilder.RelationalServices are registered.
        // InfoCarrier.Core stopped referencing EFCore.Relational in M9 and registers none of them.
        typeof(RelationalServiceCollectionExtensionsTestBase),

        // Interception of DbCommand, DbConnection and DbTransaction. The client holds no ADO.NET
        // object to intercept. On the server this is ordinary EF, which EF tests.
        typeof(CommandInterceptionTestBase),
        typeof(ConnectionInterceptionTestBase),
        typeof(TransactionInterceptionTestBase),

        // Resolves IReverseEngineerScaffolder and IMigrationsScaffolder from the provider's
        // design-time services. Both scaffold from a database, which the client does not have.
        typeof(DesignTimeTestBase<>),

        // Precompiled queries pregenerate a provider's SQL at build time on the client. This
        // client compiles no SQL at all: the server generates it per request, after the wire.
        typeof(PrecompiledQueryRelationalTestBase),
        typeof(PrecompiledSqlPregenerationQueryRelationalTestBase),
        typeof(AdHocPrecompiledQueryRelationalTestBase),
    ];
}
