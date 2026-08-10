// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Splits a projection lambda into a server-side value projection and a client-side
///     reassembly (<c>docs/projection-split.md</c> §3.2).
/// </summary>
/// <remarks>
///     <para>
///         Cutting the query above a client-typed projection is not enough, and for three
///         separate reasons it is not even correct:
///     </para>
///     <list type="bullet">
///         <item>
///             <c>Select(c =&gt; new { c.City, c.Orders.Count })</c> cut at the projection ships
///             customers whose orders were never loaded, and the client answers <b>0</b>.
///         </item>
///         <item>
///             A correlated subquery in the projection body cannot be evaluated on the client at
///             all without issuing one query per row.
///         </item>
///         <item>
///             <c>GroupBy(…).Select(g =&gt; new { g.Key, g.Count() })</c> cut between the two
///             leaves a bare, non-composed <c>GroupBy</c> — which no provider can translate. The
///             cut <em>creates</em> that failure; the original query was fine.
///         </item>
///     </list>
///     <para>
///         So the projection is rewritten rather than cut. The body's maximal server-evaluable
///         subexpressions travel as a tuple, and the client rebuilds its own types from the tuple
///         slots. The server does the work it was always going to do, and only the values the
///         projection needs are on the wire — which is also wire-protocol W1.
///     </para>
/// </remarks>
internal sealed class ProjectionRewriter(ServerBoundaryAnalyzer analyzer) : ExpressionVisitor
{
    private static readonly MethodInfo QueryableSelect = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Queryable.Select)
            && m.GetGenericArguments().Length == 2
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments()[0].GetGenericArguments().Length == 2);

    private static readonly MethodInfo EnumerableSelect = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.Select)
            && m.GetGenericArguments().Length == 2
            && m.GetParameters() is [_, { ParameterType: { IsGenericType: true } second }]
            && second.GetGenericArguments().Length == 2);

    private static readonly MethodInfo EnumerableToList = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Enumerable.ToList) && m.GetParameters().Length == 1);

    private static readonly MethodInfo QueryableAsQueryable = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Queryable.AsQueryable) && m.IsGenericMethodDefinition);

    private readonly HashSet<Expression> _reassemblies = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    ///     Every member the query reads anywhere, by declaring type and name.
    /// </summary>
    /// <remarks>
    ///     Whether a slot is read is not a question the selector building it can answer — the read
    ///     is in the <em>next</em> operator up, and this pass works innermost-first. So it is asked
    ///     of the whole tree, once, before any rewriting. See <see cref="IsQueryableCollection" />.
    /// </remarks>
    private IReadOnlySet<(Type, string)> _read = new HashSet<(Type, string)>();

    /// <summary>
    ///     A client-side rebuild another pass already produced. Rewriting it would only wrap one
    ///     carrier in another.
    /// </summary>
    private Expression? _preserved;

    /// <summary>
    ///     Rewrites every client-typed projection in <paramref name="query" /> that sits directly
    ///     on a server-executable source.
    /// </summary>
    /// <param name="query">The captured query, after parameter substitution.</param>
    /// <param name="analyzer">Decides what the server can express.</param>
    /// <param name="reassemblies">
    ///     The client-side <c>Select</c> nodes this pass introduced. They are the <em>only</em>
    ///     client-side operators a split is allowed to produce; see
    ///     <see cref="QuerySplitter" />'s client-evaluation guard.
    /// </param>
    public static Expression Rewrite(
        Expression query,
        ServerBoundaryAnalyzer analyzer,
        out IReadOnlySet<Expression> reassemblies,
        Expression? alreadyReassembled = null)
    {
        var rewriter = new ProjectionRewriter(analyzer)
        {
            _preserved = alreadyReassembled,
            _read = MemberReadCollector.Find(query),
        };
        Expression result = rewriter.Visit(query);
        reassemblies = rewriter._reassemblies;
        return result;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Checked before descending, because this shape has to *replace* the in-place rewrite
        // rather than tidy up after it — see TryHoistCollectionProjection.
        if (TryHoistCollectionProjection(node) is { } hoisted)
        {
            return hoisted;
        }

        // Innermost first: rewriting an inner projection can turn its result into something the
        // outer one can then be measured against.
        if (base.VisitMethodCall(node) is not MethodCallExpression call)
        {
            return node;
        }

        if (ReferenceEquals(node, _preserved))
        {
            _reassemblies.Add(call);
            return call;
        }

        if (!IsResultSelectorOperator(call, out LambdaExpression? selector))
        {
            return call;
        }

        // Everything but the selector — sources, key selectors — has to travel, or there is
        // nothing to hand the server.
        for (int i = 0; i < call.Arguments.Count - 1; i++)
        {
            Expression argument = call.Arguments[i];
            if (!analyzer.Analyze(argument).FactsFor(argument).ServerOk)
            {
                return call;
            }
        }

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(selector!);
        if (bodyAnalysis.FactsFor(selector.Body).ServerOk)
        {
            // The projection is server-executable as written.
            return call;
        }

        List<Expression> fragments = [];
        var guards = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        CollectFragments(selector!.Body, bodyAnalysis, selector.Parameters, fragments, guards);
        if (fragments.Count == 0)
        {
            // A body that reads nothing from the row — leave it to the plain cut.
            return call;
        }

        IReadOnlySet<Expression> consumed = Consumed(selector.Body);

        Expression tuple = TupleCarrier.New([.. fragments.Select(f => Guarded(Materialized(f, consumed), f, guards))]);
        ParameterExpression row = Expression.Parameter(tuple.Type, "row");

        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = Requeryable(TupleCarrier.Read(row, i), fragments[i], consumed);
        }

        Expression clientBody = new SlotSubstitutingVisitor(slots).Visit(selector.Body)!;
        if (selector.Parameters.Any(p => ReferencesParameter(clientBody, p)))
        {
            // A row value the server could not carry — a parameter of a type it does not know.
            // Leaving it would build a lambda with an unbound parameter; the cut handles it.
            return call;
        }

        Type[] genericArguments = call.Method.GetGenericArguments();
        genericArguments[^1] = tuple.Type;

        // A nested projection over a collection navigation is an Enumerable call whose lambdas
        // are plain Funcs, not quoted expression trees. Rebuilding it in the Queryable flavour
        // makes Expression.Call reject the argument.
        bool quoted = call.Arguments[^1] is UnaryExpression { NodeType: ExpressionType.Quote };
        Expression Wrap(LambdaExpression lambda) => quoted ? Expression.Quote(lambda) : lambda;

        MethodCallExpression serverCall = Expression.Call(
            call.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArguments),
            [
                .. call.Arguments.Take(call.Arguments.Count - 1),
                Wrap(Expression.Lambda(tuple, selector.Parameters)),
            ]);

        // Built with an explicit delegate type rather than inferred from the body, because the two
        // can legitimately differ: C# lets `Select(x => new { … })` typed `Func<T, object>` carry a
        // body whose own type is the anonymous one, and `LambdaExpression.ReturnType` is what the
        // *operator* was instantiated with. Inferring gave `Func<row, <>f__AnonymousType>` where
        // `Select<row, object>` wanted `Func<row, object>`, and `Expression.Call` rejected it
        // outright — `Multiple_single_result_in_projection_containing_owned_types`, both
        // parameterizations, before the query reached the wire at all.
        MethodCallExpression reassembly = Expression.Call(
            (quoted ? QueryableSelect : EnumerableSelect)
                .MakeGenericMethod(tuple.Type, selector.ReturnType),
            serverCall,
            Wrap(
                Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(row.Type, selector.ReturnType), clientBody, row)));

        _reassemblies.Add(reassembly);
        return reassembly;
    }

    /// <summary>
    ///     Whether a call's last argument is the selector that <em>produces its element type</em> —
    ///     <c>Select</c>, <c>SelectMany</c>, <c>Join</c>, <c>GroupJoin</c>, <c>GroupBy</c> with a
    ///     result selector, <c>Zip</c>.
    /// </summary>
    /// <remarks>
    ///     Recognised structurally rather than by name, so no list has to be kept in step with
    ///     <see cref="Queryable" />: the operator returns <c>IQueryable&lt;TResult&gt;</c> where
    ///     <c>TResult</c> is its last generic argument and the last argument's return type.
    ///     <para>
    ///         The structural test is what keeps <c>OrderBy</c> and <c>Where</c> out.
    ///         <c>OrderBy&lt;TSource, TKey&gt;</c> also ends in a lambda returning its last
    ///         generic argument, but it returns a sequence of <c>TSource</c> — rewriting it would
    ///         silently replace the elements with their sort keys.
    ///     </para>
    /// </remarks>
    internal static bool IsResultSelectorOperator(MethodCallExpression call, out LambdaExpression? selector)
    {
        selector = null;

        if (call.Method.DeclaringType is not { } declaring
            || (declaring != typeof(Queryable) && declaring != typeof(Enumerable))
            || !call.Method.IsGenericMethod
            || call.Arguments.Count < 2)
        {
            return false;
        }

        Type resultType = call.Method.GetGenericArguments()[^1];
        if (ServerBoundaryAnalyzer.SequenceElementType(call.Method.ReturnType) != resultType
            || StripQuotes(call.Arguments[^1]) is not LambdaExpression lambda
            || lambda.ReturnType != resultType)
        {
            return false;
        }

        selector = lambda;
        return true;
    }

    /// <summary>
    ///     Rewrites <c>SelectMany(source, c =&gt; inner.Select(o =&gt; clientType))</c> so the
    ///     reassembly sits <em>above</em> the <c>SelectMany</c> instead of inside its collection
    ///     selector.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Rewritten in place, the reassembly is a client-typed <c>Select</c> nested inside the
    ///         collection selector — which makes the whole <c>SelectMany</c> client-side, so the
    ///         source ships alone and the residual reads navigations off rows the server never
    ///         sent. That is the refusal
    ///         <c>SelectMany_with_client_eval_with_collection_shaper</c> and <c>…_ignored</c> hit.
    ///     </para>
    ///     <para>
    ///         Hoisting works only if the client half needs nothing but the row, so the fragments
    ///         are collected against the <em>outer</em> parameter as well as the inner one:
    ///         <c>c.ContactName</c> has to travel in a slot, because after the hoist <c>c</c> is no
    ///         longer in scope. That is the one thing the in-place rewrite cannot do, since there
    ///         the outer parameter is still available and there was never a reason to carry it.
    ///     </para>
    ///     <para>
    ///         Deliberately narrow. It matches one shape — the two-argument <c>SelectMany</c>,
    ///         which <see cref="IsResultSelectorOperator" /> does not even consider, because its
    ///         lambda returns <c>IEnumerable&lt;TResult&gt;</c> rather than <c>TResult</c>.
    ///     </para>
    /// </remarks>
    private Expression? TryHoistCollectionProjection(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(Queryable)
            || node.Method.Name != nameof(Queryable.SelectMany)
            || node.Arguments.Count != 2
            || !node.Method.IsGenericMethod
            || StripQuotes(node.Arguments[1]) is not LambdaExpression { Parameters: [var outer] } collectionSelector
            || collectionSelector.Body is not MethodCallExpression inner
            || !IsResultSelectorOperator(inner, out LambdaExpression? innerSelector))
        {
            return null;
        }

        Expression source = Visit(node.Arguments[0]);
        if (!analyzer.Analyze(source).FactsFor(source).ServerOk)
        {
            return null;
        }

        for (int i = 0; i < inner.Arguments.Count - 1; i++)
        {
            Expression argument = inner.Arguments[i];
            if (!analyzer.Analyze(argument).FactsFor(argument).ServerOk)
            {
                return null;
            }
        }

        BoundaryAnalysis bodyAnalysis = analyzer.Analyze(innerSelector!);
        if (bodyAnalysis.FactsFor(innerSelector.Body).ServerOk)
        {
            // Nothing client-typed here; the ordinary path ships it whole.
            return null;
        }

        ParameterExpression[] rowParameters = [.. innerSelector.Parameters, outer];

        List<Expression> fragments = [];
        var guards = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        CollectFragments(innerSelector.Body, bodyAnalysis, rowParameters, fragments, guards);
        if (fragments.Count == 0)
        {
            return null;
        }

        IReadOnlySet<Expression> consumed = Consumed(innerSelector.Body);

        Expression tuple = TupleCarrier.New([.. fragments.Select(f => Guarded(Materialized(f, consumed), f, guards))]);
        ParameterExpression row = Expression.Parameter(tuple.Type, "row");

        var slots = new Dictionary<Expression, Expression>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < fragments.Count; i++)
        {
            slots[fragments[i]] = Requeryable(TupleCarrier.Read(row, i), fragments[i], consumed);
        }

        Expression clientBody = new SlotSubstitutingVisitor(slots).Visit(innerSelector.Body)!;
        if (rowParameters.Any(p => ReferencesParameter(clientBody, p)))
        {
            // Something the server could not carry. Hoisting would strand it, and the in-place
            // rewrite at least still answers.
            return null;
        }

        Type[] innerGenerics = inner.Method.GetGenericArguments();
        innerGenerics[^1] = tuple.Type;

        bool quoted = inner.Arguments[^1] is UnaryExpression { NodeType: ExpressionType.Quote };
        Expression Wrap(LambdaExpression lambda) => quoted ? Expression.Quote(lambda) : lambda;

        MethodCallExpression serverInner = Expression.Call(
            inner.Method.GetGenericMethodDefinition().MakeGenericMethod(innerGenerics),
            [.. inner.Arguments.Take(inner.Arguments.Count - 1), Wrap(Expression.Lambda(tuple, innerSelector.Parameters))]);

        Type[] outerGenerics = node.Method.GetGenericArguments();
        outerGenerics[^1] = tuple.Type;

        MethodCallExpression serverCall = Expression.Call(
            node.Method.GetGenericMethodDefinition().MakeGenericMethod(outerGenerics),
            source,
            Expression.Quote(Expression.Lambda(serverInner, outer)));

        MethodCallExpression reassembly = Expression.Call(
            QueryableSelect.MakeGenericMethod(tuple.Type, innerSelector.ReturnType),
            serverCall,
            Expression.Quote(Expression.Lambda(clientBody, row)));

        _reassemblies.Add(reassembly);
        return reassembly;
    }

    /// <summary>
    ///     Everything in <paramref name="body" /> that something else reads: a value handed to a
    ///     query operator, and a slot of a constructed row whose member the query reads elsewhere.
    /// </summary>
    private IReadOnlySet<Expression> Consumed(Expression body)
    {
        var consumed = new HashSet<Expression>(
            OperatorSourceCollector.Find(body), ReferenceEqualityComparer.Instance);

        AddReadSlots(body, consumed);

        return consumed;
    }

    private void AddReadSlots(Expression body, HashSet<Expression> consumed)
    {
        switch (body)
        {
            case NewExpression { Members: { } members } constructed:
                for (int i = 0; i < members.Count && i < constructed.Arguments.Count; i++)
                {
                    if (IsRead(members[i]))
                    {
                        consumed.Add(constructed.Arguments[i]);
                    }
                }

                break;

            case MemberInitExpression init:
                AddReadSlots(init.NewExpression, consumed);
                foreach (MemberBinding binding in init.Bindings)
                {
                    if (binding is MemberAssignment assignment && IsRead(assignment.Member))
                    {
                        consumed.Add(assignment.Expression);
                    }
                }

                break;
        }
    }

    private bool IsRead(MemberInfo member)
        => member.DeclaringType is { } declaring && _read.Contains((declaring, member.Name));

    /// <summary>
    ///     Whether a fragment is a queryable collection, which cannot travel as one.
    /// </summary>
    /// <remarks>
    ///     Deliberately the exact <see cref="IQueryable{T}" /> and nothing derived from it. An
    ///     <c>IOrderedQueryable&lt;T&gt;</c> fragment may have a <c>ThenBy</c> above it, and handing
    ///     that an <see cref="IQueryable{T}" /> back would fail to rebuild the enclosing call.
    /// </remarks>
    private static bool IsQueryableCollection(Expression fragment, IReadOnlySet<Expression> consumed)
        => fragment.Type.IsGenericType
            && fragment.Type.GetGenericTypeDefinition() == typeof(IQueryable<>)
            // Only when something *reads* it. A queryable handed straight to the result is what the
            // caller asked for, and EF is right to refuse it — three spec tests assert exactly that
            // error (`AssertInvalidMaterializationType`) and materializing here suppresses it.
            //
            // Two kinds of read count, and they are found in different places. An operator applied
            // in the projection body itself — `frag.Select(…)` — is `consumed`. A slot of a row the
            // body constructs is read by the *next* operator up, which this innermost-first pass has
            // not reached yet; `_read` is collected from the whole tree for that case, and it is what
            // separates `select new { l2, innerL1s }` followed by `ti => ti.innerL1s.ToList()` — a
            // `let`, an intermediate — from `select new { Subquery = q }`, where nothing ever looks
            // at `Subquery` and EF's refusal is the answer.
            && consumed.Contains(fragment);

    /// <summary>
    ///     Materializes a collection-valued fragment on its way into a tuple slot.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF refuses a final projection that returns an <see cref="IQueryable{T}" /> —
    ///         <c>CoreStrings</c>: "Collections in the final projection must be an
    ///         <c>IEnumerable&lt;T&gt;</c> type such as <c>List&lt;T&gt;</c>". The largest
    ///         server-evaluable fragment of
    ///         <c>Select(c =&gt; c.Orders.…Take(1).Select(…).ToList())</c> is the <c>Take(1)</c>
    ///         subquery, and putting it in a slot verbatim shipped exactly the projection EF
    ///         rejects — before running anything, so the failure named the projection and not this
    ///         rewrite.
    ///     </para>
    ///     <para>
    ///         This is the boundary on the rule phase E1 established. Descending past a
    ///         <c>ToList</c> is right at the <em>end of a query</em>, where it asks the server to
    ///         translate a materialization; it is wrong <em>inside a projection</em>, where EF
    ///         requires one. Same operator, opposite meaning, decided by position.
    ///     </para>
    /// </remarks>
    private static Expression Materialized(Expression fragment, IReadOnlySet<Expression> consumed)
        => IsQueryableCollection(fragment, consumed)
            ? Expression.Call(
                EnumerableToList.MakeGenericMethod(fragment.Type.GetGenericArguments()[0]),
                fragment)
            : UnbuildableNavigationElement(fragment) is { } element
                ? Expression.Call(EnumerableToList.MakeGenericMethod(element), fragment)
                : fragment;

    /// <summary>
    ///     The element type of a <em>bare navigation read</em> whose declared collection type the
    ///     store's shaper cannot build, or <see langword="null" /> when there is no such problem.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A slot carries a fragment with the type the user's model gave it, and a collection
    ///         navigation may be declared as something the shaper cannot fill.
    ///         <c>IReadOnlyList&lt;Name&gt;</c> is the spec's own — <c>OwnsMany_correlated_projection</c>
    ///         maps it with a <c>protected</c> setter — and InMemory's
    ///         <c>MaterializeCollection&lt;TElement, TCollection&gt;</c> constrains
    ///         <c>TCollection : class, ICollection&lt;TElement&gt;</c>, so the generic method
    ///         cannot even be closed over it: <c>VerificationException</c>, before a row is read.
    ///     </para>
    ///     <para>
    ///         EF never meets this in the user's own query because a projection returning a
    ///         collection ends in <c>ToList</c> or <c>ToArray</c> — its documented requirement. The
    ///         shape only arises because <em>this</em> rewrite is what puts the bare navigation in
    ///         a slot, so it is this rewrite that owes the materialization. A <c>List&lt;T&gt;</c>
    ///         satisfies every collection interface the body could have been written against, so
    ///         the client-side read needs no adjustment.
    ///     </para>
    ///     <para>
    ///         <b>A member read and nothing else.</b> Stated over any sequence type this cost 27
    ///         tests, and the two shapes it wrongly caught say why. An <c>IGrouping&lt;K, T&gt;</c>
    ///         is an <c>IEnumerable&lt;T&gt;</c> that is not an <c>ICollection&lt;T&gt;</c>, and
    ///         <c>ToList</c>-ing one throws its key away — twenty of the twenty-seven were
    ///         <c>GroupBy</c>. And <c>b.Posts1.OrderBy(p =&gt; p.Id)</c> in a final projection is
    ///         something EF <em>refuses</em>, which
    ///         <c>Collection_without_setter_materialized_correctly</c> asserts; materializing it
    ///         suppressed the refusal. Both are composed sequences, not member reads, which is the
    ///         line this test draws.
    ///     </para>
    ///     <para>
    ///         Deliberately not merged with <see cref="IsQueryableCollection" />: that rule turns
    ///         on whether anything <em>reads</em> the fragment, because an unread
    ///         <see cref="IQueryable{T}" /> in a final projection is another refusal three spec
    ///         tests assert. This one is about a type that cannot be built at all, read or not.
    ///     </para>
    /// </remarks>
    private static Type? UnbuildableNavigationElement(Expression fragment)
    {
        if (fragment is not MemberExpression)
        {
            return null;
        }

        Type type = fragment.Type;
        if (type == typeof(string) || type.IsArray || typeof(IQueryable).IsAssignableFrom(type))
        {
            return null;
        }

        Type element = ServerBoundaryAnalyzer.SequenceElementType(type);

        if (element == type || typeof(ICollection<>).MakeGenericType(element).IsAssignableFrom(type))
        {
            return null;
        }

        // …and only when a `List<T>` is actually a legal value for the declared type. The whole
        // remedy here is to substitute one, so a declared type it does not satisfy must be left
        // alone — otherwise this rewrite replaces the fragment with something that cannot stand
        // where it stood.
        //
        // `MultiLineString` is the case that found this: it implements `IEnumerable<Geometry>`,
        // so every test above passes, but it is a *domain type that happens to be enumerable*
        // rather than a collection. Slotting it as `List<Geometry>` left `e.MultiLineString[0]`
        // and `.Count` unable to bind — "Method 'get_Item' declared on type 'GeometryCollection'
        // cannot be called with instance of type 'List<Geometry>'" (C18's four).
        //
        // The types this method exists for are unaffected: `IReadOnlyList<Name>` is satisfied by
        // a `List<Name>`, which is the point of it.
        return type.IsAssignableFrom(typeof(List<>).MakeGenericType(element))
            ? element
            : null;
    }

    /// <summary>
    ///     Reads a materialized slot back as the queryable the client-side body was built against.
    /// </summary>
    /// <remarks>
    ///     The reassembly still holds the operators the projection wrote — <c>Select</c>,
    ///     <c>ToList</c> — bound to <see cref="IQueryable{T}" />. Substituting a
    ///     <see cref="List{T}" /> under them would not rebuild.
    /// </remarks>
    private static Expression Requeryable(
        Expression slot, Expression fragment, IReadOnlySet<Expression> consumed)
        => IsQueryableCollection(fragment, consumed)
            ? Expression.Call(
                QueryableAsQueryable.MakeGenericMethod(fragment.Type.GetGenericArguments()[0]),
                slot)
            : slot;

    /// <summary>
    ///     Every expression the projection body feeds to a query operator as its source.
    /// </summary>
    private sealed class OperatorSourceCollector : ExpressionVisitor
    {
        private readonly HashSet<Expression> _sources = new(ReferenceEqualityComparer.Instance);

        public static IReadOnlySet<Expression> Find(Expression body)
        {
            var collector = new OperatorSourceCollector();
            collector.Visit(body);
            return collector._sources;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType is { } declaring
                && (declaring == typeof(Queryable) || declaring == typeof(Enumerable))
                && node.Arguments.Count > 0)
            {
                _sources.Add(node.Arguments[0]);
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    ///     Every member the query reads, anywhere in it.
    /// </summary>
    /// <remarks>
    ///     By declaring type and name rather than by <see cref="MemberInfo" />: the member a
    ///     <see cref="NewExpression" /> records for a constructor argument and the one a
    ///     <see cref="MemberExpression" /> reads back are not required to be the same reflection
    ///     object — a property's getter and the property itself both name it.
    /// </remarks>
    private sealed class MemberReadCollector : ExpressionVisitor
    {
        private readonly HashSet<(Type, string)> _members = [];

        public static IReadOnlySet<(Type, string)> Find(Expression query)
        {
            var collector = new MemberReadCollector();
            collector.Visit(query);
            return collector._members;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member.DeclaringType is { } declaring)
            {
                _members.Add((declaring, node.Member.Name));
            }

            return base.VisitMember(node);
        }
    }

    private static bool ReferencesParameter(Expression expression, ParameterExpression parameter)
    {
        bool found = false;
        new ParameterFinder(parameter, () => found = true).Visit(expression);
        return found;
    }

    /// <summary>
    ///     The maximal subexpressions of a projection body that the server can evaluate for a row.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A fragment must read the row: a body constant needs no round trip and stays on the
    ///         client, where it costs nothing.
    ///     </para>
    ///     <para>
    ///         A fragment taken from the branch of a conditional carries that conditional's test in
    ///         <paramref name="guards" />. See <see cref="Guarded" /> for why.
    ///     </para>
    /// </remarks>
    private static void CollectFragments(
        Expression node,
        BoundaryAnalysis analysis,
        IReadOnlyCollection<ParameterExpression> rowParameters,
        List<Expression> fragments,
        IDictionary<Expression, Expression> guards,
        Expression? guard = null)
    {
        NodeFacts facts = analysis.FactsFor(node);

        if (facts.ServerOk && facts.Free.Count > 0 && facts.Free.All(rowParameters.Contains))
        {
            fragments.Add(node);
            if (guard is not null)
            {
                guards[node] = guard;
            }

            return;
        }

        if (node is ConditionalExpression conditional)
        {
            CollectFragments(conditional.Test, analysis, rowParameters, fragments, guards, guard);

            // A test the server cannot evaluate cannot guard anything, and refusing to lift the
            // branches under one is worse than lifting them unguarded: it costs six tests that
            // pass today — every one of them guarded by an *entity* compared to null, which this
            // analyzer will not ship — and fixes none. Measured, A36.
            NodeFacts test = analysis.FactsFor(conditional.Test);
            bool guardable = test.ServerOk && test.Free.All(rowParameters.Contains);

            CollectFragments(
                conditional.IfTrue, analysis, rowParameters, fragments, guards,
                guardable ? And(guard, conditional.Test) : guard);
            CollectFragments(
                conditional.IfFalse, analysis, rowParameters, fragments, guards,
                guardable ? And(guard, Expression.Not(conditional.Test)) : guard);
            return;
        }

        foreach (Expression child in ChildrenOf(node))
        {
            CollectFragments(child, analysis, rowParameters, fragments, guards, guard);
        }
    }

    private static Expression And(Expression? guard, Expression test)
        => guard is null ? test : Expression.AndAlso(guard, test);

    /// <summary>
    ///     Wraps a fragment lifted out of a conditional branch in the test that guarded it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Select(x =&gt; new { x.Note, Nullable = x.GearNickName != null ? new { x.Gear.Nickname,
    ///         x.Gear.SquadId } : null })</c> is client-typed at the conditional, so
    ///         <see cref="CollectFragments" /> descends through it and takes <c>x.Gear.Nickname</c>
    ///         and <c>x.Gear.SquadId</c> as fragments of their own — <em>outside the test that was
    ///         guarding them</em>. The server then evaluates <c>x.Gear.SquadId</c> for a tag with no
    ///         gear, which is exactly the dereference the <c>!= null</c> existed to prevent:
    ///         <c>Nullable object must have a value</c>, 26 times over
    ///         <c>GearsOfWarQueryTestBase</c>.
    ///     </para>
    ///     <para>
    ///         The slot travels as <c>test ? fragment : default</c>, so the server evaluates it only
    ///         where the projection would have. The client body still holds the conditional and only
    ///         reads the slot down the branch it belongs to, so the default is never observed.
    ///     </para>
    ///     <para>
    ///         The default is a <see cref="ConstantExpression" /> and not
    ///         <see cref="Expression.Default(Type)" />: a <c>DefaultExpression</c> is not one of the
    ///         serializable kinds (research-findings §5), so a guard built from one made the whole
    ///         rewritten call unshippable — six tests fell back to the residual, where the
    ///         navigation they read had no query to carry it.
    ///     </para>
    /// </remarks>
    private static Expression Guarded(
        Expression shipped, Expression fragment, IReadOnlyDictionary<Expression, Expression> guards)
        => guards.TryGetValue(fragment, out Expression? guard)
            ? Expression.Condition(
                guard,
                shipped,
                Expression.Constant(
                    shipped.Type.IsValueType ? Activator.CreateInstance(shipped.Type) : null, shipped.Type))
            : shipped;

    private static IEnumerable<Expression> ChildrenOf(Expression node)
    {
        var children = new List<Expression>();
        new ChildCollector(children).Visit(node);
        return children;
    }

    private static Expression StripQuotes(Expression node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            node = quote.Operand;
        }

        return node;
    }

    private sealed class SlotSubstitutingVisitor(IReadOnlyDictionary<Expression, Expression> slots)
        : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
            => node is not null && slots.TryGetValue(node, out Expression? slot)
                ? slot
                : base.Visit(node);
    }

    private sealed class ParameterFinder(ParameterExpression parameter, Action onFound) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node == parameter)
            {
                onFound();
            }

            return node;
        }
    }

    private sealed class ChildCollector(ICollection<Expression> children) : ExpressionVisitor
    {
        private bool _atRoot = true;

        public override Expression? Visit(Expression? node)
        {
            if (node is null)
            {
                return null;
            }

            if (_atRoot)
            {
                _atRoot = false;
                return base.Visit(node);
            }

            children.Add(node);
            return node;
        }
    }
}
