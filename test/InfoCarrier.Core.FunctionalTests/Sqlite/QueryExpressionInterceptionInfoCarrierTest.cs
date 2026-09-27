// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>QueryExpressionInterceptionTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         An <c>IQueryExpressionInterceptor</c> sees the query tree on its way into compilation,
///         which for this provider is the tree that is about to be <em>captured and split</em>
///         (ADR-006). That makes the base a check on where the capture sits relative to EF's own
///         interception point, which nothing else here exercises.
///     </para>
///     <para>
///         The structure — an abstract half plus two concrete classes differing only in whether
///         they subscribe to the diagnostic listener — is EF's own
///         <c>QueryExpressionInterceptionSqliteTestBase</c>.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Tier A followed EF's
///         InMemory class instead, which skips <c>Interceptor_does_not_leak_across_contexts</c>, and
///         it seeded the rows again before each context, because an InMemory store kept one test's
///         rows for the next. EF's SQLite class does neither.
///     </para>
/// </remarks>
public abstract class QueryExpressionInterceptionInfoCarrierTestBase(
    QueryExpressionInterceptionInfoCarrierTestBase.InterceptionInfoCarrierFixtureBase fixture)
    : QueryExpressionInterceptionTestBase(fixture)
{
    public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     The base builds an internal service provider for the injected interceptors and it
        ///     must carry the client's provider services, as EF's SQLite version adds
        ///     <c>AddEntityFrameworkSqlite</c>. The store factory adds this provider's, or SQLite's
        ///     in the plain-EF half of #167's slow run; Tier A added this provider's by name.
        /// </remarks>
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(TestStoreFactory.AddProviderServices(serviceCollection), injectedInterceptors);
    }
}

/// <inheritdoc cref="QueryExpressionInterceptionInfoCarrierTestBase" />
public class QueryExpressionInterceptionInfoCarrierTest(
    QueryExpressionInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : QueryExpressionInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<QueryExpressionInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override string StoreName
            => "QueryExpressionInterceptionInfoCarrier";

        protected override bool ShouldSubscribeToDiagnosticListener
            => false;
    }
}

/// <inheritdoc cref="QueryExpressionInterceptionInfoCarrierTestBase" />
public class QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest(
    QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : QueryExpressionInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override string StoreName
            => "QueryExpressionInterceptionWithDiagnosticsInfoCarrier";

        protected override bool ShouldSubscribeToDiagnosticListener
            => true;
    }
}
