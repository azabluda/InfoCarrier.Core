﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit.Abstractions;

    public class InheritanceInfoCarrierTest : InheritanceTestBase<InheritanceInfoCarrierFixture>
    {
        public InheritanceInfoCarrierTest(InheritanceInfoCarrierFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }
    }
}
