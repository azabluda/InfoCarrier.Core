// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     The wire's own <see cref="IGrouping{TKey, TElement}" />: a key and a sequence, and nothing
///     else (M9 J8).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this type exists rather than an allowlist entry.</b> A non-composed
///         <c>GroupBy</c> — <c>Set&lt;Entity&gt;().GroupBy(e =&gt; e.Key).ToList()</c>, with no
///         aggregate after it — makes the *grouping itself* the result, and EF hands back
///         <c>GroupBySingleQueryingEnumerable&lt;,&gt;+InternalGrouping</c>. Admitting that to
///         <see cref="TypeAllowlist" /> would put an <b>EF internal type</b> into an
///         ADR-008 constraint 2 control whose safety <c>security-review.md</c> §2 calls a
///         conjunction. So the server projects EF's grouping into <em>this</em> type — ours,
///         public, and safe to name — and the allowlist admits only that.
///     </para>
///     <para>
///         <c>IGrouping&lt;,&gt;</c> itself was already on the allowlist, which is why the interface
///         was never the problem: the concrete type was.
///     </para>
///     <para>
///         <b>It implements the interface on purpose.</b> The client's residual is the caller's own
///         query, so the value it receives has to *be* an <c>IGrouping&lt;TKey, TElement&gt;</c>
///         rather than something convertible to one — a DTO would push a conversion into the
///         materializer for no gain.
///     </para>
///     <para>
///         <b>Both members are settable, and that is what lets it travel with no new node kind.</b>
///         It goes across as an ordinary object shape — the reflective walk of public members that
///         already carries every anonymous type, record and DTO — and comes back through
///         <c>RehydrateObject</c>. The one thing that has to be arranged is that the *collection*
///         branch does not claim it first, because it is <see cref="IEnumerable" /> like every other
///         collection; <c>DynamicValueMapper</c> excludes it explicitly. That is the same hazard
///         that made `EnumerableClassKey` walk as a collection and throw (J9), met deliberately here
///         instead of by accident.
///     </para>
/// </remarks>
/// <typeparam name="TKey">The grouping key's type.</typeparam>
/// <typeparam name="TElement">The grouped element's type.</typeparam>
public sealed class WireGrouping<TKey, TElement> : IGrouping<TKey, TElement>
{
    /// <summary>
    ///     The key this group was formed on.
    /// </summary>
    /// <remarks>
    ///     Settable, unlike <see cref="IGrouping{TKey, TElement}.Key" />, because the object-shape
    ///     walk rehydrates by setting members.
    /// </remarks>
    public TKey Key { get; set; } = default!;

    /// <summary>
    ///     The group's elements.
    /// </summary>
    public List<TElement> Items { get; set; } = [];

    /// <inheritdoc />
    public IEnumerator<TElement> GetEnumerator() => Items.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
///     Recognises and builds <see cref="WireGrouping{TKey, TElement}" /> without naming its type
///     arguments.
/// </summary>
internal static class WireGrouping
{
    /// <summary>
    ///     Whether <paramref name="type" /> is a <see cref="WireGrouping{TKey, TElement}" />.
    /// </summary>
    public static bool Is(Type? type)
        => type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(WireGrouping<,>);

    /// <summary>
    ///     Projects any <see cref="IGrouping{TKey, TElement}" /> into a
    ///     <see cref="WireGrouping{TKey, TElement}" />, so that what travels is this provider's own
    ///     type rather than whichever implementation the backing store's provider happened to use.
    /// </summary>
    /// <remarks>
    ///     Declines anything that is not a grouping, and anything that already is a
    ///     <see cref="WireGrouping{TKey, TElement}" /> — so it is idempotent and cannot wrap twice.
    /// </remarks>
    public static bool TryWrap(object value, out object? wrapped, out Type? wrappedType)
    {
        wrapped = null;
        wrappedType = null;

        Type runtime = value.GetType();
        if (Is(runtime))
        {
            return false;
        }

        Type? grouping = Array.Find(
            runtime.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGrouping<,>));

        if (grouping is null)
        {
            return false;
        }

        Type[] arguments = grouping.GetGenericArguments();
        wrappedType = typeof(WireGrouping<,>).MakeGenericType(arguments);
        wrapped = Activator.CreateInstance(wrappedType)!;

        wrappedType.GetProperty(nameof(WireGrouping<object, object>.Key))!
            .SetValue(wrapped, grouping.GetProperty("Key")!.GetValue(value));

        object list = Activator.CreateInstance(typeof(List<>).MakeGenericType(arguments[1]))!;
        System.Reflection.MethodInfo add = list.GetType().GetMethod("Add")!;
        foreach (object? item in (IEnumerable)value)
        {
            add.Invoke(list, [item]);
        }

        wrappedType.GetProperty(nameof(WireGrouping<object, object>.Items))!.SetValue(wrapped, list);
        return true;
    }
}
