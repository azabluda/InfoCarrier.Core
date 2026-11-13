// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
/// Owns one process's database directory independently of its SQLite connection lifetimes.
/// </summary>
internal sealed class SqliteTestRun
{
    // Intentionally retained until process exit. Store/fixture disposal must not release ownership.
    private readonly FileStream _ownership;
    public string DirectoryPath { get; }

    public SqliteTestRun()
    {
        string root = Path.GetFullPath(".infocarrier-sqlite");
        Directory.CreateDirectory(root);

        // Serialize publication with reclamation, including crashes between creating the lease
        // and directory. No process can see a new directory before its ownership is established.
        using FileStream startup = AcquireStartup(Path.Combine(root, ".startup.lock"));
        foreach (string directory in Directory.EnumerateDirectories(root, "run-*"))
        {
            string name = Path.GetFileName(directory);
            if (!Guid.TryParseExact(name[4..], "N", out _)
                || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string lease = directory + ".lease";
            try
            {
                // FileShare.None is an OS-backed lease, also when every database is closed.
                // The startup lock makes a missing lease an abandoned partial creation.
                using (var abandoned = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Directory.Delete(directory, recursive: true);
                }
                File.Delete(lease);
            }
            catch (IOException)
            {
                // Active owner, or files still held externally. Leave the entire remainder for
                // a later startup; a partial cleanup cannot affect an owned run.
            }
            catch (UnauthorizedAccessException)
            {
                // An inaccessible directory is not safe to reclaim.
            }
        }

        // A crash after directory deletion, or before directory creation, can leave only a lease.
        // Publication and reclamation still hold the startup lock here.
        foreach (string lease in Directory.EnumerateFiles(root, "run-*.lease"))
        {
            string directory = lease[..^".lease".Length];
            if (!Guid.TryParseExact(Path.GetFileName(directory)[4..], "N", out _) || Directory.Exists(directory)) continue;
            try
            {
                using (var abandoned = new FileStream(lease, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(lease);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        DirectoryPath = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
        _ownership = new FileStream(DirectoryPath + ".lease", FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        try
        {
            Directory.CreateDirectory(DirectoryPath);
        }
        catch
        {
            _ownership.Dispose();
            File.Delete(DirectoryPath + ".lease");
            throw;
        }
    }

    private static FileStream AcquireStartup(string path)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(30))
            {
                Thread.Sleep(20);
            }
        }
    }
}
