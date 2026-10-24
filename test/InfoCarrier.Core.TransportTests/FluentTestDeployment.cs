// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.TransportTests;

internal static class FluentTestDeployment
{
    internal static void Configure(InfoCarrierDeploymentBuilder builder)
        => Configure(builder, allowSql: true);

    internal static void Configure(InfoCarrierDeploymentBuilder builder, bool allowSql)
    {
        ConfigureTypes(builder);
        builder.AllowArbitrarySqlExecution(allowSql);
    }

    private static void ConfigureTypes(InfoCarrierDeploymentBuilder builder)
        => builder.AddAllowedTypes(typeof(TimeOnly))
            .AddAnonymousShapes(new { Id = 0 });
}
