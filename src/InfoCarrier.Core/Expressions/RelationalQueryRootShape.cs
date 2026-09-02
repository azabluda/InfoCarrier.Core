// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Reads and rebuilds EF Core's two relational raw-SQL query roots —
///     <c>FromSqlQueryRootExpression</c> (#60) and <c>SqlQueryRootExpression</c> (#56) — without
///     naming either type.
/// </summary>
/// <remarks>
///     <para>
///         <b>Two roots, one call site.</b> <c>Database.SqlQuery&lt;T&gt;</c> builds
///         <c>SqlQueryRootExpression</c> when the type-mapping source recognises <c>T</c> and
///         <c>FromSqlQueryRootExpression</c> over an ad-hoc entity type when it does not, so both
///         arrive from the same caller and are told apart here. The scalar one has no entity type,
///         which is why it is rebuilt from a CLR <see cref="Type" /> rather than an
///         <see cref="IEntityType" />.
///     </para>
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

    private const string SqlQueryRootSimpleName = "SqlQueryRootExpression";

    private const string SqlQueryRootFullName
        = "Microsoft.EntityFrameworkCore.Query.Internal.SqlQueryRootExpression";

    private const string RelationalAssemblyName = "Microsoft.EntityFrameworkCore.Relational";

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

    /// <summary>
    ///     Whether a node is EF's relational raw-SQL query root for a SCALAR result (#56).
    /// </summary>
    /// <remarks>
    ///     The sibling of <see cref="IsFromSqlRoot" />, and the exact simple name for the same
    ///     reason. <c>Database.SqlQuery&lt;T&gt;</c> builds this root when the type-mapping source
    ///     recognises <c>T</c>, and a <c>FromSqlQueryRootExpression</c> over an ad-hoc entity type
    ///     when it does not — so the two arrive from one call site and must be told apart here.
    /// </remarks>
    internal static bool IsSqlQueryRoot(Expression node)
        => node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression
            && node.GetType().Name == SqlQueryRootSimpleName;

    /// <summary>
    ///     Reads the SQL text and the argument expression off a scalar raw-SQL query root.
    /// </summary>
    /// <remarks>
    ///     The property names are the same two as the entity root's, which is not a coincidence:
    ///     EF declares <c>Sql</c> and <c>Argument</c> on both, so one reader body serves both and
    ///     only the type test differs.
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute'",
        Justification = "Sql and Argument are read off EF Core's own SqlQueryRootExpression, which "
            + "EF constructs and reads on every Database.SqlQuery call, so both properties are "
            + "rooted by EF's own use wherever this branch can be reached at all. The type cannot "
            + "be named statically because InfoCarrier.Core does not reference EFCore.Relational "
            + "(D3).")]
    internal static bool TryReadSqlQuery(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        sql = null;
        argument = null;

        if (!IsSqlQueryRoot(node))
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
    ///     Rebuilds a scalar raw-SQL query root against the given element type.
    /// </summary>
    /// <remarks>
    ///     <b>An element <see cref="Type" />, not an <c>IEntityType</c>, and that is the whole
    ///     difference from <see cref="CreateFromSqlRoot" />.</b> A scalar root has no entity type
    ///     — nothing for the server's model to resolve — so the server rebuilds this one from the
    ///     CLR type the wire carried. The three-argument constructor is the one EF's own
    ///     <c>DetachQueryProvider</c> uses, so the root has no query provider, exactly like the
    ///     roots built beside it.
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
            + "constructors EF itself calls on every Database.SqlQuery query.")]
    internal static Expression CreateSqlQueryRoot(Type elementType, string sql, Expression argument)
    {
        Type type = ResolveSqlQueryRootType()
            ?? throw new InvalidOperationException(
                $"'{SqlQueryRootFullName}' was not found. A server that permits raw SQL execution "
                + "must reference a relational EF Core provider.");

        return (Expression)Activator.CreateInstance(type, elementType, sql, argument)!;
    }

    private static Type? ResolveFromSqlRootType()
        => Type.GetType($"{FromSqlRootFullName}, {RelationalAssemblyName}", throwOnError: false)
            ?? ScanLoadedAssemblies(FromSqlRootFullName);

    /// <inheritdoc cref="ResolveFromSqlRootType" />
    private static Type? ResolveSqlQueryRootType()
        => Type.GetType($"{SqlQueryRootFullName}, {RelationalAssemblyName}", throwOnError: false)
            ?? ScanLoadedAssemblies(SqlQueryRootFullName);

    /// <summary>
    ///     The fallback: the same full name against every assembly already loaded.
    /// </summary>
    /// <remarks>
    ///     <b>The two resolvers above keep their own <see cref="Type.GetType(string, bool)" />
    ///     call rather than sharing this one, and that is measured rather than stylistic.</b>
    ///     Interpolating two <c>const</c> strings folds to a literal, which the trim analyzer can
    ///     read; passing the name as a PARAMETER does not, and it then raises <c>IL2057</c>. Both
    ///     calls written as one shared method taking a <c>string</c> failed
    ///     <c>eng/trim-ratchet.sh</c> at 90 against a baseline of 89, and splitting them put it
    ///     back to 89. This is the same lesson as the <c>foreach</c> note below, from the other
    ///     direction: a suppression covers a member's own body, and so does the analyzer's ability
    ///     to see a constant.
    ///     <para>
    ///         <b>A <c>foreach</c> rather than a <c>Select</c> below, and that is not style
    ///         either.</b> An <see cref="UnconditionalSuppressMessageAttribute" /> covers the
    ///         annotated member's own body, and a lambda compiles to a member of its own — so the
    ///         same call written as <c>.Select(a =&gt; a.GetType(...))</c> reported its IL2026
    ///         against the closure class, outside the suppression, and raised the trim count by
    ///         one.
    ///     </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access",
        Justification = "EF Core's own query-root expressions, looked up by full name in the "
            + "assemblies already loaded. A server that has granted raw SQL execution references a "
            + "relational provider, which roots the types; where it does not, this returns null "
            + "and the caller raises a named error rather than misbehaving.")]
    private static Type? ScanLoadedAssemblies(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType(fullName, throwOnError: false) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
