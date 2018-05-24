// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.FunctionalTests.SqlServer;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class InfoCarrierTransactionManagerTest : IClassFixture<InfoCarrierTransactionManagerTest.TestFixture>
    {
        private readonly Func<DbContext> createContext;

        public InfoCarrierTransactionManagerTest(TestFixture fixture)
        {
            this.createContext = fixture.CreateContext;
        }

        [Fact]
        public void BeginTransaction_throws_if_another_transaction_started()
        {
            using (var context = this.createContext())
            {
                using (context.Database.BeginTransaction())
                {
                    Assert.Equal(
                        RelationalStrings.TransactionAlreadyStarted,
                        Assert.Throws<InvalidOperationException>(
                            () => context.Database.BeginTransaction()).Message);
                }
            }
        }

        [Fact]
        public void Throws_when_commit_is_called_without_active_transaction1()
        {
            using (var context = this.createContext())
            {
                using (var tr = context.Database.BeginTransaction())
                {
                    tr.Commit();

                    Assert.Equal(
                        RelationalStrings.NoActiveTransaction,
                        Assert.Throws<InvalidOperationException>(
                            () => tr.Commit()).Message);
                }
            }
        }

        [Fact]
        public void Throws_when_commit_is_called_without_active_transaction2()
        {
            using (var context = this.createContext())
            {
                using (context.Database.BeginTransaction())
                {
                    context.Database.CommitTransaction();

                    Assert.Equal(
                        RelationalStrings.NoActiveTransaction,
                        Assert.Throws<InvalidOperationException>(
                            () => context.Database.CommitTransaction()).Message);
                }
            }
        }

        [Fact]
        public void Throws_when_commit_is_called_without_active_transaction3()
        {
            using (var context = this.createContext())
            {
                Assert.Equal(
                    RelationalStrings.NoActiveTransaction,
                    Assert.Throws<InvalidOperationException>(
                        () => context.Database.CommitTransaction()).Message);
            }
        }

        [Fact]
        public void Throws_when_rollback_is_called_without_active_transaction1()
        {
            using (var context = this.createContext())
            {
                using (var tr = context.Database.BeginTransaction())
                {
                    tr.Rollback();

                    Assert.Equal(
                        RelationalStrings.NoActiveTransaction,
                        Assert.Throws<InvalidOperationException>(
                            () => tr.Rollback()).Message);
                }
            }
        }

        [Fact]
        public void Throws_when_rollback_is_called_without_active_transaction2()
        {
            using (var context = this.createContext())
            {
                using (context.Database.BeginTransaction())
                {
                    context.Database.RollbackTransaction();

                    Assert.Equal(
                        RelationalStrings.NoActiveTransaction,
                        Assert.Throws<InvalidOperationException>(
                            () => context.Database.RollbackTransaction()).Message);
                }
            }
        }

        [Fact]
        public void Throws_when_rollback_is_called_without_active_transaction3()
        {
            using (var context = this.createContext())
            {
                Assert.Equal(
                    RelationalStrings.NoActiveTransaction,
                    Assert.Throws<InvalidOperationException>(
                        () => context.Database.RollbackTransaction()).Message);
            }
        }

        [Fact]
        public void Context_pooling_is_not_supported()
        {
            var serviceProvider =
                new ServiceCollection()
                    .AddEntityFrameworkInfoCarrierBackend()
                    .AddDbContextPool<DbContext>(
                        ob => ob.UseInfoCarrierBackend(
                            InfoCarrierTestHelpers.CreateDummyBackend(typeof(DbContext))))
                    .BuildServiceProvider();

            var serviceScope = serviceProvider.CreateScope();
            var scopedProvider = serviceScope.ServiceProvider;
            var context = scopedProvider.GetService<DbContext>();
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            Assert.Throws<NotImplementedException>(() => serviceScope.Dispose());
        }

        public class TestFixture : GraphUpdatesInfoCarrierTest.TestFixture
        {
            protected override string StoreName => base.StoreName + "TransactionManagerTest";
        }
    }
}
