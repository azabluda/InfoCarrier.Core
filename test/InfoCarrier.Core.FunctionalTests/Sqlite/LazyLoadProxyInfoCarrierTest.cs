// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>LazyLoadProxyRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         Lazy loading through Castle proxies rather than through an injected
///         <c>ILazyLoader</c> — a different mechanism from the one Phase L fixed, and the one v1
///         covered with this same base. <c>Microsoft.EntityFrameworkCore.Proxies</c> and
///         <c>Castle.Core</c> arrive transitively with the specification-tests package, so this
///         adds no dependency and does not touch ADR-001.
///     </para>
///     <para>
///         <b>Moved from Tier A, and the move deletes 700 lines of accommodation.</b> The class it
///         replaces ignored <c>Milk</c> and <c>Culture</c> on twenty-nine entity types, because the
///         InMemory store has no complex types, and then carried two 680-line JSON strings
///         restating what the model looked like once they were gone. The relational base maps both
///         complex properties itself, so all of it goes.
///     </para>
///     <para>
///         <b>Nothing here needs a <c>UseTransaction</c> override.</b> Neither base declares one and
///         neither calls the transaction helper — checked, not assumed.
///     </para>
/// </remarks>
public class LazyLoadProxyInfoCarrierTest(LazyLoadProxyInfoCarrierTest.InfoCarrierFixture fixture)
    : LazyLoadProxyRelationalTestBase<LazyLoadProxyInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    // EF's own `LazyLoadProxySqliteTest` overrides both of these strings in full -- 1360 lines
    // between them -- to change one token: `"Charge": 1.00` becomes `"Charge": 1.0`, because
    // SQLite has no decimal type and the scale that comes back is the scale the store wrote.
    // Every other character of both strings is the base's, so the substitution is written as a
    // substitution. A base whose text stops carrying the token makes the assertion fail rather
    // than silently pass, which is the property that makes this safe to write this way.
    protected override string SerializedBlogs1
        => base.SerializedBlogs1.Replace("\"Charge\": 1.00", "\"Charge\": 1.0");

    /// <inheritdoc cref="SerializedBlogs1" />
    protected override string SerializedBlogs2
        => base.SerializedBlogs2.Replace("\"Charge\": 1.00", "\"Charge\": 1.0");

    /// <summary>
    ///     The lazy-loading-proxy fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class InfoCarrierFixture : LoadRelationalFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "LazyLoadProxyInfoCarrierTest";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
