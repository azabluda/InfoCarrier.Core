// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     A captured class for <c>SqlCaptureComplianceTest</c> to check against files it writes into a
///     folder of its own: one test labelled <see cref="DeviationKind.SqlDiffers" />, and one not.
/// </summary>
/// <remarks>
///     <b>Abstract, so that xUnit runs none of it</b> and the checks over the real repository skip it:
///     they read the classes xUnit runs. In the Sqlite namespace, because only a captured class has
///     files.
/// </remarks>
public abstract class SqlCaptureComplianceFixture
{
    [ConditionalFact]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers)]
    public void Differs()
    {
    }

    [ConditionalFact]
    public void Plain()
    {
    }
}

/// <summary>
///     A captured class whose <see cref="DeviationKind.SqlDiffers" /> reason covers one case of a
///     theory.
/// </summary>
/// <inheritdoc cref="SqlCaptureComplianceFixture" path="/remarks" />
public abstract class SqlCaptureComplianceCaseFixture
{
    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers, Case = "async: True")]
    public void Differs_when_async(bool async)
        => _ = async;
}
