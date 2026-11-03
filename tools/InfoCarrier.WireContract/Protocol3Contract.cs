// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Nodes;

namespace InfoCarrier.WireContract;

/// <summary>Checks historical effective metadata independently of the live protocol integer.</summary>
internal static class Protocol3Contract
{
    internal static void Verify(WireContract current, string descriptionPath)
    {
        WireContract previous = ContractSchema.Read(JsonNode.Parse(File.ReadAllText(descriptionPath))
            ?? throw new InvalidOperationException("Missing original protocol 3 description."));
        ComparisonResult result = ContractComparer.Compare(previous, Project(previous, current), descriptionPath);
        if (!result.Passed)
        {
            throw new InvalidOperationException("Protocol 3 changed independently of the live version: "
                + string.Join("; ", result.Changes.Where(c => c.Kind != ChangeKind.Compatible).Select(c => c.Path + ": " + c.Reason)));
        }
    }

    internal static WireContract Project(WireContract previous, WireContract current)
    {
        var types = current.Types.Where(p => p.Key.StartsWith("InfoCarrier.Core.Wire.V3.", StringComparison.Ordinal))
            .ToDictionary(p => Name(p.Key), p => p.Value with
            {
                Properties = p.Value.Properties.ToDictionary(q => q.Key, q => q.Value with { Type = Name(q.Value.Type) }),
                Mappings = p.Value.Mappings.ToDictionary(q => q.Key, q => Name(q.Value)),
                Children = p.Value.Children.Select(Name).ToArray(),
            });
        var frames = current.Frames.Where(p => p.Key.StartsWith("V3/", StringComparison.Ordinal))
            .ToDictionary(p => p.Key[3..], p => p.Value.Select(Name).ToArray());
        // Only ownership names changed. Inventory is checked by the full inspector; this
        // comparison isolates the original effective shapes and actual frame-codec probes.
        return previous with { Types = types, Frames = frames };
    }

    private static string Name(string value) => value
        .Replace("InfoCarrier.Core.Wire.V3.Expressions.Protocol3ExpressionJsonContext", "InfoCarrier.Core.Expressions.ExpressionJsonContext", StringComparison.Ordinal)
        .Replace("InfoCarrier.Core.Wire.V3.Protocol3JsonContext", "InfoCarrier.Core.InfoCarrierJsonContext", StringComparison.Ordinal)
        .Replace("InfoCarrier.Core.Wire.V3.Common.QueryTrackingBehavior", "Microsoft.EntityFrameworkCore.QueryTrackingBehavior", StringComparison.Ordinal)
        .Replace("InfoCarrier.Core.Wire.V3.Common.QuerySplittingBehavior", "Microsoft.EntityFrameworkCore.QuerySplittingBehavior", StringComparison.Ordinal)
        .Replace("InfoCarrier.Core.Wire.V3.Expressions.", "InfoCarrier.Core.Expressions.", StringComparison.Ordinal)
        .Replace("InfoCarrier.Core.Wire.V3.Common.", "InfoCarrier.Core.Common.", StringComparison.Ordinal);
}
