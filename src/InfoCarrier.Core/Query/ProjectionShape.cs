// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.Query;

/// <summary>
///     The entity types a query's result rows carry, read off the query itself.
/// </summary>
/// <remarks>
///     <para>
///         An owned entity type — like a shared-type one — has no CLR-type name: EF names it for
///         the navigation that owns it, and four of <c>OwnedQueryTestBase</c>'s owned types are the
///         same <c>OwnedAddress</c>. A52 solved that for a value reached <em>through</em> a
///         navigation, because whoever maps the navigation's value knows the navigation. This is
///         the same rule one level higher, for a value the query projects <em>directly</em>:
///         <c>Select(p =&gt; p.PersonAddress)</c> has no navigation in hand at mapping time, but
///         the query still says which one it came from.
///     </para>
///     <para>
///         Neither of the other two sources can answer here (A55). The CLR type cannot — the model
///         reports four candidates. The change tracker cannot — the server tracks exactly when the
///         client asks it to, and these are <c>NoTracking</c> queries by fixture.
///     </para>
///     <para>
///         Resolution is deliberately partial: anything not recognised yields
///         <see langword="null" />, which leaves the mapper exactly where it was. It is consulted
///         last and only applies when the value really is an instance of the named type, so a wrong
///         answer degrades to the previous behaviour rather than mislabelling a row.
///     </para>
/// </remarks>
internal sealed class ProjectionShape
{
    private readonly Dictionary<string, ProjectionShape>? _members;

    private ProjectionShape(IEntityType? entityType, Dictionary<string, ProjectionShape>? members)
    {
        EntityType = entityType;
        _members = members;
    }

    /// <summary>
    ///     The entity type this value is, or <see langword="null" /> when it is not an entity.
    /// </summary>
    /// <remarks>
    ///     For a <em>collection</em>-valued slot this is the element's entity type, which is what
    ///     the mapper wants: it hands the same value down to the items and guards the collection
    ///     object itself out by <c>IsInstanceOfType</c> (A52).
    /// </remarks>
    public IEntityType? EntityType { get; }

    /// <summary>
    ///     The shape of an entity type's own values — what a navigation's target is.
    /// </summary>
    public static ProjectionShape For(IEntityType entityType) => new(entityType, null);

    /// <summary>
    ///     The shape of one member of a constructed row, or <see langword="null" /> when this row
    ///     has no member by that name (which includes every row that is not a constructed shape).
    /// </summary>
    public ProjectionShape? Member(string name)
        => _members is not null && _members.TryGetValue(name, out ProjectionShape? member) ? member : null;

    /// <summary>
    ///     Reads the shape of <paramref name="query" />'s result rows, or <see langword="null" />
    ///     when nothing can be said about them.
    /// </summary>
    public static ProjectionShape? Of(Expression query)
        => Resolve(query, []);

    /// <summary>
    ///     Every entity type this row carries, at any depth.
    /// </summary>
    /// <remarks>
    ///     Partial in the same way the rest of this class is: a member whose shape could not be
    ///     resolved is simply absent, so a caller reading this can under-report but never invent an
    ///     entity type the query does not project.
    /// </remarks>
    public IEnumerable<IEntityType> EntityTypes()
    {
        if (EntityType is not null)
        {
            yield return EntityType;
        }

        foreach (ProjectionShape member in _members?.Values ?? Enumerable.Empty<ProjectionShape>())
        {
            foreach (IEntityType entityType in member.EntityTypes())
            {
                yield return entityType;
            }
        }
    }

    private static ProjectionShape? Resolve(
        Expression node,
        Dictionary<ParameterExpression, ProjectionShape> bindings)
    {
        switch (node)
        {
            // The root of it all: a query root is its entity type, and every navigation reachable
            // from here names its own.
            case EntityQueryRootExpression root:
                return For(root.EntityType);

            case ParameterExpression parameter:
                return bindings.GetValueOrDefault(parameter);

            // A cast changes nothing about which entity type the value is. A quote is not a value
            // at all.
            case UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs
                or ExpressionType.Quote,
            } unary:
                return Resolve(unary.Operand, bindings);

            case MemberExpression { Expression: { } target } member:
            {
                ProjectionShape? inner = Resolve(target, bindings);

                // A navigation names its target entity type — the whole point.
                return inner?.EntityType?.FindNavigation(member.Member.Name) is { } navigation
                    ? For(navigation.TargetEntityType)
                    : inner?.Member(member.Member.Name);
            }

            // Both branches produce the same row shape, so either one that resolves will do.
            case ConditionalExpression conditional:
                return Resolve(conditional.IfTrue, bindings) ?? Resolve(conditional.IfFalse, bindings);

            case NewExpression @new:
                return Constructed(@new, MemberNames(@new), bindings);

            case MemberInitExpression init:
                return Initialized(init, bindings);

            case MethodCallExpression call when call.Arguments.Count > 0:
                return Operator(call, bindings);

            default:
                return null;
        }
    }

    /// <summary>
    ///     A query operator: either one that re-shapes the row, or one that leaves it alone.
    /// </summary>
    private static ProjectionShape? Operator(
        MethodCallExpression call,
        Dictionary<ParameterExpression, ProjectionShape> bindings)
    {
        ProjectionShape? source = Resolve(call.Arguments[0], bindings);

        if (Lambda(call.Arguments[^1]) is { } selector
            && call.Method.Name is nameof(Queryable.Select) or nameof(Queryable.SelectMany)
                or nameof(Queryable.Join))
        {
            var inner = new Dictionary<ParameterExpression, ProjectionShape>(bindings);

            switch (selector.Parameters.Count)
            {
                // Select(source, e => …) and SelectMany(source, e => e.Collection).
                case 1 when source is not null:
                    inner[selector.Parameters[0]] = source;
                    break;

                // SelectMany(source, e => e.Collection, (e, c) => …): the second parameter is an
                // element of what the collection selector produced. Join(outer, inner, …, (o, i) => …)
                // reads its second from the *other* source instead.
                case 2 when source is not null:
                    inner[selector.Parameters[0]] = source;
                    if (Companion(call, source, bindings) is { } companion)
                    {
                        inner[selector.Parameters[1]] = companion;
                    }

                    break;

                default:
                    return null;
            }

            return Resolve(selector.Body, inner);
        }

        // Everything else that keeps the element type keeps the shape: Where, OrderBy, Take,
        // Distinct, Include, Reverse, ToList, AsQueryable — and the single-result operators, whose
        // result type *is* the element type.
        Type element = SequenceElementType(call.Arguments[0].Type);

        return call.Type == element || SequenceElementType(call.Type) == element ? source : null;
    }

    /// <summary>
    ///     What a two-parameter result selector's second parameter ranges over.
    /// </summary>
    private static ProjectionShape? Companion(
        MethodCallExpression call,
        ProjectionShape source,
        Dictionary<ParameterExpression, ProjectionShape> bindings)
    {
        if (call.Method.Name == nameof(Queryable.Join))
        {
            return call.Arguments.Count >= 2 ? Resolve(call.Arguments[1], bindings) : null;
        }

        // SelectMany's collection selector, evaluated against the outer element.
        if (call.Arguments.Count >= 3 && Lambda(call.Arguments[1]) is { Parameters.Count: 1 } collection)
        {
            var inner = new Dictionary<ParameterExpression, ProjectionShape>(bindings)
            {
                [collection.Parameters[0]] = source,
            };

            return Resolve(collection.Body, inner);
        }

        return null;
    }

    private static ProjectionShape? Constructed(
        NewExpression @new,
        IReadOnlyList<string>? names,
        Dictionary<ParameterExpression, ProjectionShape> bindings)
    {
        if (names is null || names.Count != @new.Arguments.Count)
        {
            return null;
        }

        Dictionary<string, ProjectionShape>? members = null;
        for (int i = 0; i < names.Count; i++)
        {
            if (Resolve(@new.Arguments[i], bindings) is { } member)
            {
                // The wire's object shape is read back by name, case-insensitively
                // (`RehydrateObject`), and a `ValueTuple` carrier names its constructor parameters
                // `item1…` while the fields the mapper walks are `Item1…`.
                members ??= new Dictionary<string, ProjectionShape>(StringComparer.OrdinalIgnoreCase);
                members[names[i]] = member;
            }
        }

        return members is null ? null : new ProjectionShape(null, members);
    }

    private static ProjectionShape? Initialized(
        MemberInitExpression init,
        Dictionary<ParameterExpression, ProjectionShape> bindings)
    {
        Dictionary<string, ProjectionShape>? members = null;

        // Whatever the constructor already bound, plus the initializer's assignments.
        if (Constructed(init.NewExpression, MemberNames(init.NewExpression), bindings) is { _members: { } fromCtor })
        {
            members = new Dictionary<string, ProjectionShape>(fromCtor, StringComparer.OrdinalIgnoreCase);
        }

        foreach (MemberBinding binding in init.Bindings)
        {
            if (binding is MemberAssignment assignment && Resolve(assignment.Expression, bindings) is { } member)
            {
                members ??= new Dictionary<string, ProjectionShape>(StringComparer.OrdinalIgnoreCase);
                members[binding.Member.Name] = member;
            }
        }

        return members is null ? null : new ProjectionShape(null, members);
    }

    /// <summary>
    ///     The member each constructor argument stands for. An anonymous type and a re-carried
    ///     carrier both record them; a plain DTO constructor does not, and its parameter names are
    ///     what the reverse path matches on.
    /// </summary>
    private static IReadOnlyList<string>? MemberNames(NewExpression @new)
    {
        if (@new.Members is { } members)
        {
            return [.. members.Select(m => m.Name)];
        }

        ParameterInfo[] parameters = @new.Constructor?.GetParameters() ?? [];

        return parameters.Length == @new.Arguments.Count && Array.TrueForAll(parameters, p => p.Name is not null)
            ? [.. parameters.Select(p => p.Name!)]
            : null;
    }

    private static LambdaExpression? Lambda(Expression node)
        => node switch
        {
            LambdaExpression lambda => lambda,
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression quoted } => quoted,
            _ => null,
        };

    private static Type SequenceElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        Type? sequence = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : Array.Find(type.GetInterfaces(), i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return sequence?.GetGenericArguments()[0] ?? type;
    }
}
