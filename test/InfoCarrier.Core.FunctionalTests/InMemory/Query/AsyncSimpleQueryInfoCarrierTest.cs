// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class AsyncSimpleQueryInfoCarrierTest : AsyncSimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsyncSimpleQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        public override async Task Throws_on_concurrent_query_list()
        {
            // UGLY: have to copy the whole test and add .AsEnumerable() to avoid stack overflow
            // https://github.com/aspnet/EntityFrameworkCore/blob/2.1.0-preview2-final/src/EFCore.Specification.Tests/Query/AsyncSimpleQueryTestBase.cs#L3374
            using (var context = this.CreateContext())
            {
                using (var synchronizationEvent = new ManualResetEventSlim(false))
                {
                    using (var blockingSemaphore = new SemaphoreSlim(0))
                    {
                        var blockingTask = Task.Run(
                            () =>
                                context.Customers.AsEnumerable().Select(
                                    c => Process(c, synchronizationEvent, blockingSemaphore)).ToList());

                        var throwingTask = Task.Run(
                            async () =>
                            {
                                synchronizationEvent.Wait();

                                Assert.Equal(
                                    CoreStrings.ConcurrentMethodInvocation,
                                    (await Assert.ThrowsAsync<InvalidOperationException>(
                                        () => context.Customers.ToListAsync())).Message);
                            });

                        await throwingTask;

                        blockingSemaphore.Release(1);

                        await blockingTask;
                    }
                }
            }
        }

        public override async Task Throws_on_concurrent_query_first()
        {
            // UGLY: have to copy the whole test and add .AsEnumerable() to avoid stack overflow
            // https://github.com/aspnet/EntityFrameworkCore/blob/2.1.0-preview2-final/src/EFCore.Specification.Tests/Query/AsyncSimpleQueryTestBase.cs#L3409
            using (var context = this.CreateContext())
            {
                using (var synchronizationEvent = new ManualResetEventSlim(false))
                {
                    using (var blockingSemaphore = new SemaphoreSlim(0))
                    {
                        var blockingTask = Task.Run(
                            () =>
                                context.Customers.AsEnumerable().Select(
                                    c => Process(c, synchronizationEvent, blockingSemaphore)).ToList());

                        var throwingTask = Task.Run(
                            async () =>
                            {
                                synchronizationEvent.Wait();
                                Assert.Equal(
                                    CoreStrings.ConcurrentMethodInvocation,
                                    (await Assert.ThrowsAsync<InvalidOperationException>(
                                        () => context.Customers.FirstAsync())).Message);
                            });

                        await throwingTask;

                        blockingSemaphore.Release(1);

                        await blockingTask;
                    }
                }
            }
        }

        private static Customer Process(Customer c, ManualResetEventSlim e, SemaphoreSlim s)
        {
            e.Set();
            s.Wait();
            s.Release(1);
            return c;
        }
    }
}
