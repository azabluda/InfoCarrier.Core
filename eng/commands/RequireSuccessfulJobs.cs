// Licensed under the MIT license. See license.txt file in the project root for license information.

internal static class RequireSuccessfulJobs
{
    public static int Run(string[] args, TextWriter error)
    {
        if (args.Length == 0)
        {
            error.WriteLine("usage: RequireSuccessfulJobs.cs <job=result> [job=result ...]");
            return 2;
        }

        var failed = false;
        foreach (var argument in args)
        {
            var separator = argument.IndexOf('=');
            if (separator <= 0 || separator == argument.Length - 1)
            {
                error.WriteLine($"Invalid job result '{argument}'; expected job=result.");
                return 2;
            }

            var name = argument[..separator];
            var result = argument[(separator + 1)..];
            if (result == "success") continue;
            error.WriteLine($"Required job '{name}' ended with '{result}', not 'success'.");
            failed = true;
        }

        return failed ? 1 : 0;
    }
}
