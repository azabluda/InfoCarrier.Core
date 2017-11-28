// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;

    public static class WebApiShared
    {
        public static string BaseAddress { get; } =
            Environment.GetEnvironmentVariable(@"WebApi__DefaultBaseAddress")
                ?? @"http://localhost:4809";
    }
}