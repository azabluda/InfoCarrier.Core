// Licensed under the MIT license. See license.txt file in the project root for license information.


// Create the existing MkDocs environment when needed. --build is strict; preview accepts all
// MkDocs serve arguments. This wrapper removes Bash, not MkDocs' interpreter dependency.
internal static class DocsServe
{
    private static string Root() => ScriptHost.Root;
    private static async Task<int> Run(string executable, string[] args, CancellationToken token, bool quiet = false)
        => (await ScriptHost.Execute(executable, args, Root(), quiet, token)).ExitCode;

    public static async Task<int> Run(string[] args, CancellationToken token)
    {
        try
        {
            string? interpreter = null;
            foreach (var candidate in OperatingSystem.IsWindows() ? new[] { "python", "py", "python3" } : ["python3", "python", "py"])
            {
                try { if (await Run(candidate, ["-c", "pass"], token, true) == 0) { interpreter = candidate; break; } }
                catch (System.ComponentModel.Win32Exception) { }
            }
            if (interpreter is null) throw new IOException("docs-serve: no working Python interpreter found for MkDocs.");
            var bin = Path.Combine(Root(), ".venv", OperatingSystem.IsWindows() ? "Scripts" : "bin");
            var mkdocs = Path.Combine(bin, OperatingSystem.IsWindows() ? "mkdocs.exe" : "mkdocs");
            if (!File.Exists(mkdocs))
            {
                Console.WriteLine("docs-serve: creating .venv and installing website/requirements.txt…");
                var status = await Run(interpreter, ["-m", "venv", ".venv"], token);
                if (status != 0) return status;
                status = await Run(Path.Combine(bin, OperatingSystem.IsWindows() ? "python.exe" : "python"),
                    ["-m", "pip", "install", "--quiet", "--disable-pip-version-check", "-r", "website/requirements.txt"], token);
                if (status != 0) return status;
            }
            // MkDocs prints the actual preview URL; do not invent a second, potentially wrong URL.
            return await Run(mkdocs, args.FirstOrDefault() == "--build"
                ? ["build", "--strict", "--config-file", "website/mkdocs.yml"]
                : ["serve", "--config-file", "website/mkdocs.yml", .. args], token);
        }
        catch (OperationCanceledException) { return 130; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { Console.Error.WriteLine(error.Message); return 1; }
    }
}
