// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.AspNetCore.Hosting;
    using Pluralsight.Crypto;
    using ServiceStack;
    using ServiceStack.Auth;
    using ServiceStack.Caching;

    public class AppHost : AppSelfHostBase
    {
        public AppHost()
            : base("InfoCarrierSample", typeof(DataService).Assembly)
        {
        }

        public override IWebHostBuilder ConfigureHost(IWebHostBuilder host, string[] urlBases)
        {
            return host
                .UseKestrel(o => o.Listen(
                    System.Net.IPAddress.Any,
                    new Uri(ServiceStackShared.BaseAddress).Port,
                    listenOptions => listenOptions.UseHttps(GenerateCertificate()))).UseContentRoot(System.IO.Directory.GetCurrentDirectory())
                .UseWebRoot(System.IO.Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseUrls(urlBases);
        }

        public override void Configure(Funq.Container container)
        {
            // Use authorization
            this.Plugins.Add(new AuthFeature(
                () => new UserSession(),
                new IAuthProvider[] { new CredentialsAuthProvider() }));

            // Use in-memory cache for session objects
            container.Register<ICacheClient>(new MemoryCacheClient());

            // Make sessions expire after 10 seconds
            this.GlobalResponseFilters.Add((req, res, dto) =>
            {
                var session = req.GetSession();
                if (session != null)
                {
                    req.SaveSession(session, TimeSpan.FromSeconds(10));
                }
            });

            // Use in-memory user repo with two hard-coded users
            var userRep = new InMemoryAuthRepository();
            var user = userRep.CreateUserAuth();
            user.UserName = "alice";
            userRep.CreateUserAuth(user, "alice1");
            user = userRep.CreateUserAuth();
            user.UserName = "bob";
            userRep.CreateUserAuth(user, "bob2");
            container.Register<IUserAuthRepository>(userRep);
        }

        private static X509Certificate2 GenerateCertificate()
        {
            string certName = new Uri(ServiceStackShared.BaseAddress).DnsSafeHost;

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
