// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore;

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
    ///     Seeded in M1-I3; see docs/plans/v10/implementation-plan.md.
    /// </summary>
    protected override ICollection<Type> IgnoredTestBases { get; } = [];
}
