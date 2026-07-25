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
public class ExpressionSerializer : IExpressionSerializer
{
    private readonly ExpressionToNodeTranslator _forward;
    private readonly TypeNodeResolver _typeResolver;
    private readonly IDynamicValueMapper _valueMapper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionSerializer" /> class.
    /// </summary>
    public ExpressionSerializer(
        ExpressionToNodeTranslator forward,
        TypeNodeResolver typeResolver,
        IDynamicValueMapper valueMapper)
    {
        _forward = forward;
        _typeResolver = typeResolver;
        _valueMapper = valueMapper;
    }

    /// <inheritdoc />
    public ExpressionNode ToNode(Expression expression)
        => _forward.Translate(expression);

    /// <inheritdoc />
    public Expression ToExpression(ExpressionNode node)
        => ToExpression(node, (stub, type) => throw new NotSupportedException(
            "Query-root rebinding requires a server query-root factory (Phase D)."));

    /// <summary>
    ///     Translates a node DTO back to a live expression, rebinding query roots via the
    ///     supplied factory (server-side, research-findings §2).
    /// </summary>
    public Expression ToExpression(
        ExpressionNode node,
        Func<QueryRootStubNode, Type, Expression> queryRootFactory)
        => new NodeToExpressionTranslator(_typeResolver, _valueMapper, queryRootFactory).Translate(node);

    /// <summary>
    ///     Builds a model-aware serializer pipeline for the given EF model. Used by the server,
    ///     which constructs the pipeline from the resolved <see cref="Microsoft.EntityFrameworkCore.DbContext.Model" />
    ///     rather than resolving <see cref="IModel" /> from DI (which is scoped to the context).
    /// </summary>
    public static ExpressionSerializer CreateForModel(Microsoft.EntityFrameworkCore.Metadata.IModel model)
    {
        var typeMapper = new TypeNodeMapper(model);
        var typeResolver = new TypeNodeResolver(model);
        var valueMapper = new DynamicValueMapper(model, typeMapper, typeResolver);
        var forward = new ExpressionToNodeTranslator(typeMapper, valueMapper);
        return new ExpressionSerializer(forward, typeResolver, valueMapper);
    }
}
