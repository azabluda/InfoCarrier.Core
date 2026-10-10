// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core;

/// <summary>Safe local failure details for client query and save diagnostic events.</summary>
/// <remarks>Contains no exception object, context, query, entity, or request values.</remarks>
public sealed class ClientFailureEventData : EventData
{
    internal ClientFailureEventData(EventDefinitionBase definition,
        Func<EventDefinitionBase, EventData, string> messageGenerator,
        string operation, string phase, string outcome, string exceptionType)
        : base(definition, messageGenerator)
    {
        Operation = operation;
        Phase = phase;
        Outcome = outcome;
        ExceptionType = exceptionType;
    }

    /// <summary>The bounded local operation name.</summary>
    public string Operation { get; }

    /// <summary>The phase observed by this client.</summary>
    public string Phase { get; }

    /// <summary>The bounded outcome classification.</summary>
    public string Outcome { get; }

    /// <summary>The runtime exception type name, without its contents.</summary>
    public string ExceptionType { get; }
}
