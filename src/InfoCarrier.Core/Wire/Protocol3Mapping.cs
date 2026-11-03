// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Old = InfoCarrier.Core.Wire.V3.Expressions;
using OldCommon = InfoCarrier.Core.Wire.V3.Common;

namespace InfoCarrier.Core.Wire;

/// <summary>Explicit protocol 3 conversions. No runtime mapping configuration or inferred type grants.</summary>
internal static class Protocol3Mapping
{
    [return: NotNullIfNotNull(nameof(n))]
    internal static TypeNode? ToLive(Old.TypeNode? n) => n is null ? null : new()
    {
        Name = n.Name, EntityTypeName = n.EntityTypeName, ArrayRank = n.ArrayRank,
        ArrayElement = ToLive(n.ArrayElement), ShapeMembers = n.ShapeMembers?.ToArray(),
        GenericArguments = n.GenericArguments.Select(x => ToLive(x)).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static Old.TypeNode? ToOld(TypeNode? n) => n is null ? null : new()
    {
        Name = n.Name, EntityTypeName = n.EntityTypeName, ArrayRank = n.ArrayRank,
        ArrayElement = ToOld(n.ArrayElement), ShapeMembers = n.ShapeMembers?.ToArray(),
        GenericArguments = n.GenericArguments.Select(x => ToOld(x)).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static MethodNode? ToLive(Old.MethodNode? n) => n is null ? null : new()
    {
        Name = n.Name, DeclaringType = ToLive(n.DeclaringType), ReturnType = ToLive(n.ReturnType),
        GenericArguments = n.GenericArguments.Select(x => ToLive(x)).ToArray(),
        ParameterTypes = n.ParameterTypes.Select(x => ToLive(x)).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static Old.MethodNode? ToOld(MethodNode? n) => n is null ? null : new()
    {
        Name = n.Name, DeclaringType = ToOld(n.DeclaringType), ReturnType = ToOld(n.ReturnType),
        GenericArguments = n.GenericArguments.Select(x => ToOld(x)).ToArray(),
        ParameterTypes = n.ParameterTypes.Select(x => ToOld(x)).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static DynamicValueNode? ToLive(Old.DynamicValueNode? n) => n is null ? null : new()
    {
        Type = ToLive(n.Type), Id = n.Id, Ref = n.Ref, IsNull = n.IsNull, IsTracked = n.IsTracked,
        PrimitiveValue = Primitive(n.PrimitiveValue), TypeValue = ToLive(n.TypeValue),
        Items = n.Items?.Select(x => ToLive(x)).ToArray(),
        EntityKey = n.EntityKey is null ? null : new EntityKeyNode
        { EntityTypeName = n.EntityKey.EntityTypeName, KeyValues = n.EntityKey.KeyValues.Select(Primitive).ToArray() },
        Properties = n.Properties.Select(x => new DynamicPropertyValue
        {
            Name = x.Name, PrimitiveValue = Primitive(x.PrimitiveValue), Value = ToLive(x.Value),
            IsLoadedNavigation = x.IsLoadedNavigation, IsJoinRows = x.IsJoinRows,
        }).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static Old.DynamicValueNode? ToOld(DynamicValueNode? n) => n is null ? null : new()
    {
        Type = ToOld(n.Type), Id = n.Id, Ref = n.Ref, IsNull = n.IsNull, IsTracked = n.IsTracked,
        PrimitiveValue = Primitive(n.PrimitiveValue), TypeValue = ToOld(n.TypeValue),
        Items = n.Items?.Select(x => ToOld(x)).ToArray(),
        EntityKey = n.EntityKey is null ? null : new Old.EntityKeyNode
        { EntityTypeName = n.EntityKey.EntityTypeName, KeyValues = n.EntityKey.KeyValues.Select(Primitive).ToArray() },
        Properties = n.Properties.Select(x => new Old.DynamicPropertyValue
        {
            Name = x.Name, PrimitiveValue = Primitive(x.PrimitiveValue), Value = ToOld(x.Value),
            IsLoadedNavigation = x.IsLoadedNavigation, IsJoinRows = x.IsJoinRows,
        }).ToArray(),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static ExpressionNode? ToLive(Old.ExpressionNode? n) => n switch
    {
        null => null,
        Old.ConstantNode x => new ConstantNode { Type = ToLive(x.Type), ValueType = ToLive(x.ValueType), PrimitiveValue = Primitive(x.PrimitiveValue), DynamicValue = ToLive(x.DynamicValue) },
        Old.ParameterNode x => new ParameterNode { Type = ToLive(x.Type), Id = x.Id, Name = x.Name },
        Old.MemberNode x => new MemberNode { Type = ToLive(x.Type), DeclaringType = ToLive(x.DeclaringType), MemberName = x.MemberName, MemberKind = ToLive(x.MemberKind), Instance = ToLive(x.Instance) },
        Old.MethodCallNode x => new MethodCallNode { Type = ToLive(x.Type), Method = ToLive(x.Method), Instance = ToLive(x.Instance), Arguments = x.Arguments.Select(a => ToLive(a)).ToArray() },
        Old.LambdaNode x => new LambdaNode { Type = ToLive(x.Type), ReturnType = ToLive(x.ReturnType), Body = ToLive(x.Body), Parameters = x.Parameters.Select(a => (ParameterNode)ToLive(a)).ToArray() },
        Old.NewNode x => new NewNode { Type = ToLive(x.Type), Arguments = x.Arguments.Select(a => ToLive(a)).ToArray(), ConstructorParameterTypes = x.ConstructorParameterTypes.Select(a => ToLive(a)).ToArray(), Members = x.Members?.ToArray() },
        Old.NewArrayNode x => new NewArrayNode { Type = ToLive(x.Type), Expressions = x.Expressions.Select(a => ToLive(a)).ToArray() },
        Old.BinaryNode x => new BinaryNode { Type = ToLive(x.Type), Operator = x.Operator, Left = ToLive(x.Left), Right = ToLive(x.Right), Method = ToLive(x.Method), IsLiftedToNull = x.IsLiftedToNull },
        Old.UnaryNode x => new UnaryNode { Type = ToLive(x.Type), Operator = x.Operator, Operand = ToLive(x.Operand), Method = ToLive(x.Method) },
        Old.TypeBinaryNode x => new TypeBinaryNode { Type = ToLive(x.Type), Operator = x.Operator, Operand = ToLive(x.Operand), TypeOperand = ToLive(x.TypeOperand) },
        Old.ConditionalNode x => new ConditionalNode { Type = ToLive(x.Type), Test = ToLive(x.Test), IfTrue = ToLive(x.IfTrue), IfFalse = ToLive(x.IfFalse) },
        Old.MemberInitNode x => new MemberInitNode { Type = ToLive(x.Type), NewExpression = (NewNode)ToLive(x.NewExpression), Bindings = x.Bindings.Select(a => new MemberBindingNode { DeclaringType = ToLive(a.DeclaringType), MemberName = a.MemberName, MemberKind = ToLive(a.MemberKind), Value = ToLive(a.Value) }).ToArray() },
        Old.ListInitNode x => new ListInitNode { Type = ToLive(x.Type), NewExpression = (NewNode)ToLive(x.NewExpression), Initializers = x.Initializers.Select(a => new ElementInitNode { AddMethod = ToLive(a.AddMethod), Arguments = a.Arguments.Select(b => ToLive(b)).ToArray() }).ToArray() },
        Old.InvocationNode x => new InvocationNode { Type = ToLive(x.Type), Expression = ToLive(x.Expression), Arguments = x.Arguments.Select(a => ToLive(a)).ToArray() },
        Old.FromSqlQueryRootStubNode x => new FromSqlQueryRootStubNode { Type = ToLive(x.Type), ElementType = ToLive(x.ElementType), Sql = x.Sql, IsAdHoc = x.IsAdHoc, Arguments = ToLive(x.Arguments) },
        Old.SqlQueryRootStubNode x => new SqlQueryRootStubNode { Type = ToLive(x.Type), ElementType = ToLive(x.ElementType), Sql = x.Sql, Arguments = ToLive(x.Arguments) },
        Old.QueryRootStubNode x when x.GetType() == typeof(Old.QueryRootStubNode) => new QueryRootStubNode { Type = ToLive(x.Type), ElementType = ToLive(x.ElementType) },
        Old.ServerContextStubNode x => new ServerContextStubNode { Type = ToLive(x.Type) },
        _ => throw new NotSupportedException("Protocol 3 expression variant has no live mapping."),
    };

    [return: NotNullIfNotNull(nameof(n))]
    internal static Old.ExpressionNode? ToOld(ExpressionNode? n) => n switch
    {
        null => null,
        ConstantNode x => new Old.ConstantNode { Type = ToOld(x.Type), ValueType = ToOld(x.ValueType), PrimitiveValue = Primitive(x.PrimitiveValue), DynamicValue = ToOld(x.DynamicValue) },
        ParameterNode x => new Old.ParameterNode { Type = ToOld(x.Type), Id = x.Id, Name = x.Name },
        MemberNode x => new Old.MemberNode { Type = ToOld(x.Type), DeclaringType = ToOld(x.DeclaringType), MemberName = x.MemberName, MemberKind = ToOld(x.MemberKind), Instance = ToOld(x.Instance) },
        MethodCallNode x => new Old.MethodCallNode { Type = ToOld(x.Type), Method = ToOld(x.Method), Instance = ToOld(x.Instance), Arguments = x.Arguments.Select(a => ToOld(a)).ToArray() },
        LambdaNode x => new Old.LambdaNode { Type = ToOld(x.Type), ReturnType = ToOld(x.ReturnType), Body = ToOld(x.Body), Parameters = x.Parameters.Select(a => (Old.ParameterNode)ToOld(a)).ToArray() },
        NewNode x => new Old.NewNode { Type = ToOld(x.Type), Arguments = x.Arguments.Select(a => ToOld(a)).ToArray(), ConstructorParameterTypes = x.ConstructorParameterTypes.Select(a => ToOld(a)).ToArray(), Members = x.Members?.ToArray() },
        NewArrayNode x => new Old.NewArrayNode { Type = ToOld(x.Type), Expressions = x.Expressions.Select(a => ToOld(a)).ToArray() },
        BinaryNode x => new Old.BinaryNode { Type = ToOld(x.Type), Operator = x.Operator, Left = ToOld(x.Left), Right = ToOld(x.Right), Method = ToOld(x.Method), IsLiftedToNull = x.IsLiftedToNull },
        UnaryNode x => new Old.UnaryNode { Type = ToOld(x.Type), Operator = x.Operator, Operand = ToOld(x.Operand), Method = ToOld(x.Method) },
        TypeBinaryNode x => new Old.TypeBinaryNode { Type = ToOld(x.Type), Operator = x.Operator, Operand = ToOld(x.Operand), TypeOperand = ToOld(x.TypeOperand) },
        ConditionalNode x => new Old.ConditionalNode { Type = ToOld(x.Type), Test = ToOld(x.Test), IfTrue = ToOld(x.IfTrue), IfFalse = ToOld(x.IfFalse) },
        MemberInitNode x => new Old.MemberInitNode { Type = ToOld(x.Type), NewExpression = (Old.NewNode)ToOld(x.NewExpression), Bindings = x.Bindings.Select(a => new Old.MemberBindingNode { DeclaringType = ToOld(a.DeclaringType), MemberName = a.MemberName, MemberKind = ToOld(a.MemberKind), Value = ToOld(a.Value) }).ToArray() },
        ListInitNode x => new Old.ListInitNode { Type = ToOld(x.Type), NewExpression = (Old.NewNode)ToOld(x.NewExpression), Initializers = x.Initializers.Select(a => new Old.ElementInitNode { AddMethod = ToOld(a.AddMethod), Arguments = a.Arguments.Select(b => ToOld(b)).ToArray() }).ToArray() },
        InvocationNode x => new Old.InvocationNode { Type = ToOld(x.Type), Expression = ToOld(x.Expression), Arguments = x.Arguments.Select(a => ToOld(a)).ToArray() },
        FromSqlQueryRootStubNode x => new Old.FromSqlQueryRootStubNode { Type = ToOld(x.Type), ElementType = ToOld(x.ElementType), Sql = x.Sql, IsAdHoc = x.IsAdHoc, Arguments = ToOld(x.Arguments) },
        SqlQueryRootStubNode x => new Old.SqlQueryRootStubNode { Type = ToOld(x.Type), ElementType = ToOld(x.ElementType), Sql = x.Sql, Arguments = ToOld(x.Arguments) },
        QueryRootStubNode x when x.GetType() == typeof(QueryRootStubNode) => new Old.QueryRootStubNode { Type = ToOld(x.Type), ElementType = ToOld(x.ElementType) },
        ServerContextStubNode x => new Old.ServerContextStubNode { Type = ToOld(x.Type) },
        _ => throw new NotSupportedException("Live expression variant cannot be represented in protocol 3."),
    };

    internal static QueryDataRequest ToLive(OldCommon.QueryDataRequest x, InfoCarrierPayloadLimits? limits = null)
    {
        (limits ?? InfoCarrierPayloadLimits.Default).GuardRequest(x.SerializedQuery.Length, "protocol 3 expression");
        return new()
        {
            ProtocolVersion = x.ProtocolVersion,
            Query = ToLive(JsonSerializer.Deserialize(x.SerializedQuery, V3.Expressions.Protocol3ExpressionJsonContext.Default.ExpressionNode)
                ?? throw new JsonException("Protocol 3 query is null.")),
            TrackingBehavior = x.TrackingBehavior switch
            {
                OldCommon.QueryTrackingBehavior.TrackAll => QueryTrackingBehavior.TrackAll,
                OldCommon.QueryTrackingBehavior.NoTracking => QueryTrackingBehavior.NoTracking,
                OldCommon.QueryTrackingBehavior.NoTrackingWithIdentityResolution => QueryTrackingBehavior.NoTrackingWithIdentityResolution,
                _ => throw new JsonException("Unknown protocol 3 tracking behavior."),
            },
            SplitQueryBehavior = x.SplitQueryBehavior switch
            {
                null => null, OldCommon.QuerySplittingBehavior.SingleQuery => QuerySplittingBehavior.SingleQuery,
                OldCommon.QuerySplittingBehavior.SplitQuery => QuerySplittingBehavior.SplitQuery,
                _ => throw new JsonException("Unknown protocol 3 splitting behavior."),
            },
            IsAsync = x.IsAsync, ReturnsSingleResult = x.ReturnsSingleResult, TransactionId = x.TransactionId,
        };
    }

    internal static OldCommon.QueryDataRequest ToOld(QueryDataRequest x) => new()
    {
        ProtocolVersion = 3,
        SerializedQuery = JsonSerializer.SerializeToUtf8Bytes(ToOld(x.Query), V3.Expressions.Protocol3ExpressionJsonContext.Default.ExpressionNode),
        TrackingBehavior = x.TrackingBehavior switch
        {
            QueryTrackingBehavior.TrackAll => OldCommon.QueryTrackingBehavior.TrackAll,
            QueryTrackingBehavior.NoTracking => OldCommon.QueryTrackingBehavior.NoTracking,
            QueryTrackingBehavior.NoTrackingWithIdentityResolution => OldCommon.QueryTrackingBehavior.NoTrackingWithIdentityResolution,
            _ => throw new JsonException("Tracking behavior has no protocol 3 mapping."),
        },
        SplitQueryBehavior = x.SplitQueryBehavior switch
        {
            null => null, QuerySplittingBehavior.SingleQuery => OldCommon.QuerySplittingBehavior.SingleQuery,
            QuerySplittingBehavior.SplitQuery => OldCommon.QuerySplittingBehavior.SplitQuery,
            _ => throw new JsonException("Splitting behavior has no protocol 3 mapping."),
        },
        IsAsync = x.IsAsync, ReturnsSingleResult = x.ReturnsSingleResult, TransactionId = x.TransactionId,
    };

    internal static QueryDataResult ToLive(OldCommon.QueryDataResult x) => new()
    {
        Rows = (JsonSerializer.Deserialize(x.SerializedResults, V3.Expressions.Protocol3ExpressionJsonContext.Default.ListDynamicValueNode)
            ?? throw new JsonException("Protocol 3 rows are null.")).Select(n => ToLive(n)).ToArray(),
        IsEntityResult = x.IsEntityResult, ElementTypeName = x.ElementTypeName,
        ServerLog = x.ServerLog?.Select(ToLive).ToArray(),
    };

    internal static OldCommon.QueryDataResult ToOld(QueryDataResult x) => new()
    {
        SerializedResults = JsonSerializer.SerializeToUtf8Bytes(x.Rows.Select(n => ToOld(n)).ToList(), V3.Expressions.Protocol3ExpressionJsonContext.Default.ListDynamicValueNode),
        IsEntityResult = x.IsEntityResult, ElementTypeName = x.ElementTypeName,
        ServerLog = x.ServerLog?.Select(ToOld).ToArray(),
    };

    internal static ServerLogEvent ToLive(OldCommon.ServerLogEvent x) => new() { Level = x.Level, EventId = x.EventId, EventName = x.EventName, Category = x.Category, Message = x.Message };
    internal static OldCommon.ServerLogEvent ToOld(ServerLogEvent x) => new() { Level = x.Level, EventId = x.EventId, EventName = x.EventName, Category = x.Category, Message = x.Message };

    [return: NotNullIfNotNull(nameof(x))]
    internal static InfoCarrierFault? ToLive(OldCommon.InfoCarrierFault? x) => x is null ? null : new() { TypeName = x.TypeName, Message = x.Message, StackTrace = x.StackTrace, Inner = ToLive(x.Inner), FailedCorrelationIds = x.FailedCorrelationIds?.ToArray() };
    [return: NotNullIfNotNull(nameof(x))]
    internal static OldCommon.InfoCarrierFault? ToOld(InfoCarrierFault? x) => x is null ? null : new() { TypeName = x.TypeName, Message = x.Message, StackTrace = x.StackTrace, Inner = ToOld(x.Inner), FailedCorrelationIds = x.FailedCorrelationIds?.ToArray() };

    internal static byte[] ToLiveValue(byte[] bytes)
    {
        return JsonSerializer.SerializeToUtf8Bytes(ToLive(JsonSerializer.Deserialize(bytes, V3.Expressions.Protocol3ExpressionJsonContext.Default.DynamicValueNode)), ExpressionJsonContext.Default.DynamicValueNode);
    }
    internal static byte[] ToOldValue(byte[] bytes) => JsonSerializer.SerializeToUtf8Bytes(ToOld(JsonSerializer.Deserialize(bytes, ExpressionJsonContext.Default.DynamicValueNode)), V3.Expressions.Protocol3ExpressionJsonContext.Default.DynamicValueNode);

    internal static SaveChangesRequest ToLive(OldCommon.SaveChangesRequest x) => new()
    {
        TransactionId = x.TransactionId, Entries = x.Entries.Select(n => new ChangeEntry
        {
            CorrelationId = n.CorrelationId, EntityTypeName = n.EntityTypeName, ClrTypeName = n.ClrTypeName, State = n.State,
            SerializedValues = ToLiveValue(n.SerializedValues), SerializedOriginalValues = n.SerializedOriginalValues is null ? null : ToLiveValue(n.SerializedOriginalValues),
            TemporaryProperties = n.TemporaryProperties?.ToArray(), ModifiedProperties = n.ModifiedProperties?.ToArray(), ModifiedComplexProperties = n.ModifiedComplexProperties?.ToArray(), SentinelProperties = n.SentinelProperties?.ToArray(),
        }).ToArray(),
    };
    internal static OldCommon.SaveChangesRequest ToOld(SaveChangesRequest x) => new()
    {
        TransactionId = x.TransactionId, Entries = x.Entries.Select(n => new OldCommon.ChangeEntry
        {
            CorrelationId = n.CorrelationId, EntityTypeName = n.EntityTypeName, ClrTypeName = n.ClrTypeName, State = n.State,
            SerializedValues = ToOldValue(n.SerializedValues), SerializedOriginalValues = n.SerializedOriginalValues is null ? null : ToOldValue(n.SerializedOriginalValues),
            TemporaryProperties = n.TemporaryProperties?.ToArray(), ModifiedProperties = n.ModifiedProperties?.ToArray(), ModifiedComplexProperties = n.ModifiedComplexProperties?.ToArray(), SentinelProperties = n.SentinelProperties?.ToArray(),
        }).ToArray(),
    };
    internal static SaveChangesResult ToLive(OldCommon.SaveChangesResult x) => new() { Count = x.Count, ServerLog = x.ServerLog?.Select(ToLive).ToArray(), GeneratedValues = x.GeneratedValues.Select(n => new GeneratedValues { CorrelationId = n.CorrelationId, SerializedValues = ToLiveValue(n.SerializedValues) }).ToArray() };
    internal static OldCommon.SaveChangesResult ToOld(SaveChangesResult x) => new() { Count = x.Count, ServerLog = x.ServerLog?.Select(ToOld).ToArray(), GeneratedValues = x.GeneratedValues.Select(n => new OldCommon.GeneratedValues { CorrelationId = n.CorrelationId, SerializedValues = ToOldValue(n.SerializedValues) }).ToArray() };

    private static MemberKind ToLive(Old.MemberKind kind) => kind switch { Old.MemberKind.Property => MemberKind.Property, Old.MemberKind.Field => MemberKind.Field, _ => throw new JsonException("Unknown protocol 3 member kind.") };
    private static Old.MemberKind ToOld(MemberKind kind) => kind switch { MemberKind.Property => Old.MemberKind.Property, MemberKind.Field => Old.MemberKind.Field, _ => throw new JsonException("Member kind has no protocol 3 mapping.") };
    private static object? Primitive(object? value) => value switch { JsonElement element => element.Clone(), byte[] bytes => bytes.ToArray(), _ => value };
}
