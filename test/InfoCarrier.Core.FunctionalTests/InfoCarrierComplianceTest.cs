// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore.BulkUpdates;
using Microsoft.EntityFrameworkCore.ModelBuilding;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.Query.Associations.Navigations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Types;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The coverage scoreboard for ADR-009 <b>Tier A</b>: fails while any
///     <c>EFCore.Specification.Tests</c> base class has no InfoCarrier subclass <em>in this
///     assembly</em>, listing every one that is missing.
/// </summary>
/// <remarks>
///     <para>
///         The core and relational scans share one test project since R136. This scan accounts
///         for core specification bases; <c>RelationalInfoCarrierComplianceTest</c> accounts for
///         the relational bases. Core bases assigned to Tier B are listed below so the two
///         scans preserve their separate coverage responsibilities.
///     </para>
///     <para>
///         The old inapplicable relational list moved to the relational scan. This project now
///         references both specification assemblies; the former assembly-boundary explanation
///         stopped applying when the test projects merged.
///     </para>
///     <para>
///         <strong>What it ignores instead is a CORE base adopted on Tier B</strong>, and each
///         entry says so. A base that InMemory cannot host runs on the tier that translates
///         (ADR-009, and CLAUDE.md's rule that "EF ships no InMemory test for this base" means move
///         it to Tier B rather than drop it). The entry here records that assignment.
///     </para>
///     <para>
///         <strong>A base that is merely not built yet must stay out of this list</strong> so this
///         test keeps reporting it.
///     </para>
/// </remarks>
public class InfoCarrierComplianceTest : ComplianceTestBase
{
    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(InfoCarrierComplianceTest).Assembly;

    /// <summary>
    ///     Bases that are conceptually inapplicable to InfoCarrier — each with the reason.
    ///     Seeded in M1-I3; the relational entries were added for #56 in R2 and R45.
    /// </summary>
    /// <remarks>
    ///     A base that is merely unadopted must NOT be listed here. Everything below needs a
    ///     service or an object that does not exist on this side of the wire, and no amount of
    ///     work in this repository would change that.
    /// </remarks>
    /// <summary>
    ///     Core specification bases adopted on <b>Tier B</b>, in
    ///     <c>InfoCarrier.Core.Relational.FunctionalTests</c>, so this assembly holds no subclass
    ///     of them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <strong>One reason covers the whole list, and it is a real one.</strong> EF's
    ///         InMemory provider client-evaluates nearly everything, so a base it cannot host runs
    ///         on the tier that translates instead (ADR-009, and CLAUDE.md: "EF ships no InMemory
    ///         test for this base" means move it to Tier B, not drop it). Where a base could go
    ///         either way, the tier that translates is the one whose green means more. Each of
    ///         these is <em>implemented and running</em>; nothing here is a gap. The per-base
    ///         reasoning is in the plan and its archive, not repeated 108 times.
    ///     </para>
    ///     <para>
    ///         <strong>THIS LIST IS NOT A PLACE TO PUT A BASE THAT IS MERELY UNADOPTED.</strong>
    ///         An entry claims a subclass exists in the sibling assembly, and the claim is checked:
    ///         <c>RelationalInfoCarrierComplianceTest</c> and this test are green together only
    ///         while every base is accounted for on exactly one of the two.
    ///     </para>
    ///     <para>
    ///         Generated from this test's own output when the projects were split (R122), not
    ///         written by hand.
    ///     </para>
    /// </remarks>
    protected override ICollection<Type> IgnoredTestBases { get; } =
    [
        typeof(BuiltInDataTypesTestBase<>),
        typeof(ComplexTypesTrackingTestBase<>),
        typeof(ConcurrencyDetectorDisabledTestBase<>),
        typeof(ConcurrencyDetectorEnabledTestBase<>),
        typeof(ConcurrencyDetectorTestBase<>),
        typeof(ConferencePlannerTestBase<>),
        typeof(ConvertToProviderTypesTestBase<>),
        typeof(CustomConvertersTestBase<>),
        typeof(DataAnnotationTestBase<>),
        typeof(GraphUpdatesTestBase<>),
        typeof(ProxyGraphUpdatesTestBase<>),
        typeof(KeysWithConvertersTestBase<>),
        typeof(LazyLoadProxyTestBase<>),
        typeof(ManyToManyTrackingTestBase<>),
        typeof(OptimisticConcurrencyTestBase<,>),
        typeof(PropertyValuesTestBase<>),
        typeof(AdHocMiscellaneousQueryTestBase),
        typeof(OwnedEntityQueryTestBase),
        typeof(SharedTypeQueryTestBase),
        typeof(StoreGeneratedFixupTestBase<>),
        typeof(StoreGeneratedTestBase<>),
        typeof(UpdatesTestBase<>),
        typeof(TypeTestBase<,>),
        typeof(AdHocAdvancedMappingsQueryTestBase),
        typeof(AdHocNavigationsQueryTestBase),
        typeof(AdHocQueryFiltersQueryTestBase),
        typeof(ComplexNavigationsCollectionsQueryTestBase<>),
        typeof(ComplexNavigationsCollectionsSharedTypeQueryTestBase<>),
        typeof(ComplexNavigationsQueryTestBase<>),
        typeof(ComplexNavigationsSharedTypeQueryTestBase<>),
        typeof(ComplexTypeQueryTestBase<>),
        typeof(CompositeKeysQueryTestBase<>),
        typeof(FunkyDataQueryTestBase<>),
        typeof(JsonQueryTestBase<>),
        typeof(InheritanceComplexTypesQueryTestBase<>),
        typeof(NorthwindAggregateOperatorsQueryTestBase<>),
        typeof(NorthwindFunctionsQueryTestBase<>),
        typeof(NorthwindGroupByQueryTestBase<>),
        typeof(NorthwindJoinQueryTestBase<>),
        typeof(NorthwindKeylessEntitiesQueryTestBase<>),
        typeof(NorthwindMiscellaneousQueryTestBase<>),
        typeof(NorthwindNavigationsQueryTestBase<>),
        typeof(NorthwindSelectQueryTestBase<>),
        typeof(NorthwindSetOperationsQueryTestBase<>),
        typeof(NorthwindWhereQueryTestBase<>),
        typeof(OwnedQueryTestBase<>),
        typeof(PrimitiveCollectionsQueryTestBase<>),
        typeof(ByteArrayTranslationsTestBase<>),
        typeof(EnumTranslationsTestBase<>),
        typeof(GuidTranslationsTestBase<>),
        typeof(MathTranslationsTestBase<>),
        typeof(MiscellaneousTranslationsTestBase<>),
        typeof(StringTranslationsTestBase<>),
        typeof(DateOnlyTranslationsTestBase<>),
        typeof(DateTimeOffsetTranslationsTestBase<>),
        typeof(DateTimeTranslationsTestBase<>),
        typeof(TimeOnlyTranslationsTestBase<>),
        typeof(TimeSpanTranslationsTestBase<>),
        typeof(ArithmeticOperatorTranslationsTestBase<>),
        typeof(BitwiseOperatorTranslationsTestBase<>),
        typeof(ComparisonOperatorTranslationsTestBase<>),
        typeof(LogicalOperatorTranslationsTestBase<>),
        typeof(MiscellaneousOperatorTranslationsTestBase<>),
        typeof(AssociationsBulkUpdateTestBase<>),
        typeof(AssociationsCollectionTestBase<>),
        typeof(AssociationsMiscellaneousTestBase<>),
        typeof(AssociationsPrimitiveCollectionTestBase<>),
        typeof(AssociationsProjectionTestBase<>),
        typeof(AssociationsSetOperationsTestBase<>),
        typeof(AssociationsStructuralEqualityTestBase<>),
        typeof(OwnedNavigationsCollectionTestBase<>),
        typeof(OwnedNavigationsMiscellaneousTestBase<>),
        typeof(OwnedNavigationsPrimitiveCollectionTestBase<>),
        typeof(OwnedNavigationsProjectionTestBase<>),
        typeof(OwnedNavigationsSetOperationsTestBase<>),
        typeof(OwnedNavigationsStructuralEqualityTestBase<>),
        typeof(NavigationsCollectionTestBase<>),
        typeof(NavigationsIncludeTestBase<>),
        typeof(NavigationsMiscellaneousTestBase<>),
        typeof(NavigationsPrimitiveCollectionTestBase<>),
        typeof(NavigationsProjectionTestBase<>),
        typeof(NavigationsSetOperationsTestBase<>),
        typeof(NavigationsStructuralEqualityTestBase<>),
        typeof(ComplexPropertiesBulkUpdateTestBase<>),
        typeof(ComplexPropertiesCollectionTestBase<>),
        typeof(ComplexPropertiesMiscellaneousTestBase<>),
        typeof(ComplexPropertiesPrimitiveCollectionTestBase<>),
        typeof(ComplexPropertiesProjectionTestBase<>),
        typeof(ComplexPropertiesSetOperationsTestBase<>),
        typeof(ComplexPropertiesStructuralEqualityTestBase<>),
        typeof(BulkUpdatesTestBase<>),
        typeof(FiltersInheritanceBulkUpdatesTestBase<>),
        typeof(InheritanceBulkUpdatesTestBase<>),
        typeof(NonSharedModelBulkUpdatesTestBase),
        typeof(NorthwindBulkUpdatesTestBase<>),
        typeof(ModelBuilderTest.ComplexCollectionTestBase),
        typeof(ModelBuilderTest.ComplexTypeTestBase),
        typeof(ModelBuilderTest.ModelBuilderTestBase),
        typeof(ModelBuilderTest.InheritanceTestBase),
        typeof(ModelBuilderTest.ManyToManyTestBase),
        typeof(ModelBuilderTest.ManyToOneTestBase),
        typeof(ModelBuilderTest.NonRelationshipTestBase),
        typeof(ModelBuilderTest.OneToManyTestBase),
        typeof(ModelBuilderTest.OneToOneTestBase),
        typeof(ModelBuilderTest.OwnedTypesTestBase),
    ];
}
