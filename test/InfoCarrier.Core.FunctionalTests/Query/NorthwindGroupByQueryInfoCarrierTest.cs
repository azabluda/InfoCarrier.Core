// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindGroupByQueryTestBase{TFixture}" /> over the InfoCarrier client with an
///     InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Every override here <strong>asserts</strong> a backing-store limitation rather than
///         suppressing a test: EF Core's own <c>NorthwindGroupByQueryInMemoryTest</c> overrides
///         exactly these thirteen the same way, because the InMemory provider cannot translate a
///         <c>GroupBy</c> that is not composed into an aggregate or a projection of its elements.
///         The query fails to translate on the server, the failure reaches the client unchanged,
///         and that is the behavior under test.
///     </para>
///     <para>
///         The limitation is InMemory's, not InfoCarrier's — a local InMemory provider fails
///         identically with no wire involved. When the relational (SQLite) backend lands as
///         ADR-009 Tier B, these overrides do not apply to it and must be deleted rather than
///         carried over; a relational provider translates all thirteen.
///     </para>
/// </remarks>
public class NorthwindGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindGroupByQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_entity(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_entity(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_non_nullable(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_non_nullable(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_anonymous_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_anonymous_type(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_multiple_properties_entity(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_multiple_properties_entity(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_complex_key_entity(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_complex_key_entity(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_nominal_type_entity(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_nominal_type_entity(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_anonymous_type_element_selector(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_anonymous_type_element_selector(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_projecting_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_projecting_collection(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_projecting_collection_composed(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_projecting_collection_composed(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_projecting_collection_and_single_result(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_projecting_collection_and_single_result(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_TagWith(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_TagWith(async),
            InMemoryStrings.NonComposedGroupByNotSupported);
}
