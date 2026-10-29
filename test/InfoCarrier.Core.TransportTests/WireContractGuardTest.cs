// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using InfoCarrier.Core.Common;
using InfoCarrier.WireContract;
using Xunit;
using ContractDescription = InfoCarrier.WireContract.WireContract;

namespace InfoCarrier.Core.TransportTests;

public class WireContractGuardTest
{
    [Fact]
    public async Task Default_routes_have_nonempty_coverage_and_all_frame_kinds()
    {
        var frames = await FrameContracts.InspectAsync();
        var description = ContractInspector.Inspect([InfoCarrierEnvelope.CurrentProtocolVersion], frames);
        Assert.Equal(InfoCarrierEnvelope.CurrentProtocolVersion, description.Version);
        Assert.Contains(description.Types, p => p.Key.EndsWith("/InfoCarrier.Core.Expressions.TypeNode", StringComparison.Ordinal));
        Assert.Contains(description.Types, p => p.Value.Mappings.Keys.Any(k => k.StartsWith("kind:", StringComparison.Ordinal)));
        Assert.Contains(description.Types, p => p.Value.Mappings.Keys.Any(k => k.StartsWith("enum:", StringComparison.Ordinal)));
        Assert.Equal(["Tag:0", "Version:int32", "Slot:int32", "IsEntityResult:boolean", "ElementTypeName:string?"],
            frames["QueryStartFrame/scalar"]);
        Assert.Equal(frames["QueryStartFrame/scalar"], frames["QueryStartFrame/entity"]);
        Assert.Equal(["Tag:3"], frames["QueryCompleteFrame"]);
        Assert.StartsWith("Payload:", frames["QueryRowFrame"][1]);
        Assert.StartsWith("Payload:", frames["QueryLogFrame"][1]);
        Assert.StartsWith("Payload:", frames["QueryFaultFrame"][1]);
        Assert.True(Compare(description, description).Passed);
    }

    [Fact]
    public void Optional_addition_passes_and_both_actual_readers_accept_it()
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var after = Describe(GuardJsonContext.Default.OptionalMessage);
        ComparisonResult result = Compare(before, after);
        Assert.True(result.Passed, Details(result));
        Assert.Contains(result.Changes, c => c.Direction == WireDirection.Request && c.Kind == ChangeKind.Compatible);
        Assert.Contains(result.Changes, c => c.Direction == WireDirection.Response && c.Kind == ChangeKind.Compatible);
        Assert.Null(JsonSerializer.Deserialize("{\"value\":7}", GuardJsonContext.Default.OptionalMessage)!.Label);
        Assert.Equal(7, JsonSerializer.Deserialize("{\"value\":7,\"label\":\"new\"}", GuardJsonContext.Default.OriginalMessage)!.Value);
    }

    [Fact]
    public void Required_input_addition_fails_and_the_reader_rejects_the_old_message()
    {
        ComparisonResult result = Compare(Describe(GuardJsonContext.Default.OriginalMessage),
            Describe(GuardJsonContext.Default.RequiredMessage));
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Direction == WireDirection.Request && c.Kind == ChangeKind.Incompatible);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"value\":7}", GuardJsonContext.Default.RequiredMessage));
    }

    [Fact]
    public void Integer_to_string_fails_in_both_directions_without_a_version_increase()
    {
        ComparisonResult result = Compare(Describe(GuardJsonContext.Default.OriginalMessage),
            Describe(GuardJsonContext.Default.StringMessage));
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path == "message/value" && c.Kind == ChangeKind.Incompatible);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"value\":7}", GuardJsonContext.Default.StringMessage));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"value\":\"seven\"}", GuardJsonContext.Default.OriginalMessage));
    }

    [Fact]
    public void Constructor_requiredness_uses_effective_metadata()
    {
        var context = new GuardJsonContext(new JsonSerializerOptions
        {
            RespectRequiredConstructorParameters = true,
        });
        TypeContract info = ContractInspector.InspectType(context.ConstructorMessage, WireDirection.Request, t => t.ToString());
        Assert.True(info.Properties["Value"].Required);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{}", context.ConstructorMessage));
    }

    [Fact]
    public void Response_addition_checks_the_older_readers_unknown_property_rule()
    {
        var context = new GuardJsonContext(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        });
        var before = Describe(context.OriginalMessage);
        ComparisonResult result = Compare(before, Describe(GuardJsonContext.Default.OptionalMessage));
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path == "message/label" && c.Direction == WireDirection.Response
            && c.Kind == ChangeKind.Incompatible);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{\"value\":7,\"label\":\"new\"}", context.OriginalMessage));
    }

    [Fact]
    public void Semantic_only_increase_needs_no_fabricated_structural_change()
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        ComparisonResult result = Compare(before, before with { Version = 4, AllocatedVersions = [1, 2, 3, 4] });
        Assert.True(result.Passed, Details(result));
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void A_fresh_version_acknowledges_a_known_break_but_not_incomplete_analysis()
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var after = Describe(GuardJsonContext.Default.StringMessage) with { Version = 4, AllocatedVersions = [1, 2, 3, 4] };
        Assert.True(Compare(before, after).Passed);
        after.Types["message"].Settings["caseInsensitive"] = "True";
        Assert.False(Compare(before, after).Passed);
        Assert.Contains(Compare(before, after).Changes, c => c.Kind == ChangeKind.Unclassified);
    }

    [Theory]
    [InlineData(2, new[] { 1, 2 })]
    [InlineData(3, new[] { 1, 3 })]
    [InlineData(4, new[] { 1, 2, 3, 3, 4 })]
    [InlineData(4, new[] { 1, 2, 3 })]
    public void Allocation_history_cannot_be_rewritten_or_rolled_back(int version, int[] allocations)
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var result = Compare(before, before with { Version = version, AllocatedVersions = allocations });
        Assert.False(result.Passed);
        Assert.NotEmpty(result.AllocationErrors);
    }

    [Fact]
    public void Retiring_or_reverting_a_contract_does_not_free_its_integer()
    {
        var original = Describe(GuardJsonContext.Default.OriginalMessage);
        var allocated = original with { Version = 4, AllocatedVersions = [1, 2, 3, 4] };
        Assert.False(Compare(allocated, original).Passed);
        Assert.True(Compare(allocated, original with { Version = 5, AllocatedVersions = [1, 2, 3, 4, 5] }).Passed);
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("length")]
    [InlineData("position")]
    public void Frame_shape_changes_fail_with_the_same_version(string change)
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var after = Describe(GuardJsonContext.Default.OriginalMessage);
        after.Frames["start"] = change switch
        {
            "tag" => ["Tag:9", "Version:int32", "Slot:int32"],
            "length" => ["Tag:0", "Version:int32"],
            _ => ["Tag:0", "Slot:int32", "Version:int32"],
        };
        var result = Compare(before, after);
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path == "frames/start" && c.Kind == ChangeKind.Incompatible);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void New_variants_are_classified_in_the_correct_direction(int value, bool pass)
    {
        var direction = (WireDirection)value;
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var after = Describe(GuardJsonContext.Default.OriginalMessage);
        before.Types["message"] = before.Types["message"] with { Direction = direction };
        after.Types["message"] = after.Types["message"] with { Direction = direction };
        after.Types["message"].Mappings["kind:18"] = "message";
        Assert.Equal(pass, Compare(before, after).Passed);
    }

    [Fact]
    public void Reassigned_discriminator_and_removed_fields_are_detected()
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        var after = Describe(GuardJsonContext.Default.OriginalMessage);
        before.Types["message"].Mappings["kind:0"] = "first";
        after.Types["message"].Mappings["kind:0"] = "second";
        after.Types["message"].Properties.Clear();
        var result = Compare(before, after);
        Assert.False(result.Passed);
        Assert.Contains(result.Changes, c => c.Path == "message/kind:0" && c.Kind == ChangeKind.Incompatible);
        Assert.Contains(result.Changes, c => c.Path == "message/value" && c.Kind == ChangeKind.Incompatible);
    }

    [Fact]
    public void Empty_inventory_or_missing_baseline_identity_cannot_pass()
    {
        var before = Describe(GuardJsonContext.Default.OriginalMessage);
        Assert.False(Compare(before, before with { Types = [] }).Passed);
        Assert.False(Compare(before with { Frames = [] }, before).Passed);
        Assert.False(ContractComparer.Compare(before, before, "").Passed);
    }

    private static ContractDescription Describe(JsonTypeInfo metadata)
    {
        string Key(Type type) => type == metadata.Type ? "message" : type.ToString();
        var types = new Dictionary<string, TypeContract>
        {
            ["message"] = ContractInspector.InspectType(metadata, WireDirection.Request | WireDirection.Response, Key),
        };
        foreach (JsonTypeInfo scalar in new JsonTypeInfo[] { GuardJsonContext.Default.Int32, GuardJsonContext.Default.String })
        {
            types.Add(Key(scalar.Type), ContractInspector.InspectType(scalar, WireDirection.Request | WireDirection.Response, Key));
        }
        return new(3, [1, 2, 3], types, new() { ["start"] = ["Tag:0", "Version:int32", "Slot:int32"] },
            new() { ["authored"] = "test message" });
    }

    private static ComparisonResult Compare(ContractDescription before, ContractDescription after)
        => ContractComparer.Compare(before, after, "authored-reference");

    private static string Details(ComparisonResult result) => JsonSerializer.Serialize(result);
}

internal sealed class OriginalMessage { public int Value { get; set; } }
internal sealed class OptionalMessage { public int Value { get; set; } public string? Label { get; set; } }
internal sealed class RequiredMessage { public int Value { get; set; } public required string Label { get; set; } }
internal sealed class StringMessage { public string? Value { get; set; } }
internal sealed record ConstructorMessage(int Value);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OriginalMessage))]
[JsonSerializable(typeof(OptionalMessage))]
[JsonSerializable(typeof(RequiredMessage))]
[JsonSerializable(typeof(StringMessage))]
[JsonSerializable(typeof(ConstructorMessage))]
internal partial class GuardJsonContext : JsonSerializerContext;
