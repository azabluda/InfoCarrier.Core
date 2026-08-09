// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Scaffolding;

/// <summary>
///     <c>CompiledModelTestBase</c>, on ADR-009 <b>Tier A</b> (C0).
/// </summary>
/// <remarks>
///     <para>
///         C0 priced this as expensive on the strength of the 112 generated baseline files EF ships
///         beside its own InMemory variant. <b>That price was wrong</b>: <c>AssertBaseline</c>
///         returns early when the baseline directory does not exist — <i>"cannot look for the
///         baseline"</i> — so the baselines are opt-in, and the base's real contract is one abstract
///         member, <c>TestHelpers</c>, which this project already has for <c>F1FixtureBase</c>'s
///         sake.
///     </para>
///     <para>
///         What is under test is that this provider's model can be <em>scaffolded into source,
///         compiled, and loaded back</em> as a runtime model matching the design-time one — which
///         for a provider whose whole job is shipping models across a wire is a more pertinent
///         question than the file count suggested.
///     </para>
/// </remarks>
public class CompiledModelInfoCarrierTest(NonSharedFixture fixture) : CompiledModelTestBase(fixture)
{
    /// <inheritdoc />
    protected override TestHelpers TestHelpers
        => InfoCarrierTestHelpers.Instance;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            typeof(Microsoft.EntityFrameworkCore.DbContext),
            onModelCreating: null);
}
