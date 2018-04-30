// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;
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
            // Old implementation prior to
            // https://github.com/aspnet/EntityFrameworkCore/commit/654dbcc408d4649a54d0ed7de5f1f06b64114f8b
            using (var context = this.CreateContext())
            {
                ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IConcurrencyDetector>().EnterCriticalSection();

                Assert.Equal(
                    CoreStrings.ConcurrentMethodInvocation,
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.ToList()).Message);
            }
        }

        public override void Throws_on_concurrent_query_first()
        {
            // Old implementation prior to
            // https://github.com/aspnet/EntityFrameworkCore/commit/654dbcc408d4649a54d0ed7de5f1f06b64114f8b
            using (var context = this.CreateContext())
            {
                ((IInfrastructure<IServiceProvider>)context).Instance.GetService<IConcurrencyDetector>().EnterCriticalSection();

                Assert.Equal(
                    CoreStrings.ConcurrentMethodInvocation,
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.First()).Message);
            }
        }
    }
}
