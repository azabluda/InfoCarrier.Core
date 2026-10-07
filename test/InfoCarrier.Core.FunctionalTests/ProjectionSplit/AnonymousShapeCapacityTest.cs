// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using InfoCarrier.Core.Expressions;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

public class AnonymousShapeCapacityTest(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task Generation_capacity_is_shared_across_callers_but_cached_shapes_remain_usable()
    {
        const string childFlag = "INFOCARRIER_SHAPE_CAPACITY_CHILD";
        string test = typeof(AnonymousShapeCapacityTest).FullName + "."
            + nameof(Generation_capacity_is_shared_across_callers_but_cached_shapes_remain_usable);
        if (Environment.GetEnvironmentVariable(childFlag) == test)
        {
            MeasureCapacity();
            return;
        }

        // Exhaust only a separate test host. Its factory and provider caches die with the host,
        // so the regular suite never consumes or resets the production process's static budget.
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(AnonymousShapeCapacityTest).Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName=" + test);
        start.ArgumentList.Add("/Logger:console;verbosity=detailed");
        start.Environment[childFlag] = test;
        using Process child = Process.Start(start)!;
        Task<string> stdout = child.StandardOutput.ReadToEndAsync();
        Task<string> stderr = child.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await child.WaitForExitAsync(deadline.Token);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
        }

        string result = await stdout + await stderr;
        _output.WriteLine(result);
        Assert.True(child.ExitCode == 0, result);
        Assert.Contains("CAPACITY: generated=4096; new-caller=refused; cached-caller=reused", result);
    }

    private void MeasureCapacity()
    {
        var template = new TypeNode
        {
            Name = "InfoCarrier.AnonymousShape.v1/" + 0.ToString("X64"),
            ShapeMembers = ["Value"],
            GenericArguments = [new TypeNode { Name = "System.Int32" }],
        };
        using Process process = Process.GetCurrentProcess();
        long before = process.WorkingSet64;
        var timer = Stopwatch.StartNew();
        Type legitimate = new TypeNodeResolver().Resolve(template);
        for (int i = 1; i < 4096; i++)
        {
            TypeNode hostile = template with { Name = "InfoCarrier.AnonymousShape.v1/" + i.ToString("X64") };
            // Each resolver represents a different exchange. No exchange exceeds its own cap.
            Assert.NotNull(new TypeNodeResolver().Resolve(hostile));
        }

        var otherCaller = new TypeNodeResolver();
        TypeNode newLegitimate = template with { Name = "InfoCarrier.AnonymousShape.v1/" + 4096.ToString("X64") };
        Assert.Throws<InvalidOperationException>(() => otherCaller.Resolve(newLegitimate));
        Assert.Throws<InvalidOperationException>(() => otherCaller.Resolve(newLegitimate));
        Assert.Same(legitimate, otherCaller.Resolve(template));
        timer.Stop();
        process.Refresh();
        _output.WriteLine("CAPACITY: generated=4096; new-caller=refused; cached-caller=reused");
        _output.WriteLine($"Elapsed: {timer.Elapsed}; working-set increase: {process.WorkingSet64 - before} bytes.");
    }
}
