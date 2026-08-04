// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

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

public class NorthwindFunctionsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindFunctionsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);

// Classes that assert an InMemory store limitation live in their own files, each mirroring
// EF Core's own *InMemoryTest override set. See docs/roadmap.md M3 for the inventory.

public class NorthwindNavigationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindNavigationsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    // Category 4 — client evaluation outside the final projection. The base expects this to
    // succeed because the InMemory provider client-evaluates everything in-process. A remoting
    // provider cannot: evaluating the predicate here means fetching the whole table first. EF
    // Core's own NorthwindNavigationsQueryRelationalTestBase overrides this test the same way,
    // for the same reason.
    public override Task Where_subquery_on_navigation_client_eval(bool async)
        => AssertTranslationFailed(() => base.Where_subquery_on_navigation_client_eval(async));
}

public class NorthwindQueryFiltersQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer> fixture)
    : NorthwindQueryFiltersQueryTestBase<NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer>>(fixture);

public class NorthwindQueryTaggingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);
