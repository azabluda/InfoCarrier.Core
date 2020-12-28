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
            optionsBuilder.UseInfoCarrierClient(
                InfoCarrierTestHelpers.CreateDummyClient(optionsBuilder.Options.ContextType));

            var extension = optionsBuilder.Options.Extensions.OfType<InfoCarrierOptionsExtension>().Single();

            Assert.Equal("DummyDatabase", extension.InfoCarrierClient.ServerUrl);
        }

        [Fact]
        public void Can_replace_extension()
        {
            IInfoCarrierClient client = InfoCarrierTestHelpers.CreateDummyClient(typeof(DbContext));
            var optionsBuilder = new DbContextOptionsBuilder();

            optionsBuilder.UseInfoCarrierClient(client);
            var extension1 = optionsBuilder.Options.Extensions.OfType<InfoCarrierOptionsExtension>().Single();

            optionsBuilder.UseInfoCarrierClient(client);
            var extension2 = optionsBuilder.Options.Extensions.OfType<InfoCarrierOptionsExtension>().Single();

            Assert.NotSame(extension1, extension2);
            Assert.Same(extension1.InfoCarrierClient, extension2.InfoCarrierClient);
        }

        [ConditionalFact]
        public void Can_create_db_context_without_internal_provider()
        {
            IInfoCarrierClient client = InfoCarrierTestHelpers.CreateDummyClient(typeof(DbContext));
            var options = new DbContextOptionsBuilder().UseInfoCarrierClient(client).Options;
            using var context = new DbContext(options);
            Assert.Equal("InfoCarrier.Core", context.Database.ProviderName);
        }
    }
}
