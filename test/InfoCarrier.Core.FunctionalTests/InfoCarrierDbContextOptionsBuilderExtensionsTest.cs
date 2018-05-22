// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System.Linq;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Client.Infrastructure.Internal;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class InfoCarrierDbContextOptionsBuilderExtensionsTest
    {
        [Fact]
        public void Can_add_extension_with_server_url_using_generic_options()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
            optionsBuilder.UseInfoCarrierBackend(
                InfoCarrierTestHelpers.CreateDummyBackend(optionsBuilder.Options.ContextType));

            var extension = optionsBuilder.Options.Extensions.OfType<InfoCarrierOptionsExtension>().Single();

            Assert.Equal("DummyDatabase", extension.InfoCarrierBackend.ServerUrl);
        }
    }
}
