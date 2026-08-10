// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     The two query-pipeline factories every EF Core provider is required to register, for a
///     provider that deliberately does not translate.
/// </summary>
/// <remarks>
///     <para>
///         <b>ADR-006 is why these exist and why they throw.</b> This provider captures the raw
///         query at <c>IDatabase.CompileQuery</c> and ships it; the expression tree is translated
///         by the <em>server's</em> real provider, against the server's model. So there is no
///         translating visitor to build here and no shaper to compile — not as a gap, but as the
///         design.
///     </para>
///     <para>
///         They are registered anyway, because "not registered" is the wrong way to say that.
///         <c>EntityFrameworkServicesBuilder.CoreServices</c> lists both as services a provider
///         supplies, and
///         <c>EntityFrameworkServiceCollectionExtensionsTestBase.Required_services_are_registered_with_expected_lifetimes</c>
///         asserts it. Leaving them out gave anyone who resolved one EF's generic <i>"no service
///         has been registered"</i>; these give the reason instead. Nothing in this provider
///         resolves them, so the message is for whoever goes looking.
///     </para>
/// </remarks>
public class InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    /// <inheritdoc />
    public virtual QueryableMethodTranslatingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => throw new NotSupportedException(
            "InfoCarrier does not translate queries. It captures the query at "
            + "IDatabase.CompileQuery and sends it to the server, whose own provider translates it "
            + "(ADR-006). There is no client-side translating visitor to create.");
}

/// <inheritdoc cref="InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory" />
public class InfoCarrierShapedQueryCompilingExpressionVisitorFactory
    : IShapedQueryCompilingExpressionVisitorFactory
{
    /// <inheritdoc />
    public virtual ShapedQueryCompilingExpressionVisitor Create(QueryCompilationContext queryCompilationContext)
        => throw new NotSupportedException(
            "InfoCarrier does not compile shaped queries. Results arrive already materialized "
            + "from the server and are rebuilt by ClientResultMaterializer (ADR-006). There is no "
            + "client-side shaper to create.");
}
