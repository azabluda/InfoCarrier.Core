// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Metadata;

/// <summary>
///     The methods a model maps with <c>HasDbFunction</c>, read off the model by annotation name.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists.</b> A user-defined function is an ordinary static or instance method
///         in the caller's source, and its body is usually
///         <c>throw new NotSupportedException()</c> — the model maps it to something the store
///         runs. Two parts of this provider have to know which methods those are, and both were
///         getting it wrong in opposite directions. <see cref="Expressions.TypeAllowlist" /> refused
///         to let the call be named, because the class declaring it is not an entity type; and EF's
///         parameter extraction <em>evaluated</em> the call whenever its arguments were all
///         constants, which runs the throwing body. Between them they accounted for **81 red tests**
///         — the 75 in <c>UdfDbFunctionInfoCarrierTest</c> and the six <c>BoolSwitch</c> and
///         <c>Cases</c> tests inside <c>NullSemanticsQueryInfoCarrierTest</c>, which nobody had
///         connected to the same cause.
///     </para>
///     <para>
///         <b>Read by string, as M9 J5 decided.</b> <c>InfoCarrier.Core</c> does not reference
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, so <c>IDbFunction</c> cannot be named
///         and <c>model.GetDbFunctions()</c> cannot be called. The annotation holds a dictionary
///         whose values expose a public <c>MethodInfo</c>, which is reached through the
///         non-generic <see cref="IDictionary" /> and one property lookup.
///         <c>RelationalMetadataAgreementTest</c> checks the result
///         against EF's own <c>GetDbFunctions()</c>, so a rename or a reshape is a red test rather
///         than a silent behaviour change.
///     </para>
///     <para>
///         <b>This is model-derived, which is what keeps it out of
///         <c>docs/security-review.md</c> §2's conjunction.</b> Nothing static is widened. The
///         methods come from the application's own model, exactly as the entity and property types
///         the allowlist already admits do, and §2a's argument for C53 applies unchanged.
///     </para>
/// </remarks>
public static class ModelDbFunctions
{
    /// <summary>
    ///     <c>RelationalAnnotationNames.DbFunctions</c>. EF's own constant, so a rename is a build error.
    /// </summary>
    public const string DbFunctionsAnnotation = RelationalAnnotationNames.DbFunctions;

    private const string MethodInfoProperty = "MethodInfo";

    // Keyed on the model and held weakly: a model outlives every request against it, and there is
    // one per context configuration rather than one per query. The filter below is a singleton and
    // asks this question for every method call in every tree, so the annotation must be walked
    // once and not per node.
    private static readonly ConditionalWeakTable<IReadOnlyModel, HashSet<MethodInfo>> Cache = new();

    private static readonly HashSet<MethodInfo> None = [];

    /// <summary>
    ///     The methods <paramref name="model" /> maps with <c>HasDbFunction</c>, or an empty set
    ///     when it maps none — which is the usual case and costs one annotation lookup.
    /// </summary>
    /// <param name="model">The model to read, or <see langword="null" /> when none is available.</param>
    public static IReadOnlySet<MethodInfo> ForModel(IReadOnlyModel? model)
        => model is null ? None : Cache.GetValue(model, Read);

    private static HashSet<MethodInfo> Read(IReadOnlyModel model)
    {
        var methods = new HashSet<MethodInfo>();

        // `FindAnnotation` rather than the indexer: a runtime (compiled) model throws from some
        // relational accessors, and an absent annotation is the answer for every model that maps
        // no function at all.
        if (model.FindAnnotation(DbFunctionsAnnotation)?.Value is not IDictionary functions)
        {
            return methods;
        }

        foreach (object? function in functions.Values)
        {
            if (function is not null && FindMethod(function) is { } method)
            {
                methods.Add(method);
            }
        }

        return methods;
    }

    /// <remarks>
    ///     <b>Through the interface, not the concrete class, and that is not a style choice.</b>
    ///     A finalized model holds <c>RuntimeDbFunction</c>, which implements
    ///     <c>IReadOnlyDbFunction.MethodInfo</c> <em>explicitly</em> — so a public property lookup
    ///     on the concrete type finds nothing and answers "this model maps no functions". The
    ///     model this provider actually sees is always the finalized one, so the concrete-class
    ///     route worked on no model at all. <c>RelationalMetadataAgreementTest</c> caught it, by
    ///     comparing against EF's own <c>GetDbFunctions()</c> rather than asserting a count.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:DynamicallyAccessedMembers",
        Justification =
            "The property is EF Core's own `IReadOnlyDbFunction.MethodInfo`, part of a public "
            + "relational metadata interface that EF Core's own query pipeline reads on every "
            + "path translating a mapped function. A model carrying this annotation belongs to an "
            + "application that references the relational assembly, so the member is rooted by EF "
            + "itself. A null result is handled: the function is not admitted, and the call is "
            + "refused exactly as it was before this type existed.")]
    private static MethodInfo? FindMethod(object function)
    {
        foreach (Type contract in function.GetType().GetInterfaces())
        {
            if (contract.GetProperty(MethodInfoProperty) is { } property
                && property.PropertyType == typeof(MethodInfo)
                && property.GetValue(function) is MethodInfo method)
            {
                return method;
            }
        }

        return null;
    }
}
