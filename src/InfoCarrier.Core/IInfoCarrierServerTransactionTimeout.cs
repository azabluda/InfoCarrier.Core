// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     Present in a <em>server's</em> service collection when that server evicts a transaction no
///     client has touched for <see cref="IdleTimeout" /> (#54).
/// </summary>
/// <remarks>
///     <para>
///         <b>Absent by default, and that is a decision rather than an oversight.</b> A server
///         that does not register this holds an open transaction until the process exits, which is
///         how every version up to <c>10.1.0</c> behaved. Turning eviction on by default would
///         change what a working deployment does: a transaction held open across a slow user step
///         would begin rolling back where it previously did not. So the deployment says how long
///         is too long, because only the deployment knows.
///     </para>
///     <para>
///         <b>What it protects against is a client that never runs again.</b>
///         <c>InProcessInfoCarrierServer</c> pins a DI scope, a <c>DbContext</c> and a
///         store connection per open transaction, keyed by the wire token. The client's own
///         <c>DisposeAsync</c> covers every ordinary path including an exception; it cannot cover
///         a closed tab, a dropped network or a crashed process. Once such a transaction has
///         written, it also holds the store's write lock.
///     </para>
///     <para>
///         <b>Any request naming the token counts as activity</b>, because every one of them goes
///         through the server's single lookup: a query, a save, and each of the four savepoint
///         operations. So the timeout measures idleness rather than age, and a long unit of work
///         that keeps talking is never evicted.
///     </para>
///     <para>
///         <b>An evicted transaction is rolled back.</b> A later query, save or savepoint naming
///         the token is refused with the message it gets for one that was committed or rolled
///         back, because that is what happened to it. The eviction is logged on the server at
///         <c>Warning</c>, which
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierServerLogForwarding" />
///         carries to the client where a deployment has granted that too. A commit is the one case
///         that is answered differently, and the paragraph below is why.
///     </para>
///     <para>
///         <b>A COMMIT ARRIVING AFTER AN EVICTION THROWS, and getting this wrong first is what
///         made it worth writing down.</b> The server has always tolerated an end for a token it
///         does not hold, so that a client rolling back on disposal after an explicit commit is
///         not punished for the ordinary <c>using</c> pattern. Extending that tolerance to a
///         COMMIT is silent data loss once eviction exists:
///     </para>
///     <para>
///         <c>Begin</c>, <c>SaveChanges</c>, idle past the timeout so the server rolls it back,
///         then <c>Commit</c>, which reports success for writes that no longer exist. The argument
///         that first excused this claimed such a client would meet the refusal at its next
///         request. It does not: <c>Commit</c> is the one operation that never looks the token up,
///         and the work happened <em>before</em> the idle period rather than after it.
///     </para>
///     <para>
///         <b>So a commit for an unheld token throws and a rollback stays silent</b>, which needs
///         no record of evicted tokens: the pattern the tolerance exists for ends with a rollback,
///         never with a second commit.
///     </para>
///     <para>
///         It answers only part 1 of #54. A token is still not bound to its creator, so any caller
///         holding one can still end that transaction, and the registry is still process-local, so
///         a load-balanced deployment still needs session affinity for a transaction's life. Both
///         need a protocol change and neither is affected by this.
///     </para>
/// </remarks>
public interface IInfoCarrierServerTransactionTimeout
{
    /// <summary>
    ///     How long a transaction may go untouched before the server rolls it back.
    /// </summary>
    /// <remarks>
    ///     Measured from the last request that named the token, not from
    ///     <c>BeginTransactionAsync</c>.
    /// </remarks>
    TimeSpan IdleTimeout { get; }
}
