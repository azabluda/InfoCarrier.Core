// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Text;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.CosmosTests;

/// <summary>Captures actual provider reads for the optional direct/wire comparison, without replacing fixture loggers.</summary>
internal sealed class CosmosQueryCapture : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly IDisposable _listeners;
    private readonly Action<CosmosReadItemEventData>? _pointRead;

    // Observer mode inspects input identifiers without adding comparison records in concurrent tests.
    public CosmosQueryCapture(Action<CosmosReadItemEventData>? pointRead = null)
    {
        _pointRead = pointRead;
        _listeners = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "Microsoft.EntityFrameworkCore")
            lock (_subscriptions) _subscriptions.Add(listener.Subscribe(this, name =>
                name == CosmosEventId.ExecutingSqlQuery.Name || name == CosmosEventId.ExecutingReadItem.Name));
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (_pointRead is { } observe)
        {
            if (value.Value is CosmosReadItemEventData item) observe(item);
            return;
        }
        if (CurrentTest.Value is not { } test) return;
        switch (value.Value)
        {
            case CosmosQueryEventData query:
                test.RecordProviderRead($"-- container: {query.ContainerId}\n-- partition: {query.PartitionKeyValue}\n"
                    + CanonicalQuery(query.QuerySql));
                break;
            case CosmosReadItemEventData item:
                test.RecordProviderRead($"-- container: {item.ContainerId}\n-- partition: {item.PartitionKeyValue}\n"
                    + "ReadItem()");
                break;
        }
    }

    /// <summary>Renames parameters only outside quoted text. JSON names, literals, and query structure remain intact.</summary>
    internal static string CanonicalQuery(string query)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new StringBuilder();
        char quote = '\0';
        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];
            if (quote != '\0')
            {
                result.Append(c);
                if (c == '\\' && i + 1 < query.Length) result.Append(query[++i]);
                else if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                result.Append(c);
            }
            else if (c == '@')
            {
                int start = i++;
                while (i < query.Length && (char.IsLetterOrDigit(query[i]) || query[i] == '_')) i++;
                string name = query[start..i--];
                if (!names.TryGetValue(name, out string? canonical)) names.Add(name, canonical = $"@p{names.Count}");
                result.Append(canonical);
            }
            else if (char.IsWhiteSpace(c))
            {
                if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
            }
            else result.Append(c);
        }
        return result.ToString().Trim();
    }

    public void OnCompleted() { }
    public void OnError(Exception error) { }
    public void Dispose()
    {
        _listeners.Dispose();
        lock (_subscriptions)
        {
            foreach (var subscription in _subscriptions) subscription.Dispose();
            _subscriptions.Clear();
        }
    }
}
