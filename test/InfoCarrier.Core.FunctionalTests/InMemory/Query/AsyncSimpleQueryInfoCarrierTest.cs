// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class AsyncSimpleQueryInfoCarrierTest : AsyncSimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsyncSimpleQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        public override async Task Throws_on_concurrent_query_list()
        {
            // Old implementation prior to
            // https://github.com/aspnet/EntityFrameworkCore/commit/654dbcc408d4649a54d0ed7de5f1f06b64114f8b
            using (var context = this.CreateContext())
            {
                ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IConcurrencyDetector>().EnterCriticalSection();

                Assert.Equal(
                    CoreStrings.ConcurrentMethodInvocation,
                    (await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await context.Customers.ToListAsync())).Message);
            }
        }

        public override async Task Throws_on_concurrent_query_first()
        {
            // Old implementation prior to
            // https://github.com/aspnet/EntityFrameworkCore/commit/654dbcc408d4649a54d0ed7de5f1f06b64114f8b
            using (var context = this.CreateContext())
            {
                ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IConcurrencyDetector>().EnterCriticalSection();

                Assert.Equal(
                    CoreStrings.ConcurrentMethodInvocation,
                    (await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await context.Customers.FirstAsync())).Message);
            }
        }
    }
}
