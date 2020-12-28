// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.SqlServer;
    using InfoCarrier.Core.Properties;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class InfoCarrierDatabaseTransactionTest : IClassFixture<InfoCarrierDatabaseTransactionTest.TestFixture>
    {
        private readonly Func<DbContext> createContext;

        public InfoCarrierDatabaseTransactionTest(TestFixture fixture)
        {
            this.createContext = fixture.CreateContext;
        }

        [Fact]
        public void Can_BeginTransaction()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();
            Assert.NotEqual(Guid.Empty, tr.TransactionId);
        }

        [Fact]
        public void BeginTransaction_throws_if_another_transaction_started()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();

            Assert.Equal(
                InfoCarrierStrings.TransactionAlreadyStarted,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.BeginTransaction()).Message);
        }

        [Fact]
        public async Task BeginTransactionAsync_throws_if_another_transaction_started()
        {
            using var context = this.createContext();
            await using var tr = await context.Database.BeginTransactionAsync();

            Assert.Equal(
                InfoCarrierStrings.TransactionAlreadyStarted,
                (await Assert.ThrowsAsync<InvalidOperationException>(
                    () => context.Database.BeginTransactionAsync())).Message);
        }

        [Fact]
        public void Throws_when_Commit_is_called_without_active_transaction1()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();
            tr.Commit();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => tr.Commit()).Message);
        }

        [Fact]
        public async Task Throws_when_CommitAsync_is_called_without_active_transaction1()
        {
            using var context = this.createContext();
            using var tr = await context.Database.BeginTransactionAsync();
            await tr.CommitAsync();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                (await Assert.ThrowsAsync<InvalidOperationException>(
                    () => tr.CommitAsync())).Message);
        }

        [Fact]
        public void Throws_when_Commit_is_called_without_active_transaction2()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();
            context.Database.CommitTransaction();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.CommitTransaction()).Message);
        }

        [Fact]
        public void Throws_when_Commit_is_called_without_active_transaction3()
        {
            using var context = this.createContext();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.CommitTransaction()).Message);
        }

        [Fact]
        public void Throws_when_Rollback_is_called_without_active_transaction1()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();
            tr.Rollback();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => tr.Rollback()).Message);
        }

        [Fact]
        public async Task Throws_when_RollbackAsync_is_called_without_active_transaction1()
        {
            using var context = this.createContext();
            await using var tr = await context.Database.BeginTransactionAsync();
            await tr.RollbackAsync();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                (await Assert.ThrowsAsync<InvalidOperationException>(
                    () => tr.RollbackAsync())).Message);
        }

        [Fact]
        public void Throws_when_Rollback_is_called_without_active_transaction2()
        {
            using var context = this.createContext();
            using var tr = context.Database.BeginTransaction();
            context.Database.RollbackTransaction();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.RollbackTransaction()).Message);
        }

        [Fact]
        public void Throws_when_Rollback_is_called_without_active_transaction3()
        {
            using var context = this.createContext();

            Assert.Equal(
                InfoCarrierStrings.NoActiveTransaction,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.RollbackTransaction()).Message);
        }

        public class TestFixture : GraphUpdatesInfoCarrierTest.TestFixture
        {
            protected override string StoreName => base.StoreName + "TransactionManagerTest";
        }
    }
}
