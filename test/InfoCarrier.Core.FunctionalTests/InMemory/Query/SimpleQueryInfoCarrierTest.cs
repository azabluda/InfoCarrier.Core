// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class SimpleQueryInfoCarrierTest : SimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public SimpleQueryInfoCarrierTest(
            NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture,
            ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        public override void Throws_on_concurrent_query_list()
        {
            // UGLY: have to copy the whole test and add .AsEnumerable() to avoid stack overflow
            // https://github.com/aspnet/EntityFrameworkCore/blob/2.1.0-preview2-final/src/EFCore.Specification.Tests/Query/SimpleQueryTestBase.cs#L2597
            using (var context = this.CreateContext())
            {
                context.Database.EnsureCreated();

                using (var synchronizationEvent = new ManualResetEventSlim(false))
                {
                    using (var blockingSemaphore = new SemaphoreSlim(0))
                    {
                        var blockingTask = Task.Run(() =>
                            context.Customers.AsEnumerable().Select(
                                c => Process(c, synchronizationEvent, blockingSemaphore)).ToList());

                        var throwingTask = Task.Run(() =>
                        {
                            synchronizationEvent.Wait();
                            Assert.Equal(
                                CoreStrings.ConcurrentMethodInvocation,
                                Assert.Throws<InvalidOperationException>(
                                    () => context.Customers.ToList()).Message);
                        });

                        throwingTask.Wait();

                        blockingSemaphore.Release(1);

                        blockingTask.Wait();
                    }
                }
            }
        }

        public override void Throws_on_concurrent_query_first()
        {
            // UGLY: have to copy the whole test and add .AsEnumerable() to avoid stack overflow
            // https://github.com/aspnet/EntityFrameworkCore/blob/2.1.0-preview2-final/src/EFCore.Specification.Tests/Query/SimpleQueryTestBase.cs#LL2632
            using (var context = this.CreateContext())
            {
                context.Database.EnsureCreated();

                using (var synchronizationEvent = new ManualResetEventSlim(false))
                {
                    using (var blockingSemaphore = new SemaphoreSlim(0))
                    {
                        var blockingTask = Task.Run(() =>
                            context.Customers.AsEnumerable().Select(
                                c => Process(c, synchronizationEvent, blockingSemaphore)).ToList());

                        var throwingTask = Task.Run(() =>
                        {
                            synchronizationEvent.Wait();
                            Assert.Equal(
                                CoreStrings.ConcurrentMethodInvocation,
                                Assert.Throws<InvalidOperationException>(
                                    () => context.Customers.First()).Message);
                        });

                        throwingTask.Wait();

                        blockingSemaphore.Release(1);

                        blockingTask.Wait();
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
