// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;

    public class FiltersInheritanceInfoCarrierTest : FiltersInheritanceTestBase<FiltersInheritanceInfoCarrierTest.TestFixture>
    {
        public FiltersInheritanceInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : InheritanceInfoCarrierFixture
        {
            protected override bool EnableFilters => true;
        }
    }
}
