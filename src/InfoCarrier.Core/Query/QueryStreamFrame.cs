// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     One item in a streamed query response.
/// </summary>
public abstract record QueryStreamFrame;

/// <summary>
///     Starts one result slot and declares its materialization metadata.
/// </summary>
public sealed record QueryStartFrame : QueryStreamFrame
{
    /// <summary>
    ///     Creates a start frame.
    /// </summary>
    public QueryStartFrame(int version, int slot, bool isEntityResult, string? elementTypeName)
    {
        Version = version;
        Slot = slot;
        IsEntityResult = isEntityResult;
        ElementTypeName = elementTypeName;
    }

    /// <summary>
    ///     Gets the query stream protocol version.
    /// </summary>
    public int Version { get; }

    /// <summary>
    ///     Gets the result slot position.
    /// </summary>
    public int Slot { get; }

    /// <summary>
    ///     Gets whether each row is an entity result.
    /// </summary>
    public bool IsEntityResult { get; }

    /// <summary>
    ///     Gets the diagnostic element type name.
    /// </summary>
    public string? ElementTypeName { get; }
}

/// <summary>
///     Carries one independently decodable result row.
/// </summary>
public sealed record QueryRowFrame : QueryStreamFrame
{
    /// <summary>
    ///     Creates a row frame.
    /// </summary>
    public QueryRowFrame(DynamicValueNode value)
        => Value = value;

    /// <summary>
    ///     Gets the mapped row value.
    /// </summary>
    public DynamicValueNode Value { get; }
}

/// <summary>
///     Carries one forwarded server log event.
/// </summary>
public sealed record QueryLogFrame : QueryStreamFrame
{
    /// <summary>
    ///     Creates a log frame.
    /// </summary>
    public QueryLogFrame(ServerLogEvent @event)
        => Event = @event;

    /// <summary>
    ///     Gets the forwarded event.
    /// </summary>
    public ServerLogEvent Event { get; }
}

/// <summary>
///     Marks successful query completion.
/// </summary>
public sealed record QueryCompleteFrame : QueryStreamFrame;

/// <summary>
///     Terminates a query with a server failure.
/// </summary>
public sealed record QueryFaultFrame : QueryStreamFrame
{
    /// <summary>
    ///     Creates a fault frame.
    /// </summary>
    public QueryFaultFrame(InfoCarrierFault fault)
        => Fault = fault;

    /// <summary>
    ///     Gets the transported failure.
    /// </summary>
    public InfoCarrierFault Fault { get; }
}
