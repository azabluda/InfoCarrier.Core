// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 EmbeddedDocumentsTest; SQL baselines are omitted.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.EntityFrameworkCore.TestModels.TransportationModel;

// ReSharper disable InconsistentNaming
namespace InfoCarrier.Core.CosmosTests.Specifications;

#pragma warning disable EF1001 // Preserve the pinned provider diagnostic assertions.

public class EmbeddedDocumentsTest : IAsyncLifetime
{
    protected CosmosFixture Fixture { get; } = new();
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => Fixture.DisposeAsync();

    [Fact(Skip = "Issue #17670")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/EmbeddedDocumentsTest.cs", 22, 48,
        Skip = true, Justification = "Issue #17670")]
    public virtual async Task Can_update_dependents()
    {
        var options = await Fixture.CreateOptions();
        Operator firstOperator;
        Engine firstEngine;
        using (var context = new EmbeddedTransportationContext(options))
        {
            firstOperator = context.Set<Vehicle>().Select(v => v.Operator!).OrderBy(o => o.VehicleName).First();
            firstOperator.Name += "1";
            firstEngine = context.Set<PoweredVehicle>().Select(v => v.Engine!).OrderBy(o => o.VehicleName).First();
            firstEngine.Description += "1";

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var @operator = await context.Set<Vehicle>()
                .Select(v => v.Operator!).OrderBy(o => o.VehicleName).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(firstOperator.Name, @operator.Name);

            var engine = await context.Set<PoweredVehicle>()
                .Select(v => v.Engine!).OrderBy(o => o.VehicleName).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(firstEngine.Description, engine.Description);
        }
    }

    [Fact]
    public virtual async Task Can_update_owner_with_dependents()
    {
        var options = await Fixture.CreateOptions();
        Operator firstOperator;
        using (var context = new EmbeddedTransportationContext(options))
        {
            firstOperator = (await context.Set<Vehicle>().OrderBy(o => o.Name).FirstAsync(cancellationToken: TestContext.Current.CancellationToken)).Operator;
            firstOperator.Name += "1";

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Set<Vehicle>().OrderBy(o => o.Name).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(firstOperator.Name, vehicle.Operator.Name);
        }
    }

    [Fact]
    public virtual async Task Can_attach_owner_with_dependents()
    {
        var options = await Fixture.CreateOptions();
        Vehicle firstVehicle;
        using (var context = new EmbeddedTransportationContext(options))
        {
            firstVehicle = await context.Set<Vehicle>().OrderBy(o => o.Name).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            //Issue #15289
            var firstVehicleEntry = await context.AddAsync(firstVehicle, TestContext.Current.CancellationToken);
            firstVehicleEntry.State = EntityState.Unchanged;
            firstVehicle.Operator.Name += "1";

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Set<Vehicle>().OrderBy(o => o.Name).FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(firstVehicle.Operator.Name, vehicle.Operator.Name);
        }
    }

    [Fact]
    public virtual async Task Can_manipulate_embedded_document_simple()
    {
        var options = await Fixture.CreateOptions(seed: false);
        var swappedOptions = await Fixture.CreateOptions(seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            await context.AddAsync(new Person { Id = 1, MainAddress = new Address { Street = "First", City = "Village" } }, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var person = await context.Set<Person>().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            person.MainAddress.Street = "Second Street";
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var person = await context.Set<Person>().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("Second Street", person.MainAddress.Street);
        }
    }

    [Fact]
    public virtual async Task Can_manipulate_embedded_collections_simple()
    {
        var options = await Fixture.CreateOptions(seed: false);
        var swappedOptions = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Person>(eb => eb.OwnsMany(
                v => v.Addresses, b =>
                {
                    b.OwnsMany(a => a.Notes).ToJsonProperty("IdNotes");
                    b.OwnsMany(a => a.IdNotes).ToJsonProperty("Notes");
                })),
            seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            await context.AddAsync(new Person { Id = 1, Addresses = [new Address { Street = "First", City = "Village" }] }, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var person = await context.Set<Person>().SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            person.Addresses.Add(new Address { Street = "Second", City = "Village" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Theory, InlineData(false), InlineData(true)]
    public virtual async Task Can_manipulate_embedded_collections(bool useIds)
    {
        var options = await Fixture.CreateOptions(seed: false);
        var swappedOptions = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Person>(eb => eb.OwnsMany(
                v => v.Addresses, b =>
                {
                    b.OwnsMany(a => a.Notes).ToJsonProperty("IdNotes");
                    b.OwnsMany(a => a.IdNotes).ToJsonProperty("Notes");
                })),
            seed: false);

        Address existingAddress1Person2;
        Address existingAddress1Person3;
        Address existingAddress2Person3;
        Address addedAddress1;
        Address addedAddress2;
        Address addedAddress3;
        using (var context = new EmbeddedTransportationContext(options))
        {
            await context.AddAsync(new Person { Id = 1 }, TestContext.Current.CancellationToken);
            existingAddress1Person2 = new Address { Street = "Second", City = "Village" };
            if (useIds)
            {
                existingAddress1Person2.IdNotes =
                [
                    new() { Id = 4, Content = "First note" }, new() { Id = 3, Content = "Second note" }
                ];
            }
            else
            {
                existingAddress1Person2.Notes = [new() { Content = "First note" }, new() { Content = "Second note" }];
            }

            var existingAddress2Person2 = new Address { Street = "First", City = "Village" };
            await context.AddAsync(new Person { Id = 2, Addresses = [existingAddress1Person2, existingAddress2Person2] }, TestContext.Current.CancellationToken);
            existingAddress1Person3 = new Address
            {
                Street = "First",
                City = "City",
                AddressTitle = new AddressTitle { Title = "P3 Shipping" },
            };
            if (useIds)
            {
                existingAddress1Person3.IdNotes = [new() { Id = 2, Content = "First City note" }];
            }
            else
            {
                existingAddress1Person3.Notes = [new() { Content = "First City note" }];
            }

            existingAddress2Person3 = new Address
            {
                Street = "Second",
                City = "City",
                AddressTitle = new AddressTitle { Title = "P3 Billing" }
            };

            await context.AddAsync(new Person { Id = 3, Addresses = [existingAddress1Person3, existingAddress2Person3] }, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            var people = await context.Set<Person>().OrderBy(o => o.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Empty(people[0].Addresses);

            Assert.Equal(2, people[1].Addresses.Count);
            Assert.Same(existingAddress1Person2, people[1].Addresses.First());
            Assert.Same(existingAddress2Person2, people[1].Addresses.Last());

            if (useIds)
            {
                Assert.Equal(2, existingAddress1Person2.IdNotes.Count);
            }
            else
            {
                Assert.Equal(2, existingAddress1Person2.Notes.Count);
            }

            Assert.Same(existingAddress1Person3, people[2].Addresses.First());
            Assert.Same(existingAddress2Person3, people[2].Addresses.Last());

            Assert.Equal(2, people[2].Addresses.Count);
            Assert.Same(existingAddress1Person3, people[2].Addresses.First());
            Assert.Same(existingAddress2Person3, people[2].Addresses.Last());
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var people = await context.Set<Person>().OrderBy(o => o.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            addedAddress1 = new Address
            {
                Street = "First",
                City = "Town",
                AddressTitle = new AddressTitle { Title = "P1" }
            };
            people[0].Addresses.Add(addedAddress1);

            addedAddress2 = new Address
            {
                Street = "Another",
                City = "Village",
                AddressTitle = new AddressTitle { Title = "P2" }
            };
            if (useIds)
            {
                addedAddress2.IdNotes = existingAddress1Person2.IdNotes;
                foreach (var note in addedAddress2.IdNotes)
                {
                    note.AddressId = 0;
                }
            }
            else
            {
                addedAddress2.Notes = existingAddress1Person2.Notes;
            }

            people[1].Addresses.Remove(people[1].Addresses.First());
            people[1].Addresses.Add(addedAddress2);

            addedAddress3 = new Address
            {
                Street = "Another",
                City = "City",
                AddressTitle = new AddressTitle { Title = "P3 Alternative" }
            };
            if (useIds)
            {
                addedAddress3.IdNotes = [new() { Id = -1, Content = "Another note" }];
            }
            else
            {
                addedAddress3.Notes = [new() { Content = "Another note" }];
            }

            var existingFirstAddressEntry = context.Entry(people[2].Addresses.First());

            Assert.Equal("First", people[2].Addresses.First().Street);

            existingAddress1Person3 = people[2].Addresses.First();
            existingAddress2Person3 = people[2].Addresses.Last();
            people[2].Addresses.Remove(existingAddress2Person3);
            people[2].Addresses.Add(addedAddress3);
            people[2].Addresses.Add(existingAddress2Person3);

            if (useIds)
            {
                existingAddress1Person3.IdNotes = [new() { Id = 1, Content = "Some City note" }];
            }
            else
            {
                existingAddress1Person3.Notes = [new() { Content = "Some City note" }];
            }

            if (useIds)
            {
                existingAddress2Person3.IdNotes.Add(new NoteWithId { Id = 4, Content = "City note" });
            }
            else
            {
                existingAddress2Person3.Notes.Add(new Note { Content = "City note" });
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            await AssertState(context, useIds);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            await AssertState(context, useIds);
        }

        static async Task AssertState(EmbeddedTransportationContext context, bool useIds)
        {
            var people = await context.Set<Person>().OrderBy(o => o.Id).ToListAsync();
            var firstAddress = people[0].Addresses.Single();
            Assert.Equal("First", firstAddress.Street);
            Assert.Equal("Town", firstAddress.City);
            Assert.Equal("P1", firstAddress.AddressTitle.Title);
            Assert.Empty(firstAddress.Notes);
            Assert.Empty(firstAddress.IdNotes);

            var addresses = people[1].Addresses.ToList();
            Assert.Equal(2, addresses.Count);

            Assert.Equal("First", addresses[0].Street);
            Assert.Equal("Village", addresses[0].City);
            Assert.Null(addresses[0].AddressTitle);
            Assert.Empty(addresses[0].Notes);
            Assert.Empty(addresses[0].IdNotes);

            Assert.Equal("Another", addresses[1].Street);
            Assert.Equal("Village", addresses[1].City);
            Assert.Equal("P2", addresses[1].AddressTitle.Title);
            if (useIds)
            {
                var notes = addresses[1].IdNotes;
                Assert.Equal(2, notes.Count);
                if (useIds)
                {
                    Assert.Equal(4, notes.First().Id);
                    Assert.Equal(3, notes.Last().Id);
                }
                else
                {
                    Assert.Equal(1, notes.First().Id);
                    Assert.Equal(2, notes.Last().Id);
                }

                Assert.Equal("First note", notes.First().Content);
                Assert.Equal("Second note", notes.Last().Content);
            }
            else
            {
                var notes = addresses[1].Notes;
                Assert.Equal(2, notes.Count);
                Assert.Equal("First note", notes.First().Content);
                Assert.Equal("Second note", notes.Last().Content);
            }

            addresses = people[2].Addresses.ToList();
            Assert.Equal(3, addresses.Count);

            Assert.Equal("First", addresses[0].Street);
            Assert.Equal("City", addresses[0].City);
            Assert.Equal("P3 Shipping", addresses[0].AddressTitle.Title);
            if (useIds)
            {
                Assert.Single(addresses[0].IdNotes);
                Assert.Equal(1, addresses[0].IdNotes.First().Id);
                Assert.Equal("Some City note", addresses[0].IdNotes.First().Content);
            }
            else
            {
                Assert.Single(addresses[0].Notes);
                Assert.Equal("Some City note", addresses[0].Notes.First().Content);
            }

            var existingAddressEntry = context.Entry(addresses[0]);

            Assert.Equal("First", addresses[0].Street);

            Assert.Equal("Another", addresses[1].Street);
            Assert.Equal("City", addresses[1].City);
            Assert.Equal("P3 Alternative", addresses[1].AddressTitle.Title);
            if (useIds)
            {
                Assert.Single(addresses[1].IdNotes);
                Assert.Equal(-1, addresses[1].IdNotes.First().Id);
                Assert.Equal("Another note", addresses[1].IdNotes.First().Content);
            }
            else
            {
                Assert.Single(addresses[1].Notes);
                Assert.Equal("Another note", addresses[1].Notes.First().Content);
            }

            Assert.Equal("Second", addresses[2].Street);
            Assert.Equal("City", addresses[2].City);
            Assert.Equal("P3 Billing", addresses[2].AddressTitle.Title);
            if (useIds)
            {
                Assert.Single(addresses[2].IdNotes);
                Assert.Equal(4, addresses[2].IdNotes.First().Id);
                Assert.Equal("City note", addresses[2].IdNotes.First().Content);
            }
            else
            {
                Assert.Single(addresses[2].Notes);
                Assert.Equal("City note", addresses[2].Notes.First().Content);
            }
        }
    }

    [Fact]
    public virtual async Task Old_still_works()
    {
        var options = await Fixture.CreateOptions(seed: false);
        var swappedOptions = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Person>(eb => eb.OwnsMany(
                v => v.Addresses, b =>
                {
                    b.OwnsMany(a => a.Notes).ToJsonProperty("IdNotes");
                    b.OwnsMany(a => a.IdNotes).ToJsonProperty("Notes");
                })),
            seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            await context.AddAsync(new Person
            {
                Id = 1,
                Addresses =
                    [
                        new()
                        {
                            Street = "Second",
                            City = "Village",
                            Notes = [new() { Content = "First note" }, new() { Content = "Second note" }],
                            IdNotes = [new() { Id = 3, Content = "Third note" }, new() { Id = 4, Content = "Fourth note" }]
                        }
                    ]
            }, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var people = await context.Set<Person>().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            var address = people.Single().Addresses.Single();

            Assert.Equal("First note", address.Notes.First().Content);
            Assert.Equal("Second note", address.Notes.Skip(1).First().Content);

            var idNote1 = address.IdNotes.First();
            Assert.Equal(3, idNote1.Id);
            Assert.Equal("Third note", idNote1.Content);

            var idNote2 = address.IdNotes.Skip(1).First();
            Assert.Equal(4, idNote2.Id);
            Assert.Equal("Fourth note", idNote2.Content);
        }

        using (var context = new EmbeddedTransportationContext(swappedOptions))
        {
            var people = await context.Set<Person>().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            var address = people.Single().Addresses.Single();

            Assert.Equal("Third note", address.Notes.First().Content);
            Assert.Equal("Fourth note", address.Notes.Skip(1).First().Content);

            var idNote1 = address.IdNotes.First();
            Assert.Equal(1, idNote1.Id);
            Assert.Equal("First note", idNote1.Content);
            var idNote2 = address.IdNotes.Skip(1).First();
            Assert.Equal(2, idNote2.Id);
            Assert.Equal("Second note", idNote2.Content);
        }
    }

    public record struct CosmosPage<T>(List<T> Results, string ContinuationToken);

    [Fact]
    public virtual async Task Properties_on_owned_types_can_be_client_generated()
    {
        var options = await Fixture.CreateOptions(seed: false);

        using var context = new EmbeddedTransportationContext(options);
        var address = new Address
        {
            Street = "First",
            City = "City",
            AddressTitle = new AddressTitle()
        };

        await context.AddAsync(new Person { Id = 1, Addresses = [address] }, TestContext.Current.CancellationToken);
        Assert.Equal("DefaultTitle", address.AddressTitle.Title);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var people = await context.Set<Person>().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Same(address, people[0].Addresses.Single());
    }

    [Fact]
    public virtual async Task Can_use_non_int_keys_for_embedded_entities()
    {
        var options = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Person>(eb => eb.OwnsMany(
                v => v.Addresses, b =>
                {
                    b.Property<Guid>("Id");
                    b.Ignore(a => a.IdNotes);
                })),
            seed: false);

        Address address;
        Guid addressGuid;
        await using (var context = new EmbeddedTransportationContext(options))
        {
            var person = new Person { Id = 1 };
            address = new Address { Street = "Second", City = "Village" };
            person.Addresses.Add(address);
            await context.AddAsync(person, TestContext.Current.CancellationToken);

            var addressEntry = context.Entry(address);
            addressGuid = (Guid)addressEntry.Property("Id").CurrentValue!;

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = new EmbeddedTransportationContext(options))
        {
            var people = await context.Set<Person>().OrderBy(o => o.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            var addresses = people[0].Addresses.ToList();
            Assert.Single(addresses);

            Assert.Equal(address.Street, addresses[0].Street);
            Assert.Equal(address.City, addresses[0].City);

            var addressEntry = context.Entry(addresses[0]);
            Assert.Equal(addressGuid, (Guid)addressEntry.Property("Id").CurrentValue!);
        }
    }

    [Fact]
    public virtual async Task Can_query_and_modify_nested_embedded_types()
    {
        var options = await Fixture.CreateOptions();
        using (var context = new EmbeddedTransportationContext(options))
        {
            var missile = await context.Set<Vehicle>().FirstAsync(v => v.Name == "AIM-9M Sidewinder", cancellationToken: TestContext.Current.CancellationToken);


            Assert.Equal("Heat-seeking", missile.Operator.Details!.Type);

            missile.Operator.Details.Type = "IR";

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var missile = await context.Set<Vehicle>().FirstAsync(v => v.Name == "AIM-9M Sidewinder", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal("IR", missile.Operator.Details!.Type);
        }
    }

    [Fact]
    public virtual async Task Can_query_just_embedded_reference()
    {
        var options = await Fixture.CreateOptions();
        using var context = new EmbeddedTransportationContext(options);
        var firstOperator = await context.Set<Vehicle>().OrderBy(o => o.Name).Select(v => v.Operator)
            .AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Albert Williams", firstOperator.Name);
        Assert.Null(firstOperator.Vehicle);
    }

    [Fact]
    public virtual async Task Can_query_just_embedded_collection()
    {
        var options = await Fixture.CreateOptions(seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            await context.AddAsync(new Person
            {
                Id = 3,
                Addresses =
                    [
                        new Address { Street = "First", City = "City" }, new Address { Street = "Second", City = "City" }
                    ]
            }, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var addresses = await context.Set<Person>().Select(p => p.Addresses).AsNoTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, addresses.Count);
        }
    }

    [Fact]
    public virtual async Task Inserting_dependent_without_principal_throws()
    {
        var options = await Fixture.CreateOptions(seed: false);
        using var context = new EmbeddedTransportationContext(options);
        await context.AddAsync(new LicensedOperator
        {
            Name = "Jack Jackson",
            LicenseType = "Class A CDC",
            VehicleName = "Fuel transport"
        }, TestContext.Current.CancellationToken);

        Assert.Equal(
            CosmosStrings.OrphanedNestedDocumentSensitive(
                nameof(Operator), nameof(Vehicle), "{VehicleName: Fuel transport}"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken))).Message);
    }

    [Fact]
    public virtual async Task Can_change_nested_instance_non_derived()
    {
        var options = await Fixture.CreateOptions();
        using (var context = new EmbeddedTransportationContext(options))
        {
            var bike = await context.Vehicles.SingleAsync(v => v.Name == "Trek Pro Fit Madone 6 Series", cancellationToken: TestContext.Current.CancellationToken);

            bike.Operator = new Operator { Name = "Chris Horner" };

            context.ChangeTracker.DetectChanges();

            bike.Operator = new LicensedOperator { Name = "repairman" };

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var bike = await context.Vehicles.SingleAsync(v => v.Name == "Trek Pro Fit Madone 6 Series", cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("repairman", bike.Operator.Name);
        }
    }

    [Fact]
    public virtual async Task Can_change_principal_instance_non_derived()
    {
        var options = await Fixture.CreateOptions();
        using (var context = new EmbeddedTransportationContext(options))
        {
            var bike = await context.Vehicles.SingleAsync(v => v.Name == "Trek Pro Fit Madone 6 Series", cancellationToken: TestContext.Current.CancellationToken);

            var newBike = new Vehicle
            {
                Name = "Trek Pro Fit Madone 6 Series",
                Operator = bike.Operator,
                SeatingCapacity = 2
            };

            context.Remove(bike);
            await context.AddAsync(newBike, TestContext.Current.CancellationToken);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var bike = await context.Vehicles.SingleAsync(v => v.Name == "Trek Pro Fit Madone 6 Series", cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, bike.SeatingCapacity);
            Assert.NotNull(bike.Operator);
        }
    }

    [Fact]
    public virtual async Task Can_use_non_persisted_properties_owned()
    {
        var options = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Vehicle>(eb => eb.OwnsOne(
                v => v.Operator, b => b.Property(x => x.Name).ToJsonProperty(""))),
            seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = new Vehicle { Name = "Test Vehicle", Operator = new Operator { Name = "Test Operator" } };
            await context.AddAsync(vehicle, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Test Operator", vehicle.Operator.Name);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Vehicles.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(vehicle.Operator.Name);

            vehicle.Operator.Name = "Theon Greyjoy";
            // Assert.Equal(0, await context.SaveChangesAsync()); // @TODO: #37929 non-persisted property changes should not cause SaveChanges to update the document
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Vehicles.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(vehicle.Operator.Name);
        }
    }

    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    [Fact(Skip = "Complex-type equality comparisons return no results")]
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/EmbeddedDocumentsTest.cs", 741, 787,
        Skip = true, Justification = "Complex-type equality comparisons return no results")]
    public virtual async Task Can_use_non_persisted_properties_complex()
    {
        var options = await Fixture.CreateOptions(
            modelBuilder => modelBuilder.Entity<Vehicle>(eb =>
            {
                eb.Ignore(x => x.Operator);
                eb.ComplexProperty(
                    v => v.Operator, b => b.Property(x => x.Name).ToJsonProperty(""));
            }),
            seed: false);

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = new Vehicle { Name = "Test Vehicle", Operator = new Operator { Name = "Test Operator" } };
            await context.AddAsync(vehicle, TestContext.Current.CancellationToken);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            Assert.Equal("Test Operator", vehicle.Operator.Name);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Vehicles.SingleAsync(x => x.Operator == new Operator(), cancellationToken: TestContext.Current.CancellationToken);



            Assert.Null(vehicle.Operator.Name);

            vehicle.Operator.Name = "Theon Greyjoy";
            // Assert.Equal(0, await context.SaveChangesAsync()); // @TODO: #37929 non-persisted property changes should not cause SaveChanges to update the document
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = new EmbeddedTransportationContext(options))
        {
            var vehicle = await context.Vehicles.SingleAsync(x => x.Operator == new Operator(), cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(vehicle.Operator.Name);
        }
    }

    // Model variants within one test must read the same physical documents. Different tests
    // get different fixture instances and databases; no class shares another class's database.
    public sealed class CosmosFixture : IAsyncDisposable
    {
        private readonly string _database = "icc-embedded-" + Guid.NewGuid().ToString("N");
        private readonly List<ServiceProvider> _providers = [];
        private readonly List<InProcessInfoCarrierServer> _servers = [];
        private EmbeddedTransportationContextOptions? _firstBackendOptions;

        public async Task<EmbeddedTransportationContextOptions> CreateOptions(
            Action<ModelBuilder>? onModelCreating = null, bool seed = true)
        {
            var token = TestContext.Current.CancellationToken;
            await CosmosEmulator.EnsureAvailableAsync(token);
            if (_firstBackendOptions is null) CosmosEmulator.ReportDatabaseOwner(_database, this, nameof(EmbeddedDocumentsTest));
            var builder = new DbContextOptionsBuilder();
            CosmosEmulator.Configure(builder, _database).EnableSensitiveDataLogging()
                .ReplaceService<IModelCacheKeyFactory, MappingCacheKeyFactory>()
                .ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined).Ignore(CoreEventId.MappedNavigationIgnoredWarning));
            var backendOptions = new EmbeddedTransportationContextOptions(builder.Options, onModelCreating);
            _firstBackendOptions ??= backendOptions;
            using var setup = new EmbeddedTransportationContext(backendOptions);
            // Ensure rather than recreate: alternative mappings must retain these same documents.
            await setup.Database.EnsureCreatedAsync(token);
            if (seed) await setup.SeedAsync();

            var services = new ServiceCollection();
            services.AddScoped<DbContext>(_ => new EmbeddedTransportationContext(backendOptions));
            var provider = services.BuildServiceProvider();
            _providers.Add(provider);
            var serverOptions = new InfoCarrierServerOptions().WithStoresOwnedDataInsideOwner(true);
            serverOptions.AddAllowedTypes(typeof(CosmosQueryableExtensions));
            var server = new InProcessInfoCarrierServer(provider, serverOptions);
            _servers.Add(server);
            var serializer = new SystemTextJsonInfoCarrierSerializer();
            var envelope = new InfoCarrierEnvelopeServer(server, serializer);
            var transport = new InfoCarrier.Core.FunctionalTests.TestUtilities.InProcessInfoCarrierTransport(
                envelope.Dispatch, envelope.DispatchAsync, serializer);
            var client = new TransportInfoCarrierClient(transport, serializer);
            var clientOptions = new DbContextOptionsBuilder().UseInfoCarrier(client, options =>
            {
                options.ServerUsesRelationalQueryRules = false;
                options.AddAllowedTypes(typeof(CosmosQueryableExtensions));
            }).UseModel(setup.Model).Options;
            return new EmbeddedTransportationContextOptions(clientOptions, onModelCreating);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_firstBackendOptions is not null)
                    await CosmosEmulator.RunDatabaseCleanupAsync(async () =>
                    {
                        using var context = new EmbeddedTransportationContext(_firstBackendOptions);
                        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                    }, _database, nameof(EmbeddedDocumentsTest), CancellationToken.None);
            }
            finally
            {
                foreach (var server in _servers) await server.DisposeAsync();
                foreach (var provider in _providers) await provider.DisposeAsync();
            }
        }
    }

    private sealed class MappingCacheKeyFactory : IModelCacheKeyFactory
    {
        public MappingCacheKeyFactory() { }
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((EmbeddedTransportationContext)context).Options.OnModelCreating, designTime);
    }

    public record class EmbeddedTransportationContextOptions(DbContextOptions Options, Action<ModelBuilder>? OnModelCreating);

    protected class EmbeddedTransportationContext(EmbeddedTransportationContextOptions options) : TransportationContext(options.Options)
    {
        public EmbeddedTransportationContextOptions Options { get; } = options;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDiscriminatorInJsonIds();

            modelBuilder.Entity<Vehicle>(eb =>
            {
                eb.HasKey(e => e.Name);
                eb.OwnsOne(v => v.Operator).OwnsOne(v => v.Details);
            });
            modelBuilder.Entity<PoweredVehicle>();

            modelBuilder.Entity<Engine>(eb =>
            {
                eb.HasKey(e => e.VehicleName);
                eb.HasOne(e => e.Vehicle)
                    .WithOne(e => e.Engine)
                    .HasForeignKey<Engine>(e => e.VehicleName);
            });

            modelBuilder.Entity<FuelTank>(eb =>
            {
                eb.HasKey(e => e.VehicleName);
                eb.HasOne(e => e.Engine)
                    .WithOne(e => e.FuelTank)
                    .HasForeignKey<FuelTank>(e => e.VehicleName)
                    .OnDelete(DeleteBehavior.Restrict);
                eb.HasOne(e => e.Vehicle)
                    .WithOne()
                    .HasForeignKey<FuelTank>("VehicleName1");
            });

            modelBuilder.Entity<ContinuousCombustionEngine>();
            modelBuilder.Entity<IntermittentCombustionEngine>();

            modelBuilder.Ignore<SolidFuelTank>();
            modelBuilder.Ignore<SolidRocket>();

            modelBuilder.Entity<PersonBase>();
            modelBuilder.Entity<Person>(eb =>
            {
                eb.OwnsMany(
                    v => v.Addresses, b =>
                    {
                        b.ToJsonProperty("Stored Addresses");
                        b.OwnsOne(a => a.AddressTitle).Property(a => a.Title).HasValueGenerator<TitleGenerator>().IsRequired();
                    });
                eb.OwnsOne(x => x.MainAddress);
            });
            Options.OnModelCreating?.Invoke(modelBuilder);
        }
    }

    private class TitleGenerator : ValueGenerator<string>
    {
        public override bool GeneratesTemporaryValues
            => false;

        public override string Next(EntityEntry entry)
            => "DefaultTitle";
    }

    private abstract class PersonBase
    {
        public int Id { get; set; }
    }

    private class Person : PersonBase
    {
        public Address MainAddress { get; set; } = null!;
        public ICollection<Address> Addresses { get; set; } = [];
    }

    public class Address
    {
        public string Street { get; set; } = null!;
        public string City { get; set; } = null!;
        public AddressTitle AddressTitle { get; set; } = null!;
        public ICollection<Note> Notes { get; set; } = [];
        public ICollection<NoteWithId> IdNotes { get; set; } = [];
    }

    public class AddressTitle
    {
        public string Title { get; set; } = null!;
    }

    public class Note
    {
        public string Content { get; set; } = null!;
    }

    public class NoteWithId
    {
        public int Id { get; set; }
        public int AddressId { get; set; }
        public string Content { get; set; } = null!;
    }
}
