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
    public async Task Forged_shapes_across_exchanges_cannot_exhaust_registered_callers_capacity()
    {
        const string childFlag = "INFOCARRIER_SHAPE_CAPACITY_CHILD";
        string test = typeof(AnonymousShapeCapacityTest).FullName + "."
            + nameof(Forged_shapes_across_exchanges_cannot_exhaust_registered_callers_capacity);
        if (Environment.GetEnvironmentVariable(childFlag) == test)
        {
            MeasureCapacity();
            return;
        }

        // A separate host exercises the real process budget without resetting or reducing it.
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
        Assert.Contains("CAPACITY: aliases=8192; unknown=8192; registered-callers=usable", result);
        Assert.Contains("CATALOG: oversized=refused-before-emission; trusted-slots=4096; cached-registrations=usable", result);
    }

    private void MeasureCapacity()
    {
        Type firstOriginal = new { CapacityFirst = 1 }.GetType();
        Type secondOriginal = new { CapacitySecond = 2 }.GetType();
        AnonymousShapeCatalog catalog = AnonymousShapeCatalog.Create(null, [firstOriginal, secondOriginal]);
        var mapper = new TypeNodeMapper();
        TypeNode template = mapper.ToTypeNode(firstOriginal);
        var firstCaller = new TypeNodeResolver();
        firstCaller.UseAnonymousShapeCatalog(catalog);
        using Process process = Process.GetCurrentProcess();
        long before = process.WorkingSet64;
        var timer = Stopwatch.StartNew();
        Type legitimate = firstCaller.Resolve(template);
        for (int i = 0; i < 8192; i++)
        {
            TypeNode hostile = template with { Name = "InfoCarrier.AnonymousShape.v1/" + i.ToString("X64") };
            // Each resolver represents a different exchange. No exchange exceeds its own cap.
            var attacker = new TypeNodeResolver();
            attacker.UseAnonymousShapeCatalog(catalog);
            Assert.Same(legitimate, attacker.Resolve(hostile));
            var unknown = new TypeNodeResolver();
            unknown.UseAnonymousShapeCatalog(catalog);
            Assert.Throws<InvalidOperationException>(() => unknown.Resolve(
                hostile with { ShapeMembers = ["Unknown" + i] }));
        }

        var otherCaller = new TypeNodeResolver();
        otherCaller.UseAnonymousShapeCatalog(catalog);
        Assert.NotNull(otherCaller.Resolve(mapper.ToTypeNode(secondOriginal)));
        Assert.Same(legitimate, otherCaller.Resolve(template));
        // A trusted catalog created after the attack still has capacity for a new legitimate shape.
        Type laterOriginal = new { CapacityAfterAttack = 3 }.GetType();
        otherCaller.UseAnonymousShapeCatalog(AnonymousShapeCatalog.Create(null, [laterOriginal]));
        Assert.NotNull(otherCaller.Resolve(mapper.ToTypeNode(laterOriginal)));

        // Three trusted shapes exist in this otherwise isolated factory. An oversized catalog
        // must fail before consuming any of the real remaining 4093 emission slots.
        Type definition = new { CapacityTrustedBudget = 0 }.GetType().GetGenericTypeDefinition();
        Type[] leaves = Enumerable.Range(0, 20).Select(i =>
        {
            Type leaf = i < 10 ? typeof(int) : typeof(string);
            for (int depth = 0; depth < i % 10; depth++)
            {
                leaf = typeof(List<>).MakeGenericType(leaf);
            }

            return leaf;
        }).ToArray();

        Type[] excessive = Enumerable.Range(0, 4094).Select(i => definition.MakeGenericType(
            typeof(Tuple<,,>).MakeGenericType(leaves[i / 400], leaves[i / 20 % 20], leaves[i % 20]))).ToArray();
        Assert.Throws<InvalidOperationException>(() => AnonymousShapeCatalog.Create(null, excessive));
        var concurrent = new AnonymousShapeCatalog[4];
        Parallel.For(0, concurrent.Length, i => concurrent[i] = AnonymousShapeCatalog.Create(null, excessive.Take(4093)));
        AnonymousShapeCatalog complete = concurrent[0];
        otherCaller.UseAnonymousShapeCatalog(complete);
        TypeNode finalNode = new TypeNodeMapper().ToTypeNode(excessive[4092]);
        Type finalType = otherCaller.Resolve(finalNode);
        foreach (AnonymousShapeCatalog repeated in concurrent)
        {
            var repeatedCaller = new TypeNodeResolver();
            repeatedCaller.UseAnonymousShapeCatalog(repeated);
            Assert.Same(finalType, repeatedCaller.Resolve(finalNode));
        }
        Assert.Throws<InvalidOperationException>(() => AnonymousShapeCatalog.Create(null, [excessive[4093]]));
        firstCaller.UseAnonymousShapeCatalog(catalog);
        Assert.Same(legitimate, firstCaller.Resolve(template));
        timer.Stop();
        process.Refresh();
        _output.WriteLine("CAPACITY: aliases=8192; unknown=8192; registered-callers=usable");
        _output.WriteLine("CATALOG: oversized=refused-before-emission; trusted-slots=4096; cached-registrations=usable");
        _output.WriteLine($"Elapsed: {timer.Elapsed}; working-set increase: {process.WorkingSet64 - before} bytes.");
    }
}
