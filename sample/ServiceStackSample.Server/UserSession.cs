// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.Data.Common;
    using ServiceStack;

    public class UserSession : AuthUserSession
    {
        public DbTransaction DbTransaction { get; set; }
    }
}
