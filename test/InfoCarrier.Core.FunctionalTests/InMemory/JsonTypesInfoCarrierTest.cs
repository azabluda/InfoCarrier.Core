// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>JsonTypesTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     <para>
///         Every mapped type written and read back through its <c>JsonValueReaderWriter</c> — which
///         is exactly the mechanism A34 made this provider's fallback for a value the wire has no
///         primitive for. The base builds its model per test, so the two forwarded members are
///         A49's <see cref="NonSharedModelInfoCarrierHarness" />.
///     </para>
///     <para>
///         **EF's eight spatial overrides are adopted, and were not before C15.** Until the
///         NetTopologySuite branch landed in <c>InfoCarrierTypeMappingSource</c> this provider
///         failed one step earlier than InMemory does — it mapped no spatial type at all, so the
///         model never built and the override would have asserted a symptom this provider did not
///         have (A39). With the branch the model validates, the failure is InMemory's own
///         <c>NullReferenceException</c> from a geometry having no <c>JsonValueReaderWriter</c>,
///         and the reason matches character for character. That is A63's bar, so they are taken.
///     </para>
///     <para>
///         The seven <c>_as_GeoJson</c> variants stay red and are **not** ours: EF's own
///         <c>JsonGeoJsonReaderWriter</c> re-emits a number with
///         <c>StringBuilder.Append(reader.GetDecimal())</c>, which is culture-sensitive, so under
///         this machine's <c>en-SE</c> a coordinate array <c>[2.0,4.0]</c> comes back as
///         <c>[2,0,4,0]</c> and the point reads as <c>POINT (2 0)</c>. The A64 family.
///     </para>
/// </remarks>
public class JsonTypesInfoCarrierTest(NonSharedFixture fixture) : JsonTypesTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

    // The eight below are EF's `JsonTypesInMemoryTest` overrides verbatim: no built-in JSON
    // support exists for a spatial type on a non-relational provider, so the round-trip
    // dereferences a null reader/writer. Matched by reason before being taken (A63).

    /// <inheritdoc />
    public override Task Can_read_write_point()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_point());

    /// <inheritdoc />
    public override Task Can_read_write_point_with_M()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_point_with_M());

    /// <inheritdoc />
    public override Task Can_read_write_point_with_Z()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_point_with_Z());

    /// <inheritdoc />
    public override Task Can_read_write_point_with_Z_and_M()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_point_with_Z_and_M());

    /// <inheritdoc />
    public override Task Can_read_write_line_string()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_line_string());

    /// <inheritdoc />
    public override Task Can_read_write_multi_line_string()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_multi_line_string());

    /// <inheritdoc />
    public override Task Can_read_write_polygon()
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Can_read_write_polygon());

    /// <inheritdoc />
    public override Task Can_read_write_polygon_typed_as_geometry()
        => Assert.ThrowsAsync<NullReferenceException>(base.Can_read_write_polygon_typed_as_geometry);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
