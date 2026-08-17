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
    /// <param name="valueMappers">
    ///     The server's <see cref="ValueMapping.IInfoCarrierValueMapper" /> chain. A value the
    ///     wire cannot walk has to be mapped on <em>both</em> halves, and the server's half is
    ///     not DI-resolved along with the rest of this pipeline, so it is passed in — see
    ///     <see cref="InProcessInfoCarrierServer" />, which reads it from the service provider it
    ///     was built with.
    /// </param>
    public static ExpressionSerializer CreateForModel(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        IEnumerable<ValueMapping.IInfoCarrierValueMapper>? valueMappers = null)
    {
        var typeMapper = new TypeNodeMapper(model);
        var typeResolver = new TypeNodeResolver(model);
        var valueMapper = new DynamicValueMapper(model, typeMapper, typeResolver, valueMappers);
        var forward = new ExpressionToNodeTranslator(typeMapper, valueMapper);
        return new ExpressionSerializer(forward, typeResolver, valueMapper);
    }
}
