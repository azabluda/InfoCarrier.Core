// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace WcfSample
{
    using System;

    public static class WcfShared
    {
        public static string UriString =>
            Environment.GetEnvironmentVariable(@"Wcf__DefaultUri")
            ?? @"http://localhost:8080/MyRemoteService";
    }
}