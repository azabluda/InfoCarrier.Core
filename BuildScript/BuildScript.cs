// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace BuildScript
{
    using FlubuCore.Context;
    using FlubuCore.Scripting;

    public class BuildScript : DefaultBuildScript
    {
        protected override void ConfigureBuildProperties(IBuildPropertiesContext context)
        {
        }

        protected override void ConfigureTargets(ITaskContext context)
        {
            context.CreateTarget("clean")
                .AddCoreTask(x => x.Clean())
                .AddTask(x => x.DeleteDirectoryTask("artifacts", false));

            context.CreateTarget("build")
                .AddCoreTask(x => x.Build());

            context.CreateTarget("pack")
                .AddCoreTask(x => x.Pack().WithArguments(
                    @"src\InfoCarrier.Core\InfoCarrier.Core.csproj",
                    @"--output",
                    @"artifacts",
                    @"--configuration",
                    @"Debug",
                    @"--include-symbols"));

            context.CreateTarget("test")
                .AddCoreTask(x => x.Test().WithArguments(
                    @"test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj"));

            context.CreateTarget("coverage")
                .AddCoreTask(x => x.Test().WithArguments(
                    @"test\InfoCarrier.Core.FunctionalTests\InfoCarrier.Core.FunctionalTests.csproj",
                    @"/p:CollectCoverage=true",
                    @"/p:CoverletOutputFormat=opencover",
                    @"/p:CoverletOutput=..\..\TestResults\coverage.xml",
                    @"/p:ExcludeByFile=""**\GitVersionInformation_InfoCarrier.Core*.cs""",
                    @"/p:Include=""[InfoCarrier.Core]*"""))
                .AddCoreTask(x => x.ExecuteDotnetTask("reportgenerator").WithArguments(
                    @"-reports:TestResults\coverage*.xml",
                    @"-targetdir:TestResults\coverage"))
                .AddTask(x => x.ExecutePowerShellScript("start").WithArguments(
                    string.Empty,
                    @"TestResults\coverage\index.htm"));
        }
    }
}
