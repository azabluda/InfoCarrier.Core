// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

// The remaining Northwind spec-test bases, adopted wholesale (ADR-004). Fixture generics mirror
// EF Core's own NorthwindQuery*InMemoryTest classes.
//
// These start with NO overrides on purpose. Every failure is real information, triaged in
// docs/plans/v10/implementation-plan.md into: conceptually inapplicable to a remoting provider, backing-
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

// NorthwindFunctions and NorthwindNavigations moved to ADR-009 Tier B (SQLite) in R20 —
// see InfoCarrier.Core.FunctionalTests.Sqlite.Query. A base belongs to exactly one tier
// (CLAUDE.md), and both translate on a relational backend.

public class NorthwindQueryFiltersQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer> fixture)
    : NorthwindQueryFiltersQueryTestBase<NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer>>(fixture);

public class NorthwindQueryTaggingQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture);
