// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.Relational;

/// <summary>
///     The client's convention set when the backing store is a relational database (#97, level 2).
/// </summary>
/// <remarks>
///     <para>
///         <b>It EXTENDS the core builder rather than standing beside it.</b>
///         <see cref="InfoCarrierConventionSetBuilder" /> stays the one builder; this subclass
///         takes its set and adds the relational conventions EF already ships. That is the owner's
///         rule for this package, stated 2026-09-03: <c>InfoCarrier.Core.Relational</c> overwrites
///         or extends <c>InfoCarrier.Core</c>'s DI services and never carries a near-copy beside
///         one. Two implementations of a single fact in two packages drift, and the drift is
///         silent.
///     </para>
///     <para>
///         <b>What it deletes.</b> <c>InfoCarrierHierarchyMappingConvention</c> — 131 hand-written
///         lines, a deliberately narrower copy of EF's, with four <c>Relational:</c> strings it had
///         to spell by hand and a pin test to keep them honest. EF's own convention is used now, so
///         a rename in EF is a compile error rather than silent wrong behaviour.
///     </para>
///     <para>
///         <b>Why the relational dependency object can be a stub, and why the stub throws.</b>
///         Every convention EF ships takes <see cref="RelationalConventionSetBuilderDependencies" />
///         and the four registered here <b>never touch it</b> — read from EF's source in R123, and
///         measured again in R170, where a full spec run built every one of these models without
///         reaching a stub member. The two things that object carries, an
///         <c>IRelationalAnnotationProvider</c> and an <c>IUpdateSqlGenerator</c>, are command-side
///         services a client with no database cannot have. That is this package's charter
///         (<c>architecture.md</c> §6a D3): annotations and type identity, never a connection or
///         anything standing for one. So every member of both stubs throws. If EF ever starts
///         calling one, this fails loudly at model build instead of answering plausibly and
///         wrongly — the same reasoning ADR-013 records for
///         <see cref="InfoCarrierRelationalFacadeDependencies" />'s three throwing members.
///     </para>
/// </remarks>
/// <param name="dependencies">EF's core convention-set dependencies.</param>
/// <param name="documentMapping">The document-mapping seam the base builder needs.</param>
public class InfoCarrierRelationalConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    InfoCarrier.Core.Metadata.IInfoCarrierDocumentMapping documentMapping)
    : InfoCarrierConventionSetBuilder(dependencies, documentMapping)
{
    /// <inheritdoc />
    public override ConventionSet CreateConventionSet()
    {
        ConventionSet conventionSet = base.CreateConventionSet();

        // THE FIX-UP CONVENTIONS, AND ONLY THOSE. Each of the three below answers one question:
        // when EF replaces an entity type or a property during model building, what happens to the
        // relational annotation that was hanging off the old instance? EF's own answer is to move
        // it, and a client that does not move it keeps a STALE instance where the server's model
        // has the finalized one:
        //
        //     Expected: EntityType: Book.Label#BookLabel CLR Type: BookLabel Owned
        //     Actual:   EntityType: BookLabel Keyless Owned
        //
        // None of the three decides a name, a type or a shape, so none of them can disagree with
        // the server about one. That is what makes them safe here and the rest of EF's list not.
        conventionSet.Add(new PropertyOverridesConvention(Dependencies, RelationalDependencies));
        conventionSet.Add(new CheckConstraintConvention(Dependencies, RelationalDependencies));
        conventionSet.Add(new StoredProcedureConvention(Dependencies, RelationalDependencies));

        // Core EF gives every hierarchy a discriminator and this is the convention that takes it
        // back for TPT and TPC, so without it a client model keeps a discriminator the server's
        // model has dropped. It was the only entry here between R123 and R170.
        conventionSet.Add(new EntityTypeHierarchyMappingConvention(Dependencies, RelationalDependencies));

        // EF'S WHOLE RELATIONAL LIST WAS TRIED HERE FIRST AND IT COST 681 TESTS (R170, measured:
        // 43 -> 724). Read `RelationalConventionSetBuilder.CreateConventionSet` beside this method
        // for what is missing, and do not add an entry back without measuring it alone. What the
        // 724 were made of says why:
        //
        //   * ~560 were JSON queries, from `RelationalMapToJsonConvention`;
        //   * 114 were `EntitySplittingQueryInfoCarrierTest`, and `EntitySplittingConvention` was
        //     confirmed to be the whole of that on its own;
        //   * the rest were compiled-model and bulk-update tests.
        //
        // THE SHAPE OF THE MISTAKE GENERALISES. A convention that decides a table name, a column
        // name, a JSON container or a value-generation strategy makes a decision the SERVER also
        // makes, with a provider this client cannot see. When the two agree the convention is
        // redundant; when they disagree the client's answer is the wrong one, because the store is
        // the server's. Only a fix-up convention -- one that moves an annotation the caller wrote
        // -- is free of that, because the caller wrote the same thing on both sides.
        //
        // `RelationalMaxIdentifierLengthConvention` is not in EF's list at all: each provider adds
        // it with its own limit, which is the concrete-store fact that stays on the server.
        //
        // THE TWO EF REPLACES HERE AND THIS BUILDER DOES NOT are `KeyDiscoveryConvention` and
        // `QueryFilterRewritingConvention`. `InfoCarrierConventionSetBuilder` has already put its
        // own in both slots, and both exist for a reason the relational half does not remove: key
        // discovery has to agree with the document-mapping seam, and the filter rewriter has to
        // leave a `FromSql` root alone. Replacing them with EF's would undo that silently.
        //
        // `RuntimeModelConvention` is left alone for the same shape of reason: EF's relational one
        // writes the store schema into the compiled model, and this client has no schema to write.
        return conventionSet;
    }

    /// <summary>
    ///     The relational dependency object EF's conventions declare and this one does not read.
    /// </summary>
    /// <remarks>
    ///     Built here rather than injected, because neither service it names exists on a client
    ///     with no database. See the class remarks for why a stub is sound and why it throws.
    /// </remarks>
    protected virtual RelationalConventionSetBuilderDependencies RelationalDependencies { get; }
        = new(new NoAnnotationProvider(), new NoUpdateSqlGenerator());

    private static InvalidOperationException NoDatabase(string member)
        => new(
            $"The InfoCarrier client has no database of its own, so '{member}' has no value here. "
            + "InfoCarrier.Core.Relational supplies relational METADATA to the client model and "
            + "nothing that reaches a connection. A caller arriving here wants the SERVER's "
            + "command pipeline, which does not cross the wire.");

    /// <summary>
    ///     The annotation provider a client with no database does not have. Every member throws.
    /// </summary>
    /// <remarks>
    ///     It exists only to fill <see cref="RelationalConventionSetBuilderDependencies" />, which
    ///     <see cref="EntityTypeHierarchyMappingConvention" /> holds and never reads. Every member
    ///     here answers about the <em>store's</em> schema — tables, columns, sequences, triggers —
    ///     which is the far side of the wire. See the class remarks.
    /// </remarks>
    private sealed class NoAnnotationProvider : IRelationalAnnotationProvider
    {
        public IEnumerable<IAnnotation> For(IRelationalModel value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ITable value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IColumn value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IView value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IViewColumn value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ISqlQuery value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ISqlQueryColumn value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IStoreFunction value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IStoreFunctionParameter value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IFunctionColumn value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IStoreStoredProcedure value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IStoreStoredProcedureParameter value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IStoreStoredProcedureResultColumn value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IUniqueConstraint value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ITableIndex value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(IForeignKeyConstraint value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ISequence value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ICheckConstraint value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));

        public IEnumerable<IAnnotation> For(ITrigger value, bool designTime)
            => throw NoDatabase(nameof(IRelationalAnnotationProvider));
    }

    /// <summary>
    ///     The update SQL generator a client with no database does not have. Every member throws.
    /// </summary>
    /// <remarks>
    ///     The other half of the same stub, and the more obvious one: every member writes SQL for a
    ///     command this client will never issue. The server does the writing, against its own
    ///     provider.
    /// </remarks>
    private sealed class NoUpdateSqlGenerator : IUpdateSqlGenerator
    {
        public void AppendBatchHeader(StringBuilder commandStringBuilder)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public void PrependEnsureAutocommit(StringBuilder commandStringBuilder)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public void AppendNextSequenceValueOperation(StringBuilder commandStringBuilder, string name, string? schema)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public void AppendObtainNextSequenceValueOperation(StringBuilder commandStringBuilder, string name, string? schema)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public string GenerateNextSequenceValueOperation(string name, string? schema)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public string GenerateObtainNextSequenceValueOperation(string name, string? schema)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public ResultSetMapping AppendInsertOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public ResultSetMapping AppendUpdateOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public ResultSetMapping AppendDeleteOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));

        public ResultSetMapping AppendStoredProcedureCall(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => throw NoDatabase(nameof(IUpdateSqlGenerator));
    }
}
