// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.WireContract;
using Json.Schema;
using Xunit;
using ContractDescription = InfoCarrier.WireContract.WireContract;

namespace InfoCarrier.Core.TransportTests;

public class ContractSchemaTest
{
    [Fact]
    public async Task Current_metadata_round_trips_through_standard_schema_without_losing_coverage()
    {
        ContractDescription original = await Inspect();
        JsonObject schema = ContractSchema.Write(original);
        ContractDescription restored = ContractSchema.Read(schema);
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(original), JsonSerializer.SerializeToNode(restored)));
        Assert.Equal(original.Types.Count + original.Frames.Count, schema["$defs"]!.AsObject().Count);
        Assert.Null(schema["Contract"]);
        Assert.DoesNotContain(schema["$defs"]!.AsObject(), d => d.Value?[ContractSchema.Extension]?["children"] is not null);
    }

    [Fact]
    public async Task Standard_validator_accepts_catalogue_as_draft_2020_12_with_custom_annotations()
    {
        JsonObject catalogue = ContractSchema.Write(await Inspect());
        JsonElement schemaJson = JsonSerializer.SerializeToElement(catalogue);
        Assert.True(MetaSchemas.Draft202012.Evaluate(schemaJson).IsValid);
        Assert.NotNull(JsonSchema.Build(schemaJson, new BuildOptions { Dialect = Dialect.Draft202012 }));
    }

    [Theory]
    [InlineData("{\"serializedQuery\":\"AA==\",\"trackingBehavior\":0,\"isAsync\":false,\"returnsSingleResult\":false}", true)]
    [InlineData("{\"serializedQuery\":\"AA==\",\"trackingBehavior\":\"zero\",\"isAsync\":false,\"returnsSingleResult\":false}", false)]
    public async Task Standard_properties_and_shared_references_validate_real_request_shapes(string instance, bool valid)
    {
        JsonObject catalogue = ContractSchema.Write(await Inspect());
        string key = catalogue["$defs"]!.AsObject().Select(d => d.Key)
            .Single(k => k.EndsWith("/InfoCarrier.Core.Common.QueryDataRequest", StringComparison.Ordinal));
        Assert.Equal(valid, Evaluate(catalogue, key, instance));
    }

    [Theory]
    [InlineData("[0,3,97,false,null]", true)]
    [InlineData("[0,3,97,false]", false)]
    [InlineData("[0,3,97,false,null,123]", false)]
    [InlineData("[9,3,97,false,null]", false)]
    public async Task Standard_tuple_keywords_enforce_frame_tag_types_and_length(string instance, bool valid)
        => Assert.Equal(valid, Evaluate(ContractSchema.Write(await Inspect()), "frame:QueryStartFrame/scalar", instance));

    [Fact]
    public async Task Named_integer_positions_remain_covered_beyond_standard_validation()
    {
        var original = await Inspect();
        var schema = ContractSchema.Write(original);
        var positions = schema["$defs"]!["frame:QueryStartFrame/scalar"]!["prefixItems"]!;
        positions[1]![ContractSchema.Extension]!["position"] = "Slot";
        positions[2]![ContractSchema.Extension]!["position"] = "Version";
        Assert.True(Evaluate(schema, "frame:QueryStartFrame/scalar", "[0,3,97,false,null]"));
        var result = ContractComparer.Compare(original, ContractSchema.Read(schema), "protected-revision");
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path == "frames/QueryStartFrame/scalar" && c.Kind == ChangeKind.Incompatible);
    }

    [Fact]
    public async Task Historical_format_one_and_temporary_inputs_remain_readable_without_rebuilding_source()
    {
        var original = await Inspect();
        var historical = new JsonObject { ["FormatVersion"] = 1, ["Contract"] = JsonSerializer.SerializeToNode(original) };
        Assert.True(ContractComparer.Compare(ContractSchema.Read(historical), ContractSchema.Read(ContractSchema.Write(original)), "old-revision").Passed);
        Assert.True(ContractComparer.Compare(ContractSchema.Read(JsonSerializer.SerializeToNode(original)!), original, "old-temporary").Passed);
    }

    [Theory]
    [InlineData("constraint")]
    [InlineData("missing-metadata")]
    [InlineData("external-reference")]
    [InlineData("dangling-reference")]
    public async Task Unsupported_schema_edits_fail_as_incomplete_even_after_a_version_increase(string edit)
    {
        var original = await Inspect();
        var schema = ContractSchema.Write(original with { Version = 4, AllocatedVersions = [3, 4] });
        var definitions = schema["$defs"]!.AsObject();
        string key = definitions.Select(d => d.Key).First(k => k.EndsWith("/System.Int32", StringComparison.Ordinal));
        switch (edit)
        {
            case "constraint": definitions[key]!["minimum"] = 5; break;
            case "missing-metadata": definitions[key]!.AsObject().Remove(ContractSchema.Extension); break;
            default:
                var field = definitions.First(d => d.Value?["properties"] is JsonObject p && p.Count != 0).Value!["properties"]!.AsObject().First();
                field.Value!["$ref"] = edit == "dangling-reference"
                    ? "#/$defs/type%3Amissing" : "https://example.invalid/unknown.json";
                break;
        }
        var error = Assert.Throws<InvalidOperationException>(() => ContractSchema.Read(schema));
        Assert.Contains("Incomplete schema analysis", error.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Strict_polymorphic_base_or_derived_reader_needs_explicit_schema_coverage(bool changeBase)
    {
        var contract = await Inspect();
        string baseKey = contract.Types.Keys.Single(k => k.EndsWith("/InfoCarrier.Core.Expressions.ExpressionNode", StringComparison.Ordinal));
        string changed = changeBase ? baseKey : contract.Types[baseKey].Mappings.First(p => p.Key.StartsWith("kind:", StringComparison.Ordinal)).Value;
        contract.Types[changed] = contract.Types[changed] with { SkipUnknown = false };

        var error = Assert.Throws<InvalidOperationException>(() => ContractSchema.Write(contract));

        Assert.Contains("Polymorphic readers that forbid additional fields", error.Message);
    }

    [Fact]
    public async Task Real_protocol_field_type_change_needs_a_fresh_version_after_schema_round_trip()
    {
        var original = await Inspect();
        var schema = ContractSchema.Write(original);
        string request = schema["$defs"]!.AsObject().Select(p => p.Key)
            .Single(k => k.EndsWith("/InfoCarrier.Core.Common.QueryDataRequest", StringComparison.Ordinal));
        JsonNode properties = schema["$defs"]![request]!["properties"]!;
        properties["protocolVersion"]!["$ref"] = properties["transactionId"]!["$ref"]!.DeepClone();

        var result = ContractComparer.Compare(original, ContractSchema.Read(schema), "protected-target");

        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path.EndsWith("/protocolVersion", StringComparison.Ordinal)
            && c.Kind == ChangeKind.Incompatible);
        schema[ContractSchema.Extension]!["protocolVersion"] = 4;
        schema[ContractSchema.Extension]!["allocatedVersions"] = new JsonArray(3, 4);
        Assert.True(ContractComparer.Compare(original, ContractSchema.Read(schema), "protected-target").Passed);
    }

    [Fact]
    public async Task Named_floating_point_literals_and_numeric_enum_assignments_are_preserved()
    {
        var schema = ContractSchema.Write(await Inspect());
        var definitions = schema["$defs"]!.AsObject();
        string number = definitions.Select(d => d.Key).Single(k => k.StartsWith("type:InfoCarrier.Core.Expressions.", StringComparison.Ordinal)
            && k.EndsWith("/System.Double", StringComparison.Ordinal));
        Assert.True(Evaluate(schema, number, "\"NaN\""));
        Assert.True(Evaluate(schema, number, "1.5"));
        Assert.False(Evaluate(schema, number, "\"unrelated\""));
        string operation = definitions.Select(d => d.Key).Single(k => k.EndsWith("/InfoCarrier.Core.Common.InfoCarrierOperation", StringComparison.Ordinal));
        Assert.Equal("integer", definitions[operation]!["type"]!.GetValue<string>());
        Assert.NotEmpty(definitions[operation]![ContractSchema.Extension]!["enumAssignments"]!.AsObject());
    }

    [Fact]
    public async Task Standard_variant_constraints_validate_actual_source_generated_expression_payloads()
    {
        var catalogue = ContractSchema.Write(await Inspect());
        string key = catalogue["$defs"]!.AsObject().Select(d => d.Key)
            .Single(k => k.EndsWith("/InfoCarrier.Core.Expressions.ExpressionNode", StringComparison.Ordinal));
        ExpressionNode expression = new ConstantNode { Type = new TypeNode { Name = "System.Int32" }, PrimitiveValue = 7 };
        string encoded = JsonSerializer.Serialize(expression, ExpressionJsonContext.Default.ExpressionNode);
        Assert.True(Evaluate(catalogue, key, encoded));
        var unknown = JsonNode.Parse(encoded)!;
        unknown["$kind"] = 99999;
        Assert.False(Evaluate(catalogue, key, unknown.ToJsonString()));
    }

    private static async Task<ContractDescription> Inspect()
        => ContractInspector.Inspect([InfoCarrierEnvelope.CurrentProtocolVersion], await FrameContracts.InspectAsync());

    private static bool Evaluate(JsonObject catalogue, string definition, string instance)
    {
        JsonObject selected = (JsonObject)catalogue.DeepClone();
        selected["$ref"] = "#/$defs/" + Uri.EscapeDataString(definition.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal));
        var schema = JsonSchema.Build(JsonSerializer.SerializeToElement(selected), new BuildOptions { Dialect = Dialect.Draft202012 });
        using var document = JsonDocument.Parse(instance);
        return schema.Evaluate(document.RootElement).IsValid;
    }
}
