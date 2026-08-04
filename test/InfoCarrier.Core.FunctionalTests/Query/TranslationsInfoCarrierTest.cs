// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     EF Core 10's per-type translation suites on ADR-009 Tier A — all sixteen of them.
/// </summary>
/// <remarks>
///     <para>
///         These replaced the single sprawling <c>*FunctionsQuery</c> base with one class per CLR
///         type or operator family, over one shared model (<c>BasicTypesQueryFixtureBase</c>). They
///         are the densest scalar coverage EF has, and this provider has had none of it: every
///         value here crosses the wire as a constant, a parameter or a projected column, which is
///         exactly what <c>PrimitiveCoercion</c> and the allowlist decide (A19, A34).
///     </para>
///     <para>
///         Adopted as EF's own <c>*InMemoryTest</c> pieces are: one shared fixture, one class per
///         base, and the three <c>StringComparison</c> overrides EF's InMemory suite carries — the
///         culture-sensitive comparisons no real provider supports and the InMemory one does, so
///         the base asserts a throw that this backing store will not produce.
///     </para>
/// </remarks>
public class ByteArrayTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ByteArrayTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class EnumTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : EnumTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class GuidTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : GuidTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class MathTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MathTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
/// <remarks>
///     The six overrides are EF's own <c>MiscellaneousTranslationsRelationalTestBase</c> (A27).
///     <c>Random.Next()</c> in a <c>Where</c> is client code in a row-deciding argument, so
///     <c>RejectClientEvaluation</c> refuses it for the reason A44 states — and it is worse here
///     than for a relational provider, since a random number drawn on the client would decide
///     which rows are fetched from the server, once, and then be gone.
/// </remarks>
public class MiscellaneousTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MiscellaneousTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Random_Shared_Next_with_no_args()
        => AssertTranslationFailed(() => base.Random_Shared_Next_with_no_args());

    /// <inheritdoc />
    public override Task Random_Shared_Next_with_one_arg()
        => AssertTranslationFailed(() => base.Random_Shared_Next_with_one_arg());

    /// <inheritdoc />
    public override Task Random_Shared_Next_with_two_args()
        => AssertTranslationFailed(() => base.Random_Shared_Next_with_two_args());

    /// <inheritdoc />
    public override Task Random_new_Next_with_no_args()
        => AssertTranslationFailed(() => base.Random_new_Next_with_no_args());

    /// <inheritdoc />
    public override Task Random_new_Next_with_one_arg()
        => AssertTranslationFailed(() => base.Random_new_Next_with_one_arg());

    /// <inheritdoc />
    public override Task Random_new_Next_with_two_args()
        => AssertTranslationFailed(() => base.Random_new_Next_with_two_args());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
/// <remarks>
///     The three overrides are EF's own <c>StringTranslationsInMemoryTest</c>:
///     <c>StringComparison.CurrentCulture</c> and <c>InvariantCulture</c> (with and without
///     <c>IgnoreCase</c>) are unsupported in real providers and the base asserts that — but the
///     InMemory store, which is what sits behind this wire, does support them.
/// </remarks>
public class StringTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : StringTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task StartsWith_with_StringComparison_unsupported()
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task EndsWith_with_StringComparison_unsupported()
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Contains_with_StringComparison_unsupported()
        => Task.CompletedTask;
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class ArithmeticOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ArithmeticOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class BitwiseOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : BitwiseOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class ComparisonOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ComparisonOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class LogicalOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : LogicalOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class MiscellaneousOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MiscellaneousOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateOnlyTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateOnlyTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateTimeTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateTimeTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateTimeOffsetTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateTimeOffsetTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class TimeOnlyTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : TimeOnlyTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class TimeSpanTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : TimeSpanTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The basic-types fixture, wired to an InMemory backend behind the wire. Shared by all
///     sixteen classes above, exactly as EF shares its own.
/// </summary>
public class BasicTypesQueryInfoCarrierFixture : BasicTypesQueryFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
}
