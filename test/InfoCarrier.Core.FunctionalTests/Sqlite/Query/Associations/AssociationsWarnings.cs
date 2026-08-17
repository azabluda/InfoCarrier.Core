// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The one warning the <c>Associations</c> bases contract with their fixtures about, forwarded
///     to the server (C69).
/// </summary>
/// <remarks>
///     <para>
///         <b>What the base asks for.</b> <c>AssociationsCollectionTestBase.AssertOrderedCollectionQuery</c>
///         expects an <see cref="InvalidOperationException" /> whenever
///         <c>Fixture.AreCollectionsOrdered</c> is <see langword="false" />, and names the event in
///         a comment on the line above: <i>"An error was generated for warning
///         'Microsoft.EntityFrameworkCore.Query.RowLimitingOperationWithoutOrderByWarning'"</i>.
///         `NavigationsFixtureBase` and `OwnedNavigationsRelationalFixtureBase` both return
///         <see langword="false" />, so for those two families the throw is part of the contract
///         rather than an accident of configuration.
///     </para>
///     <para>
///         <b>Why the server needs telling.</b> That diagnostic comes from
///         <c>RelationalQueryableMethodTranslatingExpressionVisitor</c> — the <em>backing store's</em>
///         translator — and under this provider the backing store is a second EF instance which
///         does not inherit the fixture's <c>ConfigureWarnings</c>. C55 probed it: the query ships
///         whole and unrewritten, the server raises the warning, and nothing turns it into an
///         error. So the eight <c>Index_*</c> failures were never a missing client-side check.
///     </para>
///     <para>
///         <b>Why one event and not the fixture's whole configuration.</b> Forwarding
///         <c>Default(Throw)</c> to the server is measured at <b>8 fixed, 626 broken</b> (C55).
///         Most of the 626 are <em>model</em> warnings — the server's model is built by
///         <c>TestModelSource</c> against the backing store and is not the caller's — and the query
///         ones land on trees <c>ProjectionRewriter</c> and <c>GroupJoinFlattener</c> produced.
///         Neither is a statement the test author wrote. This event is: it is about
///         <c>AssociateCollection[0]</c>, which is verbatim what the test wrote.
///     </para>
///     <para>
///         <b>And why per fixture rather than globally.</b> Forwarding this single event to every
///         server would fix the eight and break <b>26</b> others — all of them, checked against
///         C55's log, in <c>NorthwindBulkUpdates</c> (14), <c>NorthwindWhere</c> (10) and
///         <c>NorthwindSelect</c> (2), and none in these families. Those bases do not contract for
///         the throw the way this one does.
///     </para>
///     <para>
///         <b>The honest counter-argument, recorded rather than argued away:</b> naming one event
///         is a narrower knob than "mirror the fixture", and a reader is entitled to ask whether it
///         was chosen because it makes eight tests pass. The answer is that the spec base names
///         this event itself, in the helper those eight tests go through — but the number and the
///         reasoning are both here so the trade can be re-judged.
///     </para>
/// </remarks>
internal static class AssociationsWarnings
{
    public static DbContextOptionsBuilder ThrowOnUnorderedRowLimiting(DbContextOptionsBuilder builder)
        => builder.ConfigureWarnings(w => w.Throw(CoreEventId.RowLimitingOperationWithoutOrderByWarning));
}
