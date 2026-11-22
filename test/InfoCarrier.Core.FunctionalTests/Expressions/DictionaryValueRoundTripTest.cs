// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

public class DictionaryValueRoundTripTest
{
    [Theory]
    [InlineData(false), InlineData(true)]
    public void Dictionary_interfaces_keep_nullable_values(bool readOnly)
    {
        var value = new SortedDictionary<string, long?> { ["second"] = null, ["first"] = 17 };
        IEnumerable<KeyValuePair<string, long?>> pairs = readOnly
            ? RoundTrip<IReadOnlyDictionary<string, long?>>(value)
            : RoundTrip<IDictionary<string, long?>>(value);
        var result = pairs.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(new[] { "first", "second" }, result.Keys.Order());
        Assert.Equal(17, result["first"]);
        Assert.Null(result["second"]);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void Nested_dictionary_interfaces_preserve_values_and_shared_references(bool readOnly)
    {
        IDictionary<string, long?> shared = new SortedDictionary<string, long?> { ["number"] = 23, ["missing"] = null };
        var value = new Dictionary<string, IDictionary<string, long?>> { ["left"] = shared, ["right"] = shared };
        IEnumerable<KeyValuePair<string, IDictionary<string, long?>>> pairs = readOnly
            ? RoundTrip<IReadOnlyDictionary<string, IDictionary<string, long?>>>(value)
            : RoundTrip<IDictionary<string, IDictionary<string, long?>>>(value);
        var result = pairs.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(new[] { "left", "right" }, result.Keys.Order());
        Assert.Equal(23, result["left"]["number"]);
        Assert.Null(result["right"]["missing"]);
        Assert.Same(result["left"], result["right"]);
    }

    private static T RoundTrip<T>(T value)
    {
        var types = TypeAllowlist.ForModel(null).With([
            typeof(T), typeof(IDictionary<string, long?>), typeof(SortedDictionary<string, long?>),
            typeof(KeyValuePair<string, long?>), typeof(KeyValuePair<string, IDictionary<string, long?>>)]);
        var writer = new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver(allowlist: types));
        DynamicValueNode node = writer.ToDynamicValue(value, typeof(T));
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        byte[] payload = serializer.Serialize(node);
        var reader = new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver(allowlist: types));
        return Assert.IsAssignableFrom<T>(reader.FromDynamicValue(serializer.Deserialize<DynamicValueNode>(payload)!));
    }
}
