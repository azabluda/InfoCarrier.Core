// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ModelBuilding;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ModelBuilding;

/// <summary>
///     EF's model-building suite against the InfoCarrier client provider.
/// </summary>
/// <remarks>
///     <para>
///         Nothing here touches a store. What it exercises is the one thing the client model has
///         that no other test reaches directly: this provider's own conventions and type mapping
///         source, applied by `ModelBuilder` to every shape EF supports — inheritance, owned types,
///         complex types and collections, and each relationship cardinality. A client `DbContext`
///         has no database, so its model is the whole of what it knows, and it must agree with the
///         server's (ADR-008).
///     </para>
///     <para>
///         Structured as EF's own `InMemoryModelBuilderTest`: abstract classes per spec base, then
///         one concrete set built through `GenericTestModelBuilder`. EF's InMemory suite adds three
///         more concrete sets — non-generic, string-named and unqualified-string — which cover the
///         *builder API's* surface rather than the provider's, and are further coverage available
///         later. The provider-specific `[ConditionalFact]`s EF adds to its own variants are
///         InMemory's tests, not spec base members, and are deliberately not carried.
///     </para>
/// </remarks>
public class ModelBuilderInfoCarrierTest : RelationalModelBuilderTest
{
    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierNonRelationship(InfoCarrierModelBuilderFixture fixture)
        : RelationalNonRelationshipTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierComplexType(InfoCarrierModelBuilderFixture fixture)
        : RelationalComplexTypeTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierComplexCollection(InfoCarrierModelBuilderFixture fixture)
        : RelationalComplexCollectionTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierInheritance(InfoCarrierModelBuilderFixture fixture)
        : RelationalInheritanceTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierOneToMany(InfoCarrierModelBuilderFixture fixture)
        : RelationalOneToManyTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierManyToMany(InfoCarrierModelBuilderFixture fixture)
        : RelationalManyToManyTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierManyToOne(InfoCarrierModelBuilderFixture fixture)
        : RelationalManyToOneTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierOneToOne(InfoCarrierModelBuilderFixture fixture)
        : RelationalOneToOneTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public abstract class InfoCarrierOwnedTypes(InfoCarrierModelBuilderFixture fixture)
        : RelationalOwnedTypesTestBase(fixture), IClassFixture<InfoCarrierModelBuilderFixture>;

    /// <summary>
    ///     The model-building fixture.
    /// </summary>
    /// <remarks>
    ///     `ForeignKeysHaveIndexes` is left at EF's default of <see langword="true" />, unlike
    ///     InMemory's fixture. It is not a preference — it is a statement of what the provider
    ///     does, and this one keeps `ForeignKeyIndexConvention` where the InMemory provider drops
    ///     it. An index on the client model is metadata about a store the client does not have, so
    ///     it costs nothing and travels nowhere; whether to drop the convention anyway is a
    ///     provider question, not a test one.
    /// </remarks>
    public class InfoCarrierModelBuilderFixture : RelationalModelBuilderFixture
    {
        /// <inheritdoc />
        public override TestHelpers TestHelpers
            => InfoCarrierTestHelpers.Instance;
    }
}

/// <summary>
///     The concrete model-building set, built through the generic `ModelBuilder` API.
/// </summary>
public class ModelBuilderGenericInfoCarrierTest : ModelBuilderInfoCarrierTest
{
    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericNonRelationship(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierNonRelationship(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericComplexType(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierComplexType(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericComplexCollection(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierComplexCollection(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericInheritance(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierInheritance(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericOneToMany(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierOneToMany(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericManyToMany(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierManyToMany(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericManyToOne(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierManyToOne(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericOneToOne(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierOneToOne(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }

    /// <inheritdoc cref="ModelBuilderInfoCarrierTest" />
    public class InfoCarrierGenericOwnedTypes(InfoCarrierModelBuilderFixture fixture)
        : InfoCarrierOwnedTypes(fixture)
    {
        /// <inheritdoc />
        protected override TestModelBuilder CreateModelBuilder(Action<ModelConfigurationBuilder>? configure = null)
            => new GenericTestModelBuilder(Fixture, configure);
    }
}
