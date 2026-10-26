// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core;

/// <summary>Creates a fresh expression pipeline for the actual leased server context.</summary>
/// <remarks>
/// Factories are host-owned singletons and must support concurrent calls. Each result belongs to
/// one operation, including streamed enumeration. It must own any disposable components it creates.
/// Never resolve another context: the supplied context can hold an existing transaction.
/// </remarks>
public interface IInfoCarrierServerPipelineFactory
{
    /// <summary>Creates translation and value mapping with the selected registration's policy.</summary>
    IInfoCarrierServerPipeline Create(DbContext context, IReadOnlyList<Type> allowedTypes,
        IEnumerable<IInfoCarrierValueMapper> valueMappers, AnonymousShapeCatalog anonymousShapes);
}

/// <summary>An operation-owned pipeline with one shared expression and row mapper.</summary>
public interface IInfoCarrierServerPipeline : IDisposable, IAsyncDisposable
{
    /// <summary>The serializer, whose ValueMapper is also used for rows and saves.</summary>
    IExpressionSerializer Serializer { get; }
}

/// <summary>The built-in model-aware pipeline factory.</summary>
public sealed class InfoCarrierServerPipelineFactory : IInfoCarrierServerPipelineFactory
{
    /// <inheritdoc />
    public IInfoCarrierServerPipeline Create(DbContext context, IReadOnlyList<Type> allowedTypes,
        IEnumerable<IInfoCarrierValueMapper> valueMappers, AnonymousShapeCatalog anonymousShapes)
        => new Pipeline(ExpressionSerializer.CreateForModel(context.Model, valueMappers, allowedTypes, anonymousShapes));

    private sealed class Pipeline(IExpressionSerializer serializer) : IInfoCarrierServerPipeline
    {
        public IExpressionSerializer Serializer { get; } = serializer;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
