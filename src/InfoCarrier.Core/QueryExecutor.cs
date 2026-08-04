// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     Executes a captured query remotely: substitutes compiled-query parameters as plain
///     constants (research-findings §6), serializes the tree, ships it via
///     <see cref="IInfoCarrierClient" />, and materializes results (materialization lands in
///     Phase E). Nested in <see cref="InfoCarrierDatabase" />'s orbit (v1 QueryExecutor pattern).
/// </summary>
internal sealed class QueryExecutor<TElement>
{
    private static readonly System.Reflection.MethodInfo MaterializeMethod
        = typeof(ClientResultMaterializer).GetMethod(nameof(ClientResultMaterializer.Materialize))!;

    private static readonly System.Reflection.MethodInfo ToListMethod
        = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!;

    private static readonly System.Reflection.MethodInfo AsQueryableMethod
        = typeof(Queryable).GetMethods()
            .Single(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethodDefinition);

    private readonly QueryContext _queryContext;
    private readonly IInfoCarrierClient _client;
    private readonly IExpressionSerializer _expressionSerializer;
    private readonly SplitQuery _split;
    private readonly QueryTrackingBehavior _trackingBehavior;
    private readonly ClientResultMaterializer _materializer;
    private readonly bool _threadSafetyChecks;

    public QueryExecutor(
        QueryContext queryContext,
        Expression query,
        IInfoCarrierClient client,
        IExpressionSerializer expressionSerializer)
    {
        _queryContext = queryContext;
        _client = client;
        _expressionSerializer = expressionSerializer;

        // Substitute compiled-query parameters as plain constants (research-findings §6).
        Expression substituted = new SubstituteParametersExpressionVisitor(queryContext).Visit(query);

        // Then, and only then, decide what the server can execute: a surviving closure field
        // access names a compiler-generated display class and would push the boundary in for
        // no reason (ADR-010, docs/projection-split.md).
        _split = new QuerySplitter(queryContext.Context.Model).Split(substituted);

        _trackingBehavior = TrackingBehaviorFinder.Find(
            query, queryContext.Context.ChangeTracker.QueryTrackingBehavior);

        // `EnableThreadSafetyChecks(false)` is answered here, not by the detector.
        // `QueryContext.ConcurrencyDetector` is never null in EF 10 and always throws when
        // re-entered from a second operation, so a provider that calls it unconditionally ignores
        // the option outright — which is what every `ConcurrencyDetectorDisabledTestBase` method
        // caught (A59). EF's own providers read the same flag:
        // `InMemoryShapedQueryCompilingExpressionVisitor` and its relational counterpart both hold
        // `dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled` and emit the call only
        // when it is set.
        _threadSafetyChecks = queryContext.Context
            .GetInfrastructure()
            .GetRequiredService<ICoreSingletonOptions>()
            .AreThreadSafetyChecksEnabled;

        // A split query materializes every row the server sent, but only the entities the
        // residual yields belong in the change tracker (docs/projection-split.md §4). One
        // materializer for the whole execution, so identity is resolved across its parts.
        _materializer = new ClientResultMaterializer(
            queryContext.Context,
            expressionSerializer,
            _trackingBehavior,
            deferTracking: !_split.IsPassThrough && _trackingBehavior == QueryTrackingBehavior.TrackAll);
    }

    public object Execute(bool async, bool singleResult)
    {
        if (async)
        {
            // The critical section belongs *inside*: this method only builds the enumerable,
            // and a `using` here would release before a single row had been fetched. EF holds
            // it across each MoveNext for the same reason.
            IAsyncEnumerable<TElement> asyncEnum = ExecuteAsync(singleResult);
            return singleResult ? (object)FirstOrDefaultAsync(asyncEnum) : asyncEnum;
        }

        using IDisposable? criticalSection = CriticalSection();

        var results = new List<object?>(_split.ServerQueries.Count);
        foreach (ServerQuery serverQuery in _split.ServerQueries)
        {
            QueryDataResult result = _client
                .QueryDataAsync(BuildRequest(serverQuery, async: false), _queryContext.Context, _queryContext.CancellationToken)
                .GetAwaiter()
                .GetResult();
            results.Add(Materialize(serverQuery, result));
        }

        IEnumerable<TElement> mapped = Guarded(ApplyResidual(results, singleResult));
        return singleResult ? mapped.FirstOrDefault()! : mapped;
    }

    /// <summary>
    ///     Holds the concurrency critical section across each row the residual produces.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The round trip was already guarded; the residual was not, and the residual is
    ///         where a client-side projection actually runs. A second use of the same context
    ///         during one — which is exactly what <c>Throws_on_concurrent_query_*</c> stages —
    ///         went undetected, so this provider answered where every other one throws.
    ///     </para>
    ///     <para>
    ///         Per row rather than per query, as EF's own enumerators do: the section is held
    ///         while a row is being produced and released between rows, so a caller interleaving
    ///         two queries legitimately is not punished for it. The detector is re-entrant on one
    ///         thread, so this nests harmlessly inside the round-trip section above.
    ///     </para>
    /// </remarks>
    private IEnumerable<TElement> Guarded(IEnumerable<TElement> rows)
    {
        using IEnumerator<TElement> enumerator = rows.GetEnumerator();

        while (true)
        {
            bool moved;
            using (CriticalSection())
            {
                moved = enumerator.MoveNext();
            }

            if (!moved)
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    /// <summary>
    ///     The concurrency critical section, or <see langword="null" /> when the context has thread
    ///     safety checks turned off.
    /// </summary>
    private IDisposable? CriticalSection()
        => _threadSafetyChecks ? _queryContext.ConcurrencyDetector.EnterCriticalSection() : null;

    private async IAsyncEnumerable<TElement> ExecuteAsync(bool singleResult)
    {
        CancellationToken cancellationToken = _queryContext.CancellationToken;

        var results = new List<object?>(_split.ServerQueries.Count);
        foreach (ServerQuery serverQuery in _split.ServerQueries)
        {
            QueryDataResult result;
            try
            {
                using (CriticalSection())
                {
                    // Checked before the round-trip, as EF does in MoveNextAsync: an
                    // already-cancelled token must surface as OperationCanceledException rather
                    // than a completed query.
                    cancellationToken.ThrowIfCancellationRequested();
                    result = await _client
                        .QueryDataAsync(BuildRequest(serverQuery, async: true), _queryContext.Context, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                // Same reporting EF's own query enumerators do — callers (and the spec tests)
                // observe cancellation through the CoreEventId.QueryCanceled log event, not only
                // through the thrown exception.
                Type contextType = _queryContext.Context.GetType();
                if (_queryContext.ExceptionDetector.IsCancellation(exception, cancellationToken))
                {
                    _queryContext.QueryLogger.QueryCanceled(contextType);
                }
                else
                {
                    _queryContext.QueryLogger.QueryIterationFailed(contextType, exception);
                }

                throw;
            }

            results.Add(Materialize(serverQuery, result));
        }

        foreach (TElement element in Guarded(ApplyResidual(results, singleResult)))
        {
            yield return element;
        }
    }

    private QueryDataRequest BuildRequest(ServerQuery serverQuery, bool async)
    {
        // Start of a message exchange: wire reference ids restart at 1 on both sides. One scope
        // per round trip, so a split query's second request does not decode against the first's
        // reference table.
        ((Expressions.DynamicValueMapper)((ExpressionSerializer)_expressionSerializer).ValueMapper)
            .ResetReferenceScope();

        return new QueryDataRequest
        {
            SerializedQuery = SerializeNode(_expressionSerializer.ToNode(serverQuery.Query)),
            TrackingBehavior = _trackingBehavior,
            IsAsync = async,
            ReturnsSingleResult = serverQuery.ReturnsSingleResult,
            TransactionId = InfoCarrierDatabase.ServerTransactionId(_queryContext.Context),
        };
    }

    /// <summary>
    ///     Turns one server result into what the residual expects for that slot.
    /// </summary>
    /// <remarks>
    ///     The boundary element type is not <typeparamref name="TElement" /> whenever a residual
    ///     exists, so materialization goes through the runtime type. Rows are drained eagerly:
    ///     the wire reference scope belongs to one round trip, and a lazily materialized sequence
    ///     would decode against whichever scope the *next* request had reset.
    /// </remarks>
    private object? Materialize(ServerQuery serverQuery, QueryDataResult result)
    {
        ClientResultMaterializer materializer = _materializer;

        object rows = Invoke(MaterializeMethod.MakeGenericMethod(serverQuery.ElementType), materializer, [result])!;
        object list = Invoke(ToListMethod.MakeGenericMethod(serverQuery.ElementType), null, [rows])!;

        if (serverQuery.ReturnsSingleResult)
        {
            return ((System.Collections.IEnumerable)list).Cast<object?>().FirstOrDefault();
        }

        return Invoke(AsQueryableMethod.MakeGenericMethod(serverQuery.ElementType), null, [list])!;
    }

    /// <summary>
    ///     Calls a reflected method and lets what it threw out, rather than
    ///     <see cref="System.Reflection.TargetInvocationException" />.
    /// </summary>
    /// <remarks>
    ///     The element type of a query boundary is only known at run time, so materialization goes
    ///     through <see cref="System.Reflection.MethodBase.Invoke(object, object[])" /> — an
    ///     implementation detail of this provider, which has no business changing the exception a
    ///     caller sees. `Join_with_result_selector_returning_queryable_throws_validation_error`
    ///     asserts the <see cref="ArgumentException" /> EF raises for a projection returning
    ///     <see cref="IQueryable{T}" /> and got the wrapper instead.
    /// </remarks>
    private static object? Invoke(System.Reflection.MethodInfo method, object? target, object?[] arguments)
    {
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (System.Reflection.TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    private IEnumerable<TElement> ApplyResidual(IReadOnlyList<object?> results, bool singleResult)
    {
        if (_split.IsPassThrough)
        {
            // Nothing to apply. Keep the untouched path allocation-for-allocation what it was
            // before the split existed.
            return results[0] is IEnumerable<TElement> sequence ? sequence : [(TElement)results[0]!];
        }

        object? applied = _split.Apply(results);

        IEnumerable<TElement> produced = singleResult
            ? [(TElement)applied!]
            : ((System.Collections.IEnumerable)applied!).Cast<TElement>();

        return _materializer.Deferred.Count == 0 ? produced : Attaching(produced);
    }

    /// <summary>
    ///     Attaches the entities the residual actually yields, as they are yielded.
    /// </summary>
    /// <remarks>
    ///     The counterpart to deferred materialization (docs/projection-split.md §4). Walking each
    ///     element rather than the whole result keeps the sequence lazy, and anything the residual
    ///     filtered out simply stays detached — which is the point: a join that ships 919 rows to
    ///     answer a projection over 7 entities must track 7.
    /// </remarks>
    private IEnumerable<TElement> Attaching(IEnumerable<TElement> produced)
    {
        foreach (TElement element in produced)
        {
            // Reference equality, because this set exists to stop the walk revisiting an
            // *instance*, and an entity's own `Equals`/`GetHashCode` is not usable for that:
            // Northwind's `Customer.GetHashCode` throws on a null key, and
            // `GraphUpdatesTestBase.MyDiscriminator` throws from `GetHashCode` deliberately.
            // Harmless while the walk stopped at entities; once it descends into their
            // navigations it reaches every one of them.
            AttachEntitiesIn(element, new HashSet<object>(ReferenceEqualityComparer.Instance));
            yield return element;
        }
    }

    private void AttachEntitiesIn(object? value, HashSet<object> seen)
    {
        if (value is null || value is string || value.GetType().IsPrimitive)
        {
            return;
        }

        // A struct is a dead end unless it is one of the two the query pipeline uses to *hold*
        // results. Stopping here is what makes `seen` sufficient: a boxed struct is a new object
        // every time it is read, so reference identity can never catch a repeat, and a property
        // returning a fresh one — `DateTime.Date` returns a `DateTime` that has a `Date` — walks
        // forever. It overflowed the test host's stack, and the run reported *fewer* failures
        // for it, which is the shape `eng/measure.sh` guards the total against.
        if (value.GetType() is { IsValueType: true } valueType
            && !(valueType.IsGenericType
                && valueType.GetGenericTypeDefinition() is var definition
                && (definition == typeof(KeyValuePair<,>) || definition.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true)))
        {
            return;
        }

        if (!seen.Add(value))
        {
            return;
        }

        _materializer.AttachIfDeferred(value);

        if (value is System.Collections.IEnumerable sequence)
        {
            foreach (object? item in sequence)
            {
                AttachEntitiesIn(item, seen);
            }

            return;
        }

        // An entity is descended into through its *navigations*, which is how an `Include`d
        // dependent gets attached: the residual yields the principal, and the dependent exists
        // only inside it. Left out, `Set<QuizTask>().Include(e => e.Choices)` tracked the
        // `QuizTask` and left its `TaskChoice` detached, so the next query in the same context
        // built a second instance of a row it was already holding.
        //
        // The worry this replaces — that navigations lead back into rows the residual discarded —
        // does not survive the walk starting from what the residual *kept*: anything reachable
        // from a yielded entity travelled as part of that entity's graph. And nothing here
        // attaches on its own account; `AttachIfDeferred` ignores whatever this materializer did
        // not defer.
        //
        // Backing fields, never property getters: on a lazy-loading entity the getter *is* the
        // load, so walking properties would query the server for every navigation of every
        // result. `FindRuntimeEntityType` for the same reason it is used in the value mapper — a
        // proxy's CLR type is not in the model, and only this overload walks up to the type that
        // is.
        //
        // The descent goes only where the model says an entity is, never back through the
        // member walk below. Letting it fall through there overflowed the stack: a member walk
        // recurses on whatever a property returns, and a property returning a fresh value each
        // call — `DateTime.Date` is one — never repeats an instance for `seen` to catch. Entity
        // instances are finite and `seen` does catch those.
        if (_queryContext.Context.Model.FindRuntimeEntityType(value.GetType()) is { } entityType)
        {
            foreach (Microsoft.EntityFrameworkCore.Metadata.INavigationBase navigation
                     in entityType.GetNavigations().Concat<Microsoft.EntityFrameworkCore.Metadata.INavigationBase>(
                         entityType.GetSkipNavigations()))
            {
                if (navigation.FieldInfo?.GetValue(value) is not { } target)
                {
                    continue;
                }

                if (navigation.IsCollection)
                {
                    foreach (object? item in (System.Collections.IEnumerable)target)
                    {
                        AttachRelated(item, seen);
                    }
                }
                else
                {
                    AttachRelated(target, seen);
                }
            }

            return;
        }

        // A client-only projection type carries its entities in its members, so the walk has to
        // go through it.

        foreach (System.Reflection.PropertyInfo property in value.GetType()
                     .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0 && property.CanRead)
            {
                AttachEntitiesIn(property.GetValue(value), seen);
            }
        }
    }

    /// <summary>
    ///     Continues the walk into a navigation's target, but only if the model calls it an
    ///     entity.
    /// </summary>
    /// <remarks>
    ///     A navigation can also point at something this walk has no business descending into —
    ///     a shared-type join entity is a <see cref="Dictionary{TKey,TValue}" />, which is both
    ///     unrecognizable to <c>FindRuntimeEntityType</c> and enumerable, so the untyped walk
    ///     would take it apart pair by pair.
    /// </remarks>
    private void AttachRelated(object? candidate, HashSet<object> seen)
    {
        if (candidate is not null
            && _queryContext.Context.Model.FindRuntimeEntityType(candidate.GetType()) is not null)
        {
            AttachEntitiesIn(candidate, seen);
        }
    }

    /// <summary>
    ///     Finds the tracking behavior a query asks for, falling back to the context default.
    /// </summary>
    /// <remarks>
    ///     <c>AsNoTracking()</c> and friends are ordinary method calls that EF strips in
    ///     <c>QueryableMethodNormalizingExpressionVisitor</c>, which runs *inside* query
    ///     compilation — so at the ADR-006 capture point they are still in the tree and this is
    ///     the only place the per-query behavior can be read. Reading only
    ///     <c>ChangeTracker.QueryTrackingBehavior</c> saw the context-wide default and silently
    ///     ignored every per-query <c>AsNoTracking()</c>.
    ///
    ///     Outermost wins, matching EF: it visits the inner query first and assigns afterwards,
    ///     so the last assignment — the outermost call — is the one that survives.
    /// </remarks>
    private sealed class TrackingBehaviorFinder : ExpressionVisitor
    {
        private QueryTrackingBehavior _behavior;

        public static QueryTrackingBehavior Find(Expression query, QueryTrackingBehavior fallback)
        {
            var finder = new TrackingBehaviorFinder { _behavior = fallback };
            finder.Visit(query);
            return finder._behavior;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Post-order: visit the inner query, then apply this call's marker.
            Expression visited = base.VisitMethodCall(node);

            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Arguments.Count == 1)
            {
                _behavior = node.Method.Name switch
                {
                    nameof(EntityFrameworkQueryableExtensions.AsTracking)
                        => QueryTrackingBehavior.TrackAll,
                    nameof(EntityFrameworkQueryableExtensions.AsNoTracking)
                        => QueryTrackingBehavior.NoTracking,
                    nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution)
                        => QueryTrackingBehavior.NoTrackingWithIdentityResolution,
                    _ => _behavior,
                };
            }

            return visited;
        }
    }

    private static byte[] SerializeNode(Expressions.ExpressionNode node)
        => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            node, ExpressionJsonContext.Default.ExpressionNode);

    private static async Task<TElement> FirstOrDefaultAsync(IAsyncEnumerable<TElement> asyncEnum)
    {
        await foreach (TElement value in asyncEnum.ConfigureAwait(false))
        {
            return value;
        }

        return default!;
    }

    /// <summary>
    ///     Substitutes EF query parameters as plain <see cref="ConstantExpression" />s of their
    ///     runtime values — never wrapped in custom generic structs (research-findings §6, the
    ///     v1 <c>ValueWrapper&lt;T&gt;</c> trap).
    /// </summary>
    /// <remarks>
    ///     Two node forms must be handled. EF Core 10's <c>ExpressionTreeFuncletizer</c> lifts
    ///     closure-captured values into <see cref="QueryParameterExpression" /> — an
    ///     <see cref="ExpressionType.Extension" /> node, so it arrives at
    ///     <see cref="VisitExtension" />, not <see cref="VisitParameter" />. Compiled queries
    ///     additionally produce ordinary <see cref="ParameterExpression" />s named with the
    ///     <c>__</c> prefix. The funcletizer keys
    ///     <see cref="QueryContext.Parameters" /> by the same name it gives the node, so a
    ///     single lookup serves both.
    /// </remarks>
    private sealed class SubstituteParametersExpressionVisitor : ExpressionVisitor
    {
        private readonly QueryContext _queryContext;

        public SubstituteParametersExpressionVisitor(QueryContext queryContext)
            => _queryContext = queryContext;

        protected override Expression VisitExtension(Expression node)
            => node is QueryParameterExpression queryParameter
                && _queryContext.Parameters.TryGetValue(queryParameter.Name, out object? value)
                    ? Substitute(WithoutNullEntities(value, queryParameter.Type), queryParameter.Type)
                    : base.VisitExtension(node);

        /// <summary>
        ///     Drops <see langword="null" /> elements from a collection of <em>entities</em>.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         EF rewrites <c>customers.Contains(c)</c> into a comparison of primary keys, and
        ///         it has two ways of getting the key list. From a query <em>parameter</em> it maps
        ///         each element with <c>e != null ? getter.GetClrValue(e) : null</c>. From a
        ///         <em>constant</em> — which is what §6 substitution produces — it calls the getter
        ///         unconditionally and dereferences the null
        ///         (<c>InMemoryExpressionTranslatingExpressionVisitor.TryRewriteContainsEntity</c>).
        ///         The failure surfaces as a bare <see cref="NullReferenceException" /> inside EF,
        ///         with nothing to connect it to this substitution.
        ///     </para>
        ///     <para>
        ///         Dropping the element is what EF's parameter path amounts to: it yields a
        ///         <see langword="null" /> <em>key</em>, and a null key cannot equal the key of an
        ///         entity a query root produced. Entity-typed collections only — a
        ///         <c>null</c> in a scalar collection is a value that can genuinely match, and
        ///         removing it would change the answer.
        ///     </para>
        /// </remarks>
        private object? WithoutNullEntities(object? value, Type parameterType)
        {
            if (value is not System.Collections.IEnumerable sequence
                || value is string
                || SequenceElementType(parameterType) is not { } elementType
                || _queryContext.Context.Model.FindEntityType(elementType) is null)
            {
                return value;
            }

            var kept = (System.Collections.IList)Activator.CreateInstance(
                typeof(List<>).MakeGenericType(elementType))!;

            bool sawNull = false;
            foreach (object? element in sequence)
            {
                if (element is null)
                {
                    sawNull = true;
                }
                else
                {
                    kept.Add(element);
                }
            }

            if (!sawNull)
            {
                return value;
            }

            return parameterType.IsAssignableFrom(kept.GetType()) ? kept : value;
        }

        /// <summary>
        ///     Substitutes a parameter's value, spelling a collection out element by element.
        /// </summary>
        /// <remarks>
        ///     A scalar becomes a plain constant (research-findings §6). A <em>collection</em>
        ///     cannot: relational EF recognises an inline collection from the shape of the
        ///     expression, and a single constant holding a <c>List&lt;T&gt;</c> is not that
        ///     shape — <c>cities.Contains(c.City)</c> then failed to translate with
        ///     "Translation of method 'Enumerable.Contains' failed". Writing the elements out as
        ///     a <see cref="NewArrayExpression" /> is the form
        ///     <c>QueryRootProcessor</c> turns into an <c>InlineQueryRootExpression</c>.
        ///     <para>
        ///         Tier A never noticed: EF's InMemory provider client-evaluates the
        ///         <c>Contains</c> either way.
        ///     </para>
        /// </remarks>
        private static Expression Substitute(object? value, Type parameterType)
        {
            if (value is not System.Collections.IEnumerable sequence
                || value is string
                || SequenceElementType(parameterType) is not { } elementType
                // An `object` element type carries boxed values whose real types differ per
                // element. Spelling those out as `Constant(x, typeof(object))` tells EF less
                // than the collection itself did, and cost more than it gained.
                || elementType == typeof(object))
            {
                return Expression.Constant(value, parameterType);
            }

            var elements = new List<Expression>();
            foreach (object? element in sequence)
            {
                elements.Add(Expression.Constant(element, elementType));
            }

            NewArrayExpression array = Expression.NewArrayInit(elementType, elements);

            // Only when the array can stand in for the declared type; a parameter typed as a
            // concrete List<T> cannot be handed one.
            return parameterType.IsAssignableFrom(array.Type)
                ? array
                : Expression.Constant(value, parameterType);
        }

        private static Type? SequenceElementType(Type type)
            => type.IsArray
                ? type.GetElementType()
                : (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        ? type
                        : type.GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType
                                && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                    ?.GetGenericArguments()[0];

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name is not null
                && node.Name.StartsWith("__", StringComparison.Ordinal)
                && _queryContext.Parameters.TryGetValue(node.Name, out object? value))
            {
                return Substitute(value, node.Type);
            }

            return base.VisitParameter(node);
        }
    }
}
