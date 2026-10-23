// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     Retains a version 10 public type that InfoCarrier never used or registered in its query flow.
/// </summary>
/// <remarks>
///     <para>
///         <b>ADR-006 is why this type throws.</b> This provider captures the raw
///         query at <c>IDatabase.CompileQuery</c> and ships it; the expression tree is translated
///         by the <em>server's</em> real provider, against the server's model. So there is no
///         translating visitor to build here and no shaper to compile — not as a gap, but as the
///         design.
///     </para>
///     <para>
///         InfoCarrier does not register this type. It remains public only because version 10
///         shipped it. The obsolete diagnostic records its version 11 removal.
///     </para>
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Obsolete compatibility stub that only throws.")]
[Obsolete(
    "InfoCarrier does not use EF Core's translating query pipeline. "
    + "This compatibility type will be removed in version 11.",
    DiagnosticId = "ICC1006",
    UrlFormat = "https://github.com/azabluda/InfoCarrier.Core/issues/226")]
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
