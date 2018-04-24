// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;
    using Xunit.Abstractions;

    public class InheritanceInfoCarrierTest : InheritanceTestBase<InheritanceInfoCarrierFixture>
    {
        public InheritanceInfoCarrierTest(InheritanceInfoCarrierFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        public override void Discriminator_used_when_projection_over_derived_type2()
        {
            Assert.Equal(
                CoreStrings.PropertyNotFound(property: "Discriminator", entityType: "Bird"),
                Assert.Throws<InvalidOperationException>(
                    () => base.Discriminator_used_when_projection_over_derived_type2()).Message);
        }

        public override void Discriminator_with_cast_in_shadow_property()
        {
            Assert.Equal(
                CoreStrings.PropertyNotFound(property: "Discriminator", entityType: "Animal"),
                Assert.Throws<InvalidOperationException>(
                    () => base.Discriminator_with_cast_in_shadow_property()).Message);
        }
    }
}
