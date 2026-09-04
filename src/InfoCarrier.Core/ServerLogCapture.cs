// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Collects the log events raised while one server request executes, so the server can send
///     them back with its result (#97, R172).
/// </summary>
/// <remarks>
///     <para>
///         <b>An <see cref="ILoggerProvider" /> and an ambient sink, because EF gives no other
///         hook.</b> A log event is written through the application's
///         <see cref="ILoggerFactory" />, which the server does not own and must not replace. So
///         this adds one more provider beside whatever the application already has: every event
///         still goes wherever it went before, and a copy lands in the sink when one is open.
///     </para>
///     <para>
///         <b>The sink is <see cref="AsyncLocal{T}" /> and that is the whole of the request
///         scoping.</b> A server serves many clients at once, so "the events of this request" has
///         to follow the request's own async flow rather than a field. Outside a
///         <see cref="Begin" /> scope the loggers collect nothing at all, which is what makes the
///         provider safe to leave registered on a server that has not granted forwarding.
///     </para>
/// </remarks>
public sealed class ServerLogCapture : ILoggerProvider
{
    private static readonly AsyncLocal<Sink?> Current = new();

    /// <summary>
    ///     Opens a sink for the current async flow and returns the scope that closes it.
    /// </summary>
    /// <param name="minimumLevel">The lowest level to keep; anything below is dropped here.</param>
    /// <returns>The scope to dispose once the request has run.</returns>
    public static Scope Begin(LogLevel minimumLevel)
        => new(minimumLevel);

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
        => new CaptureLogger(categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    ///     An open capture scope. Disposing it restores whatever was in scope before.
    /// </summary>
    public sealed class Scope : IDisposable
    {
        private readonly Sink? _outer;
        private readonly Sink _sink;

        internal Scope(LogLevel minimumLevel)
        {
            _sink = new Sink(minimumLevel);
            _outer = Current.Value;
            Current.Value = _sink;
        }

        /// <summary>
        ///     What the request raised, or <see langword="null" /> when it raised nothing worth
        ///     keeping.
        /// </summary>
        /// <remarks>
        ///     Null rather than an empty list, so the ordinary request carries no such field on
        ///     the wire at all.
        /// </remarks>
        public IReadOnlyList<ServerLogEvent>? Events
            => _sink.Events.Count == 0 ? null : _sink.Events;

        /// <inheritdoc />
        public void Dispose()
            => Current.Value = _outer;
    }

    private sealed class Sink(LogLevel minimumLevel)
    {
        public LogLevel MinimumLevel { get; } = minimumLevel;

        public List<ServerLogEvent> Events { get; } = [];
    }

    /// <summary>
    ///     Categories that never cross, because each side owns one of its own.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A model event belongs to the side that built the model, and a context event to
    ///         the side that built the context.</b> Both halves of this provider build a model
    ///         from the same <c>OnModelCreating</c> and both validate it, so the server's
    ///         <c>OptionalDependentWithoutIdentifyingPropertyWarning</c> is not news to a client
    ///         that raised its own — it is the same finding twice, attributed to the wrong place.
    ///         The same goes for <c>SensitiveDataLoggingEnabledWarning</c>, which says something
    ///         about a context and each side has its own answer.
    ///     </para>
    ///     <para>
    ///         What is left after the exclusion is what the <em>store</em> did, which is the half
    ///         a client genuinely cannot see. That is the whole point of forwarding.
    ///     </para>
    ///     <para>
    ///         Prefix-matched, so <c>Microsoft.EntityFrameworkCore.Model</c> covers
    ///         <c>…Model.Validation</c> too. Found by measurement (R172): with these included, the
    ///         server's model warnings landed in the client's log and
    ///         <c>Warn_when_save_optional_dependent_with_null_values_sensitive</c>, which asserts
    ///         one warning, saw three.
    ///     </para>
    /// </remarks>
    private static readonly string[] NotForwarded =
    [
        Microsoft.EntityFrameworkCore.DbLoggerCategory.Model.Name,
        Microsoft.EntityFrameworkCore.DbLoggerCategory.Infrastructure.Name,
    ];

    private sealed class CaptureLogger(string category) : ILogger
    {
        private readonly bool _forwarded = !NotForwarded.Any(
            excluded => category.StartsWith(excluded, StringComparison.Ordinal));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => _forwarded && Current.Value is { } sink && logLevel >= sink.MinimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            // `IsEnabled` above is advisory: EF's own `IDiagnosticsLogger` decides enablement from
            // EF's options and its own definitions, not from a provider's answer, so the sink is
            // re-checked at the point of writing rather than trusted to have been asked.
            if (!_forwarded || Current.Value is not { } sink || logLevel < sink.MinimumLevel)
            {
                return;
            }

            sink.Events.Add(
                new ServerLogEvent
                {
                    Level = (int)logLevel,
                    EventId = eventId.Id,
                    EventName = eventId.Name,
                    Category = category,
                    Message = formatter(state, exception),
                });
        }
    }
}
