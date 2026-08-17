// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Replaces a client-only carrier type that the query creates and consumes internally with a
///     <see cref="ValueTuple" />, so the operators above it stay on the server
///     (<c>docs/transparent-identifiers.md</c> §3.2,
///     [ADR-011](../../../docs/decisions.md#adr-011)).
/// </summary>
/// <remarks>
///     <para>
///         `from c in cs from o in c.Orders where … select c` has no anonymous type in it. The C#
///         compiler puts one there — <c>SelectMany(cs, c => c.Orders, (c, o) => new { c, o })</c> —
///         so that the later clauses can still see <c>c</c>. EF handles that natively; this
///         provider cannot, because an anonymous type is by definition absent from the server's
///         assembly, so [ADR-010](../../../docs/decisions.md#adr-010) makes it a type boundary and
///         every operator above it falls to the client. The client then reads a navigation the
///         server never sent, and the split refuses rather than answer <c>0</c>.
///     </para>
///     <para>
///         The condition this pass actually tests is not "is it a transparent identifier" — that
///         is a fact about the C# compiler, not about the tree — but the structural property that
///         matters: <b>the type is created inside the query and never reaches its result</b>. No
///         caller can observe it, so nothing is owed a reassembly and there is nothing to rebuild.
///         That is what separates this from the reassembly deferral that failed: that one had to
///         move a projection the caller had asked for, and prove each move safe.
///     </para>
///     <para>
///         Two things this pass does <em>not</em> decide. Whether the rewritten tree is an
///         improvement is <see cref="RewriteVerifier" />'s question, and this pass is expected to
///         be discarded whenever it is not. And whether the server can translate the result is
///         nobody's question here — server-ok is a type property, not a translatability one
///         (spec §4), which is why the phase is measured against the suite rather than argued for.
///     </para>
/// </remarks>
internal static class TransparentIdentifierRewriter
{
    /// <summary>
    ///     Rewrites the internal carriers of <paramref name="query" />, or returns it unchanged
    ///     when there are none.
    /// </summary>
    internal static Expression Rewrite(
        Expression query, TypeAllowlist allowlist, out Expression? rootRebuild)
    {
        rootRebuild = null;

        Dictionary<Type, MemberInfo[]> carriers = CarrierFinder.Find(
            query, allowlist, out IReadOnlySet<Type> referenceTyped, out Type? elementCarrier);

        if (carriers.Count == 0)
        {
            return query;
        }

        try
        {
            var rewriter = new Rewriter(carriers, referenceTyped);
            Expression rewritten = rewriter.Visit(query);

            if (elementCarrier is null)
            {
                return rewritten;
            }

            // Handed back so `ProjectionRewriter` leaves it alone. Both passes build the same
            // thing — a server-side tuple plus a client-side rebuild — and letting the second
            // one rewrite the first one's output ships a pointless tuple-to-tuple projection and
            // buries the `GroupBy` an operator deeper than it belongs.
            rootRebuild = rewriter.RebuildAtRoot(rewritten, elementCarrier);
            return rootRebuild;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            // A node kind this pass does not know how to retype. Rebuilding one with mismatched
            // types is exactly what the expression API refuses, so the refusal arrives here
            // rather than as a wrong answer — and the original tree is a perfectly good answer.
            // Deliberately not a silent success: the caller still verifies before keeping
            // anything, so a rewrite that cannot be built and one that does not help take the
            // same path.
            return query;
        }
    }

    private static bool IsSequence(Type type)
        => type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static Type MemberType(MemberInfo member)
        => member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException($"'{member.Name}' is neither a field nor a property."),
        };

    /// <summary>
    ///     Every type occurring in <paramref name="type" />, including through generic arguments.
    /// </summary>
    private static void CollectTypes(Type type, HashSet<Type> into)
    {
        if (!into.Add(type))
        {
            return;
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                CollectTypes(argument, into);
            }
        }

        if (type.GetElementType() is { } element)
        {
            CollectTypes(element, into);
        }
    }

    /// <summary>
    ///     Finds the carrier types worth replacing.
    /// </summary>
    private sealed class CarrierFinder(TypeAllowlist allowlist) : ExpressionVisitor
    {
        private readonly Dictionary<Type, MemberInfo[]> _candidates = [];
        private readonly HashSet<Type> _disqualified = [];
        private readonly HashSet<Type> _referenceTyped = [];

        /// <summary>
        ///     Carriers holding a sequence in a slot, and the types of rows an operator compares
        ///     whole. A carrier in both is disqualified; a carrier in only the first is not.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The guard used to be unconditional — <em>any</em> sequence in a slot
        ///         disqualified the carrier — because <c>t.Item2.DefaultIfEmpty()</c> asks SQL to
        ///         navigate out of a projected tuple back into a correlated collection, and 107
        ///         translation failures followed (spec §4). Re-measured in C75 the whole cost is
        ///         **eight tests in one family**, all of them
        ///         <c>Projecting_…_correlated_collection_followed_by_Distinct</c>, and all of them
        ///         failing on `InMemoryStrings.DistinctOnSubqueryNotSupported` — the *store*
        ///         refusing `Distinct` over a projection containing a subquery.
        ///     </para>
        ///     <para>
        ///         So the line is not "a sequence is in a slot" but "a sequence in a slot is part
        ///         of a row something compares". <c>FirstOrDefault()</c> on the slot reads it and
        ///         is fine; <c>Distinct</c> and the set operators compare the whole row, and no
        ///         store can compare one containing a subquery. That is the same argument Z6 made
        ///         for a client-only join key with no value equality, one level along.
        ///     </para>
        ///     <para>
        ///         A slot holding a <em>literal null</em> is neither, and never was: it is what
        ///         the group-join flattener leaves behind for a grouping nothing reads, and
        ///         refusing it costs the whole flattening.
        ///     </para>
        /// </remarks>
        private readonly HashSet<Type> _sequenceSlotted = [];

        private readonly HashSet<Type> _rowCompared = [];

        /// <summary>
        ///     Carriers found inside another carrier's construction, and the carrier they sit in.
        /// </summary>
        /// <remarks>
        ///     A nested carrier may only be retyped together with the one holding it. Rewriting the
        ///     inner half alone hands <see cref="Expression.New(ConstructorInfo, IEnumerable{Expression}, IEnumerable{MemberInfo})" />
        ///     a tuple where the outer constructor declares the original type, which throws — and
        ///     the catch in <see cref="Rewrite" /> would then discard the whole pass for a query
        ///     that was doing nothing wrong. So whatever removes a parent removes its children.
        /// </remarks>
        private readonly Dictionary<Type, Type> _nestedIn = [];

        public static Dictionary<Type, MemberInfo[]> Find(
            Expression query,
            TypeAllowlist allowlist,
            out IReadOnlySet<Type> referenceTyped,
            out Type? elementCarrier)
        {
            var finder = new CarrierFinder(allowlist);
            finder.Visit(query);
            referenceTyped = finder._referenceTyped;

            // The narrowed sequence-slot guard: a carrier holding a sequence is refused only
            // where an operator compares the whole row it is part of. See `_sequenceSlotted`.
            foreach (Type slotted in finder._sequenceSlotted)
            {
                if (finder._rowCompared.Contains(slotted))
                {
                    finder.Disqualify(slotted);
                }
            }

            // Any queryable, not the exact `IQueryable<>`. A query ending in `OrderBy(…).ThenBy(…)`
            // is an `IOrderedQueryable<>`, and testing the generic definition said "no element
            // carrier" — so the carrier was then struck out for being reachable from the result
            // type, and `Projecting_property_converted_to_nullable_and_use_it_in_order_by` stayed
            // on the client where its siblings had moved to the server (A40).
            elementCarrier = typeof(IQueryable).IsAssignableFrom(query.Type)
                && ServerBoundaryAnalyzer.ElementTypeOf(query.Type) is var element
                && element != query.Type
                && finder._candidates.ContainsKey(element)
                && !finder._disqualified.Contains(element)
                    ? element
                    : null;

            var reachable = new HashSet<Type>();
            CollectTypes(query.Type, reachable);

            if (elementCarrier is not null)
            {
                // Everything reachable only *through* the element carrier is rebuilt with it:
                // `RebuildAtRoot` recurses through nested carriers. Excluding those for being
                // "reachable from the result type" is what kept `new { Note, Nullable = cond ?
                // new { … } : null }` half-rewritten — the outer carrier became a tuple, the
                // inner one stayed anonymous because it is a *generic argument* of the outer,
                // and a tuple with an anonymous type in it ships no further than the anonymous
                // type did.
                var throughCarrier = new HashSet<Type>();
                CollectTypes(elementCarrier, throughCarrier);
                reachable.ExceptWith(throughCarrier);
            }

            foreach (Type type in reachable.Concat(finder._disqualified))
            {
                finder._candidates.Remove(type);
            }

            // A carrier whose enclosing carrier is gone has to go with it — and so does anything
            // nested inside *that*, hence the fixed point rather than one pass.
            bool removed = true;
            while (removed)
            {
                removed = false;
                foreach ((Type child, Type parent) in finder._nestedIn)
                {
                    if (!finder._candidates.ContainsKey(parent) && finder._candidates.Remove(child))
                    {
                        removed = true;
                    }
                }
            }

            return finder._candidates;
        }

        /// <summary>
        ///     A constant of the carrier type is either a value that already exists — which no
        ///     rewrite can turn into a tuple — or the <see langword="null" /> half of a comparison,
        ///     which is a different thing entirely.
        /// </summary>
        /// <remarks>
        ///     <c>Where(c =&gt; c.Orders.Select(o =&gt; new { o.OrderID }).First() == null)</c> builds
        ///     the carrier inside a <em>predicate</em>, so it never crosses the wire — but the
        ///     server still has to construct it, and treating the <see langword="null" /> as an
        ///     existing value disqualified the type and left the whole predicate on the client.
        ///     There LINQ-to-Objects applies <c>First</c> strictly and throws "Sequence contains no
        ///     elements", where SQL yields <see langword="null" /> and EF's own expected result is
        ///     the <c>FirstOrDefault</c> one.
        /// </remarks>
        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is not null)
            {
                Disqualify(node.Type);
            }

            return node;
        }

        /// <summary>
        ///     Records a carrier that is compared to <see langword="null" />, so it can be given a
        ///     reference-typed tuple instead of a <see cref="ValueTuple" />.
        /// </summary>
        /// <remarks>
        ///     The null has to be found through the comparison rather than through its own type:
        ///     the C# compiler emits <c>anonymous == null</c> with the null constant typed
        ///     <see cref="object" />, so keying off the constant marks <see cref="object" /> and
        ///     the carrier is never recognised — measured as no movement at all before the check
        ///     moved here.
        /// </remarks>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                if (IsNull(node.Right))
                {
                    _referenceTyped.Add(node.Left.Type);
                }

                if (IsNull(node.Left))
                {
                    _referenceTyped.Add(node.Right.Type);
                }
            }

            return base.VisitBinary(node);
        }

        /// <summary>
        ///     Records a carrier that is a <em>branch</em> of a conditional with
        ///     <see langword="null" /> opposite it.
        /// </summary>
        /// <remarks>
        ///     The same situation as a carrier compared to null, reached differently:
        ///     <c>cond ? new { … } : null</c> has no <see cref="ValueTuple" /> form, because a
        ///     <see cref="ValueTuple" /> cannot be null and <see cref="Expression.Condition(Expression, Expression, Expression)" />
        ///     refuses the mismatch — which the catch in <see cref="Rewrite" /> then turns into
        ///     "no rewrite at all". Fourth trigger, after the null comparison, the
        ///     absence-producing operator and the <c>class</c> constraint.
        /// </remarks>
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            if (IsNull(node.IfTrue) || IsNull(node.IfFalse))
            {
                _referenceTyped.Add(node.Type);
                _referenceTyped.Add(node.IfTrue.Type);
                _referenceTyped.Add(node.IfFalse.Type);
            }

            return base.VisitConditional(node);
        }

        private static bool IsNull(Expression node)
        {
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
        ///     A widening conversion hides the carrier from the query's signature while the value
        ///     still reaches the caller.
        /// </summary>
        /// <remarks>
        ///     Measured: <c>select new { c, o }</c> followed by <c>.Cast&lt;object&gt;()</c> makes
        ///     the query an <c>IQueryable&lt;object&gt;</c>, so the "does the carrier reach the
        ///     result type" test says no — and the caller receives a boxed tuple where it asked
        ///     for the anonymous type. Checking the declared result type is therefore not enough;
        ///     the conversion out of the carrier has to be caught where it happens.
        /// </remarks>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
                    or ExpressionType.TypeAs
                && node.Type != node.Operand.Type)
            {
                Disqualify(node.Operand.Type);
            }

            return base.VisitUnary(node);
        }

        private void Disqualify(Type type)
        {
            var types = new HashSet<Type>();
            CollectTypes(type, types);
            _disqualified.UnionWith(types);
        }

        /// <summary>
        ///     Operators whose semantics are row equality: they compare elements to each other
        ///     rather than reading a member out of one.
        /// </summary>
        private static readonly HashSet<string> RowComparing =
        [
            nameof(Queryable.Distinct),
            nameof(Queryable.DistinctBy),
            nameof(Queryable.Union),
            nameof(Queryable.UnionBy),
            nameof(Queryable.Intersect),
            nameof(Queryable.IntersectBy),
            nameof(Queryable.Except),
            nameof(Queryable.ExceptBy),
            nameof(Queryable.Contains),
            nameof(Queryable.SequenceEqual),
        ];

        private static readonly HashSet<string> AbsenceProducing =
        [
            nameof(Queryable.FirstOrDefault),
            nameof(Queryable.SingleOrDefault),
            nameof(Queryable.LastOrDefault),
            nameof(Queryable.ElementAtOrDefault),
            nameof(Queryable.DefaultIfEmpty),
        ];

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // An operator whose whole job is to answer "there was no row" needs a carrier that
            // can say so. `FirstOrDefault` over a `ValueTuple<string, int>` yields `(null, 0)` —
            // a row that looks real — where the anonymous type it replaced yielded `null`.
            // Measured as "Nullable object must have a value" downstream, which names neither
            // the carrier nor the operator. Same rule as a null comparison, different trigger.
            if (node.Method.DeclaringType is { } source
                && (source == typeof(Queryable) || source == typeof(Enumerable))
                && AbsenceProducing.Contains(node.Method.Name)
                && node.Arguments.Count > 0)
            {
                _referenceTyped.Add(ServerBoundaryAnalyzer.SequenceElementType(node.Arguments[0].Type));
            }

            // Operators that compare a whole row rather than reading a member of it. Recorded
            // rather than acted on: whether it matters depends on whether the row turns out to be
            // a carrier with a sequence in a slot, which `Find` resolves once the walk is done.
            if (node.Method.DeclaringType is { } comparer
                && (comparer == typeof(Queryable) || comparer == typeof(Enumerable))
                && RowComparing.Contains(node.Method.Name)
                && node.Arguments.Count > 0)
            {
                CollectTypes(ServerBoundaryAnalyzer.SequenceElementType(node.Arguments[0].Type), _rowCompared);
            }

            // The same escape as a conversion, spelled as an operator.
            if (node.Method.DeclaringType is { } declaring
                && (declaring == typeof(Queryable) || declaring == typeof(Enumerable))
                && node.Method.Name is nameof(Queryable.Cast) or nameof(Queryable.OfType)
                && node.Arguments.Count > 0)
            {
                Disqualify(ServerBoundaryAnalyzer.SequenceElementType(node.Arguments[0].Type));
            }

            // A type argument to a `where TEntity : class` method has to stay a reference type.
            // Every one of EF's queryable markers -- `AsNoTracking`, `IgnoreQueryFilters`,
            // `AsSplitQuery` -- carries that constraint, so an element carrier under one of them
            // cannot become a `ValueTuple`: `MakeGenericMethod` throws, the catch in `Rewrite`
            // discards the whole pass, and the query loses a re-carry the marker has nothing to do
            // with. Measured as the four `ManyToManyNoTracking` parameterizations of
            // `Left_join_with_skip_navigation` failing where the tracking four passed, on the same
            // query. Same rule as a null comparison, third trigger.
            if (node.Method.IsGenericMethod)
            {
                Type[] arguments = node.Method.GetGenericArguments();
                Type[] parameters = node.Method.GetGenericMethodDefinition().GetGenericArguments();

                for (int i = 0; i < arguments.Length; i++)
                {
                    if (parameters[i].GenericParameterAttributes
                        .HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                    {
                        _referenceTyped.Add(arguments[i]);
                    }
                }
            }

            if (ProjectionRewriter.IsResultSelectorOperator(node, out LambdaExpression? selector))
            {
                Register(selector!.Body, parent: null);
            }

            // A join *key* is a carrier too, and it is the body of no result selector.
            // `join l2 in … on new { A, B } equals new { A, B }` builds an anonymous type that is
            // created inside the query and never reaches its result — exactly the structural
            // property this pass is about — but registering only result-selector bodies left it a
            // client type. The whole join then fell to the client, where its key selectors read
            // the shadow FKs the keys are made of and `ClientPropertyReader` refused: four
            // `Join_condition_optimizations_applied_correctly_when_anonymous_type_*` failures
            // naming a shadow property and not the join.
            if (node.Method.DeclaringType is { } owner
                && (owner == typeof(Queryable) || owner == typeof(Enumerable))
                && node.Method.Name is nameof(Queryable.Join) or nameof(Queryable.GroupJoin)
                && node.Arguments.Count >= 4)
            {
                RegisterKeySelector(node.Arguments[2], asJoinKey: true);
                RegisterKeySelector(node.Arguments[3], asJoinKey: true);
            }

            // And so is a `GroupBy` key or element selector, for the same reason.
            // `GroupBy(t => new { t.Gear.HasSoulPatch, t.Gear.Squad.Name })` left the whole
            // grouping on the client, where the key selector dereferenced the optional navigation
            // it is made of. Every lambda argument, because `GroupBy` overloads its 3rd and 4th
            // between an element selector and a result selector — and the result selector is
            // already registered above, harmlessly twice.
            if (node.Method.DeclaringType is { } grouper
                && (grouper == typeof(Queryable) || grouper == typeof(Enumerable))
                && node.Method.Name == nameof(Queryable.GroupBy))
            {
                for (int i = 1; i < node.Arguments.Count; i++)
                {
                    RegisterKeySelector(node.Arguments[i]);
                }
            }

            return base.VisitMethodCall(node);
        }

        /// <summary>
        ///     Registers a key selector's body as a carrier.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b><paramref name="asJoinKey" /> forces the reference-typed tuple, and it is a
        ///         measured requirement rather than a preference (J10).</b> EF's relational
        ///         translation accepts an <em>anonymous type</em> as a join key and refuses a
        ///         <see cref="ValueTuple" /> — even one built with
        ///         <see cref="NewExpression.Members" /> supplied, which is the thing that makes a
        ///         carrier transparent everywhere else. So the re-carry that lets a join stay on
        ///         the server was simultaneously making the join untranslatable.
        ///     </para>
        ///     <para>
        ///         <b>Proved outside this provider entirely.</b> A plain SQLite context, the same
        ///         join written three ways: anonymous key <c>TRANSLATED</c>, `ValueTuple` key
        ///         <c>InvalidOperationException … could not be translated</c>, <c>Tuple</c> key
        ///         <c>TRANSLATED</c>. Nothing of ours was in the probe, so the limitation is EF's
        ///         and the defect was ours only in choosing the shape that trips it.
        ///     </para>
        ///     <para>
        ///         <b>The failure was hidden behind a misleading message.</b> EF renders the key it
        ///         cannot decompose as <c>(object)new ValueTuple&lt;…&gt;(…)</c>, which reads as
        ///         "somebody boxed this". Nobody did: the tree this provider ships and the tree the
        ///         server rebinds are both clean, verified by probing all four stages. The
        ///         <c>(object)</c> is EF's own rendering of its refusal.
        ///     </para>
        ///     <para>
        ///         Deliberately <em>not</em> applied to a <c>GroupBy</c> key: nothing has been
        ///         measured about those, and the reference tuple is a different type with different
        ///         null behaviour. One evidenced change at a time.
        ///     </para>
        /// </remarks>
        private void RegisterKeySelector(Expression argument, bool asJoinKey = false)
        {
            while (argument is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            {
                argument = quote.Operand;
            }

            if (argument is LambdaExpression key)
            {
                Register(key.Body, parent: null);

                if (asJoinKey)
                {
                    _referenceTyped.Add(key.Body.Type);
                }
            }
        }

        /// <summary>
        ///     Records <paramref name="node" /> as a carrier if it is one, then does the same for
        ///     the carriers it holds.
        /// </summary>
        /// <remarks>
        ///     The nesting matters because the C# compiler builds one transparent identifier out of
        ///     another — <c>from … join … into g from … select</c> produces
        ///     <c>new { new { t, g }, s }</c> — and the inner one is not the body of any result
        ///     selector, so registering only the outermost construction left an anonymous type
        ///     inside the tuple and the whole chain on the client anyway.
        /// </remarks>
        private void Register(Expression node, Type? parent)
        {
            // A carrier can be built inside a conditional *branch* — `Nullable = x.GearNickName
            // != null ? new { x.Gear.Nickname, … } : null` — where it is the argument of no
            // construction and the body of no result selector. Left unregistered it stayed an
            // anonymous type inside the tuple, which made the enclosing carrier unusable too.
            if (node is ConditionalExpression branches)
            {
                Register(branches.IfTrue, parent);
                Register(branches.IfFalse, parent);
                return;
            }

            // A DTO written as an object initializer is the same thing spelled differently:
            // `new Level1Dto { Id = l1.Id, Level2 = cond ? null : new Level2Dto { … } }` creates a
            // type inside the query and never lets it reach the result, which is the whole test.
            // It arrives as a `MemberInitExpression`, and handling only `NewExpression` left the
            // anonymous-type sibling of this test passing (A40) and the DTO one on the client.
            if (node is MemberInitExpression { NewExpression.Arguments.Count: 0 } initializer
                && initializer.Bindings.Count > 0
                && !allowlist.IsAllowed(initializer.Type)
                && initializer.Bindings.All(b => b.BindingType == MemberBindingType.Assignment))
            {
                MemberAssignment[] assignments = [.. initializer.Bindings.Cast<MemberAssignment>()];

                if (assignments.Any(a => IsSequence(a.Expression.Type) && !IsNull(a.Expression)))
                {
                    _sequenceSlotted.Add(initializer.Type);
                }

                _candidates[initializer.Type] = [.. assignments.Select(a => a.Member)];
                if (parent is not null)
                {
                    _nestedIn[initializer.Type] = parent;
                }

                foreach (MemberAssignment assignment in assignments)
                {
                    Register(assignment.Expression, initializer.Type);
                }

                return;
            }

            if (node is not NewExpression { Members: { } members } construction
                || members.Count == 0
                || allowlist.IsAllowed(construction.Type))
            {
                return;
            }

            // The guard the reassembly deferral violated. A slot holding a sequence asks SQL to
            // navigate out of a projected tuple back into a correlated collection --
            // `t.Item2.DefaultIfEmpty()` -- and 107 translation failures followed (spec §4). A
            // slot holding a *literal null* asks for none of that: it is what the group-join
            // flattener leaves behind for a grouping nothing reads, and refusing it there costs
            // the whole flattening.
            if (construction.Arguments.Any(a => IsSequence(a.Type) && !IsNull(a)))
            {
                _sequenceSlotted.Add(construction.Type);
            }

            _candidates[construction.Type] = [.. members];
            if (parent is not null)
            {
                _nestedIn[construction.Type] = parent;
            }

            foreach (Expression argument in construction.Arguments)
            {
                Register(argument, construction.Type);
            }
        }
    }

    /// <summary>
    ///     Retypes the tree: carrier constructions become tuple constructions, member reads
    ///     through one become slot reads, and every type mentioning a carrier is mapped through.
    /// </summary>
    private sealed class Rewriter(
        IReadOnlyDictionary<Type, MemberInfo[]> carriers,
        IReadOnlySet<Type> referenceTyped) : ExpressionVisitor
    {
        private readonly Dictionary<Type, Type> _mapped = [];
        private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters
            = new(ReferenceEqualityComparer.Instance);

        private Type Map(Type type)
        {
            if (_mapped.TryGetValue(type, out Type? cached))
            {
                return cached;
            }

            Type result = Compute(type);
            _mapped[type] = result;
            return result;
        }

        private Type Compute(Type type)
        {
            if (carriers.TryGetValue(type, out MemberInfo[]? members))
            {
                // A carrier compared to null -- or handed to a `where TEntity : class` method --
                // has to stay a reference type, or the thing it is used for stops being
                // expressible at all.
                return TupleCarrier.MakeType(
                    [.. members.Select(m => Map(MemberType(m)))], referenceTyped.Contains(type));
            }

            if (type.IsGenericType)
            {
                Type[] original = type.GetGenericArguments();
                Type[] arguments = [.. original.Select(Map)];
                if (!arguments.SequenceEqual(original))
                {
                    return type.GetGenericTypeDefinition().MakeGenericType(arguments);
                }
            }

            return type;
        }

        public Expression RebuildAtRoot(Expression rewritten, Type elementCarrier)
            => Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Select),
                [Map(elementCarrier), elementCarrier],
                rewritten,
                Rebuilder(elementCarrier));

        private LambdaExpression Rebuilder(Type carrier)
        {
            var row = Expression.Parameter(Map(carrier), "row");
            return Expression.Lambda(Rebuild(row, carrier), row);
        }

        /// <summary>
        ///     Rebuilds the original type from a tuple, answering <see langword="null" /> when the
        ///     tuple itself is absent.
        /// </summary>
        /// <remarks>
        ///     A reference-typed carrier exists precisely so <c>FirstOrDefault</c> can say "no
        ///     row". Reading slots out of that <see langword="null" /> unconditionally turns the
        ///     answer it was meant to give into a <see cref="NullReferenceException" />, so the
        ///     rebuild has to pass the absence through rather than undo it.
        /// </remarks>
        private Expression Rebuild(Expression tuple, Type carrier)
        {
            MemberInfo[] members = carriers[carrier];

            var arguments = new Expression[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                Expression slot = TupleCarrier.Read(tuple, i);
                Type memberType = MemberType(members[i]);
                arguments[i] = carriers.ContainsKey(memberType) ? Rebuild(slot, memberType) : slot;
            }

            Expression rebuilt = Construct(carrier, members, arguments);

            return tuple.Type.IsValueType
                ? rebuilt
                : Expression.Condition(
                    Expression.Equal(tuple, Expression.Constant(null, tuple.Type)),
                    Expression.Constant(null, carrier),
                    rebuilt);
        }

        /// <summary>
        ///     Rebuilds a carrier from its slots, the way the query built it.
        /// </summary>
        /// <remarks>
        ///     An anonymous type has no parameterless constructor and is built by the constructor
        ///     that takes every member; a DTO written as an object initializer has one and is built
        ///     by assignment. The absence of a parameterless constructor is the discriminator,
        ///     because a <c>NewExpression.Members</c> is only ever populated for the former.
        /// </remarks>
        private static Expression Construct(Type carrier, MemberInfo[] members, Expression[] arguments)
            => carrier.GetConstructor(Type.EmptyTypes) is { } parameterless
                ? Expression.MemberInit(
                    Expression.New(parameterless),
                    [.. members.Select((m, i) => (MemberBinding)Expression.Bind(m, arguments[i]))])
                : Expression.New(carrier.GetConstructors()[0], arguments, members);

        protected override Expression VisitNew(NewExpression node)
            => carriers.ContainsKey(node.Type)
                ? TupleCarrier.New(
                    [.. node.Arguments.Select(a => Visit(a))], referenceTyped.Contains(node.Type))
                : base.VisitNew(node);

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            if (!carriers.TryGetValue(node.Type, out MemberInfo[]? members))
            {
                return base.VisitMemberInit(node);
            }

            // Slot order is the order recorded in `carriers`, not this initializer's binding
            // order: two sites can initialize the same DTO's members in different orders, and a
            // tuple whose slots disagree would be a wrong answer rather than a refused rewrite.
            Dictionary<string, Expression> bound = node.Bindings
                .Cast<MemberAssignment>()
                .ToDictionary(b => b.Member.Name, b => b.Expression);

            var arguments = new Expression[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                arguments[i] = bound.TryGetValue(members[i].Name, out Expression? value)
                    ? Visit(value)
                    // Caught by `Rewrite`, which keeps the original tree. A carrier initialized
                    // with a different member set at two sites is not one this pass can retype.
                    : throw new InvalidOperationException(
                        $"'{node.Type}' is initialized without '{members[i].Name}' here.");
            }

            return TupleCarrier.New(arguments, referenceTyped.Contains(node.Type));
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // The other half of the comparison: a typed null has to be retyped with it.
            Type mapped = Map(node.Type);

            return mapped == node.Type ? node : Expression.Constant(node.Value, mapped);
        }

        /// <summary>
        ///     Retypes a conditional whose branches are carriers.
        /// </summary>
        /// <remarks>
        ///     <see cref="ExpressionVisitor.VisitConditional" /> rebuilds through
        ///     <c>node.Update</c>, which keeps the <em>original</em> node type — so
        ///     <c>cond ? new { … } : null</c> with both branches retyped went to
        ///     <see cref="Expression.Condition(Expression, Expression, Expression, Type)" /> still
        ///     declaring the anonymous type, and "Argument types do not match" discarded the whole
        ///     pass through the catch in <see cref="Rewrite" />.
        /// </remarks>
        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Type mapped = Map(node.Type);

            return mapped == node.Type
                ? base.VisitConditional(node)
                : Expression.Condition(Visit(node.Test), Visit(node.IfTrue), Visit(node.IfFalse), mapped);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is { } inner
                && carriers.TryGetValue(inner.Type, out MemberInfo[]? members)
                && Array.FindIndex(members, m => m.Name == node.Member.Name) is >= 0 and int slot)
            {
                return TupleCarrier.Read(Visit(inner), slot);
            }

            // A member whose *declaring* type merely mentions a carrier — `g.Key`, where `g` is an
            // `IGrouping<TKey, …>` and `TKey` is one. `node.Update` keeps the original
            // `MemberInfo`, which is not declared on the mapped type, and `Expression` refuses it.
            // Re-resolving by name is exact here: the mapped type is the same generic definition.
            if (node.Expression is { } target && Map(target.Type) != target.Type)
            {
                return Expression.PropertyOrField(Visit(target), node.Member.Name);
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_parameters.TryGetValue(node, out ParameterExpression? replacement))
            {
                return replacement;
            }

            Type mapped = Map(node.Type);
            if (mapped == node.Type)
            {
                return node;
            }

            replacement = Expression.Parameter(mapped, node.Name);
            _parameters[node] = replacement;
            return replacement;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // The parameters have to be rewritten before the body, or the body's references to
            // them would each mint a different replacement.
            ParameterExpression[] parameters = [.. node.Parameters.Select(p => (ParameterExpression)VisitParameter(p))];
            Expression body = Visit(node.Body);

            if (ReferenceEquals(body, node.Body) && parameters.SequenceEqual(node.Parameters))
            {
                return node;
            }

            // Map the delegate type rather than letting `Expression.Lambda` re-infer it from the
            // body. `SelectMany`'s collection selector is declared
            // `Func<TSource, IEnumerable<TCollection>>` while its body — a collection navigation —
            // is an `ICollection<TCollection>`; inference produces the narrower delegate, the
            // rebuilt call no longer matches the operator's parameter, and the whole rewrite is
            // discarded by the catch in `Rewrite`. That is what kept the family this phase exists
            // for from moving at all.
            return Expression.Lambda(Map(node.Type), body, node.Name, node.TailCall, parameters);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Expression operand = Visit(node.Operand);
            Type type = Map(node.Type);

            if (ReferenceEquals(operand, node.Operand) && type == node.Type)
            {
                return node;
            }

            // `Update` keeps the original type, which is the whole thing that changed. A quoted
            // lambda is the common case: its type is `Expression<Func<carrier, …>>`.
            return node.NodeType == ExpressionType.Quote
                ? Expression.Quote(operand)
                : Expression.MakeUnary(node.NodeType, operand, type, node.Method);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression? instance = Visit(node.Object);
            Expression[] arguments = [.. node.Arguments.Select(a => Visit(a))];

            MethodInfo method = node.Method;
            if (method.IsGenericMethod)
            {
                Type[] original = method.GetGenericArguments();
                Type[] generics = [.. original.Select(Map)];
                if (!generics.SequenceEqual(original))
                {
                    method = method.GetGenericMethodDefinition().MakeGenericMethod(generics);
                }
            }

            return ReferenceEquals(method, node.Method)
                && ReferenceEquals(instance, node.Object)
                && arguments.SequenceEqual(node.Arguments)
                    ? node
                    : Expression.Call(instance, method, arguments);
        }
    }
}
