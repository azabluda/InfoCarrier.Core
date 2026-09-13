// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using Mongo2Go;

namespace InfoCarrier.Core.DocumentStoreTests.TestUtilities;

/// <summary>
///     One embedded <c>mongod</c>, started and — unlike <see cref="MongoDbRunner" /> — actually
///     stopped.
/// </summary>
/// <remarks>
///     <para>
///         <b>Extracted from <see cref="DocumentStoreFixture" /> on 2026-09-13, because a second
///         caller appeared.</b> The specification-base fixtures need a server on the same terms and
///         for the same reasons, and two copies of process bookkeeping is two places to get the
///         reaping wrong.
///     </para>
///     <para>
///         <b>STARTING IS SERIALIZED AND STOPPING IS ENFORCED, BECAUSE
///         <c>MongoDbRunner.Dispose()</c> DOES NOT RELIABLY STOP <c>mongod</c> (#102).</b> A run of
///         this tier was measured finishing green and leaving SIX live processes behind; the next
///         run then failed on data a previous run had written. Orphans are not a tidiness problem
///         here, they are the failure.
///     </para>
///     <para>
///         So each instance records which <c>mongod</c> its own start created and kills that process
///         if disposal left it running. <b>Identifying it is the whole reason the start is
///         serialized</b>: the only portable way to name a process a library started is to compare
///         the set of them before and after, and that difference means nothing while another thread
///         is starting one too.
///     </para>
///     <para>
///         <b>A REPLICA SET, NOT A STANDALONE, AND THAT IS NOT OPTIONAL.</b> The MongoDB EF provider
///         wraps <c>SaveChanges</c> in a transaction so that a multi-document write applies wholly
///         or not at all, and MongoDB has transactions only on a replica set. A standalone
///         <c>MongoDbRunner.Start()</c> throws on the first save.
///     </para>
/// </remarks>
public sealed class EmbeddedMongo : IAsyncDisposable
{
    private static readonly SemaphoreSlim StartGate = new(1, 1);

    private readonly MongoDbRunner _runner;

    private readonly Process[] _mongod;

    private EmbeddedMongo(MongoDbRunner runner, Process[] mongod)
    {
        _runner = runner;
        _mongod = mongod;
    }

    /// <summary>The connection string of this server.</summary>
    public string ConnectionString => _runner.ConnectionString;

    /// <summary>
    ///     Starts a server and records which <c>mongod</c> the start created.
    /// </summary>
    /// <remarks>
    ///     <see cref="Process.GetProcessesByName(string)" /> answers on Windows, Linux and macOS
    ///     alike, which this tier's no-installation bar requires.
    /// </remarks>
    public static async Task<EmbeddedMongo> StartAsync()
    {
        await StartGate.WaitAsync();
        try
        {
            HashSet<int> before = [.. Process.GetProcessesByName("mongod").Select(p => p.Id)];
            MongoDbRunner runner = MongoDbRunner.Start(singleNodeReplSet: true);
            Process[] mine = [.. Process.GetProcessesByName("mongod").Where(p => !before.Contains(p.Id))];
            return new EmbeddedMongo(runner, mine);
        }
        finally
        {
            StartGate.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _runner.Dispose();

        // What `Dispose` was supposed to do. Anything still alive here is an orphan that will hold
        // its port and its data into the next run, which is the failure this guards (#102).
        foreach (Process process in _mongod)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10_000);
                }
            }
            catch (Exception)
            {
                // Already gone, or not ours to kill. Either way there is nothing left to do, and a
                // fixture that throws while tearing down hides the result of the tests it ran.
            }

            process.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
