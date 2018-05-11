// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;

    public class InfoCarrierServiceCollectionExtensionsTest : EntityFrameworkServiceCollectionExtensionsTestBase
    {
        public InfoCarrierServiceCollectionExtensionsTest()
            : base(InfoCarrierTestHelpers.Instance)
        {
        }
    }
}
