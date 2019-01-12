// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
    using Xunit.Abstractions;

    public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryTestBase<GearsOfWarQueryInfoCarrierFixture>
    {
        public GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture testFixture, ITestOutputHelper testOutputHelper)
            : base(testFixture)
        {
        }

        [ConditionalTheory(Skip = "issue #12295")]
        public override Task Double_order_by_on_nullable_bool_coming_from_optional_navigation(bool isAsync)
        {
            return base.Double_order_by_on_nullable_bool_coming_from_optional_navigation(isAsync);
        }

        [ConditionalFact(Skip = "Client-side evaluation is not supported")]
        public override Task Multiple_includes_with_client_method_around_qsre_and_also_projecting_included_collection()
        {
            return base.Multiple_includes_with_client_method_around_qsre_and_also_projecting_included_collection();
        }
    }
}
