// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Reads and rebuilds EF Core's relational <c>FromSqlQueryRootExpression</c> without naming
///     its type (#60).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why by shape.</b> <c>FromSqlQueryRootExpression</c> lives in
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, which <c>InfoCarrier.Core</c>
///         deliberately does not reference (<c>architecture.md</c> section 6a D3, M9 J5) - the
///         client's model is not relational, and referencing the package to name one type would
///         reverse that. <see cref="Query.ServerBoundaryAnalyzer.IsSerializableKind" /> already
///         names this same type by shape, and has since R75.
///     </para>
///     <para>
///         <b>What it costs, stated rather than hidden.</b> Two <c>GetProperty</c> reads on the
///         client and one <c>Activator.CreateInstance</c> on the server, all on a runtime type -
///         the pattern <c>eng/trim-baseline.txt</c> describes as this provider's premise. The
///         suppressions below are narrow and each names why the members survive trimming: EF Core
///         itself reads <c>Sql</c> and <c>Argument</c> and constructs this expression on every
///         <c>FromSql</c> call, so an application whose server can run raw SQL at all has EF's own
///         uses of them rooted.
///     </para>
///     <para>
///         <b>Nothing here decides whether raw SQL is permitted.</b> That is
///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" /> on the
///         client and <see cref="IInfoCarrierArbitrarySqlExecution" /> on the server. This class
///         only knows the shape.
///     </para>
/// </remarks>
internal static class RelationalQueryRootShape
{
    private const string FromSqlRootSimpleName = "FromSqlQueryRootExpression";

    private const string FromSqlRootFullName
        = "Microsoft.EntityFrameworkCore.Query.Internal.FromSqlQueryRootExpression";

    /// <summary>
    ///     Whether a node is EF's relational raw-SQL query root.
    /// </summary>
    /// <remarks>
    ///     The EXACT simple name, not <c>is</c> against a base: every query root that carries
    ///     state beyond its entity type is a subclass, and treating one as its base is what
    ///     shipped a <c>FromSqlRaw</c> as the whole table before R75.
    /// </remarks>
    internal static bool IsFromSqlRoot(Expression node)
        => node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression
            && node.GetType().Name == FromSqlRootSimpleName;

    /// <summary>
    ///     Reads the SQL text and the argument expression off a raw-SQL query root.
    /// </summary>
    /// <returns><c>true</c> when both were present, which is every real instance.</returns>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute'",
        Justification = "Sql and Argument are read off EF Core's own FromSqlQueryRootExpression, "
            + "which EF constructs and reads on every FromSql call, so both properties are rooted "
            + "by EF's own use wherever this branch can be reached at all. The type cannot be named "
            + "statically because InfoCarrier.Core does not reference EFCore.Relational (D3).")]
    internal static bool TryRead(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        sql = null;
        argument = null;

        if (!IsFromSqlRoot(node))
        {
            return false;
        }

        Type type = node.GetType();
        sql = type.GetProperty("Sql", BindingFlags.Public | BindingFlags.Instance)?.GetValue(node) as string;
        argument = type.GetProperty("Argument", BindingFlags.Public | BindingFlags.Instance)?.GetValue(node)
            as Expression;

        return sql is not null && argument is not null;
    }

    /// <summary>
    ///     Rebuilds a raw-SQL query root against the <em>server's</em> entity type.
    /// </summary>
    /// <remarks>
    ///     The three-argument constructor, which is the one EF's own
    ///     <c>DetachQueryProvider</c> and <c>UpdateEntityType</c> use: a root rebuilt here has no
    ///     query provider, exactly like the plain <c>EntityQueryRootExpression</c> that
    ///     <c>ServerQueryExecutor.RebindQueryRoot</c> builds beside it.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2057:Type.GetType",
        Justification = "The type is EF Core's own and is loaded by the server's relational "
            + "provider; a server that has granted raw SQL execution necessarily references it.")]
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Native AOT is not a supported configuration (roadmap.md, M8 exit "
            + "criteria); this call is no more dynamic than the rest of the rebuild path.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:'DynamicallyAccessedMembers' in call to target method",
        Justification = "Activator.CreateInstance over EF Core's own expression type, whose "
            + "constructors EF itself calls on every FromSql query.")]
    internal static Expression CreateFromSqlRoot(IEntityType entityType, string sql, Expression argument)
    {
        Type type = ResolveFromSqlRootType()
            ?? throw new InvalidOperationException(
                $"'{FromSqlRootFullName}' was not found. A server that permits raw SQL execution "
                + "must reference a relational EF Core provider.");

        return (Expression)Activator.CreateInstance(type, entityType, sql, argument)!;
    }

    /// <remarks>
    ///     <b>A <c>foreach</c> rather than a <c>Select</c>, and that is not style.</b> An
    ///     <see cref="UnconditionalSuppressMessageAttribute" /> covers the annotated member's own
    ///     body, and a lambda compiles to a member of its own - so the same call written as
    ///     <c>.Select(a =&gt; a.GetType(...))</c> reported its IL2026 against
    ///     <c>&lt;&gt;c.&lt;ResolveFromSqlRootType&gt;b__5_0</c>, outside the suppression, and
    ///     raised the trim count by one. Measured, not guessed: <c>eng/trim-ratchet.sh</c> failed
    ///     at 90 against a baseline of 89 and passed at 89 once the lambda was gone.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access",
        Justification = "EF Core's own FromSqlQueryRootExpression, looked up by full name in the "
            + "assemblies already loaded. A server that has granted raw SQL execution references a "
            + "relational provider, which roots the type; where it does not, this returns null and "
            + "the caller raises a named error rather than misbehaving.")]
    private static Type? ResolveFromSqlRootType()
    {
        Type? direct = Type.GetType(
            $"{FromSqlRootFullName}, Microsoft.EntityFrameworkCore.Relational",
            throwOnError: false);

        if (direct is not null)
        {
            return direct;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(FromSqlRootFullName, throwOnError: false) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
