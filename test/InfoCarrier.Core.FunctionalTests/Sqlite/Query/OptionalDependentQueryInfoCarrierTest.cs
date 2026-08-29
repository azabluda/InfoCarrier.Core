// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     Optional dependents, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>The question is when an optional dependent is <see langword="null" /> and when it is
///         an object whose properties are all null</b> — a distinction a store makes at the row
///         level and a wire has to preserve. Entities with every property optional and entities
///         with some required are both present, which is what makes the two cases separable.
///     </para>
///     <para>
///         <b>Tier B</b> by ADR-009's rule: EF ships <c>OptionalDependentQuerySqliteTest</c> and no
///         InMemory counterpart. The base rather than EF's SQLite subclass, whose every override is
///         an <c>AssertSql</c> golden string.
///     </para>
/// </remarks>
public class OptionalDependentQueryInfoCarrierTest : OptionalDependentQueryTestBase<OptionalDependentQueryInfoCarrierFixture>
{
    public OptionalDependentQueryInfoCarrierTest(
        OptionalDependentQueryInfoCarrierFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }
}

/// <summary>
///     The optional-dependent fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class OptionalDependentQueryInfoCarrierFixture : OptionalDependentQueryFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
