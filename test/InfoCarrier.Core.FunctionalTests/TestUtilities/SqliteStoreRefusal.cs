// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Data.Sqlite;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The assertion of an override whose store refuses the query, in whichever form the refusal
///     reaches the test: across the wire, or from plain EF Core in a slow run's plain-EF half.
/// </summary>
/// <remarks>
///     <para>
///         <b>Across the wire the store's exception arrives as data</b>
///         (<see cref="DeviationKind.StoreExceptionAsData" />): an <see cref="InfoCarrierServerException" />
///         carrying the engine's type name and message, both of which are asserted.
///     </para>
///     <para>
///         <b>Plain EF Core throws the engine's own <see cref="SqliteException" /></b>, which is what
///         EF's own SQLite tests assert. Accepting it there is what lets a slow run compare this
///         test at all (#167): the store refuses at the same place, with the same message, on both
///         sides, and only the form differs. The four overrides that assert this used to carry four
///         copies of the wire half.
///     </para>
/// </remarks>
internal static class SqliteStoreRefusal
{
    /// <summary>
    ///     Asserts that <paramref name="query" /> fails in the store with a message containing
    ///     <paramref name="messageFragment" />.
    /// </summary>
    public static async Task AssertAsync(Func<Task> query, string messageFragment)
    {
        if (DirectClient.IsEnabled)
        {
            Assert.Contains(messageFragment, (await Assert.ThrowsAsync<SqliteException>(query)).Message);
            return;
        }

        InfoCarrierServerException exception = await Assert.ThrowsAsync<InfoCarrierServerException>(query);

        Assert.Equal(typeof(SqliteException).FullName, exception.ServerExceptionTypeName);
        Assert.Contains(messageFragment, exception.Message);
    }
}
