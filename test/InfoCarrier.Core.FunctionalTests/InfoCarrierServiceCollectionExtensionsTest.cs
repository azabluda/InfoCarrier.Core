// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class InfoCarrierServiceCollectionExtensionsTest : EntityFrameworkServiceCollectionExtensionsTestBase
    {
        public InfoCarrierServiceCollectionExtensionsTest()
            : base(InfoCarrierTestHelpers.Instance)
        {
        }

        [ConditionalFact]
        public override void Required_services_are_registered_with_expected_lifetimes()
        {
            var infoCarrierCoreServices = EntityFrameworkServicesBuilder.CoreServices.ToDictionary(x => x.Key, x => x.Value);
            infoCarrierCoreServices.Remove(typeof(IQueryableMethodTranslatingExpressionVisitorFactory));
            infoCarrierCoreServices.Remove(typeof(IShapedQueryCompilingExpressionVisitorFactory));
            this.LifetimeTest(infoCarrierCoreServices);
        }
    }
}
