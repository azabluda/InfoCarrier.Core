// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 CosmosNoTrackingWithIdentityResolutionProjectionTest.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications;

public class IdentityResolutionTest
    : QueryTestBase<IdentityResolutionTest.ProjectionFixture>
{
    public IdentityResolutionTest(ProjectionFixture fixture) : base(fixture) { }



    [Fact]
    public async Task Owned_reference_with_owner_key_after_owned_reference_projects_owner_key_first()
    {
        await AssertQuery(
            ss => ss.Set<Owner>()
                .Select(owner => new { owner.OwnedReference, owner.Id })
                .AsNoTrackingWithIdentityResolution(),
            elementSorter: result => result.Id,
            elementAsserter: (expected, actual) =>
            {
                Assert.Equal(expected.Id, actual.Id);
                AssertOwnedReference(expected.OwnedReference, actual.OwnedReference);
            });


    }

    [Fact]
    [StoreDefect("2.3", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 43, 65, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public async Task Owned_collection_with_owner_key_after_owned_collection_projects_owner_key_first()
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        await AssertQuery(
            ss => ss.Set<Owner>()
                .Select(owner => new { owner.OwnedCollection, owner.Id })
                .AsNoTrackingWithIdentityResolution(),
            elementSorter: result => result.Id,
            elementAsserter: (expected, actual) =>
            {
                Assert.Equal(expected.Id, actual.Id);
                AssertOwnedCollection(expected.OwnedCollection, actual.OwnedCollection);
            });


    }











    private static void SkipOnLinuxEmulator() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    private ValidationContext CreateContext()
        => Fixture.CreateContext();

    public class NonSharedModel(NonSharedFixture fixture) : NonSharedModelTestBase(fixture), IClassFixture<NonSharedFixture>
    {
        private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());

        protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

        protected override ContextFactory<TContext> CreateContextFactory<TContext>(
            Action<ModelBuilder>? onModelCreating = null,
            Action<DbContextOptionsBuilder>? onConfiguring = null,
            Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
            Action<ModelConfigurationBuilder>? configureConventions = null,
            Func<string, bool>? shouldLogCategory = null,
            Func<TestStore>? createTestStore = null,
            bool usePooling = true,
            bool useServiceProvider = true,
            bool skipValidation = false)
        {
            NonSharedFixture = null;
            _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddNonSharedOptions);
            return base.CreateContextFactory<TContext>(
                onModelCreating, onConfiguring, addServices, configureConventions,
                shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
        }


        protected override string NonSharedStoreName
            => nameof(IdentityResolutionTest) + nameof(NonSharedModel);



        [Fact]
        public async Task Double_owned_reference_returns_same()
        {
            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    context.Add(
                        new Owner
                        {
                            Id = Guid.NewGuid(),
                            Name = "Owner" + i,
                            OwnedReference = new OwnedReference { Id = Guid.NewGuid(), Name = "OwnedReference" }
                        });
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners.AsNoTrackingWithIdentityResolution()
                    .Select(owner => new
                    {
                        owner.Id,
                        first = owner.OwnedReference,
                        second = owner.OwnedReference
                    })
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);

                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Same(result.first, result.second);
                    if (i > 0)
                    {
                        var otherResult = results[i - 1];
                        Assert.NotSame(otherResult.first, result.first);
                    }
                }
            }
        }

        [Fact]
        [StoreDefect("2.3", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 195, 244, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
        public async Task Owned_collection_with_duplicate_returns_same()
        {
            // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
            SkipOnLinuxEmulator();

            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    var ownedCollectionElement = new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection" };
                    var owner = new Owner
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner" + i,
                        OwnedCollection =
                        [
                            ownedCollectionElement,
                            ownedCollectionElement
                        ]
                    };
                    context.Add(owner);
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners.Select(x => new { x.Id, x.OwnedCollection })
                    .AsNoTrackingWithIdentityResolution()
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Equal(2, result.OwnedCollection.Count);
                    Assert.Same(result.OwnedCollection[0], result.OwnedCollection[1]);
                    if (i > 0)
                    {
                        var otherResult = results[i - 1];
                        Assert.NotSame(otherResult.OwnedCollection[0], result.OwnedCollection[0]);
                        Assert.NotSame(otherResult.OwnedCollection[1], result.OwnedCollection[1]);
                    }
                }
            }
        }

        [Fact]
        [StoreDefect("2.3", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 246, 297, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
        public async Task Owned_collection_concat_returns_same()
        {
            // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
            SkipOnLinuxEmulator();

            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    var owner = new Owner
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner" + i,
                        OwnedCollection =
                        [
                            new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection1" },
                            new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection2" }
                        ]
                    };
                    context.Add(owner);
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners
                    .Select(x => new { x.Id, OwnedCollection = x.OwnedCollection.Concat(x.OwnedCollection).ToList() })
                    .AsNoTrackingWithIdentityResolution()
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);

                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Equal(4, result.OwnedCollection.Count);
                    for (var j = 0; j < result.OwnedCollection.Count / 2; j++)
                    {
                        Assert.Same(result.OwnedCollection[j], result.OwnedCollection[j + 2]);
                        if (j > 0)
                        {
                            Assert.NotSame(result.OwnedCollection[j], result.OwnedCollection[j - 1]);
                        }
                    }
                }
            }
        }

        [Fact]
        [StoreDefect("2.3", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 299, 361, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
        public async Task Duplicate_owned_collection_returns_same()
        {
            // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
            SkipOnLinuxEmulator();

            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    var owner = new Owner
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner" + i,
                        OwnedCollection =
                        [
                            new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection1" },
                            new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection2" }
                        ]
                    };
                    context.Add(owner);
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners
                    .Select(x => new
                    {
                        x.Id,
                        First = x.OwnedCollection,
                        Second = x.OwnedCollection
                    })
                    .AsNoTrackingWithIdentityResolution()
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);

                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Equal(result.First.Count, result.Second.Count);
                    for (var j = 0; j < result.First.Count; j++)
                    {
                        Assert.Same(result.First[j], result.Second[j]);
                    }

                    if (i > 0)
                    {
                        var otherResult = results[i - 1];
                        for (var j = 0; j < result.First.Count; j++)
                        {
                            Assert.NotSame(otherResult.First[j], result.First[j]);
                            Assert.NotSame(otherResult.Second[j], result.Second[j]);
                        }
                    }
                }
            }
        }

        [Fact(Skip = "Fails on main, and also: #37954")]
        [StoreIssue(IssueTracker.EfCore, 37954, UpstreamRepository.EfCore,
            "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 363, 411,
            Skip = true, Justification = "Fails on main, and also: #37954")]
        public async Task Ordinal_owned_collection_concat_returns_same()
        {
            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    var owner = new Owner
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner" + i,
                        OrdinalOwnedCollection =
                        [
                            new OrdinalOwnedCollectionElement { Name = "OrdinalOwnedCollection1" },
                            new OrdinalOwnedCollectionElement { Name = "OrdinalOwnedCollection2" }
                        ]
                    };
                    context.Add(owner);
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners
                    .Select(x => new { x.Id, OrdinalOwnedCollection = x.OrdinalOwnedCollection.Concat(x.OrdinalOwnedCollection).ToList() })
                    .AsNoTrackingWithIdentityResolution()
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);

                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Equal(4, result.OrdinalOwnedCollection.Count);
                    for (var j = 0; j < result.OrdinalOwnedCollection.Count / 2; j++)
                    {
                        Assert.Same(result.OrdinalOwnedCollection[j], result.OrdinalOwnedCollection[j + 2]);
                        if (j > 0)
                        {
                            Assert.NotSame(result.OrdinalOwnedCollection[j], result.OrdinalOwnedCollection[j - 1]);
                        }
                    }
                }
            }
        }

        [Fact]
        [StoreDefect("2.3", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/CosmosNoTrackingWithIdentityResolutionProjectionTest.cs", 413, 475, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
        public async Task Duplicate_ordinal_owned_collection_returns_same()
        {
            // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
            SkipOnLinuxEmulator();

            var contextFactory = await InitializeNonSharedTest<ValidationContext>();

            await using (var context = contextFactory.CreateDbContext())
            {
                for (var i = 0; i < 2; i++)
                {
                    var owner = new Owner
                    {
                        Id = Guid.NewGuid(),
                        Name = "Owner" + i,
                        OrdinalOwnedCollection =
                        [
                            new OrdinalOwnedCollectionElement { Name = "OrdinalOwnedCollection1" },
                            new OrdinalOwnedCollectionElement { Name = "OrdinalOwnedCollection2" }
                        ]
                    };
                    context.Add(owner);
                }

                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var context = contextFactory.CreateDbContext())
            {
                var results = await context.Owners
                    .Select(x => new
                    {
                        x.Id,
                        First = x.OrdinalOwnedCollection,
                        Second = x.OrdinalOwnedCollection
                    })
                    .AsNoTrackingWithIdentityResolution()
                    .ToListAsync(TestContext.Current.CancellationToken);

                Assert.Equal(2, results.Count);

                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Assert.Equal(result.First.Count, result.Second.Count);
                    for (var j = 0; j < result.First.Count; j++)
                    {
                        Assert.Same(result.First[j], result.Second[j]);
                    }

                    if (i > 0)
                    {
                        var otherResult = results[i - 1];
                        for (var j = 0; j < result.First.Count; j++)
                        {
                            Assert.NotSame(otherResult.First[j], result.First[j]);
                            Assert.NotSame(otherResult.Second[j], result.Second[j]);
                        }
                    }
                }
            }
        }
    }





    private static void AssertOwner(Owner expected, Owner actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        AssertOwnedReference(expected.OwnedReference, actual.OwnedReference);
        AssertOwnedCollection(expected.OwnedCollection, actual.OwnedCollection);
        Assert.Equal(expected.ComplexReference.Name, actual.ComplexReference.Name);
        AssertComplexCollection(expected.ComplexCollection, actual.ComplexCollection);
    }

    private static void AssertOwnedReference(OwnedReference expected, OwnedReference actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
    }

    private static void AssertOwnedCollection(IEnumerable<OwnedCollectionElement> expected, IEnumerable<OwnedCollectionElement> actual)
        => Assert.Collection(
            actual,
            expected.ToList().Select<OwnedCollectionElement, Action<OwnedCollectionElement>>(expectedElement => actualElement =>
            {
                Assert.Equal(expectedElement.Id, actualElement.Id);
                Assert.Equal(expectedElement.Name, actualElement.Name);
            }).ToArray());

    private static void AssertComplexCollection(
        IEnumerable<ComplexCollectionElement> expected,
        IEnumerable<ComplexCollectionElement> actual)
        => Assert.Collection(
            actual,
            expected.ToList().Select<ComplexCollectionElement, Action<ComplexCollectionElement>>(expectedElement
                => actualElement => Assert.Equal(expectedElement.Name, actualElement.Name)).ToArray());



    public class ProjectionFixture : QueryFixtureBase<ValidationContext>
    {
        private ProjectionData? _expectedData;

        protected override string StoreName
            => nameof(IdentityResolutionTest);

        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), typeof(ValidationContext), null);



        protected override Task SeedAsync(ValidationContext context)
        {
            var data = (ProjectionData)GetExpectedData();
            context.AddRange(data.Owners);
            return context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        public override ISetSource GetExpectedData()
            => _expectedData ??= new ProjectionData();

        public override IReadOnlyDictionary<Type, object> EntitySorters { get; } =
            new Dictionary<Type, Func<object?, object?>> { { typeof(Owner), e => ((Owner?)e)?.Id } }.ToDictionary(
                e => e.Key, e => (object)e.Value);

        public override IReadOnlyDictionary<Type, object> EntityAsserters { get; } = new Dictionary<Type, Action<object?, object?>>
        {
            {
                typeof(Owner), (e, a) =>
                {
                    Assert.Equal(e is null, a is null);

                    if (a is not null)
                    {
                        AssertOwner((Owner)e!, (Owner)a);
                    }
                }
            }
        }.ToDictionary(e => e.Key, e => (object)e.Value);
    }

    public class ProjectionData : ISetSource
    {
        public IReadOnlyList<Owner> Owners { get; } =
        [
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Owner",
                OwnedReference = new OwnedReference { Id = Guid.NewGuid(), Name = "OwnedReference" },
                OwnedCollection =
                [
                    new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection" }
                ],
                ComplexReference = new ComplexReference { Name = "ComplexReference" },
                ComplexCollection =
                [
                    new ComplexCollectionElement { Name = "ComplexCollection" }
                ]
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Owner",
                OwnedReference = new OwnedReference { Id = Guid.NewGuid(), Name = "OwnedReference" },
                OwnedCollection =
                [
                    new OwnedCollectionElement { Id = Guid.NewGuid(), Name = "OwnedCollection" }
                ],
                ComplexReference = new ComplexReference { Name = "ComplexReference" },
                ComplexCollection =
                [
                    new ComplexCollectionElement { Name = "ComplexCollection" }
                ]
            }
        ];

        public IQueryable<TEntity> Set<TEntity>()
            where TEntity : class
            => typeof(TEntity) == typeof(Owner)
                ? (IQueryable<TEntity>)Owners.AsQueryable()
                : throw new InvalidOperationException("Invalid entity type: " + typeof(TEntity));
    }

    public class Owner
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Owner";
        public OwnedReference OwnedReference { get; set; } = new();
        public List<OwnedCollectionElement> OwnedCollection { get; set; } = [];
        public List<OrdinalOwnedCollectionElement> OrdinalOwnedCollection { get; set; } = [];
        public ComplexReference ComplexReference { get; set; } = new();
        public List<ComplexCollectionElement> ComplexCollection { get; set; } = [];
    }

    public class OwnedReference
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "OwnedReference";
    }

    public class OwnedCollectionElement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "OwnedCollection";
    }

    public class OrdinalOwnedCollectionElement
    {
        public string Name { get; set; } = "OrdinalOwnedCollection";
    }

    public class ComplexReference
    {
        public string Name { get; set; } = "ComplexReference";
    }

    public class ComplexCollectionElement
    {
        public string Name { get; set; } = "ComplexCollection";
    }

    public class ValidationContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Owner> Owners
            => Set<Owner>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Owner>(builder =>
            {
                builder.HasPartitionKey(owner => owner.Id);
                builder.OwnsOne(owner => owner.OwnedReference);
                builder.OwnsMany(owner => owner.OwnedCollection);
                builder.OwnsMany(owner => owner.OrdinalOwnedCollection);
                builder.ComplexProperty(owner => owner.ComplexReference);
                builder.ComplexCollection(owner => owner.ComplexCollection);
            });
    }
}

