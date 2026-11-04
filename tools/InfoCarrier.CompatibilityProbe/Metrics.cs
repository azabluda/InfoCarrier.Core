// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Wire;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.CompatibilityProbe;

internal static class Metrics
{
    internal static void Run(int version)
    {
        using ServiceProvider services = new ServiceCollection().AddInfoCarrierWireProtocols().BuildServiceProvider();
        InfoCarrierWireProtocol protocol = services.GetRequiredService<InfoCarrierWireProtocol>();
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, protocol);
        using var model = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("Data Source=:memory:").Options);
        Console.WriteLine("version,shape,size,bytes,iterations,round,allocated_bytes,elapsed_ms,managed_heap_bytes,peak_working_set_bytes");
        foreach (int size in new[] { 1, 64, 1024 })
        {
            ParameterExpression item = Expression.Parameter(typeof(ProbeItem), "item");
            Expression Predicate(int start, int count) => count == 1
                ? Expression.Equal(Expression.Property(item, nameof(ProbeItem.Number)), Expression.Constant(start))
                : Expression.OrElse(Predicate(start, count / 2), Predicate(start + count / 2, count - count / 2));
            var query = model.Items.Where(Expression.Lambda<Func<ProbeItem, bool>>(Predicate(0, size), item));
            var request = new QueryDataRequest
            {
                ProtocolVersion = version, Query = ExpressionSerializer.CreateForModel(model.Model).ToNode(query.Expression),
                TrackingBehavior = QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
            };
            Measure("balanced-query", size, request);
            var result = new QueryDataResult
            {
                IsEntityResult = false, Rows = Enumerable.Range(0, size).Select(index => new DynamicValueNode
                {
                    Type = new TypeNode { Name = typeof(string).AssemblyQualifiedName! }, PrimitiveValue = new string('x', 256) + index,
                }).ToArray(),
            };
            Measure("buffered-rows", size, result);
        }

        void Measure<T>(string shape, int size, T value) where T : class
        {
            int iterations = Math.Max(10, 1024 / size);
            Cycle(); // Prepare serializer metadata and JIT before warm measurements.
            for (int round = 0; round < 5; round++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long initial = GC.GetAllocatedBytesForCurrentThread();
                var timer = Stopwatch.StartNew();
                int bytes = 0;
                for (int index = 0; index < iterations; index++) { bytes = Cycle(); }
                timer.Stop();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - initial;
                using Process process = Process.GetCurrentProcess();
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{version},{shape},{size},{bytes},{iterations},{round},{allocated},{timer.Elapsed.TotalMilliseconds:F3},{GC.GetTotalMemory(false)},{process.PeakWorkingSet64}"));
            }

            int Cycle()
            {
                byte[] bytes = protocol.Serialize(serializer, value, version);
                T restored = protocol.Deserialize<T>(serializer, bytes, version)
                    ?? throw new InvalidOperationException("The measured value disappeared.");
                GC.KeepAlive(restored);
                return bytes.Length;
            }
        }
    }
}
