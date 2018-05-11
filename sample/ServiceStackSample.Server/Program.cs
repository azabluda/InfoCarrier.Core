// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Linq;
    using Microsoft.AspNetCore.Hosting.Server.Features;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(@"Preparing the database...");
            SqlServerShared.RecreateDatabase();

            // Start self-hosted server
            using (var host = new AppHost().Init().Start(ServiceStackShared.BaseAddress))
            {
                Console.WriteLine(
                    "AppHost Created at {0}, listening on {1}",
                    DateTime.Now,
                    ((AppHost)host).WebHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.Single());

                Console.ReadKey();
            }
        }
    }
}
