// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A test that needs SpatiaLite loaded into the SQLite this suite runs on: it runs on Windows,
///     where the <c>mod_spatialite</c> package brings the library, and is skipped elsewhere.
/// </summary>
/// <remarks>
///     <para>
///         EF's own <c>SpatialiteRequiredAttribute</c>, with one more condition. EF's tries the
///         load anywhere and skips when it fails. On Linux that is not safe: the package has no
///         Linux library, and Ubuntu's <c>libsqlite3-mod-spatialite</c>, loaded into the SQLite
///         EF's package bundles, crashed the test host outright on 2026-09-27 (GitHub run
///         36348293565). So off Windows nothing is loaded.
///     </para>
///     <para>
///         The Windows check is for a machine that HAS that package. CI's runner has none, and
///         without the check it skipped these tests cleanly too (run 36351274149, 2026-09-27): the
///         load fails and <c>TryLoad</c> returns false. Such a skip is printed by xUnit and recorded
///         in no TRX file, so CI's totals leave these tests out.
///     </para>
///     <para>
///         The owner, 2026-09-27: spatial stays on ADR-009 Tier A, and the spatial tests that need a
///         SQL store run on the Windows machines the suite is developed on and are skipped in CI.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SpatialiteRequiredAttribute : Attribute, ITestCondition
{
    private static readonly Lazy<bool> Loaded = new(() =>
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var connection = new SqliteConnection("Data Source=:memory:");
        return SpatialiteLoader.TryLoad(connection);
    });

    /// <inheritdoc />
    public ValueTask<bool> IsMetAsync()
        => new(Loaded.Value);

    /// <inheritdoc />
    public string SkipReason
        => "Needs SpatiaLite, which this suite loads on Windows only.";
}
