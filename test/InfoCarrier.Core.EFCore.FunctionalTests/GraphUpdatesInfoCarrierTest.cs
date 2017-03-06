namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Specification.Tests.TestUtilities.Xunit;
    using Microsoft.Extensions.DependencyInjection;

    public class GraphUpdatesInfoCarrierTest
        : GraphUpdatesTestBase<InMemoryTestStore, GraphUpdatesInfoCarrierTest.GraphUpdatesInfoCarrierFixture>
    {
        public GraphUpdatesInfoCarrierTest(GraphUpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalFact]
        public override void Optional_One_to_one_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Required_One_to_one_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Optional_One_to_one_with_AK_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalFact]
        public override void Required_One_to_one_with_AK_relationships_are_one_to_one()
        {
            // FK uniqueness not enforced in in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_one_to_one_changed_by_reference_with_alternate_key(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_non_PK_one_to_one_changed_by_reference_with_alternate_key(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_one_to_one_changed_by_reference(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_removed_required_many_to_one_dependents(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Save_required_non_PK_one_to_one_changed_by_reference(ChangeMechanism changeMechanism, bool useExistingEntities)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_one_to_one_with_alternate_key(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_one_to_one(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_non_PK_one_to_one(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalTheory]
        public override void Sever_required_non_PK_one_to_one_with_alternate_key(ChangeMechanism changeMechanism)
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_many_to_one_dependents_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_many_to_one_dependents_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_deleted_in_store()
        {
            // Cascade delete not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_many_to_one_dependents_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_one_to_one_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_many_to_one_dependents_with_alternate_key_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Optional_one_to_one_with_alternate_key_are_orphaned_in_store()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_with_alternate_key_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_one_to_one_with_alternate_key_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        [ConditionalFact]
        public override void Required_non_PK_one_to_one_are_cascade_detached_when_Added()
        {
            // Cascade nulls not supported by in-memory database
        }

        public class GraphUpdatesInfoCarrierFixture : GraphUpdatesFixtureBase
        {
            private static readonly string StoreName = nameof(GraphUpdatesInfoCarrierTest);
            private readonly Func<DbContext> inMemoryDbContextFactory;

            public GraphUpdatesInfoCarrierFixture()
            {
                var optionsBuilder = new DbContextOptionsBuilder();
                optionsBuilder.UseInMemoryDatabase(StoreName);
                var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
                this.OnModelCreating(modelBuilder);
                optionsBuilder.UseModel(modelBuilder.Model);
                optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                this.inMemoryDbContextFactory = () => new GraphUpdatesContext(optionsBuilder.Options);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<BadOrder>();
            }

            public override InMemoryTestStore CreateTestStore()
                => new InMemoryTestStore(
                    StoreName,
                    this.inMemoryDbContextFactory,
                    this.Seed);

            public override DbContext CreateContext(InMemoryTestStore testStore)
                => new GraphUpdatesContext(new DbContextOptionsBuilder()
                    .UseInfoCarrierBackend(new TestInfoCarrierBackend(this.inMemoryDbContextFactory, true))
                    .UseInternalServiceProvider(
                        new ServiceCollection()
                            .AddEntityFrameworkInfoCarrierBackend()
                            .AddSingleton(TestInfoCarrierModelSource.GetFactory(this.OnModelCreating))
                            .BuildServiceProvider()).Options);
        }
    }
}
