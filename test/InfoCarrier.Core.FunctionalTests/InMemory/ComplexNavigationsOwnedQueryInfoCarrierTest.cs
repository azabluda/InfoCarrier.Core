// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class ComplexNavigationsOwnedQueryInfoCarrierTest : ComplexNavigationsOwnedQueryTestBase<TestStoreBase, ComplexNavigationsOwnedQueryInfoCarrierFixture>
    {
        public ComplexNavigationsOwnedQueryInfoCarrierTest(ComplexNavigationsOwnedQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "issue #4311 (from ComplexNavigationsOwnedQueryInMemoryTest)")]
        public override void Nested_group_join_with_take()
        {
            base.Nested_group_join_with_take();
        }

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Null_reference_protection_complex_client_eval()
        {
            base.Null_reference_protection_complex_client_eval();
        }
    }
}
