// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;

namespace Northwind.Shared;

public static class NorthwindDeployment
{
    public static TBuilder ConfigureInfoCarrier<TBuilder>(this TBuilder builder)
        where TBuilder : InfoCarrierDeploymentBuilder
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
}
