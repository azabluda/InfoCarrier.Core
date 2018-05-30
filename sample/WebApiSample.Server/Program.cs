// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Pluralsight.Crypto;

    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
            => WebHost.CreateDefaultBuilder(args)
                .UseKestrel(o => o.Listen(
                    IPAddress.Any,
                    new Uri(WebApiShared.BaseAddress).Port,
                    listenOptions => listenOptions.UseHttps(GenerateCertificate())))
                .UseStartup<Startup>()
                .UseUrls()
                .Build();

        private static X509Certificate2 GenerateCertificate()
        {
            string certName = new Uri(WebApiShared.BaseAddress).DnsSafeHost;

            using (var ctx = new CryptContext())
            {
                ctx.Open();
                return ctx.CreateSelfSignedCertificate(
                    new SelfSignedCertProperties
                    {
                        IsPrivateKeyExportable = true,
                        KeyBitLength = 4096,
                        Name = new X500DistinguishedName($"cn={certName}"),
                        ValidFrom = DateTime.Today.AddDays(-1),
                        ValidTo = DateTime.Today.AddYears(1),
                    });
            }
        }
    }
}
