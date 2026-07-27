// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     The first Northwind spec-test class for InfoCarrier (F7): inherits the
///     <see cref="NorthwindWhereQueryTestBase{TFixture}" /> suite via the InfoCarrier fixture.
///     Tests that exercise not-yet-supported query features are overridden to no-op
///     (skip) until the pipeline supports them.
/// </summary>
public class NorthwindWhereQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindWhereQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
}
