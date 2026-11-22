// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using InfoCarrier.Core.Wire;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class Protocol3ExpressionTest
{
    [Fact]
    public void Historical_mapped_instance_function_binds_the_server_context_instead_of_the_client()
    {
        using var client = new FunctionContext(10);
        using var server = new FunctionContext(20);
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver(allowlist: TypeAllowlist.ForModel(server.Model));
        var values = new DynamicValueMapper(server.Model, mapper, resolver);
        Expression call = Expression.Call(new ServerContextExpression(typeof(FunctionContext), client),
            typeof(FunctionContext).GetMethod(nameof(FunctionContext.AddMarker))!, Expression.Constant(7));
        var request = new QueryDataRequest
        {
            Query = new ExpressionToNodeTranslator(mapper, values).Translate(call),
            TrackingBehavior = QueryTrackingBehavior.NoTracking, IsAsync = false, ReturnsSingleResult = true,
        };
        var protocols = new InfoCarrierWireProtocol([new CurrentWireProtocolCodec(), new Protocol3WireProtocolCodec()]);
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, protocols);
        QueryDataRequest restored = protocols.Deserialize<QueryDataRequest>(serializer,
            protocols.Serialize(serializer, request, 3), 3)!;
        Assert.IsType<ServerContextStubNode>(Assert.IsType<MethodCallNode>(restored.Query).Instance);
        var reverse = new NodeToExpressionTranslator(resolver, values,
            (_, _) => throw new InvalidOperationException("This function has no query root."))
        {
            ServerContextFactory = declared => Expression.Constant(server, declared),
        };

        Assert.Equal(27, Expression.Lambda<Func<int>>(reverse.Translate(restored.Query)).Compile()());
        Assert.Equal(17, client.AddMarker(7));
    }

    [Fact]
    public void Nullable_arithmetic_and_checked_overflow_keep_historical_meanings()
    {
        Func<int?, int?, int?> add = RoundTrip<Func<int?, int?, int?>>((left, right) => left + right);
        Assert.Null(add(null, 7));
        Assert.Null(add(7, null));
        Assert.Equal(12, add(5, 7));
        Func<int, int> increment = RoundTrip<Func<int, int>>(value => checked(value + 1));
        Assert.Equal(2, increment(1));
        Assert.Throws<OverflowException>(() => increment(int.MaxValue));
    }

    [Fact]
    public void Conditions_calls_arithmetic_and_type_tests_keep_their_meaning()
    {
        Func<object, int, int> read = RoundTrip<Func<object, int, int>>((value, amount) =>
            value is string ? ((string)value).Length + Math.Abs(amount) : -amount);
        Assert.Equal(7, read("abc", -4));
        Assert.Equal(-4, read(17, 4));
    }

    [Fact]
    public void Member_and_list_initializers_keep_fields_properties_and_elements()
    {
        Func<int, Projection> create = RoundTrip<Func<int, Projection>>(value => new Projection
        {
            Field = value + 1, Name = value.ToString(), Values = new List<int> { value, -value },
        });
        Projection projection = create(7);
        Assert.Equal(8, projection.Field);
        Assert.Equal("7", projection.Name);
        Assert.Equal(new[] { 7, -7 }, projection.Values);

        Func<Projection, int> read = RoundTrip<Func<Projection, int>>(value => value.Field + value.Name.Length);
        Assert.Equal(9, read(projection));
    }

    [Fact]
    public void Array_construction_and_generic_method_descriptors_keep_values()
    {
        Func<int, int[]> create = RoundTrip<Func<int, int[]>>(value => new[] { value, value + 1 });
        Assert.Equal(new[] { 3, 4 }, create(3));
        Func<int[], int[]> reverse = RoundTrip<Func<int[], int[]>>(values => values.Reverse().ToArray());
        Assert.Equal(new[] { 4, 3 }, reverse(create(3)));
    }

    [Fact]
    public void Invocation_keeps_parameter_binding()
    {
        Expression<Func<int, int>> inner = value => value * 2;
        ParameterExpression argument = Expression.Parameter(typeof(int), "argument");
        Expression<Func<int, int>> expression = Expression.Lambda<Func<int, int>>(
            Expression.Invoke(inner, Expression.Add(argument, Expression.Constant(1))), argument);
        Assert.Equal(10, RoundTrip(expression)(4));
    }

    private static TDelegate RoundTrip<TDelegate>(Expression<TDelegate> expression) where TDelegate : Delegate
    {
        var mapper = new TypeNodeMapper();
        var resolver = new TypeNodeResolver(allowlist: TypeAllowlist.ForModel(null, [typeof(Projection)]));
        var values = new DynamicValueMapper(null, mapper, resolver);
        var request = new QueryDataRequest
        {
            Query = new ExpressionToNodeTranslator(mapper, values).Translate(expression),
            TrackingBehavior = QueryTrackingBehavior.NoTracking, IsAsync = false, ReturnsSingleResult = true,
        };
        var protocols = new InfoCarrierWireProtocol([new CurrentWireProtocolCodec(), new Protocol3WireProtocolCodec()]);
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, protocols);
        QueryDataRequest restored = protocols.Deserialize<QueryDataRequest>(serializer,
            protocols.Serialize(serializer, request, 3), 3)!;
        var reverse = new NodeToExpressionTranslator(resolver, values,
            (_, _) => throw new InvalidOperationException("These pure expressions have no query roots."));
        return ((Expression<TDelegate>)reverse.Translate(restored.Query)).Compile();
    }

    public sealed class Projection
    {
        public int Field;
        public string Name { get; set; } = string.Empty;
        public List<int> Values { get; set; } = [];
    }

    public sealed class FunctionContext(int marker) : DbContext(
        new DbContextOptionsBuilder<FunctionContext>().UseSqlite("Data Source=:memory:").Options)
    {
        public int AddMarker(int value) => marker + value;
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.HasDbFunction(typeof(FunctionContext).GetMethod(nameof(AddMarker))!);
    }
}
