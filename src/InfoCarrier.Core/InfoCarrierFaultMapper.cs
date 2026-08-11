// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     Turns a server-side exception into an <see cref="InfoCarrierFault" /> and back
///     (wire-protocol W5).
/// </summary>
/// <remarks>
///     <para>
///         <b>The direction decides how much freedom this may take, and it is the same argument
///         as <see cref="InfoCarrierPayloadLimits" />'s.</b> A fault travels server → client, so
///         it is the answer a client got from the server it chose to talk to — not an
///         unauthenticated stranger's bytes. The strict default-deny that governs
///         <c>ResolveMethod</c> and <c>TypeAllowlist</c> is aimed the other way. So this resolves
///         an exception type by name, and bounds itself in three ways instead:
///     </para>
///     <list type="number">
///         <item>the type must already be loaded — no assembly is loaded to satisfy a payload;</item>
///         <item>it must derive from <see cref="Exception" />;</item>
///         <item>it is constructed only through an exception's ordinary
///               <c>(string)</c> / <c>(string, Exception)</c> constructor.</item>
///     </list>
///     <para>
///         Anything that fails those becomes an <see cref="InfoCarrierServerException" /> naming
///         the type it could not rebuild. **That fallback is a feature, not a gap**: a
///         store-specific exception — <c>SqliteException</c>, a SQL Server one — is a type the
///         *client* assembly has no reason to reference, and inventing a way to load it would
///         make every client depend on every backend. The type name survives in the message
///         either way.
///     </para>
/// </remarks>
public static class InfoCarrierFaultMapper
{
    /// <summary>
    ///     The <see cref="Exception.Data" /> key under which a rehydrated exception carries the
    ///     stack trace of the server that actually threw.
    /// </summary>
    public const string ServerStackTraceKey = "InfoCarrier.ServerStackTrace";

    private static readonly ConcurrentDictionary<string, Type?> ResolvedTypes = new(StringComparer.Ordinal);

    /// <summary>
    ///     Captures an exception, and its inner chain, as a wire fault.
    /// </summary>
    public static InfoCarrierFault Capture(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new InfoCarrierFault
        {
            // FullName, not AssemblyQualifiedName: assembly identity never crosses this wire
            // (research-findings §7), and a client and server on different patch versions of the
            // same library must still agree about what an `InvalidOperationException` is.
            TypeName = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            Inner = exception.InnerException is { } inner ? Capture(inner) : null,
        };
    }

    /// <summary>
    ///     Rebuilds the exception a fault describes, as faithfully as this process can.
    /// </summary>
    public static Exception Rehydrate(InfoCarrierFault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);

        Exception? inner = fault.Inner is { } innerFault ? Rehydrate(innerFault) : null;
        Exception exception = Construct(fault, inner);

        if (fault.StackTrace is { } serverStack)
        {
            // Not spliced into the exception's own stack, which belongs to where the client threw
            // it, and not appended to the message, which spec tests compare exactly.
            exception.Data[ServerStackTraceKey] = serverStack;
        }

        return exception;
    }

    private static Exception Construct(InfoCarrierFault fault, Exception? inner)
    {
        if (Resolve(fault.TypeName) is { } type)
        {
            // `(string message, Exception? innerException)` first, and **even when there is no
            // inner exception**. That ordering is not a preference; the obvious alternative is
            // wrong, and eight `GearsOfWarQuery` tests said so in one run.
            //
            // The one-string constructor of several BCL exceptions does not take a message. It
            // takes a *parameter name*: `ArgumentNullException(string paramName)`,
            // `ArgumentOutOfRangeException(string paramName)`,
            // `ObjectDisposedException(string objectName)`. Rebuilding through it nests the whole
            // message inside a new one —
            //
            //     expected: Value cannot be null. (Parameter 'value')
            //     actual:   Value cannot be null. (Parameter 'Value cannot be null. (Parameter 'value')')
            //
            // — and the two-argument form is unambiguous for every one of them, because
            // `(string, Exception)` can only mean message-then-inner.
            if (Invoke(type, [typeof(string), typeof(Exception)], [fault.Message, inner]) is { } withInner)
            {
                return withInner;
            }

            if (inner is null && Invoke(type, [typeof(string)], [fault.Message]) is { } withMessage)
            {
                return withMessage;
            }

            // A type the caller could not name anyway is worth trading for one it can. The runtime
            // type of a failure is often `internal` — `System.Text.Json` reports malformed JSON as
            // `JsonReaderException`, which is internal and derives from the public `JsonException` —
            // and above the fallback below is the only route such a failure had, so a caller who
            // wrote `catch (JsonException)` got an `InfoCarrierServerException` instead. C82 found it
            // through four `AdHocJsonQuery` tests asserting `ThrowsAny<JsonException>`.
            //
            // Only for a type that is not visible: a **public** type that merely has no usable
            // constructor keeps the fallback, because its name is something the caller can still act
            // on and its own base may mean something quite different — `SqliteException` would
            // otherwise become `ExternalException`, which says nothing about a store.
            //
            // And never `Exception` itself: degrading to that loses everything the fallback carries,
            // including which type the server actually threw.
            if (!type.IsVisible)
            {
                for (Type? candidate = type.BaseType;
                     candidate is not null && candidate != typeof(Exception);
                     candidate = candidate.BaseType)
                {
                    if (!candidate.IsVisible || candidate.IsAbstract)
                    {
                        continue;
                    }

                    if (Invoke(candidate, [typeof(string), typeof(Exception)], [fault.Message, inner]) is { } asBase)
                    {
                        return asBase;
                    }

                    if (inner is null && Invoke(candidate, [typeof(string)], [fault.Message]) is { } asBaseMessage)
                    {
                        return asBaseMessage;
                    }
                }
            }
        }

        // The chain is never dropped: a type that cannot carry an inner exception loses the type,
        // not the cause.
        return new InfoCarrierServerException(fault.TypeName, fault.Message, inner);
    }

    private static Exception? Invoke(Type type, Type[] signature, object?[] arguments)
    {
        try
        {
            return type.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance, binder: null, signature, modifiers: null)
                ?.Invoke(arguments) as Exception;
        }
        catch (TargetInvocationException)
        {
            // An exception type whose constructor validates its own arguments — rare, but a
            // rehydration that throws would replace the server's failure with a different one,
            // which is the one outcome this must never produce.
            return null;
        }
    }

    /// <summary>
    ///     Finds an already-loaded exception type by full name. Never loads an assembly, and
    ///     never returns anything that is not an <see cref="Exception" />.
    /// </summary>
    private static Type? Resolve(string typeName)
        => ResolvedTypes.GetOrAdd(typeName, static name =>
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(name, throwOnError: false) is { } candidate
                    && typeof(Exception).IsAssignableFrom(candidate)
                    && !candidate.IsAbstract)
                {
                    return candidate;
                }
            }

            return null;
        });
}
