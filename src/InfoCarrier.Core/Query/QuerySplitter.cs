// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Divides a captured query into what the server executes and what the client applies to the
///     results (<c>docs/projection-split.md</c>, [ADR-010](../../../docs/decisions.md)).
/// </summary>
/// <remarks>
///     The split runs on the client, before serialization, because the allowlist rejects during
///     <em>deserialization</em>: a tree naming an anonymous type throws before the server has an
///     expression to analyze. This must therefore run <b>after</b> query parameters are
///     substituted — a surviving closure field access would name a compiler-generated display
///     class and push the boundary in for no reason.
/// </remarks>
public sealed class QuerySplitter
{
    private static readonly HashSet<string> QueryMarkers =
    [
        nameof(EntityFrameworkQueryableExtensions.AsTracking),
        nameof(EntityFrameworkQueryableExtensions.AsNoTracking),
        nameof(EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution),
        nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters),
        nameof(EntityFrameworkQueryableExtensions.IgnoreAutoIncludes),
        "AsSplitQuery",
        "AsSingleQuery",
    ];

    private readonly IModel _model;
    private readonly TypeAllowlist _allowlist;
    private readonly ServerBoundaryAnalyzer _analyzer;
    private readonly IDiagnosticsLogger<DbLoggerCategory.Query>? _queryLogger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QuerySplitter" /> class.
    /// </summary>
    /// <param name="model">The client model — the same entity types the server has.</param>
    /// <param name="allowlist">
    ///     The types the server accepts. Defaults to one derived from <paramref name="model" />,
    ///     which is what the server derives from its own.
    /// </param>
    /// <param name="queryLogger">
    ///     The query logger, for the diagnostics EF raises during navigation expansion — which
    ///     ADR-006 means this provider never runs, so anything raised there has to be raised here
    ///     instead. Optional because the splitter is also constructed in tests that have no
    ///     context; a null one simply skips those checks.
    /// </param>
    public QuerySplitter(
        IModel model,
        TypeAllowlist? allowlist = null,
        IDiagnosticsLogger<DbLoggerCategory.Query>? queryLogger = null)
    {
        _model = model;
        _allowlist = allowlist ?? TypeAllowlist.ForModel(model);
        _analyzer = new ServerBoundaryAnalyzer(_allowlist);
        _queryLogger = queryLogger;
    }

    /// <summary>
    ///     Splits a captured query.
    /// </summary>
    public SplitQuery Split(Expression query)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Held for `RejectInvalidIncludes` below, which quotes the offending lambda back to the
        // caller. Everything under here retypes the tree, and quoting the retyped one names
        // `x.Item1.Capital` where the caller wrote `x.f.Capital` — an internal carrier leaking
        // into a user-facing message. The check itself is syntactic and reads the same either way.
        Expression captured = query;

        // Three checks on the caller's own query, all of them before the
        // `IsWhollyServerExecutable` return below, and all for one reason: what they refuse is a
        // mistake in what the caller wrote, not a property of where the boundary falls. Each
        // mirrors a diagnostic EF raises downstream of ADR-006's capture point, so the client
        // never runs it — and a *shippable* query gets it from the server, which is why the gap
        // only ever showed on queries the split leaves work on.
        //
        // In EF's order. `QueryableMethodNormalizingExpressionVisitor` opens
        // `QueryTranslationPreprocessor` and refuses a projection that is still a query (C56);
        // `NavigationExpandingExpressionVisitor` follows and validates includes (C40, C47).
        //
        // The placement used to matter — C40 measured moving the lambda include check up at
        // **1 fixed, 18 broken**, and left it below the early return for that reason. The 18 were
        // `IsEntity` failing to look through a collection type, not the strictness they were
        // attributed to; with that fixed the checks are safe everywhere, which is where a
        // validity check on the caller's query belongs.
        QueryableProjectionValidator.Validate(captured);
        ValidateStringIncludePaths(captured);
        RejectInvalidIncludes(captured);

        // A C# collection expression is a constant whose *runtime* type is the compiler's
        // `<>z__ReadOnlyArray<T>` — a type the server's assembly does not have, and one the
        // allowlist rightly refuses because a constant is read by what it is (A20). What the
        // caller wrote is a sequence, and an array says that with no compiler-generated type in
        // it. Left alone, `IgnoreQueryFilters(["ActiveFilter", "NameFilter"])` was unshippable
        // whole, so only the query root travelled and the marker stayed on the client where it
        // does nothing at all — the server applied the filters the caller had just excluded.
        query = CollectionExpressionNormalizer.Normalize(query, _allowlist);

        // Flatten `GroupJoin` + `SelectMany` into a single join first (ADR-011). The transparent
        // identifier between them holds the *grouping*, which the carrier re-carry below must
        // refuse to put in a tuple slot — so unless it is removed here, `join … into … from …
        // DefaultIfEmpty()` stays on the client and answers a left join's null rows by throwing.
        query = GroupJoinFlattener.Flatten(query);

        // Replace the carrier types the query creates and consumes internally — transparent
        // identifiers, mostly — with tuples, so the operators above them stay on the server
        // (ADR-011). Guarded: kept only if it demonstrably ships more.
        query = ReCarryInternalTypes(query, out Expression? rootRebuild);

        // Rewrite client-typed projections into a server-side tuple plus a client-side
        // reassembly *before* looking for the boundary (§3.2). Cutting above such a projection
        // is not merely coarse — it strands navigation reads and correlated subqueries on the
        // client, and it decomposes a `GroupBy` from the aggregate that makes it translatable.
        query = ProjectionRewriter.Rewrite(query, _analyzer, out IReadOnlySet<Expression> reassemblies, rootRebuild);

        BoundaryAnalysis analysis = _analyzer.Analyze(query);

        if (analysis.IsWhollyServerExecutable)
        {
            // Nothing to do on the client. Keep the residual an identity so callers have one
            // shape to handle, and flag it so they can skip the work entirely.
            ParameterExpression only = Expression.Parameter(query.Type, "server");
            return new SplitQuery(
                [ToServerQuery(query)],
                Expression.Lambda(only, only),
                isPassThrough: true);
        }

        if (analysis.Shippable.Count == 0)
        {
            throw new InvalidOperationException(
                $"No part of the query can be executed on the server: '{query}'. {Diagnose(query)}");
        }

        RejectOpenFragments(analysis);
        RejectClientEvaluation(query, analysis, reassemblies, _allowlist);

        // Substitute each shipped subtree with a parameter the caller binds to its results.
        var parameters = new List<ParameterExpression>(analysis.Shippable.Count);
        var substitutions = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < analysis.Shippable.Count; i++)
        {
            Expression shipped = analysis.Shippable[i];

            // Bind by the interface, not by the shipped expression's exact type. A subtree ending
            // in `Include(c => c.Orders)` is typed IIncludableQueryable, which the materialized
            // EnumerableQuery does not implement — the residual then failed at invocation time
            // with a cast complaint far from the cause. Every operator the residual can apply
            // takes IQueryable<T> anyway.
            var parameter = Expression.Parameter(
                typeof(IQueryable).IsAssignableFrom(shipped.Type)
                    ? typeof(IQueryable<>).MakeGenericType(ElementTypeOf(shipped.Type))
                    : shipped.Type,
                $"server{i}");
            parameters.Add(parameter);
            substitutions[shipped] = parameter;
        }

        Expression residualBody = new SubstitutingVisitor(substitutions).Visit(query)!;
        residualBody = new MarkerStrippingVisitor().Visit(residualBody)!;

        residualBody = new EfPropertyRewritingVisitor(_model).Visit(residualBody)!;

        IReadOnlyList<Expression> augmented = AugmentWithNavigations(analysis.Shippable, residualBody);

        return new SplitQuery(
            [.. augmented.Select(ToServerQuery)],
            Expression.Lambda(residualBody, parameters),
            isPassThrough: false);
    }

    /// <summary>
    ///     Names the first reason a query is wholly unshippable.
    /// </summary>
    /// <remarks>
    ///     The message used to guess ("this usually means the query root names a type the server
    ///     does not know"), which was wrong often enough to cost a diagnosis: adopting
    ///     <c>GraphUpdatesTestBase</c> produced 1,421 of these and the guess pointed at the query
    ///     root, while the actual rejection was elsewhere in the tree. A verdict this coarse is
    ///     worth one extra walk when it is already about to throw.
    /// </remarks>
    private string Diagnose(Expression query)
    {
        var nodes = new List<Expression>();
        new OrderedNodeCollector(nodes).Visit(query);

        foreach (Expression node in nodes)
        {
            if (!ServerBoundaryAnalyzer.IsSerializableKind(node))
            {
                return $"'{Abbreviate(node)}' ({node.NodeType}) has no wire representation.";
            }

            foreach (Type type in WireTypeCollector.CollectOwn(node))
            {
                if (!_allowlist.IsAllowed(type))
                {
                    return $"'{Abbreviate(node)}' names the type '{type}', which is not on the "
                        + "type allowlist the server enforces.";
                }
            }
        }

        return "Every node is expressible, so the tree contains no runnable query — the root is "
            + "reached only through something that is not a query operator.";
    }

    private static string Abbreviate(Expression node)
    {
        string text = node.ToString();
        return text.Length <= 120 ? text : string.Concat(text.AsSpan(0, 117), "...");
    }

    /// <summary>
    ///     Every node of a tree, parents before children.
    /// </summary>
    private sealed class OrderedNodeCollector(ICollection<Expression> into) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            into.Add(node);
            return base.Visit(node);
        }
    }

    /// <summary>
    ///     Applies <see cref="TransparentIdentifierRewriter" />, keeping the result only if
    ///     <see cref="RewriteVerifier" /> agrees it is an improvement.
    /// </summary>
    /// <remarks>
    ///     The rewrite is a guess about a tree the server has not seen, and the last unguarded
    ///     one of those cost a 91 → 383 regression. Verifying first means the worst it can do is
    ///     nothing.
    /// </remarks>
    private Expression ReCarryInternalTypes(Expression query, out Expression? rootRebuild)
    {
        Expression candidate = TransparentIdentifierRewriter.Rewrite(query, _allowlist, out rootRebuild);

        if (ReferenceEquals(candidate, query))
        {
            return query;
        }

        Expression kept = new RewriteVerifier(_analyzer).Verify(query, candidate).Kept;
        if (!ReferenceEquals(kept, candidate))
        {
            rootRebuild = null;
        }

        return kept;
    }

    private static ServerQuery ToServerQuery(Expression query)
    {
        bool sequence = typeof(IQueryable).IsAssignableFrom(query.Type);
        return new ServerQuery(query, sequence ? ElementTypeOf(query.Type) : query.Type, !sequence);
    }

    private static Type ElementTypeOf(Type queryableType)
        => (queryableType.IsGenericType && queryableType.GetGenericTypeDefinition() == typeof(IQueryable<>)
                ? queryableType
                : queryableType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>)))
            ?.GetGenericArguments()[0]
            ?? queryableType;

    /// <summary>
    ///     Rejects an <c>Include</c> whose lambda is not a property path.
    /// </summary>
    /// <remarks>
    ///     EF validates this during translation, which this provider replaces — so without the
    ///     check <c>Include(o =&gt; new { o.Customer, o.OrderDetails })</c> reached the splitter,
    ///     was found to name an anonymous type, and was quietly treated as a projection boundary
    ///     instead of the mistake it is.
    /// </remarks>
    /// <summary>
    ///     Reports a string <c>Include</c> path naming a navigation the model does not have.
    /// </summary>
    /// <remarks>
    ///     A string include names no member, so the syntactic check below cannot see it:
    ///     <c>Include("Wheels")</c> is well-formed and only the model can say the navigation does
    ///     not exist. EF finds out in
    ///     <c>NavigationExpandingExpressionVisitor.ProcessInclude</c> and raises
    ///     <c>CoreEventId.InvalidIncludePathError</c>, a warning-as-error by default — and that
    ///     visitor is exactly what ADR-006's capture point means this provider never runs, so an
    ///     unvalidated path used to travel to the server and be silently ignored.
    ///     <para>
    ///         Raised through EF's own logger extension rather than by composing the message
    ///         here. The spec test asserts
    ///         <c>WarningAsErrorTemplate(…, LogInvalidIncludePath.GenerateMessage(…), …)</c>;
    ///         reproducing that string would mean reproducing EF's warning-as-error plumbing too,
    ///         and calling the extension gets both for free and cannot drift from it.
    ///     </para>
    /// </remarks>
    private void ValidateStringIncludePaths(Expression query)
    {
        if (_queryLogger is not null)
        {
            StringIncludeValidator.Validate(query, _model, _queryLogger);
        }
    }

    private void RejectInvalidIncludes(Expression query)
    {
        if (InvalidIncludeFinder.Find(query, _model) is not var (invalid, onNonEntity))
        {
            return;
        }

        throw new InvalidOperationException(
            onNonEntity
                ? Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings.IncludeOnNonEntity(invalid.ToString())
                : Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings.InvalidIncludeExpression(invalid));
    }

    /// <summary>
    ///     Walks a string <c>Include</c> path against the model and reports the first segment
    ///     that names no navigation, exactly where EF would.
    /// </summary>
    /// <remarks>
    ///     A deliberate mirror of <c>NavigationExpandingExpressionVisitor.ProcessInclude</c> and
    ///     its <c>FindNavigations</c>, down to the breadth-first queue and the decision to keep
    ///     going after a failed segment. What matters is the <em>set</em> of navigations that
    ///     counts as found — a derived type's declared navigation and a skip navigation both do,
    ///     and a stricter walk here would refuse paths EF accepts.
    /// </remarks>
    private sealed class StringIncludeValidator(
        IModel model,
        IDiagnosticsLogger<DbLoggerCategory.Query> logger) : ExpressionVisitor
    {
        public static void Validate(
            Expression query, IModel model, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
            => new StringIncludeValidator(model, logger).Visit(query);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Only `Include` has a string overload; `ThenInclude` does not.
            if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                && node.Arguments is [_, ConstantExpression { Value: string navigationChain }]
                && node.Method.IsGenericMethod
                && model.FindEntityType(node.Method.GetGenericArguments()[0]) is { } rootEntityType)
            {
                Walk(navigationChain, rootEntityType);
            }

            return base.VisitMethodCall(node);
        }

        private void Walk(string navigationChain, IEntityType rootEntityType)
        {
            var reached = new Queue<IEntityType>();
            reached.Enqueue(rootEntityType);

            foreach (string navigationName in navigationChain.Split('.'))
            {
                int toProcess = reached.Count;
                while (toProcess-- > 0)
                {
                    foreach (INavigationBase navigation in FindNavigations(reached.Dequeue(), navigationName))
                    {
                        reached.Enqueue(navigation.TargetEntityType);
                    }
                }

                if (reached.Count == 0)
                {
                    // Not stopped, because EF does not: with the queue empty every later segment
                    // reports too, so `Include("Wheels.Spokes")` logs twice. Immaterial by
                    // default — the event is a warning-as-error and the first one throws — but it
                    // is what an application that downgraded it to a warning would see from EF.
                    logger.InvalidIncludePathError(navigationChain, navigationName);
                }
            }
        }

        private static IEnumerable<INavigationBase> FindNavigations(IEntityType entityType, string navigationName)
        {
            if (entityType.FindNavigation(navigationName) is { } navigation)
            {
                yield return navigation;
            }
            else
            {
                foreach (IEntityType derived in entityType.GetDerivedTypes())
                {
                    if (derived.FindDeclaredNavigation(navigationName) is { } derivedNavigation)
                    {
                        yield return derivedNavigation;
                    }
                }
            }

            if (entityType.FindSkipNavigation(navigationName) is { } skipNavigation)
            {
                yield return skipNavigation;
            }
            else
            {
                foreach (IEntityType derived in entityType.GetDerivedTypes())
                {
                    if (derived.FindDeclaredSkipNavigation(navigationName) is { } derivedSkipNavigation)
                    {
                        yield return derivedSkipNavigation;
                    }
                }
            }
        }
    }

    private sealed class InvalidIncludeFinder(IModel model) : ExpressionVisitor
    {
        private (Expression Invalid, bool OnNonEntity)? _found;

        public static (Expression Invalid, bool OnNonEntity)? Find(Expression query, IModel model)
        {
            var finder = new InvalidIncludeFinder(model);
            finder.Visit(query);
            return finder._found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_found is null
                && node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Method.Name is nameof(EntityFrameworkQueryableExtensions.Include)
                    or nameof(EntityFrameworkQueryableExtensions.ThenInclude)
                && node.Arguments.Count == 2
                && StripQuotes(node.Arguments[1]) is LambdaExpression lambda)
            {
                if (!IsPropertyPath(lambda.Body))
                {
                    _found = (lambda.Body, false);
                }
                else if (lambda.Parameters is [{ } root] && !IsEntity(root.Type))
                {
                    // EF's other include check, and the reason this whole pass reads the query as
                    // *captured*: `Select(f => new { f }).Include(x => x.f.Capital)` is a
                    // well-formed property path whose root is an anonymous type. Left to the
                    // server it is still refused, but by then the carrier re-carry has renamed
                    // the member and the message says `x.Item1.Capital` where the caller wrote
                    // `x.f.Capital`. EF names the whole lambda here, not just its body.
                    _found = (lambda, true);
                }
            }

            return _found is null ? base.VisitMethodCall(node) : node;
        }

        private bool IsEntity(Type type)
            => IsEntityType(type)
                // **A `ThenInclude` after a collection navigation is rooted at the collection.**
                // The lambda's parameter comes out as `ICollection<Gear>`, not `Gear`, so asking
                // the model about it gets a flat no — and the include is then reported as being
                // on a non-entity when it is nothing of the kind. Eighteen legitimate includes
                // were refused this way the first time this check ran on a wholly-shippable query
                // (C40 measured it; C47 found the cause with a probe, having wrongly blamed
                // `IsPropertyPath`, which handles both shapes perfectly well).
                || (ElementTypeOf(type) is { } element && IsEntityType(element));

        private bool IsEntityType(Type type)
            => model.FindEntityType(type) is not null
                // Shared-type and owned entity types are not reachable by CLR type alone — and
                // an `Include` may be rooted at an *interface* the entity implements, which EF
                // allows and `ThenInclude_with_interface_navigations` asserts. Assignability, not
                // identity: the question is whether the root can be an entity, not whether it is
                // spelled as one.
                || model.GetEntityTypes().Any(e => type.IsAssignableFrom(e.ClrType));

        /// <summary>
        ///     What a sequence type is a sequence of, or null if it is not one.
        /// </summary>
        /// <remarks>
        ///     <see cref="string" /> is an <see cref="IEnumerable{T}" /> of <see cref="char" />
        ///     and needs no special case: the caller asks the model whether the element is an
        ///     entity, and <see cref="char" /> is not.
        /// </remarks>
        private static Type? ElementTypeOf(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return type.GetGenericArguments()[0];
            }

            foreach (Type candidate in type.GetInterfaces())
            {
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    return candidate.GetGenericArguments()[0];
                }
            }

            return null;
        }

        private static bool IsPropertyPath(Expression body)
            => body switch
            {
                ParameterExpression => true,
                MemberExpression member => member.Expression is not null && IsPropertyPath(member.Expression),
                // A cast targets a navigation declared on a derived type, which EF allows.
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } convert
                    => IsPropertyPath(convert.Operand),
                // Collection navigations may be filtered by composing Where/OrderBy/Skip/Take.
                MethodCallExpression { Arguments.Count: > 0 } call => IsPropertyPath(call.Arguments[0]),
                _ => false,
            };

        private static Expression StripQuotes(Expression node)
        {
            while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            {
                node = quote.Operand;
            }

            return node;
        }
    }

    private static void RejectOpenFragments(BoundaryAnalysis analysis)
    {
        if (analysis.OpenFragments.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"The subquery '{analysis.OpenFragments[0]}' can only be evaluated on the server, but it "
                + "reads a value from the client-side projection that encloses it. Evaluating it on the "
                + "client would issue one query per row. Correlated subqueries under a client-side "
                + "projection are not supported yet (docs/projection-split.md §3.2, milestone M2-B).");
    }

    /// <summary>
    ///     Refuses to evaluate a query operator on the client unless it is the reassembly of a
    ///     rewritten projection.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF's contract is that client evaluation is legal only in the final projection —
    ///         everything else must translate or throw. Without this, a <c>Where</c> whose
    ///         predicate calls a client method would quietly be answered by fetching the whole
    ///         table and filtering locally: right answer, catastrophic plan, and a silent
    ///         departure from what every other provider does. The spec suite asserts the throw
    ///         (<c>AssertTranslationFailed</c>), which is the clearest statement of the contract.
    ///     </para>
    ///     <para>
    ///         The distinction that matters is <em>why</em> the operator stayed behind. A
    ///         <c>GroupBy</c> keyed on an anonymous type is perfectly translatable — EF does it
    ///         every day — and lands on the client only because this provider has a type boundary
    ///         EF does not. That is not a translation failure, and treating it as one cost 235
    ///         passing tests when this guard was first written the blunt way. A call to a method
    ///         the server has no way to run is a translation failure, in EF and here alike.
    ///     </para>
    /// </remarks>
    private static void RejectClientEvaluation(
        Expression query,
        BoundaryAnalysis analysis,
        IReadOnlySet<Expression> reassemblies,
        TypeAllowlist allowlist)
    {
        // Everything inside a shipped subtree runs on the server; everything else runs here.
        var serverSide = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        foreach (Expression subtree in analysis.Shippable)
        {
            new NodeCollector(serverSide).Visit(subtree);
        }

        if (ClientEvaluationFinder.Find(query, serverSide, reassemblies, allowlist) is not var (offender, details))
        {
            return;
        }

        // EF's own wording, down to the details clause — the spec suite asserts it
        // (`AssertTranslationFailed` / `WithDetails`), and a caller who has seen this message
        // from any other provider should not have to learn a second one.
        throw new InvalidOperationException(
            details is null
                ? Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings.TranslationFailed(offender.ToString())
                : Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings.TranslationFailedWithDetails(
                    offender.ToString(), details));
    }

    /// <summary>
    ///     A type's name in the form EF's own diagnostics use — <c>Namespace.Type&lt;Arg&gt;</c>,
    ///     never the CLR's <c>Type`1[[…]]</c>.
    /// </summary>
    /// <remarks>
    ///     EF has this as <c>SharedTypeExtensions.DisplayName</c>, but that is a shared source
    ///     file rather than a referenceable API, so it is reproduced here rather than reached for.
    /// </remarks>
    private static string DisplayName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        string name = (type.FullName ?? type.Name).Split('`')[0];
        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(DisplayName))}>";
    }

    /// <summary>
    ///     Turns an <c>EF.Property</c> left on the client side into a metadata read.
    /// </summary>
    /// <remarks>
    ///     <c>EF.Property</c> has no runtime body — EF replaces it during translation. A residual
    ///     containing one used to be refused, but the value is right there on the materialized
    ///     entity and the model knows how to read it. Only a shadow property genuinely cannot be
    ///     answered here, and <see cref="ClientPropertyReader" /> says so by name.
    /// </remarks>
    private sealed class EfPropertyRewritingVisitor(IModel model) : ExpressionVisitor
    {
        private static readonly MethodInfo ReadMethod =
            typeof(ClientPropertyReader).GetMethod(nameof(ClientPropertyReader.Read))!;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(EF)
                || node.Method.Name != nameof(EF.Property)
                || node.Arguments.Count != 2)
            {
                return base.VisitMethodCall(node);
            }

            return Expression.Call(
                ReadMethod.MakeGenericMethod(node.Type),
                Expression.Convert(Visit(node.Arguments[0])!, typeof(object)),
                Visit(node.Arguments[1])!,
                Expression.Constant(model, typeof(IModel)));
        }
    }

    /// <summary>
    ///     Makes sure a navigation the residual reads is actually on the wire (§3.6).
    /// </summary>
    /// <remarks>
    ///     This is the one failure mode of a plain cut that would not announce itself.
    ///     <c>Select(c =&gt; new { c.City, Count = c.Orders.Count })</c> cut at the projection ships
    ///     <c>Customer</c> entities whose <c>Orders</c> were never loaded, and the client answers
    ///     <b>0</b> — no exception, no log line, a plausible wrong number. Adding the
    ///     <c>Include</c> over-fetches; that is the accepted trade, and phase B removes the need
    ///     for it by evaluating such fragments on the server instead.
    /// </remarks>
    private IReadOnlyList<Expression> AugmentWithNavigations(IReadOnlyList<Expression> shipped, Expression residual)
    {
        List<NavigationRead> reads = NavigationReadFinder.Find(residual, _model);
        if (reads.Count == 0)
        {
            return shipped;
        }

        var result = new List<Expression>(shipped);
        foreach (NavigationRead read in reads)
        {
            bool placed = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (ElementTypeOf(result[i].Type) != read.Owner.ClrType
                    || !typeof(IQueryable).IsAssignableFrom(result[i].Type))
                {
                    continue;
                }

                result[i] = Include(result[i], read.Owner.ClrType, read.Path);
                placed = true;
            }

            if (!placed)
            {
                // The rows are not that entity type — typically because the projection rewrite
                // put the entity in a tuple slot. The Include still belongs on the query, just
                // at the root it is read from rather than wrapped around the whole thing.
                for (int i = 0; i < result.Count && !placed; i++)
                {
                    Expression withInclude = IncludeAtRoot(result[i], read.Owner.ClrType, read.Path);
                    if (!ReferenceEquals(withInclude, result[i]))
                    {
                        result[i] = withInclude;
                        placed = true;
                    }
                }
            }

            if (!placed)
            {
                // The rows reached the residual through a navigation from some *other* root:
                // `Select(c => new { …, c.Orders })` ships `Customer` rows and the residual then
                // reads `o.OrderDetails` off the orders in the tuple slot. Reaching them means
                // prefixing the path with the navigation that got there, which is sound exactly
                // when there is only one — no other navigation could have produced those rows,
                // and no other shipped query returns them (the loops above just established
                // that).
                for (int i = 0; i < result.Count && !placed; i++)
                {
                    foreach (IEntityType root in RootEntityTypes(result[i]))
                    {
                        if (SingleNavigationTo(root, read.Owner) is not { } step)
                        {
                            continue;
                        }

                        Expression withInclude = IncludeAtRoot(
                            result[i], root.ClrType, $"{step}.{read.Path}");
                        if (!ReferenceEquals(withInclude, result[i]))
                        {
                            result[i] = withInclude;
                            placed = true;
                            break;
                        }
                    }
                }
            }

            if (!placed)
            {
                throw new InvalidOperationException(
                    $"The client-side part of the query reads navigation '{read.Owner.DisplayName()}."
                        + $"{read.Path}', but no query sent to the server returns "
                        + $"'{read.Owner.DisplayName()}' rows that could carry it. Reading it on the "
                        + "client would silently yield an empty value "
                        + "(docs/projection-split.md §3.6).");
            }
        }

        return result;
    }

    private static Expression Include(Expression source, Type entityType, string path)
    {
        // The string overload takes the whole dotted path, so no ThenInclude chain has to be
        // reconstructed. Duplicates are harmless: EF folds identical include paths.
        MethodInfo include = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                && m.GetParameters() is [_, { ParameterType: var second }]
                && second == typeof(string))
            .MakeGenericMethod(entityType);

        return Expression.Call(include, source, Expression.Constant(path));
    }

    /// <summary>
    ///     Wraps the query root of <paramref name="entityType" /> inside <paramref name="query" />
    ///     with an <c>Include</c>, returning the original when there is no such root.
    /// </summary>
    private static Expression IncludeAtRoot(Expression query, Type entityType, string path)
        => new RootIncludingVisitor(entityType, path).Visit(query)!;

    /// <summary>
    ///     The entity types a shipped query is rooted at.
    /// </summary>
    private IEnumerable<IEntityType> RootEntityTypes(Expression query)
    {
        var roots = new List<Type>();
        new RootCollectingVisitor(roots).Visit(query);
        return roots.Distinct().Select(_model.FindEntityType).OfType<IEntityType>();
    }

    /// <summary>
    ///     The one navigation from <paramref name="from" /> that reaches <paramref name="to" />,
    ///     or null when there is none or more than one.
    /// </summary>
    private static string? SingleNavigationTo(IEntityType from, IEntityType to)
    {
        string? found = null;

        foreach (INavigationBase navigation in from.GetNavigations().Concat<INavigationBase>(from.GetSkipNavigations()))
        {
            if (navigation.TargetEntityType != to)
            {
                continue;
            }

            if (found is not null)
            {
                return null;
            }

            found = navigation.Name;
        }

        return found;
    }

    private sealed class RootCollectingVisitor(ICollection<Type> into) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            if (node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression root)
            {
                into.Add(root.ElementType);
            }

            return base.VisitExtension(node);
        }
    }

    private sealed class RootIncludingVisitor(Type entityType, string path) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
            => node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression root
                && root.ElementType == entityType
                    ? Include(node, entityType, path)
                    : base.VisitExtension(node);
    }

    private sealed record NavigationRead(IEntityType Owner, string Path);

    private sealed class SubstitutingVisitor(IReadOnlyDictionary<Expression, Expression> substitutions)
        : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
            => node is not null && substitutions.TryGetValue(node, out Expression? replacement)
                ? replacement
                : base.Visit(node);
    }

    /// <summary>
    ///     Removes EF's queryable markers from the client-side remainder.
    /// </summary>
    /// <remarks>
    ///     <c>AsNoTracking</c> and friends have no <see cref="Enumerable" /> counterpart, so
    ///     <c>EnumerableQuery</c>'s rewriter fails on them. Tracking behavior has already been
    ///     read off the original tree by the time this runs.
    /// </remarks>
    /// <summary>
    ///     Rewrites a compiler-generated collection constant as a plain array of the same
    ///     elements.
    /// </summary>
    /// <remarks>
    ///     Deliberately narrow, and the condition is the one that matters rather than a guess at
    ///     what the compiler emits: the runtime type is <em>not on the allowlist</em>, it really is
    ///     a sequence, and an array of its element type still satisfies the node's declared type.
    ///     A value the caller built out of a type the server knows is left alone, and so is one the
    ///     server could not reconstruct from an array either.
    /// </remarks>
    private sealed class CollectionExpressionNormalizer(TypeAllowlist allowlist) : ExpressionVisitor
    {
        public static Expression Normalize(Expression query, TypeAllowlist allowlist)
            => new CollectionExpressionNormalizer(allowlist).Visit(query);

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is not System.Collections.IEnumerable sequence
                || node.Value.GetType() is not { IsArray: false } runtime
                // Unspeakable, so the caller cannot have named it and no round trip can preserve
                // it. `OrderedEnumerable<T>` is not on the allowlist either and is *not* this:
                // rewriting one to an array cost `Contains_with_local_ordered_enumerable_inline`,
                // which is about the ordering that array would have thrown away.
                || !runtime.Name.StartsWith('<')
                || allowlist.IsAllowed(runtime)
                || ServerBoundaryAnalyzer.SequenceElementType(runtime) is not { } element
                || element == runtime)
            {
                return node;
            }

            // The node is usually typed as the value itself — EF's funcletizer builds constants
            // through `Expression.Constant(value)`, which reads the runtime type — so the array
            // has to become the node's type too. Where the node was declared more broadly
            // (`IReadOnlyList<string>`), that declaration is kept and only the value changes.
            Type arrayType = element.MakeArrayType();
            Type declared = node.Type.IsAssignableFrom(arrayType) ? node.Type : arrayType;
            if (node.Type != runtime && declared != node.Type)
            {
                return node;
            }

            var items = new List<object?>();
            foreach (object? item in sequence)
            {
                items.Add(item);
            }

            Array array = Array.CreateInstance(element, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array.SetValue(items[i], i);
            }

            return Expression.Constant(array, declared);
        }
    }

    private sealed class MarkerStrippingVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
            => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Arguments.Count == 1
                && QueryMarkers.Contains(node.Method.Name)
                    ? Visit(node.Arguments[0])
                    : base.VisitMethodCall(node);
    }

    private sealed class NodeCollector(ISet<Expression> into) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is not null)
            {
                into.Add(node);
            }

            return base.Visit(node);
        }
    }

    private sealed class ClientEvaluationFinder(
        IReadOnlySet<Expression> serverSide,
        IReadOnlySet<Expression> reassemblies,
        TypeAllowlist allowlist) : ExpressionVisitor
    {
        private (Expression Operator, string? Details)? _found;

        private IReadOnlySet<Expression> _clientProjectedSources = new HashSet<Expression>();

        public static (Expression Operator, string? Details)? Find(
            Expression query,
            IReadOnlySet<Expression> serverSide,
            IReadOnlySet<Expression> reassemblies,
            TypeAllowlist allowlist)
        {
            var finder = new ClientEvaluationFinder(serverSide, reassemblies, allowlist)
            {
                _clientProjectedSources = ClientProjectedSources(reassemblies, allowlist),
            };

            finder.Visit(query);
            return finder._found;
        }

        /// <summary>
        ///     The reassemblies that compute a value with client code — the projections a later
        ///     operator may not apply a lambda to.
        /// </summary>
        private static IReadOnlySet<Expression> ClientProjectedSources(
            IReadOnlySet<Expression> reassemblies, TypeAllowlist allowlist)
        {
            var found = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
            foreach (Expression reassembly in reassemblies)
            {
                if (reassembly is MethodCallExpression { Arguments.Count: > 1 } call
                    && StripQuotes(call.Arguments[^1]) is LambdaExpression selector
                    && ClientCodeFinder.Find(selector.Body, allowlist, methodsOnly: true) is not null)
                {
                    found.Add(reassembly);
                }
            }

            return found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_found is null
                && node.Arguments.Count > 0
                && !serverSide.Contains(node)
                && !reassemblies.Contains(node)
                && node.Method.DeclaringType is { } declaring
                && (declaring == typeof(Queryable)
                    || declaring == typeof(Enumerable)
                    // `ExecuteUpdate` and `ExecuteDelete` live here rather than on `Queryable`,
                    // and leaving the type out meant an unshippable bulk update was never
                    // examined for client code — it simply ran, and EF's marker overload answered
                    // with `UnreachableException: Can't call this overload directly`, an internal
                    // sentinel no caller should ever see (C63, C67). `Include` is here too; its
                    // argument is a property path, so walking it finds nothing to refuse.
                    || declaring == typeof(EntityFrameworkQueryableExtensions)))
            {
                foreach (Expression argument in RowDecidingArguments(node))
                {
                    if (ClientCodeFinder.Find(argument, allowlist) is { } reason)
                    {
                        _found = (node, reason.Details);
                        break;
                    }
                }
            }

            if (_found is null && _clientProjectedSources.Count > 0)
            {
                RejectLambdaOverClientProjection(node);
            }

            return _found is null ? base.VisitMethodCall(node) : node;
        }

        /// <summary>
        ///     Refuses an operator that applies a row-deciding lambda to a sequence some earlier
        ///     projection computed with client code.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         **This is EF's rule, read off its own tests rather than off its prose** (C68).
        ///         "Client evaluation is legal only in the final projection" is what the docs say
        ///         and it is not implementable — C59 measured that reading at 6 fixed and
        ///         <b>18 broken</b>. The eighteen say what the real line is:
        ///     </para>
        ///     <list type="bullet">
        ///         <item><c>Select(c =&gt; Client(c)).Union(…)</c> then <c>FirstOrDefault</c> — <b>allowed</b></item>
        ///         <item><c>Select(o =&gt; Client(o)).Count()</c> — <b>allowed</b></item>
        ///         <item><c>… select Client(l1_inner)).Count() &gt; 7</c> inside a <c>Where</c> — <b>allowed</b></item>
        ///         <item><c>Join(root, Orders.Select(o =&gt; Client(o)), c =&gt; c.CustomerID, o =&gt; o.CustomerID, …)</c> — <b>refused</b></item>
        ///         <item><c>(… select Client(l1)).Take(2).LeftJoin(root, x =&gt; x.Id, …)</c> — <b>refused</b></item>
        ///     </list>
        ///     <para>
        ///         `Union`, `Count` and `FirstOrDefault` take **no lambda over the projected
        ///         element**; the two refusals apply a **join key** to it. So the line is not
        ///         whether something composes over the projection but whether it *reads* it, and
        ///         "reads it" is already this class's own <see cref="RowDecidingArguments" />
        ///         concept, applied one level up.
        ///     </para>
        ///     <para>
        ///         <b>And it explains C59's failure exactly.</b> That attempt marked every
        ///         argument of a consuming operator, including <em>lambda bodies</em> — so the
        ///         outer <c>Where</c> of `GroupJoin_in_subquery_with_client_projection` "consumed"
        ///         a reassembly sitting inside its predicate, when what actually consumes that
        ///         subquery is the <c>Count()</c> within it. The walk therefore follows
        ///         <b>source positions only</b>.
        ///     </para>
        /// </remarks>
        private void RejectLambdaOverClientProjection(MethodCallExpression node)
        {
            if (serverSide.Contains(node)
                || node.Method.DeclaringType is not { } declaring
                || (declaring != typeof(Queryable) && declaring != typeof(Enumerable)))
            {
                return;
            }

            // No row-deciding lambda means nothing reads the projected value: cardinality
            // (`Count`), set operations (`Union`) and row limiting all fall here, and EF allows
            // every one of them over a client projection.
            if (!RowDecidingArguments(node).Any(a => StripQuotes(a) is LambdaExpression))
            {
                return;
            }

            foreach (Expression source in SourceArguments(node))
            {
                if (ReachesClientProjection(source))
                {
                    _found = (node, null);
                    return;
                }
            }
        }

        /// <summary>
        ///     An operator's <em>sequence</em> arguments — never its lambdas, which is the whole
        ///     correction over C59.
        /// </summary>
        private static IEnumerable<Expression> SourceArguments(MethodCallExpression node)
            => node.Arguments.Where(a => a is not LambdaExpression
                && StripQuotes(a) is not LambdaExpression
                && a.Type != typeof(string)
                && typeof(System.Collections.IEnumerable).IsAssignableFrom(a.Type));

        /// <summary>
        ///     Whether a client-projecting reassembly is this source, or reaches it through
        ///     operators that do not themselves read the projection.
        /// </summary>
        private bool ReachesClientProjection(Expression source)
        {
            while (true)
            {
                if (_clientProjectedSources.Contains(source))
                {
                    return true;
                }

                if (source is MethodCallExpression { Arguments.Count: > 0 } call
                    && call.Method.DeclaringType is { } declaring
                    && (declaring == typeof(Queryable)
                        || declaring == typeof(Enumerable)
                        || declaring == typeof(EntityFrameworkQueryableExtensions))
                    && !RowDecidingArguments(call).Any(a => StripQuotes(a) is LambdaExpression))
                {
                    source = call.Arguments[0];
                    continue;
                }

                return false;
            }
        }

        private static Expression StripQuotes(Expression node)
        {
            while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            {
                node = quote.Operand;
            }

            return node;
        }

        /// <summary>
        ///     Whether an operator produces a different element type than it consumed — that is,
        ///     whether it is a projection.
        /// </summary>
        /// <remarks>
        ///     EF's line is drawn here, and it is the right one. Client code in a projection is
        ///     legal everywhere: the rows have already been chosen, and evaluating it locally
        ///     costs nothing extra. Client code in a <c>Where</c> or an <c>OrderBy</c> decides
        ///     <em>which</em> rows, so evaluating it locally means fetching all of them first.
        /// </remarks>
        private static IEnumerable<Expression> RowDecidingArguments(MethodCallExpression node)
        {
            // A result selector runs after the rows are chosen, so client code in it costs
            // nothing extra and EF allows it. Everything else -- predicates, join keys, ordering
            // keys -- decides *which* rows, and running that locally means fetching them all.
            // `Join` is both at once: its result selector is a projection, its key selectors are
            // not, which is why the split has to be per argument rather than per operator.
            int skipLast = ProjectionRewriter.IsResultSelectorOperator(node, out _) ? 1 : 0;

            for (int i = 1; i < node.Arguments.Count - skipLast; i++)
            {
                yield return node.Arguments[i];
            }
        }
    }

    /// <summary>
    ///     Finds a call to behaviour that exists only on the client — a method the server has no
    ///     way to run.
    /// </summary>
    /// <remarks>
    ///     Constructing a client-only <em>type</em> deliberately does not count: that is the type
    ///     boundary, this milestone's whole subject, not a failure. Neither does reading a member
    ///     of one — <c>x.Outer.Name</c> on a transparent identifier declares its member on an
    ///     anonymous type the server cannot name, but the read is ordinary data access. Counting
    ///     member reads condemned every query written in the <c>from … from … select</c> form,
    ///     69 of them.
    /// </remarks>
    /// <summary>
    ///     Why an operator argument cannot be answered on the client. <see cref="Details" /> is
    ///     EF's details clause, or <see langword="null" /> when the bare message says enough.
    /// </summary>
    private readonly record struct ClientCodeReason(string? Details);

    private sealed class ClientCodeFinder(TypeAllowlist allowlist, bool methodsOnly = false)
        : ExpressionVisitor
    {
        private ClientCodeReason? _found;

        /// <param name="methodsOnly">
        ///     Restricts the search to a method the server cannot run, skipping the client-only
        ///     <em>type</em> clauses below. C68 needs that distinction: constructing a client type
        ///     is the type boundary this milestone is about, and refusing it in the composed-over
        ///     position cost 235 tests once.
        /// </param>
        public static ClientCodeReason? Find(
            Expression expression, TypeAllowlist allowlist, bool methodsOnly = false)
        {
            var finder = new ClientCodeFinder(allowlist, methodsOnly);
            finder.Visit(expression);
            return finder._found;
        }

        /// <summary>
        ///     Reference equality between two client-only types.
        /// </summary>
        /// <remarks>
        ///     This is the one shape that would otherwise be answered <em>wrongly</em> rather
        ///     than refused. An anonymous type overrides <c>Equals</c> structurally but not
        ///     <c>==</c>, so <c>new { x = c.City } == new { x = "London" }</c> evaluated on the
        ///     client compares two freshly allocated objects by reference and is always false —
        ///     zero rows, no error, a plausible answer. EF translates it structurally, or reports
        ///     a translation failure (issue #14672). Either is better than silently none.
        /// </remarks>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (_found is null
                && !methodsOnly
                && node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
                && node.Method is null
                && !node.Left.Type.IsValueType
                && node.Left.Type != typeof(string)
                && !allowlist.IsAllowed(node.Left.Type)
                && !allowlist.IsAllowed(node.Right.Type)
                && !IsNull(node.Left)
                && !IsNull(node.Right))
            {
                _found = new ClientCodeReason(null);
                return node;
            }

            return _found is null ? base.VisitBinary(node) : node;
        }

        // `x == null` is a null test, not structural equality: it means the same thing on either
        // side of the wire, and refusing it would condemn every `FirstOrDefault() == null`.
        private static bool IsNull(Expression node)
        {
            // A null may arrive boxed — `(object)x == (object)null` — so the conversions have to
            // come off before the test means anything.
            while (node is UnaryExpression
                   {
                       NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs,
                   } convert)
            {
                node = convert.Operand;
            }

            return node is ConstantExpression { Value: null } or DefaultExpression;
        }

        /// <summary>
        ///     Constructing a client-only type that has no value equality, where the operator is
        ///     about to compare it.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <c>join o in os on new Foo { Bar = c.CustomerID } equals new Foo { Bar = o.CustomerID }</c>
        ///         forces the join onto the client, because <c>Foo</c> is a type the server cannot
        ///         name. There the keys are compared with <c>EqualityComparer&lt;Foo&gt;.Default</c>
        ///         — and <c>Foo</c> does not override <c>Equals</c>, so that is reference equality
        ///         between two freshly allocated objects. Every row fails to match and the query
        ///         answers <b>nothing</b>: no exception, no log line, an empty result that looks
        ///         like data.
        ///     </para>
        ///     <para>
        ///         An anonymous type in the same position is fine and stays allowed — the compiler
        ///         gives it structural <c>Equals</c>, so the client comparison means what the query
        ///         said. That is the whole distinction, and it is why this tests the type's
        ///         equality rather than its origin: <em>constructing</em> a client-only type is the
        ///         type boundary, this milestone's subject, and refusing that outright cost 235
        ///         tests once.
        ///     </para>
        /// </remarks>
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (_found is null && !methodsOnly && LacksValueEquality(node.Type))
            {
                _found = new ClientCodeReason(null);
                return node;
            }

            return _found is null ? base.VisitMemberInit(node) : node;
        }

        protected override Expression VisitNew(NewExpression node)
        {
            if (_found is null && !methodsOnly && LacksValueEquality(node.Type))
            {
                _found = new ClientCodeReason(null);
                return node;
            }

            return _found is null ? base.VisitNew(node) : node;
        }

        private bool LacksValueEquality(Type type)
            => !type.IsValueType
                && !allowlist.IsAllowed(type)
                && type.GetMethod(nameof(Equals), [typeof(object)])?.DeclaringType == typeof(object);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (_found is not null)
            {
                return node;
            }

            if (node.Method.DeclaringType is { } declaring && !allowlist.IsAllowed(declaring))
            {
                _found = new ClientCodeReason(
                    Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings.QueryUnableToTranslateMethod(
                        DisplayName(declaring), node.Method.Name));
                return node;
            }

            // The same exemption, one level down. A collection selector may legitimately contain
            // a projection -- `c.Orders.Select(o => new { ClientMethod(o), … })` is client code
            // in a projection, which EF allows and the spec suite asserts. What must not be
            // exempt is client code applied to the *sequence*: `ClientDefaultIfEmpty(grouping)`
            // decides which rows there are.
            int skipLast = ProjectionRewriter.IsResultSelectorOperator(node, out _) ? 1 : 0;
            for (int i = 0; i < node.Arguments.Count - skipLast && _found is null; i++)
            {
                Visit(node.Arguments[i]);
            }

            if (_found is null && node.Object is not null)
            {
                Visit(node.Object);
            }

            return node;
        }
    }


    /// <summary>
    ///     Every navigation the residual reads, as a dotted path from the entity type it is
    ///     rooted at.
    /// </summary>
    /// <remarks>
    ///     Syntactic and deliberately over-approximate (§3.6): chains are followed only while
    ///     they are unbroken member accesses, and the result is keyed by the entity type read
    ///     from rather than by tracing where that entity came from. An extra <c>Include</c> costs
    ///     bandwidth; a missing one costs correctness.
    /// </remarks>
    private sealed class NavigationReadFinder(IModel model) : ExpressionVisitor
    {
        private readonly List<NavigationRead> _reads = [];

        public static List<NavigationRead> Find(Expression expression, IModel model)
        {
            var finder = new NavigationReadFinder(model);
            finder.Visit(expression);

            var reads = new List<NavigationRead>();
            foreach (NavigationRead read in finder._reads)
            {
                if (TruncateAtInverse(read) is { } kept
                    && !reads.Any(r => r.Owner == kept.Owner && r.Path == kept.Path))
                {
                    reads.Add(kept);
                }
            }

            // `b.Author.Books` records both "Author.Books" and, on the way down, "Author".
            // Including a path loads every step of it, so the prefix is dead weight on the wire.
            return
            [
                .. reads.Where(read => !reads.Any(
                    other => other.Owner == read.Owner
                        && other.Path.StartsWith(read.Path + ".", StringComparison.Ordinal))),
            ];
        }

        /// <summary>
        ///     Cuts a navigation path at the first step that walks straight back the way it came.
        /// </summary>
        /// <remarks>
        ///     A residual reading <c>p.Feet.Person.LastName</c> describes the path
        ///     <c>Feet.Person</c>, and <c>Feet.Person</c> is the inverse of <c>Person.Feet</c> — so
        ///     the `Include` this becomes is one EF refuses outright: "The navigation 'Feet.Person'
        ///     was ignored from 'Include' … Walking back include tree is not allowed", raised as an
        ///     error by the spec fixtures. It is also unnecessary, which is what the rest of that
        ///     message says: fixup wires the inverse as soon as the near side is loaded. Cutting
        ///     the path at that step asks for exactly the rows that are needed and nothing EF will
        ///     reject.
        /// </remarks>
        private static NavigationRead? TruncateAtInverse(NavigationRead read)
        {
            var kept = new List<string>();
            IEntityType current = read.Owner;
            INavigationBase? previous = null;

            foreach (string step in read.Path.Split('.'))
            {
                INavigationBase? navigation = current.FindNavigation(step)
                    ?? (INavigationBase?)current.FindSkipNavigation(step);

                if (navigation is null || (previous is not null && IsInverseOf(navigation, previous)))
                {
                    break;
                }

                kept.Add(step);
                previous = navigation;
                current = navigation.TargetEntityType;
            }

            return kept.Count == 0 ? null : read with { Path = string.Join('.', kept) };
        }

        private static bool IsInverseOf(INavigationBase navigation, INavigationBase previous)
            => navigation switch
            {
                INavigation reference => ReferenceEquals(reference.Inverse, previous),
                ISkipNavigation skip => ReferenceEquals(skip.Inverse, previous),
                _ => false,
            };

        protected override Expression VisitMember(MemberExpression node)
        {
            if (TryDescribe(node, out IEntityType? owner, out string? path)
                && !_reads.Any(r => r.Owner == owner && r.Path == path))
            {
                _reads.Add(new NavigationRead(owner!, path!));
            }

            return base.VisitMember(node);
        }

        private bool TryDescribe(MemberExpression node, out IEntityType? owner, out string? path)
        {
            owner = null;
            path = null;

            if (node.Expression is null)
            {
                return false;
            }

            IEntityType? declaring = model.FindEntityType(node.Expression.Type);
            if (declaring?.FindNavigation(node.Member.Name) is null
                && declaring?.FindSkipNavigation(node.Member.Name) is null)
            {
                return false;
            }

            owner = declaring;
            path = node.Member.Name;

            // Extend inward through an unbroken chain: `b.Author.Books` is one path on Book, not
            // a path on Book plus an unrelated one on Author.
            if (node.Expression is MemberExpression inner
                && TryDescribe(inner, out IEntityType? innerOwner, out string? innerPath))
            {
                owner = innerOwner;
                path = $"{innerPath}.{path}";
            }

            return true;
        }
    }
}
