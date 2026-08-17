// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Lets <see cref="WireGrouping" /> fill a closed <see cref="WireGrouping{TKey, TElement}" />
///     without reflecting on it.
/// </summary>
/// <remarks>
///     <b>This interface exists for the trimmer.</b> Building the closed generic needs
///     <c>MakeGenericType</c> and there is no way round that — the type arguments come from the
///     caller's model. But *filling* it did not need reflection at all, and doing it with
///     <c>GetProperty</c>/<c>GetMethod</c> cost three IL warnings (`IL2062`, `IL2065`, `IL2075`) on
///     top of the one that is unavoidable. Casting to a non-generic interface costs none.
/// </remarks>
internal interface IWireGroupingSink
{
    /// <summary>
    ///     Copies <paramref name="grouping" /> in, casting it to the closed type's own
    ///     <see cref="IGrouping{TKey, TElement}" /> — which the closed type can do and the caller
    ///     cannot.
    /// </summary>
    void Fill(object grouping);
}

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
public sealed class WireGrouping<TKey, TElement> : IGrouping<TKey, TElement>, IWireGroupingSink
{
    /// <inheritdoc />
    void IWireGroupingSink.Fill(object grouping)
    {
        var typed = (IGrouping<TKey, TElement>)grouping;
        Key = typed.Key;
        Items = [.. typed];
    }

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
    /// <remarks>
    ///     <para>
    ///         <b>The <see cref="System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute" /> is a correctness fix, not warning
    ///         cosmetics.</b> <see cref="Activator.CreateInstance(Type)" /> is the only caller of
    ///         <see cref="WireGrouping{TKey, TElement}" />'s constructor, and a trimmer cannot see a
    ///         constructor reached that way — so without this it is free to remove it and the
    ///         published app would fail at run time on the first non-composed <c>GroupBy</c>, which
    ///         is precisely the class of break `eng/trim-baseline.txt` warns the warnings are about.
    ///     </para>
    ///     <para>
    ///         Two IL warnings remain here and both are the premise this whole assembly is built on:
    ///         <c>GetInterfaces()</c> on a runtime type, and <c>MakeGenericType</c> from arguments
    ///         the caller's model supplies. No annotation can say *"whatever type the caller named"*.
    ///     </para>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.DynamicDependency(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicParameterlessConstructor,
        typeof(WireGrouping<,>))]
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

        // `MakeGenericType` is the one reflective step that cannot be removed: the key and element
        // types come from the caller's model, so no annotation can name them ahead of time. That is
        // the same premise `eng/trim-baseline.txt` records for the rest of this assembly.
        wrappedType = typeof(WireGrouping<,>).MakeGenericType(grouping.GetGenericArguments());
        wrapped = Activator.CreateInstance(wrappedType)!;

        // Everything after it is ordinary typed code, through `IWireGroupingSink`. Reading `Key`
        // and appending items with `GetProperty`/`GetMethod` worked and cost three further IL
        // warnings; the interface costs none, and the casts inside it are checked by the runtime
        // exactly as the reflection was.
        ((IWireGroupingSink)wrapped).Fill(value);

        return true;
    }
}
