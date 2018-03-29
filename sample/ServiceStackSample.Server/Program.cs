// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(@"Preparing the database...");
            SqlServerShared.RecreateDatabase();

            string listeningOn = ServiceStackShared.BaseAddress;

            // Start self-hosted server
            using (new AppHost().Init().Start(listeningOn))
            {
                Console.WriteLine(
                    "AppHost Created at {0}, listening on {1}",
                    DateTime.Now,
                    listeningOn);

                Console.ReadKey();
            }
        }
    }
}
