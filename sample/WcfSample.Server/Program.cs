﻿// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.ServiceModel;

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(@"Preparing the database...");
            SqlServerShared.RecreateDatabase();

            // Start self-hosted WCF server
            using (var host = new ServiceHost(
                new InfoCarrierService(),
                new Uri($"http://{WcfShared.BaseUrl}/{WcfShared.ServiceName}")))
            {
                host.AddDefaultEndpoints();
                foreach (var ep in host.Description.Endpoints)
                {
                    ep.Binding = new BasicHttpBinding { MaxReceivedMessageSize = WcfShared.MaxReceivedMessageSize };
                }

                host.Open();
                foreach (Uri addr in host.BaseAddresses)
                {
                    Console.WriteLine($@"The service is ready at {addr}");
                }

                Console.WriteLine(@"Press any key to stop.");
                Console.ReadKey();
                host.Close();
            }
        }
    }
}
