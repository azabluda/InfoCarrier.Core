// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

// The remaining Northwind spec-test bases, adopted wholesale (ADR-004). Fixture generics mirror
// EF Core's own NorthwindQuery*InMemoryTest classes.
//
// These start with NO overrides on purpose. Every failure is real information, triaged in
// docs/implementation-plan.md into: conceptually inapplicable to a remoting provider, backing-
// store limitation, or an InfoCarrier gap. Only the first two ever earn an override, and only
// with a stated reason — never to make the suite green (CLAUDE.md).
//
// Classes grow their own file once they accumulate overrides, as NorthwindWhereQueryInfoCarrierTest
// already has.

public class NorthwindAggregateOperatorsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindAggregateOperatorsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindAsNoTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindAsNoTrackingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindAsTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindAsTrackingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindChangeTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindChangeTrackingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindCompiledQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindCompiledQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindDbFunctionsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindDbFunctionsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindEFPropertyIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindEFPropertyIncludeQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindFunctionsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindFunctionsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindGroupByQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindIncludeNoTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindIncludeNoTrackingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindIncludeQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindJoinQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindJoinQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindKeylessEntitiesQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindKeylessEntitiesQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindMiscellaneousQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindMiscellaneousQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindNavigationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindNavigationsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindQueryFiltersQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer> fixture)
    : NorthwindQueryFiltersQueryTestBase<NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer>>(fixture);

public class NorthwindQueryTaggingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindSelectQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindSelectQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindSetOperationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindSetOperationsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

public class NorthwindStringIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindStringIncludeQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);
