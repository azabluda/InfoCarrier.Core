// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core;

/// <summary>
///     Default <see cref="IExpressionSerializer" />: ties the forward
///     (<see cref="ExpressionToNodeTranslator" />) and reverse (<see cref="NodeToExpressionTranslator" />)
///     translators together behind the DI seam. Query-root rebinding on the reverse path is
///     supplied per-call by the server; the client-side reverse path is unused for roots.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="ExpressionSerializer" /> class.
/// </remarks>
public class ExpressionSerializer(
    ExpressionToNodeTranslator forward,
    TypeNodeResolver typeResolver,
    IDynamicValueMapper valueMapper) : IExpressionSerializer
{
    private readonly ExpressionToNodeTranslator _forward = forward;
    private readonly TypeNodeResolver _typeResolver = typeResolver;
    private readonly IDynamicValueMapper _valueMapper = valueMapper;

    /// <summary>
    ///     The value mapper backing this serializer. Exposed so the server and client result
    ///     paths map entity rows through the same EF-metadata-driven mapper (and the same
    ///     per-message reference map) as the query tree.
    /// </summary>
    public virtual IDynamicValueMapper ValueMapper => _valueMapper;

    /// <inheritdoc />
    public virtual ExpressionNode ToNode(Expression expression)
        => _forward.Translate(expression);

    /// <inheritdoc />
    public virtual Expression ToExpression(ExpressionNode node)
        => ToExpression(node, (stub, type) => throw new NotSupportedException(
            "Query-root rebinding requires a server query-root factory (Phase D)."));

    /// <summary>
    ///     Translates a node DTO back to a live expression, rebinding query roots via the
    ///     supplied factory (server-side, research-findings §2).
    /// </summary>
    public virtual Expression ToExpression(
        ExpressionNode node,
        Func<QueryRootStubNode, Type, Expression> queryRootFactory)
        => new NodeToExpressionTranslator(_typeResolver, _valueMapper, queryRootFactory).Translate(node);

    /// <summary>
    ///     Builds a model-aware serializer pipeline for the given EF model. Used by the server,
    ///     which constructs the pipeline from the resolved <see cref="Microsoft.EntityFrameworkCore.DbContext.Model" />
    ///     rather than resolving <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel" /> from
    ///     DI (which is scoped to the context).
    /// </summary>
    /// <param name="model">The server's EF model.</param>
    /// <param name="allowedTypes">
    ///     Extra types a payload may name on this server
    ///     (<see cref="IInfoCarrierAllowedTypes" />). <b>This is the security boundary</b>: the
    ///     client's matching list only decides what it sends. See <c>docs/security-review.md</c>
    ///     section 2.
    /// </param>
    /// <param name="valueMappers">
    ///     The server's <see cref="ValueMapping.IInfoCarrierValueMapper" /> chain. A value the
    ///     wire cannot walk has to be mapped on <em>both</em> halves, and the server's half is
    ///     not DI-resolved along with the rest of this pipeline, so it is passed in — see
    ///     <see cref="InProcessInfoCarrierServer" />, which reads it from the service provider it
    ///     was built with.
    /// </param>
    public static ExpressionSerializer CreateForModel(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        IEnumerable<ValueMapping.IInfoCarrierValueMapper>? valueMappers = null,
        IEnumerable<Type>? allowedTypes = null)
    {
        var typeMapper = new TypeNodeMapper(model);
        var typeResolver = new TypeNodeResolver(model, TypeAllowlist.ForModel(model, allowedTypes));
        var valueMapper = new DynamicValueMapper(model, typeMapper, typeResolver, valueMappers);
        var forward = new ExpressionToNodeTranslator(typeMapper, valueMapper);
        return new ExpressionSerializer(forward, typeResolver, valueMapper);
    }
    /// <summary>
    ///     Creates a serializer for a model, admitting no types beyond the ones it implies.
    /// </summary>
    /// <remarks>
    ///     <b>A binary-compatibility overload, kept because 10.0.0 shipped this signature.</b>
    ///     Adding an optional parameter is source-compatible and <em>binary</em> breaking: the
    ///     compiler emits one member and the old arity disappears from the assembly, which
    ///     <c>dotnet pack</c>'s package validation reports as <c>CP0002</c>.
    ///     <c>Directory.Build.props</c> states the promise this keeps, and the <c>Packages</c>
    ///     workflow is the only job that checks it — it runs on <c>main</c> alone, which is how
    ///     six of these reached <c>main</c> unnoticed. Delete when the baseline moves past 10.0.x.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static ExpressionSerializer CreateForModel(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        IEnumerable<ValueMapping.IInfoCarrierValueMapper>? valueMappers)
        => CreateForModel(model, valueMappers, allowedTypes: null);

}
