// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace WcfSample
{
    using System;

    public static class WcfShared
    {
        public static string BaseUrl =>
            Environment.GetEnvironmentVariable(@"Wcf__DefaultBaseUri")
            ?? @"localhost:8080";

        public static string ServiceName => "MyRemoteService";

        public static long MaxReceivedMessageSize => 1024 * 1024;
    }
}