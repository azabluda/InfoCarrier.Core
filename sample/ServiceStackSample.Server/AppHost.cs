// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using ServiceStack;
    using ServiceStack.Auth;
    using ServiceStack.Caching;

    public class AppHost : AppSelfHostBase
    {
        public AppHost()
            : base("InfoCarrierSample", typeof(DataService).Assembly)
        {
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
    }
}