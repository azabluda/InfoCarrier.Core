// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     Keeps <c>EF.Functions.Collate</c>, <c>Least</c> and <c>Greatest</c> in the expression tree
///     instead of letting EF evaluate them on the client.
/// </summary>
/// <remarks>
///     <para>
///         <b>What goes wrong without it.</b> EF's parameter extraction evaluates every maximal
///         subtree that does not touch the query root, and the bodies of these markers exist only
///         to throw — <c>RelationalDbFunctionsExtensions.Collate</c> raises <i>"The 'Collate'
///         method is not supported because the query has switched to client-evaluation"</i>. So
///         <c>c.ContactName == EF.Functions.Collate("maria anders", collation)</c> failed while
///         <c>EF.Functions.Collate(c.ContactName, collation) == "maria anders"</c> passed: the
///         difference is not the feature but whether the operand is a column, because a constant
///         operand makes the whole call evaluatable.
///     </para>
///     <para>
///         <b>This is a port of one clause of <c>RelationalEvaluatableExpressionFilter</c>.</b> That
///         class is what every relational provider gets, and this client is not one — M9 removed the
///         reference to <c>Microsoft.EntityFrameworkCore.Relational</c>, so EF registers the plain
///         core <see cref="EvaluatableExpressionFilter" />, which knows only the <em>core</em>
///         <c>DbFunctionsExtensions</c>. The relational host was therefore invisible to it.
///     </para>
///     <para>
///         <b>Named by string, as M9 J5 decided, and pinned by <c>DocumentMappingPinTest</c>.</b>
///         The assembly is checked as well as the full name, the same by-name route
///         <see cref="Expressions.TypeAllowlist" /> takes for this very class — which already admits
///         it across the wire (R78). The two halves belong together: the allowlist lets the call be
///         serialized, and this filter is what leaves a call there to serialize.
///     </para>
///     <para>
///         <b>EF's other clause is deliberately not ported.</b> <c>RelationalEvaluatableExpressionFilter</c>
///         also refuses to evaluate anything <c>model.FindDbFunction</c> answers for — user
///         functions declared with <c>HasDbFunction</c>. That is a relational model extension, and
///         this provider does not support <c>HasDbFunction</c> at all
///         (<c>UdfDbFunctionInfoCarrierTest</c>, one mechanism, still red). Porting it would add a
///         clause that can never fire.
///     </para>
/// </remarks>
/// <param name="dependencies">The dependencies to use.</param>
public class InfoCarrierEvaluatableExpressionFilter(EvaluatableExpressionFilterDependencies dependencies)
    : EvaluatableExpressionFilter(dependencies)
{
    /// <summary>
    ///     <c>typeof(Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions).FullName</c>,
    ///     which declares <c>Collate</c>, <c>Least</c> and <c>Greatest</c>. Pinned by
    ///     <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string RelationalDbFunctionsExtensionsName
        = "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions";

    private const string RelationalAssemblyName = "Microsoft.EntityFrameworkCore.Relational";

    /// <inheritdoc />
    public override bool IsEvaluatableExpression(Expression expression, IModel model)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (expression is MethodCallExpression methodCall
            && IsRelationalDbFunctionsExtensions(methodCall.Method.DeclaringType))
        {
            return false;
        }

        return base.IsEvaluatableExpression(expression, model);
    }

    private static bool IsRelationalDbFunctionsExtensions(Type? type)
        => type?.FullName == RelationalDbFunctionsExtensionsName
            && type.Assembly.GetName().Name == RelationalAssemblyName;
}
