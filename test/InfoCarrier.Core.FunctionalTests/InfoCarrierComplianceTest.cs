// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System.Reflection;
    using Microsoft.EntityFrameworkCore;

    public class InfoCarrierComplianceTest : ComplianceTestBase
    {
        protected override Assembly TargetAssembly { get; } = typeof(InfoCarrierComplianceTest).Assembly;
    }
}
