// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     Leaves a <c>FromSql*</c> call inside a query filter exactly as the caller wrote it, so that
///     the client's model builds.
/// </summary>
/// <remarks>
///     <para>
///         <b>The defect this closes, and it fires before any query runs.</b> EF's core
///         <see cref="QueryFilterRewritingConvention" /> rewrites a <c>DbSet</c> access inside a
///         query filter into an <c>EntityQueryRootExpression</c>, which is typed
///         <c>IQueryable&lt;T&gt;</c>. A filter written as <c>Set&lt;T&gt;().FromSqlRaw(…)</c> has
///         that access as the <em>first argument</em> of a method whose parameter is
///         <c>DbSet&lt;T&gt;</c>, so the rewrite produces a call that cannot be constructed:
///         <c>"Expression of type 'IQueryable&lt;…&gt;' cannot be used for parameter of type
///         'DbSet&lt;…&gt;' of method 'FromSqlRaw'"</c>, thrown while the model is being finalized.
///     </para>
///     <para>
///         <b>EF Core Relational does not have the problem because it replaces the convention.</b>
///         <c>RelationalQueryFilterRewritingConvention</c> recognises the three <c>FromSql*</c>
///         methods and folds the whole call into a <c>FromSqlQueryRootExpression</c>. That type
///         lives in <c>Microsoft.EntityFrameworkCore.Relational</c>, which this package does not
///         reference (M9, J5), and reaching it by reflection would be building a relational query
///         root on a client that has no store.
///     </para>
///     <para>
///         <b>Leaving the call alone is the right answer here, not merely the cheap one</b>, and it
///         is R82's rule again: the server owns what the store does. This client captures the
///         caller's tree and never applies a query filter — the server applies its own model's
///         filter, with its own provider, where <c>FromSql</c> means something. The filter in the
///         client's model only has to be <em>representable</em>.
///     </para>
///     <para>
///         <b>The rest of the subtree is left alone with it, and nothing is lost by that.</b> A
///         <c>FromSql*</c> call's remaining arguments are a SQL string and its parameter values;
///         a <c>DbSet</c> access cannot meaningfully appear among them. Every other method call in
///         a filter still goes to the base visitor.
///     </para>
///     <para>
///         <b>Matching the declaring type by name is deliberate, and it is not new.</b>
///         <see cref="Metadata.AnnotationDocumentMapping" /> reads
///         <c>Relational:ContainerColumnName</c> the same way, and
///         <see cref="InfoCarrierValueGenerationConvention" /> reads two more. The strings below
///         are pinned against EF's own members by <c>DocumentMappingPinTest</c>, in the test
///         project, which is where the relational reference belongs.
///     </para>
///     <para>
///         <b>The list used to name a third, and R128 removed it.</b>
///         <c>InfoCarrierHierarchyMappingConvention</c> spelled four <c>Relational:</c> strings by
///         hand; it is deleted, and <c>InfoCarrier.Core.Relational</c> supplies EF's own
///         <c>EntityTypeHierarchyMappingConvention</c> instead, which reads EF's constants. Those
///         four are a compile error now rather than a pinned string, which is the direction the
///         rest of this list is meant to travel in.
///     </para>
/// </remarks>
public class InfoCarrierQueryFilterRewritingConvention : QueryFilterRewritingConvention
{
    /// <summary>
    ///     The full name of <c>Microsoft.EntityFrameworkCore.RelationalQueryableExtensions</c>,
    ///     which declares the three <c>FromSql*</c> methods. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string FromSqlDeclaringTypeName = "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions";

    /// <summary>
    ///     The names of the <c>FromSql*</c> methods whose first parameter is a <c>DbSet</c>.
    ///     Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> FromSqlMethodNames = ["FromSql", "FromSqlRaw", "FromSqlInterpolated"];

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierQueryFilterRewritingConvention" />
    ///     class.
    /// </summary>
    /// <param name="dependencies">The convention-set builder dependencies.</param>
    public InfoCarrierQueryFilterRewritingConvention(ProviderConventionSetBuilderDependencies dependencies)
        : base(dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);

        DbSetAccessRewriter = new InfoCarrierDbSetAccessRewritingExpressionVisitor(dependencies.ContextType);
    }

    /// <summary>
    ///     EF's rewriter, with <c>FromSql*</c> calls left untouched.
    /// </summary>
    /// <param name="contextType">The CLR type of the derived <c>DbContext</c>.</param>
    protected class InfoCarrierDbSetAccessRewritingExpressionVisitor(Type contextType)
        : DbSetAccessRewritingExpressionVisitor(contextType)
    {
        /// <inheritdoc />
        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            ArgumentNullException.ThrowIfNull(methodCallExpression);

            return methodCallExpression.Method.DeclaringType?.FullName == FromSqlDeclaringTypeName
                && FromSqlMethodNames.Contains(methodCallExpression.Method.Name)
                    ? methodCallExpression
                    : base.VisitMethodCall(methodCallExpression);
        }
    }
}
