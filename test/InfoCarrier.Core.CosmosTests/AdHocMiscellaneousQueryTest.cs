// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 AdHocMiscellaneousQueryCosmosTest.

using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

public class AdHocMiscellaneousQueryTest(NonSharedFixture fixture) : NonSharedModelTestBase(fixture), IClassFixture<NonSharedFixture>
{
    #region 21006

    [Fact]
    public virtual async Task Project_all_types_entity_with_missing_scalars()
    {
        var contextFactory = await InitializeNonSharedTest<JsonContext21006>(
            onModelCreating: OnModelCreating21006,
            seed: Seed21006);

        await using var context = contextFactory.CreateDbContext();

        var query = context.Set<JsonContext21006.Entity>();

        var result = await query.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
    }

    private void OnModelCreating21006(ModelBuilder modelBuilder)
        => modelBuilder.Entity<JsonContext21006.Entity>(b =>
        {
            b.Property(x => x.Id).ValueGeneratedNever();
            b.ToContainer("Entities");
            b.Property(x => x.TestDecimal).HasPrecision(18, 3);
            b.OwnsOne(
                x => x.Reference, bb =>
                {
                    bb.Property(x => x.TestDecimal).HasPrecision(18, 3);
                    bb.Property(x => x.TestEnumWithIntConverter).HasConversion<int>();
                });
        });

    protected async Task Seed21006(JsonContext21006 context)
    {
        // Malformed input is backend setup; the subsequent application read crosses InfoCarrier.
        var entitiesContainer = context.Database.GetCosmosClient().GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var missingTopLevel =
            """
{
  "Id": 1,
  "$type": "Entity",
  "id": "1",
  "Reference": {
    "Text": "e2 or"
  }
}
""";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(missingTopLevel));
        using var response = await entitiesContainer.CreateItemStreamAsync(stream,
            Microsoft.Azure.Cosmos.PartitionKey.None, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    protected class JsonContext21006(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Entity> Entities
            => Set<Entity>();

        public class Entity
        {
            public int Id { get; set; }

            public short TestInt16 { get; set; }
            public int TestInt32 { get; set; }
            public long TestInt64 { get; set; }
            public double TestDouble { get; set; }
            public decimal TestDecimal { get; set; }
            public DateTime TestDateTime { get; set; }
            public DateTimeOffset TestDateTimeOffset { get; set; }
            public TimeSpan TestTimeSpan { get; set; }
            public DateOnly TestDateOnly { get; set; }
            public TimeOnly TestTimeOnly { get; set; }
            public float TestSingle { get; set; }
            public bool TestBoolean { get; set; }
            public byte TestByte { get; set; }

            public byte[] TestByteArray { get; set; } = null!;
            public Guid TestGuid { get; set; }
            public ushort TestUnsignedInt16 { get; set; }
            public uint TestUnsignedInt32 { get; set; }
            public ulong TestUnsignedInt64 { get; set; }
            public char TestCharacter { get; set; }
            public sbyte TestSignedByte { get; set; }
            public int? TestNullableInt32 { get; set; }
            public JsonEnum TestEnum { get; set; }
            public byte[] TestByteCollection { get; set; } = null!;
            public IList<ushort> TestUnsignedInt16Collection { get; set; } = null!;
            public uint[] TestUnsignedInt32Collection { get; set; } = null!;
            public sbyte[] TestSignedByteCollection { get; set; } = null!;
            public JsonEntity Reference { get; set; } = null!;
        }

        public class JsonEntity
        {
            public string Text { get; set; } = null!;

            public short TestInt16 { get; set; }
            public int TestInt32 { get; set; }
            public long TestInt64 { get; set; }
            public double TestDouble { get; set; }
            public decimal TestDecimal { get; set; }
            public DateTime TestDateTime { get; set; }
            public DateTimeOffset TestDateTimeOffset { get; set; }
            public TimeSpan TestTimeSpan { get; set; }
            public DateOnly TestDateOnly { get; set; }
            public TimeOnly TestTimeOnly { get; set; }
            public float TestSingle { get; set; }
            public bool TestBoolean { get; set; }
            public byte TestByte { get; set; }
            public byte[] TestByteArray { get; set; } = null!;
            public Guid TestGuid { get; set; }
            public ushort TestUnsignedInt16 { get; set; }
            public uint TestUnsignedInt32 { get; set; }
            public ulong TestUnsignedInt64 { get; set; }
            public char TestCharacter { get; set; }
            public sbyte TestSignedByte { get; set; }
            public int? TestNullableInt32 { get; set; }
            public JsonEnum TestEnum { get; set; }
            public JsonEnum TestEnumWithIntConverter { get; set; }

            public byte[] TestByteCollection { get; set; } = null!;
            public IList<ushort> TestUnsignedInt16Collection { get; set; } = null!;
            public uint[] TestUnsignedInt32Collection { get; set; } = null!;

            public sbyte[] TestSignedByteCollection { get; set; } = null!;
        }

        public enum JsonEnum
        {
            One = -1,
            Two = 2,
            Three = -3
        }
    }

    #endregion

    #region 34911

    [Fact]
    public virtual async Task Enum_partition_key()
    {
        var contextFactory = await InitializeNonSharedTest<Context34911>(
            onModelCreating: b => b.Entity<Context34911.Member>().HasPartitionKey(d => d.MemberType),
            seed: async context =>
            {
                context.Members.Add(new Context34911.Member { MemberType = Context34911.MemberType.Admin, Name = "Some Admin" });
                await context.SaveChangesAsync();
            });

        await using (var context = contextFactory.CreateDbContext())
        {
            var admin = await context.Members.Where(p => p.MemberType == Context34911.MemberType.Admin).SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Some Admin", admin.Name);
        }
    }

    protected class Context34911(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Member> Members
            => Set<Member>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Member>().HasData(new Member { Id = 1, Name = "Product 1" });

        public class Member
        {
            public int Id { get; set; }
            public MemberType MemberType { get; set; }
            public string Name { get; set; } = null!;
        }

        public enum MemberType
        {
            User,
            Admin
        }
    }

    #endregion 34911

    #region 35094

    // TODO: Move these tests to a better location. They require nullable properties with nulls in the database.

    [Fact]
    public virtual async Task Min_over_value_type_containing_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().MinAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Min_over_value_type_containing_all_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.NullableVal == null).MinAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Min_over_reference_type_containing_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().MinAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Min_over_reference_type_containing_all_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.NullableRef == null).MinAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Min_over_reference_type_containing_no_data()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.Id < 0).MinAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Max_over_value_type_containing_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Equal(3.14, await context.Set<Context35094.Product>().MaxAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Max_over_value_type_containing_all_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.NullableVal == null).MaxAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Max_over_reference_type_containing_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Equal("Value", await context.Set<Context35094.Product>().MaxAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Max_over_reference_type_containing_all_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.NullableRef == null).MaxAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Max_over_reference_type_containing_no_data()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.Id < 0).MaxAsync(p => p.NullableRef, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Average_over_value_type_containing_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().AverageAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public virtual async Task Average_over_value_type_containing_all_nulls()
    {
        await using var context = (await InitializeNonSharedTest<Context35094>()).CreateDbContext();
        Assert.Null(await context.Set<Context35094.Product>().Where(e => e.NullableVal == null).AverageAsync(p => p.NullableVal, cancellationToken: TestContext.Current.CancellationToken));
    }

    protected class Context35094(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Product> Products
            => Set<Product>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    NullableRef = "Value",
                    NullableVal = 3.14
                },
                new Product { Id = 2, NullableVal = 3.14 },
                new Product { Id = 3, NullableRef = "Value" });

        public class Product
        {
            [DatabaseGenerated(DatabaseGeneratedOption.None)]
            public int Id { get; set; }

            public double? NullableVal { get; set; }
            public string? NullableRef { get; set; }
        }
    }

    #endregion 35094

    #region 36329

    [Fact]
    public virtual async Task Enum_discriminator_with_value_converter_on_derived_dbset()
    {
        var contextFactory = await InitializeNonSharedTest<EnumDiscriminatorContext36329>(
            onModelCreating: OnModelCreating36329,
            seed: Seed36329);

        await using var context = contextFactory.CreateDbContext();

        // This should not throw an InvalidCastException
        var dog = await context.Dogs.SingleAsync(x => x.Id == "123", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("Rover", dog.Name);
        Assert.Equal(PetType36329.Dog, dog.PetType);
    }

    private void OnModelCreating36329(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pet36329>()
            .ToContainer(nameof(Pet36329))
            .HasPartitionKey(x => x.Id)
            .HasKey(x => x.Id);

        modelBuilder.Entity<Pet36329>().Property(e => e.PetType)
            .HasConversion(new EnumToStringConverter<PetType36329>());

        modelBuilder.Entity<Pet36329>()
            .HasDiscriminator(x => x.PetType)
            .HasValue<Dog36329>(PetType36329.Dog);

        modelBuilder.Entity<Dog36329>()
            .HasBaseType<Pet36329>();
    }

    protected async Task Seed36329(EnumDiscriminatorContext36329 context)
    {
        var dog = new Dog36329
        {
            Id = "123",
            Name = "Rover",
            PetType = PetType36329.Dog,
            DogProperty = "test"
        };

        await context.Dogs.AddAsync(dog);
        await context.SaveChangesAsync();
    }

    public class EnumDiscriminatorContext36329 : DbContext
    {
        public EnumDiscriminatorContext36329(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Dog36329> Dogs
            => Set<Dog36329>();

        public DbSet<Pet36329> Pets
            => Set<Pet36329>();
    }

    public abstract record Pet36329
    {
        public string Id { get; set; } = null!;
        public PetType36329 PetType { get; set; }
        public string Name { get; set; } = null!;
    }

    public record Dog36329 : Pet36329
    {
        public string DogProperty { get; set; } = null!;
    }

    public enum PetType36329
    {
        Dog,
        Cat
    }

    #endregion 36329

    #region 37653

    [Fact]
    public virtual async Task Coalesce_in_conditional_with_value_conversion()
    {
        var contextFactory = await InitializeNonSharedTest<Context37653>();
        using var context = contextFactory.CreateDbContext();

        var query = context.Set<Context37653.Data>()
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                Foo = e.Foo ?? false ? 'A' : 'B',
            });

        var result = await query.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal('A', result[0].Foo);
        Assert.Equal('B', result[1].Foo);
        Assert.Equal('B', result[2].Foo);
    }

    // Protected so that it can be used by inheriting tests, and so that things like unused setters are not removed.
    protected class Context37653(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Data>(builder =>
            {
                builder.Property(a => a.Foo).HasConversion<FooConverter>();
                builder.HasData(
                    new Data { Id = 1, Foo = true },
                    new Data { Id = 2, Foo = false },
                    new Data { Id = 3, Foo = null });
            });

        public class Data
        {
            public int Id { get; set; }
            public bool? Foo { get; set; }
        }

#pragma warning disable EF1001 // Preserve the official null-converting converter.
        public class FooConverter : ValueConverter<bool?, short?>
        {
            public FooConverter()
                : base(
                    x => x == true ? 10 : (short?)99,
                    x => x == 10 ? true : x == 99 ? false : null,
                    convertsNulls: true)
            {
            }
        }
    }

#pragma warning restore EF1001

    #endregion

    #region 34567

    [Fact]
    public virtual async Task Html_unsafe_character_scalar()
    {
        var contextFactory = await InitializeNonSharedTest<Context34567>();
        using var context = contextFactory.CreateDbContext();

        var result = await context.Set<Context34567.Data>()
            .Where(x => x.Text == "\"")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);

        Assert.Equal("\"", result.Single().Text);


    }

    [Fact]
    public virtual async Task Html_unsafe_character_id()
    {
        var contextFactory = await InitializeNonSharedTest<Context34567>();
        using var context = contextFactory.CreateDbContext();

        var result = await context.Set<Context34567.Data>()
            .Where(x => x.Id == "\"")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);

        Assert.Equal("\"", result.Single().Text);


    }

    [Fact]
    public virtual async Task Html_unsafe_character_partition_key()
    {
        var contextFactory = await InitializeNonSharedTest<Context34567>();
        using var context = contextFactory.CreateDbContext();

        var result = await context.Set<Context34567.Data>()
            .WithPartitionKey("\"")
            .ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Single(result);

        Assert.Equal("\"", result.Single().Text);


    }

    protected class Context34567(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Data>(builder =>
            {
                builder.HasPartitionKey(x => x.Id);
                builder.HasData([new()]);
            });

        public class Data
        {
            public string Id { get; set; } = "\"" + "";
            public string Text { get; set; } = "\"" + "";
        }
    }

    #endregion

    protected override string NonSharedStoreName
        => "AdHocMiscellaneousQueryTests";

    protected override DbContextOptionsBuilder AddNonSharedOptions(DbContextOptionsBuilder builder)
        => builder.ConfigureWarnings(b => b.Ignore(CosmosEventId.NoPartitionKeyDefined));

    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier(),
        allowedTypes: [typeof(CosmosQueryableExtensions)]);
    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring,
            configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(onModelCreating, onConfiguring, addServices,
            configureConventions, shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }
}
