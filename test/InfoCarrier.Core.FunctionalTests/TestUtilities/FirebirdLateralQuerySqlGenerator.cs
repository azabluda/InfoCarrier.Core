// Licensed under the MIT license. See license.txt file in the project root for license information.

// EF1001: FbQuerySqlGenerator and IFbOptions are the Firebird provider's internal API. Subclassing
// its SQL generator is the only place a defect in that generator can be corrected from outside it.
#pragma warning disable EF1001

using System.Linq.Expressions;
using FirebirdSql.EntityFrameworkCore.Firebird.Infrastructure.Internal;
using FirebirdSql.EntityFrameworkCore.Firebird.Query.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The Firebird provider's SQL generator with one defect corrected: a table-valued function
///     used as the source of a <c>LATERAL</c> join is not wrapped in a derived table, so Firebird
///     cannot parse it.
/// </summary>
/// <remarks>
///     <para>
///         <b>This is somebody else's bug and it is corrected here rather than worked around.</b>
///         <c>FbQuerySqlGenerator</c> already knows the rule: Firebird will not take a bare source
///         after <c>LATERAL</c>, so for a plain table it writes
///         <c>(SELECT * FROM "T") AS "t"</c>. The same branch was never added for a function, so a
///         correlated call comes out as
///     </para>
///     <code>
///         JOIN LATERAL "GetOrdersOf"("c"."Id") AS "g" ON TRUE
///     </code>
///     <para>
///         and the store answers <c>Token unknown, "GetOrdersOf"</c>. Wrapping it the same way the
///         table case is wrapped is the whole fix, and the wrapped form was run against Firebird
///         5.0.3 by hand before it was written here.
///     </para>
///     <para>
///         <b>It explains fourteen skips in the Firebird provider's own suite.</b> Its
///         <c>UdfDbFunctionFbTests</c> marks every correlated queryable-function test
///         "Not supported on Firebird". The store supports all of them: a selectable stored
///         procedure can be called with an argument from the outer table, both as
///         <c>FROM a, proc(a.col)</c> and inside a real <c>LATERAL</c> derived table.
///     </para>
///     <para>
///         <b>This is a test-harness class and it belongs on the SERVER half only.</b> The server
///         in this suite is an ordinary EF application over the Firebird provider, so what it
///         emits is that provider's business. Nothing in <c>src/</c> knows about it. Delete this
///         file when the fix lands upstream.
///     </para>
/// </remarks>
public class FirebirdLateralQuerySqlGenerator(
    QuerySqlGeneratorDependencies dependencies,
    IFbOptions fbOptions) : FbQuerySqlGenerator(dependencies, fbOptions)
{
    /// <inheritdoc />
    protected override Expression VisitCrossApply(CrossApplyExpression crossApplyExpression)
    {
        ArgumentNullException.ThrowIfNull(crossApplyExpression);

        if (crossApplyExpression.Table is TableValuedFunctionExpression function)
        {
            this.Sql.Append("JOIN LATERAL ");
            this.GenerateWrappedFunction(function);
            this.Sql.Append(" ON TRUE");
            return crossApplyExpression;
        }

        return base.VisitCrossApply(crossApplyExpression);
    }

    /// <inheritdoc />
    protected override Expression VisitOuterApply(OuterApplyExpression outerApplyExpression)
    {
        ArgumentNullException.ThrowIfNull(outerApplyExpression);

        if (outerApplyExpression.Table is TableValuedFunctionExpression function)
        {
            this.Sql.Append("LEFT JOIN LATERAL ");
            this.GenerateWrappedFunction(function);
            this.Sql.Append(" ON TRUE");
            return outerApplyExpression;
        }

        return base.VisitOuterApply(outerApplyExpression);
    }

    /// <summary>
    ///     Writes <c>(SELECT * FROM "Func"(args)) AS "alias"</c>.
    /// </summary>
    /// <remarks>
    ///     The alias goes on the derived table and not on the call, which is what lets the rest of
    ///     the statement keep referring to <c>"alias"."Column"</c> unchanged.
    /// </remarks>
    private void GenerateWrappedFunction(TableValuedFunctionExpression function)
    {
        this.Sql
            .Append("(SELECT * FROM ")
            .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(function.Name));

        if (function.Arguments.Count > 0)
        {
            this.Sql.Append("(");
            for (int i = 0; i < function.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    this.Sql.Append(", ");
                }

                this.Visit(function.Arguments[i]);
            }

            this.Sql.Append(")");
        }

        this.Sql
            .Append(")")
            .Append(this.AliasSeparator)
            .Append(this.Dependencies.SqlGenerationHelper.DelimitIdentifier(function.Alias));
    }
}

/// <summary>
///     Builds <see cref="FirebirdLateralQuerySqlGenerator" />.
/// </summary>
public class FirebirdLateralQuerySqlGeneratorFactory(
    QuerySqlGeneratorDependencies dependencies,
    IFbOptions fbOptions) : IQuerySqlGeneratorFactory
{
    /// <inheritdoc />
    public QuerySqlGenerator Create()
        => new FirebirdLateralQuerySqlGenerator(dependencies, fbOptions);
}
