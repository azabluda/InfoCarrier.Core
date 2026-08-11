// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Replays a client's change-tracker entries against the server's real
///     <see cref="DbContext" /> (wire-protocol §2.2, research-findings §9).
/// </summary>
/// <remarks>
///     <para>
///         The entries are attached to a real change tracker and handed to EF, which does the
///         ordering, the fixup, the concurrency check and the store round trip. Nothing here
///         reimplements any of that — the server's job is to reconstitute state, not to persist
///         it itself.
///     </para>
///     <para>
///         Store-generated values go back keyed by correlation id, never by key value: the
///         client's key for an inserted row was temporary, which is the whole reason the
///         correlation id exists.
///     </para>
/// </remarks>
public class ServerSaveChangesExecutor
{
    private readonly DbContext _context;
    private readonly DynamicValueMapper _mapper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServerSaveChangesExecutor" /> class.
    /// </summary>
    public ServerSaveChangesExecutor(DbContext context, DynamicValueMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <summary>
    ///     Applies the request and returns the store-generated values.
    /// </summary>
    public virtual async Task<SaveChangesResult> ExecuteAsync(
        SaveChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tracked = new List<(int CorrelationId, EntityEntry Entry, IEntityType EntityType, EntityState State)>();
        var pending = new List<Replay>();

        // What each of the client's placeholder key values turned into here. A placeholder is
        // meaningless to this store, so the server generates its own key and every reference to
        // the client's has to be redirected at it.
        var placeholders = new Dictionary<object, (object? Value, bool IsTemporary)>();

        // The same, qualified by the key property the placeholder belongs to — and it is what
        // makes the redirection correct rather than merely usual.
        //
        // **A placeholder value is not unique across entity types.** The client draws them from
        // EF's temporary generator, which counts down from `int.MinValue` *per key property*, so
        // `Optional1.Id` and `Optional2.Id` hand out the very same numbers in the same request.
        // Keyed by value alone the later registration wins, and a foreign key pointing at the
        // first row resolves to the second:
        //
        //     REGISTER Optional1Derived.Id       client=-2147482643 -> 4
        //     REGISTER Optional2.Id              client=-2147482643 -> 5     <- same number
        //     RESOLVE  Optional2Derived.ParentId client=-2147482643 -> 5     <- wanted 4
        //
        // That is a **wrong foreign key written to the store**: `Optional2MoreDerived.ParentId`
        // came out as 6, and no `Optional1` has key 6, so the row is unreachable from the graph
        // that owns it. It is order-dependent — the collision only bites when the second
        // registration lands before the borrower resolves — which is why one parameterization of
        // `Save_optional_many_to_one_dependents` failed and the other thirty-five did not (C76).
        //
        // Qualified first, value-only as the fallback: a reference whose principal key property
        // cannot be named (a key borrowed other than through a foreign key) keeps exactly the
        // behaviour it had.
        var qualifiedPlaceholders = new Dictionary<(IProperty Property, object Value), (object? Value, bool IsTemporary)>();

        foreach (ChangeEntry change in request.Entries)
        {
            IEntityType entityType = _context.Model.FindEntityType(change.EntityTypeName)
                ?? throw new InvalidOperationException(
                    $"Entity type '{change.EntityTypeName}' is not in the server model.");

            // Populate the object *before* it reaches the change tracker. Assigning a key
            // through a tracked entry is refused outright — "the property 'Blog.Id' is part of a
            // key and so cannot be modified" — because EF reads that as re-keying a row rather
            // than as describing one.
            var shadow = new List<(IProperty Property, object? Value)>();
            var generatedKeys = new List<(IProperty Property, object ClientValue, bool IssuedAtSave)>();
            var references = new List<(IProperty Property, object ClientValue)>();
            var values = new List<(IProperty Property, object? Value)>();

            // Complex values cannot join `values`: that list feeds a `ValueBuffer` indexed by
            // `IProperty.GetIndex()`, and a complex property is not an `IProperty`. They are set
            // on the materialized object instead, which is also how the client's materializer
            // puts them back.
            var complexValues = new List<(IComplexProperty Property, object? Value)>();
            var temporaryNames = new HashSet<string>(change.TemporaryProperties ?? [], StringComparer.Ordinal);
            bool isAdded = change.State == nameof(EntityState.Added);

            foreach (DynamicPropertyValue value in ChangeEntryMapper.ReadValues(change.SerializedValues))
            {
                if (entityType.FindProperty(value.Name) is not { } property)
                {
                    if (entityType.FindComplexProperty(value.Name) is { } complexProperty)
                    {
                        complexValues.Add(
                            (complexProperty, _mapper.FromPropertyValue(value, complexProperty.ClrType)));
                    }

                    continue;
                }

                object? clrValue = PrimitiveCoercion.FromWireValue(
                    property, _mapper.FromPropertyValue(value, PrimitiveCoercion.WireType(property)));

                // "A key this store issues" is `ValueGenerated`, not "is not a foreign key".
                // The two agree for every ordinary model, which is why the FK test stood in for
                // it — but they part company on a **key cycle**, and there the FK test gives the
                // wrong answer for a property that is both. `CompositePrincipal.Id` is
                // `ValueGeneratedOnAdd()` *and* half of the foreign key `{Id, CurrentNumber}`
                // back to its own dependent, so the old guard sent it down the ordinary-value
                // path and the client's placeholder `-2147482646` was **written to the store as
                // an explicit key** — confirmed by reading the row.
                //
                // A borrowed placeholder is still excluded, because a borrowed one is by
                // definition not generated here: `CompositeDependent.PrincipalId` is
                // `ValueGenerated.Never` and stays a reference, as it must.
                if (isAdded
                    && clrValue is not null
                    && temporaryNames.Contains(property.Name)
                    && property.ValueGenerated is ValueGenerated.OnAdd or ValueGenerated.OnAddOrUpdate)
                {
                    // The client's own placeholder for a key this store issues.
                    bool issuedAtSave = IssuedAtSave(property, entityType);
                    generatedKeys.Add((property, clrValue, issuedAtSave));

                    if (!issuedAtSave)
                    {
                        // A backend that generates at `Add` time rather than at save (EF's
                        // InMemory provider, whose
                        // `InMemoryIntegerValueGenerator.GeneratesTemporaryValues` is `false`) has
                        // a *real* value to offer, and it is better than the placeholder. EF only
                        // runs value generation for a property still holding its sentinel, so the
                        // property is left unset for it to fill in.
                        continue;
                    }

                    // A store that issues the key at save time has nothing better to offer, and
                    // letting EF generate here is the C11 defect: its temporary generator counts
                    // down from `int.MinValue`, which is the same range the *client's* generator
                    // drew this placeholder from, and the two counters advance independently. When
                    // the client's is one ahead, EF hands the second entry of a request the very
                    // value the first entry was then forced onto, and the identity map refuses it.
                    // Putting the placeholder on the object here means generation never runs for
                    // this property at all, so there is no second value to collide with anything.
                    // `TrackOne` flags it temporary once the entry exists.
                }

                values.Add((property, clrValue));
            }

            object entity = Materialize(entityType, values);

            foreach ((IComplexProperty complexProperty, object? complexValue) in complexValues)
            {
                // Through the backing field where there is one, as everything else that writes a
                // member during materialization does (plan L6).
                if (complexProperty.FieldInfo is { } field)
                {
                    field.SetValue(entity, complexValue);
                }
                else if (complexProperty.PropertyInfo is { CanWrite: true } clrProperty)
                {
                    clrProperty.SetValue(entity, complexValue);
                }
            }

            pending.Add(new Replay(change, entity, entityType, shadow, generatedKeys, references, isAdded));
            pending[^1].Values.AddRange(values);
        }

        // Every placeholder this request expects to resolve, and the types they are.
        var expected = new HashSet<object>(pending.SelectMany(p => p.GeneratedKeys).Select(g => g.ClientValue));
        var expectedTypes = new HashSet<Type>(expected.Select(v => v.GetType()));

        // A borrowed placeholder is recognised by its **value**, not by the client's temporary
        // flag. EF only flags a value it generated as temporary, and a test — or an application —
        // that reparents a row by assigning the FK itself (`old1.RootId = newRoot.Id`) produces
        // an ordinary `int` that happens to be a placeholder. Relying on the flag left every
        // reparent pointing at a key that was about to stop existing. The candidate set is
        // exactly the placeholders above, so a false positive needs a stored key that equals one
        // of them in the same request.
        foreach (Replay replay in pending)
        {
            foreach ((IProperty property, object? value) in replay.Values)
            {
                // Only a key can be a borrowed placeholder, and only of a type some placeholder
                // actually is. Both guards earn their keep: `GraphUpdatesTestBase.MyDiscriminator`
                // throws from `GetHashCode` on purpose, so an unguarded set lookup over every
                // value in the request fails on a property that could never have been a key.
                if (value is not null
                    && (property.IsKey() || property.IsForeignKey())
                    && expectedTypes.Contains(value.GetType())
                    && expected.Contains(value))
                {
                    replay.References.Add((property, value));
                }
                else
                {
                    SetOnEntity(replay.Entity, property, value, replay.Shadow);
                }
            }
        }

        // Attach the entries describing rows that already exist before those describing new
        // ones. An `Added` and a `Deleted` entry may legitimately carry the same alternate key —
        // the client deletes a dependent and adds its replacement in one SaveChanges — and EF
        // permits that, but only in this order.
        //
        // `IdentityMap.Add` decides a conflict on
        // `(entry.State == Deleted) == (existing.State == Deleted)`, and it runs from
        // `OnStateChanging`, i.e. *before* the new state is applied. An entry we are about to
        // make `Deleted` is therefore still `Detached` when it is judged: against an already
        // tracked `Added` entry that reads as "neither is deleted" and throws. Reversed, the
        // established row is genuinely `Deleted` by the time the new one arrives and EF lets it
        // through — which is the order the client itself reached the state in, having loaded the
        // row before adding its replacement.
        //
        // Relative order within each group is preserved: an `Added` principal's temporary key
        // has to be tracked before the dependent that borrows it.
        foreach (Replay replay in pending.Where(p => p.Change.State == nameof(EntityState.Deleted)))
        {
            TrackOne(replay);
        }

        // Everything else waits on whatever it borrows: a principal has to be tracked before the
        // row that holds its placeholder, because the borrower's own key may *be* that borrowed
        // value (a one-to-one sharing its principal's primary key, or any owned dependent) and by
        // then EF will not let it be changed. Repeatedly take whatever is resolvable; if a pass
        // makes no progress the remainder refers to itself, and replaying it as sent is no worse
        // than not replaying it at all.
        //
        // Seeded principal-first, which the placeholder rule above wants anyway and which the
        // *replace* case needs even when no placeholder is involved. Tracking a dependent runs
        // EF's fixup, and fixup finds its principal by key in the identity map. While the
        // `Deleted` half of a replaced row is the only entry under that key — which the ordering
        // above guarantees until its `Added` half is tracked — the new dependent is wired onto
        // the row being *deleted*, and `StateManager.CascadeDelete` reads exactly that navigation:
        // `FirstLaw:Deleted[11].SecondLaw` came out holding the replacement, which was then
        // detached as a cascade target and never saved. Ranking by depth in the model's foreign
        // key graph puts `FirstLaw:Added[11]` in the map first, so fixup finds the surviving row.
        var depth = new Dictionary<IEntityType, int>();
        var waiting = pending
            .Where(p => p.Change.State != nameof(EntityState.Deleted))
            .OrderBy(p => PrincipalDepth(p.EntityType, depth, []))
            .ToList();
        while (waiting.Count > 0)
        {
            int remaining = waiting.Count;

            for (int i = 0; i < waiting.Count; i++)
            {
                Replay replay = waiting[i];
                if (replay.References.Any(r => expected.Contains(r.ClientValue)
                        && !placeholders.ContainsKey(r.ClientValue)))
                {
                    continue;
                }

                TrackOne(replay);
                waiting.RemoveAt(i--);
            }

            if (waiting.Count == remaining)
            {
                foreach (Replay replay in waiting)
                {
                    TrackOne(replay);
                }

                break;
            }
        }

        // Diagnostic context for an identity conflict during replay, built only when one happens.
        //
        // **This is a standing probe for a known intermittent** (plan C11): two of one request's
        // rows have twice ended up under the same *temporary* key, about one full suite run in
        // four, and never in isolation. The previous attempt at catching it wrote a line per
        // tracked entry and perturbed the suite from 154 failures to 348 — file I/O per entry
        // under xUnit's parallel collections is not free. So nothing is written unless the
        // conflict actually occurs, and then everything the diagnosis needs is already in hand.
        //
        // It is also plain good behaviour: a server replaying someone else's change entries that
        // hits an identity conflict should say which entries collided. EF's own message names one
        // key and no entry at all.
        string Diagnose(Exception failure, Replay failing)
        {
            // Never let the diagnostic replace the fault it describes. This walks arbitrary
            // application values, and this suite contains a type whose members throw *on
            // purpose* — `GraphUpdatesTestBase.MyDiscriminator.GetHashCode` — which the
            // placeholder scan above already has to guard against.
            try
            {
                return DiagnoseCore(failure, failing);
            }
            catch (Exception diagnosticFailure)
            {
                return $"{Environment.NewLine}(InfoCarrier replay diagnostic unavailable: "
                    + $"{diagnosticFailure.GetType().Name}: {diagnosticFailure.Message})";
            }
        }

        string DiagnoseCore(Exception failure, Replay failing)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine().AppendLine(
                "InfoCarrier replay diagnostic (plan C11 — identity conflict while replaying a "
                + "client's change entries):");
            report.Append("  failing entry: ").Append(failing.EntityType.Name)
                .Append(" correlationId=").Append(failing.Change.CorrelationId)
                .Append(" state=").Append(failing.Change.State)
                .Append(" temporaryProperties=[")
                .Append(string.Join("|", failing.Change.TemporaryProperties ?? []))
                .AppendLine("]");

            report.Append("  placeholders resolved so far: ").AppendLine(
                placeholders.Count == 0
                    ? "(none)"
                    : string.Join(", ", placeholders.Select(p => $"{p.Key}->{p.Value.Value}(tmp={p.Value.IsTemporary})")));
            report.Append("  placeholder values expected in this request: ").AppendLine(
                expected.Count == 0 ? "(none)" : string.Join(", ", expected));

            report.AppendLine("  every entry in this request:");
            foreach (Replay other in pending)
            {
                report.Append("    ").Append(other.EntityType.Name)
                    .Append(" correlationId=").Append(other.Change.CorrelationId)
                    .Append(" state=").Append(other.Change.State)
                    .Append(" temporaryProperties=[")
                    .Append(string.Join("|", other.Change.TemporaryProperties ?? []))
                    .Append("] generatedKeys=[")
                    .Append(string.Join("|", other.GeneratedKeys.Select(g => $"{g.Property.Name}={g.ClientValue}")))
                    .Append("] borrowedReferences=[")
                    .Append(string.Join("|", other.References.Select(r => $"{r.Property.Name}={r.ClientValue}")))
                    .Append("] keyValuesSent=[")
                    .Append(string.Join(
                        "|",
                        other.Values.Where(v => v.Property.IsKey() || v.Property.IsForeignKey())
                            .Select(v => $"{v.Property.Name}={v.Value}")))
                    .AppendLine("]");
            }

            report.AppendLine("  entries already tracked, with the keys they landed on:");
            foreach ((int correlationId, EntityEntry trackedEntry, IEntityType trackedType, EntityState trackedState) in tracked)
            {
                report.Append("    ").Append(trackedType.Name)
                    .Append(" correlationId=").Append(correlationId)
                    .Append(" state=").Append(trackedState)
                    .Append(" key=[")
                    .Append(string.Join(
                        "|",
                        (trackedType.FindPrimaryKey()?.Properties ?? []).Select(
                            p => $"{p.Name}={trackedEntry.Property(p.Name).CurrentValue}"
                                + $"(tmp={trackedEntry.Property(p.Name).IsTemporary})")))
                    .AppendLine("]");
            }

            report.Append("  underlying: ").Append(failure.Message);
            return report.ToString();
        }

        void TrackOne(Replay replay)
        {
            EntityState state = Enum.Parse<EntityState>(replay.Change.State);

            // What nobody set has to arrive unset, and the value alone cannot say so — see
            // `ChangeEntry.SentinelProperties`. Materialization has just written the property's
            // *read* value into its member (`false` for an unset `bool?` field), which is a real
            // value as far as EF is concerned; putting the server's own sentinel back is what
            // leaves the column out of the `INSERT` so the store's default applies.
            //
            // Through the backing field first, because that is where the distinction lives: the
            // sentinel of a `bool` property read through a `bool?` field is `null`, and `null`
            // cannot be written through the `bool` setter at all.
            foreach (string name in replay.Change.SentinelProperties ?? [])
            {
                if (replay.EntityType.FindProperty(name) is { } sentinelProperty)
                {
                    SetSentinel(replay, sentinelProperty);
                }
            }

            // Redirect borrowed placeholders *before* tracking, while the key is still an
            // ordinary field on a detached object.
            foreach ((IProperty property, object clientValue) in replay.References)
            {
                if (Resolve(property, clientValue) is { } principal)
                {
                    SetOnEntity(replay.Entity, property, principal.Value, replay.Shadow);
                }
            }

            // The principal key property a placeholder held in `property` would belong to, asked
            // of the model rather than guessed from the value.
            (object? Value, bool IsTemporary)? Resolve(IProperty property, object clientValue)
            {
                foreach (IForeignKey foreignKey in property.GetContainingForeignKeys())
                {
                    int index = -1;
                    for (int i = 0; i < foreignKey.Properties.Count; i++)
                    {
                        if (foreignKey.Properties[i] == property)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0
                        && qualifiedPlaceholders.TryGetValue(
                            (foreignKey.PrincipalKey.Properties[index], clientValue), out var qualified))
                    {
                        return qualified;
                    }
                }

                return placeholders.TryGetValue(clientValue, out var principal) ? principal : null;
            }

            EntityEntry entry = Track(replay.EntityType, replay.Entity);

            // Shadow state goes on while the entry is still `Detached`, for the same reason the
            // values written directly onto the object go on before it is tracked at all: a key
            // assigned through a tracked entry is refused — "the property
            // `SponsorDetails.TitleSponsorId` is part of a key and so cannot be modified" —
            // because EF reads that as re-keying a row. An *owned* dependent's key is its
            // owner's, and it has no CLR member to receive it, so it arrives here rather than in
            // the pre-tracking pass; setting it after the state was what S3c-9 had already fixed
            // everywhere else. Detached is also the right moment: the identity map is not
            // written until the state is, so the entry lands under the key it actually has.
            foreach ((IProperty property, object? value) in replay.Shadow)
            {
                entry.Property(property.Name).CurrentValue = value;
            }

            try
            {
                entry.State = state;
            }
            catch (InvalidOperationException failure)
            {
                // Rethrown as the same type with the original message first, so an assertion that
                // matches on EF's wording still matches. The diagnostic is appended, and the
                // original is the inner exception.
                throw new InvalidOperationException(failure.Message + Diagnose(failure, replay), failure);
            }

            // A *partial* update writes only the properties the client actually changed. Setting
            // `State = Modified` marks every one of them modified, which is right for an entity
            // the client loaded and edited and wrong for the stub `Save_partial_update` attaches:
            // there `Name` was never set, and writing it put `null` over "Apple Cider".
            //
            // Which properties are modified is change-tracker state, not something the values
            // imply — every value is on the wire either way — so the client names them
            // (`ChangeEntry.ModifiedProperties`). Applied after the state, because that is what
            // set them all in the first place.
            if (state == EntityState.Modified && replay.Change.ModifiedProperties is { } modifiedProperties)
            {
                var modified = new HashSet<string>(modifiedProperties, StringComparer.Ordinal);

                foreach (IProperty property in replay.EntityType.GetProperties())
                {
                    // A key is never "modified" and EF refuses to be told otherwise on a tracked
                    // entry; `State = Modified` leaves it alone already.
                    if (!property.IsKey())
                    {
                        entry.Property(property.Name).IsModified = modified.Contains(property.Name);
                    }
                }
            }

            // What the store will call this row, so references to the client's placeholder can
            // find it. On a backend that generates at save time the value is itself temporary,
            // and that travels with it — every reference stays temporary too and EF replaces the
            // lot when the store answers.
            foreach ((IProperty property, object clientValue, bool issuedAtSave) in replay.GeneratedKeys)
            {
                PropertyEntry generated = entry.Property(property.Name);

                // This store issues the key during `SaveChanges` rather than at `Add` — every
                // relational one does — so the client's placeholder is exactly what the original
                // design wants: it is already on the entity (put there before tracking, so EF's
                // own temporary generator never ran — plan C11), and flagging it temporary is what
                // lets the store replace it and propagate to everything sharing it. Adopting EF's
                // temporary value instead would be a second placeholder doing the first one's job,
                // and the join row of a many-to-many between two new entities came out pointing at
                // neither.
                //
                // Only when the value is real — EF's InMemory provider assigns one from a value
                // generator at `Add` — is there anything better to redirect references to.
                if (issuedAtSave)
                {
                    generated.IsTemporary = true;
                }
                else if (generated.IsTemporary || Equals(generated.CurrentValue, property.Sentinel))
                {
                    // The generator was asked for a real value and had none to give: this store
                    // does generate at save after all, whatever its generator claimed.
                    generated.CurrentValue = clientValue;
                    generated.IsTemporary = true;
                }

                placeholders[clientValue] = (generated.CurrentValue, generated.IsTemporary);
                qualifiedPlaceholders[(property, clientValue)] = (generated.CurrentValue, generated.IsTemporary);
            }

            // A reference is a placeholder exactly as long as what it points at is one. An
            // unresolved reference keeps the client's flagging, which is what it had before any
            // of this existed.
            foreach ((IProperty property, object clientValue) in replay.References)
            {
                // Through the same `Resolve`, so both halves of a redirection read the same
                // principal. Inert in every request the suite has: `IssuedAtSave` is a property of
                // the store, so two colliding placeholders are both temporary or neither is. It
                // stops being inert the moment one model mixes generation strategies, and a
                // lookup that is right for the value and wrong for the row is what C76 was.
                entry.Property(property.Name).IsTemporary =
                    Resolve(property, clientValue) is not { } principal || principal.IsTemporary;
            }

            // The concurrency check's left-hand side, and the one thing here that cannot be
            // derived from the current values. Last, because setting the state re-snapshots
            // originals from whatever the entity holds — doing this any earlier is undone.
            if (replay.Change.SerializedOriginalValues is { } serializedOriginals)
            {
                foreach (DynamicPropertyValue value in ChangeEntryMapper.ReadValues(serializedOriginals))
                {
                    if (replay.EntityType.FindProperty(value.Name) is { } property)
                    {
                        entry.Property(property.Name).OriginalValue =
                            PrimitiveCoercion.FromWireValue(
                                property, _mapper.FromPropertyValue(value, PrimitiveCoercion.WireType(property)));
                    }
                }
            }

            tracked.Add((replay.Change.CorrelationId, entry, replay.EntityType, state));
        }

        int count = await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SaveChangesResult
        {
            Count = count,
            GeneratedValues = [.. tracked
                .Select(t => ReadGenerated(t.CorrelationId, t.Entry, t.EntityType, t.State))
                .Where(g => g is not null)
                .Select(g => g!)],
        };
    }

    /// <summary>
    ///     How far an entity type sits from a principal in the model's foreign key graph — the
    ///     longest chain of foreign keys leading to it.
    /// </summary>
    /// <remarks>
    ///     Purely an ordering, so a cycle needs no resolution, only termination: a type already on
    ///     the current path contributes nothing and the walk unwinds. <paramref name="path" />
    ///     is that path, and <paramref name="cache" /> is memoization — a wide model would
    ///     otherwise re-walk the same principals once per dependent.
    /// </remarks>
    private static int PrincipalDepth(
        IEntityType entityType,
        Dictionary<IEntityType, int> cache,
        HashSet<IEntityType> path)
    {
        if (cache.TryGetValue(entityType, out int cached))
        {
            return cached;
        }

        if (!path.Add(entityType))
        {
            return 0;
        }

        int depth = 0;
        foreach (IForeignKey foreignKey in entityType.GetForeignKeys())
        {
            depth = Math.Max(depth, 1 + PrincipalDepth(foreignKey.PrincipalEntityType, cache, path));
        }

        path.Remove(entityType);

        // Only a depth reached without truncating a cycle is worth keeping: one computed while
        // this type was already on the path is a lower bound, not the answer.
        if (path.Count == 0)
        {
            cache[entityType] = depth;
        }

        return depth;
    }

    /// <summary>
    ///     Whether this store issues the property's value when the row is saved rather than when
    ///     it is added — which is what decides whether the client's placeholder goes onto the
    ///     entity before tracking (plan C11).
    /// </summary>
    /// <remarks>
    ///     The question the previous design asked <em>after</em> tracking, by looking at what EF
    ///     had produced. Asking EF's own selector instead means the answer is in hand before
    ///     anything is tracked, and — the point of the change — means EF's temporary value
    ///     generator is never run for a property whose value we are about to overwrite. It counts
    ///     down from <see cref="int.MinValue" />, which is the same range the client's generator
    ///     drew the placeholder from.
    /// </remarks>
    private bool IssuedAtSave(IProperty property, IEntityType entityType)
        => !_context.GetService<Microsoft.EntityFrameworkCore.ValueGeneration.IValueGeneratorSelector>()
                .TrySelect(property, entityType, out Microsoft.EntityFrameworkCore.ValueGeneration.ValueGenerator? generator)
            || generator is null
            || generator.GeneratesTemporaryValues;

    /// <summary>
    ///     One entry of the request, unpacked and waiting to be tracked.
    /// </summary>
    /// <param name="GeneratedKeys">
    ///     Temporary keys the client made up that this store issues itself. Where the store
    ///     issues at save time the client's placeholder is put straight onto
    ///     <paramref name="Entity" /> and <c>IssuedAtSave</c> is true; where the store issues a
    ///     real value at <c>Add</c> the property is left unset for it to fill in. Either way the
    ///     client's value is kept so references to it can be redirected.
    /// </param>
    /// <param name="References">
    ///     Temporary foreign keys — placeholders belonging to some other entry.
    /// </param>
    private sealed record Replay(
        ChangeEntry Change,
        object Entity,
        IEntityType EntityType,
        List<(IProperty Property, object? Value)> Shadow,
        List<(IProperty Property, object ClientValue, bool IssuedAtSave)> GeneratedKeys,
        List<(IProperty Property, object ClientValue)> References,
        bool IsAdded)
    {
        /// <summary>
        ///     Every value the entry carried, held until the full set of placeholders is known
        ///     and each can be classified as a borrowed one or an ordinary value.
        /// </summary>
        public List<(IProperty Property, object? Value)> Values { get; } = [];
    }

    /// <summary>
    ///     Reconstitutes the entity the wire describes, through EF's own materializer.
    /// </summary>
    /// <remarks>
    ///     <c>Activator.CreateInstance</c> was enough only while every entity had a parameterless
    ///     constructor. `WithConstructorsTestBase` is the model that says otherwise — a
    ///     constructor-bound `Blog(int id, string title, …)`, and a `BlogAsImmutableRecord` that
    ///     is a positional record — and both came back "No parameterless constructor defined".
    ///     The materializer is what performs constructor binding, so the values are laid into a
    ///     value buffer indexed by <c>IProperty.GetIndex()</c> and handed to it, exactly as
    ///     <see cref="ClientResultMaterializer" /> does on the other side. The pass that follows
    ///     writes the same values onto the object again, which is what carries the ones no
    ///     constructor parameter claimed.
    /// </remarks>
    private object Materialize(IEntityType entityType, List<(IProperty Property, object? Value)> values)
    {
        var runtimeType = (IRuntimeEntityType)entityType;
        var buffer = new object?[runtimeType.PropertyCount];

        // A property the request omits takes its sentinel rather than a CLR default, which is
        // EF's own rule for an absent value.
        foreach (IProperty property in entityType.GetFlattenedProperties())
        {
            if (property.GetIndex() is >= 0 and var index)
            {
                buffer[index] = property.Sentinel;
            }
        }

        foreach ((IProperty property, object? value) in values)
        {
            if (property.GetIndex() is >= 0 and var index)
            {
                buffer[index] = value;
            }
        }

        return runtimeType.GetOrCreateMaterializer(_context.GetService<IStructuralTypeMaterializerSource>())(
            new MaterializationContext(new ValueBuffer(buffer), _context));
    }

    /// <summary>
    ///     Writes one mapped value onto the object, or queues it as shadow state.
    /// </summary>
    /// <remarks>
    ///     A shared-type entity — a many-to-many join entity is one — stores its values in a
    ///     dictionary, and EF reports its properties as the <c>Item[string]</c> indexer. Handing
    ///     that to <c>SetValue</c> without an index is a parameter-count mismatch, so it goes
    ///     down the entry path with the shadow properties instead.
    /// </remarks>
    private static void SetOnEntity(
        object entity,
        IProperty property,
        object? value,
        List<(IProperty Property, object? Value)> shadow)
    {
        if (property.PropertyInfo is { } propertyInfo
            && propertyInfo.CanWrite
            && propertyInfo.GetIndexParameters().Length == 0)
        {
            propertyInfo.SetValue(entity, value);
        }
        else if (property.FieldInfo is { } fieldInfo)
        {
            fieldInfo.SetValue(entity, value);
        }
        else
        {
            // A shadow property has no CLR member; its value lives in the entry, so it has to
            // wait until there is one. Replacing an earlier queued value rather than appending
            // keeps a redirected reference from being overwritten by the value it replaced.
            shadow.RemoveAll(s => s.Property == property);
            shadow.Add((property, value));
        }
    }

    /// <summary>
    ///     Puts a property the client never set back to this model's sentinel.
    /// </summary>
    /// <remarks>
    ///     The mirror of <see cref="SetOnEntity" /> and deliberately not the same order: that one
    ///     writes a <em>value</em> and prefers the CLR property, this one writes the <em>absence</em>
    ///     of one and prefers the backing field, which is the only member that can hold it. A
    ///     property with neither — a shadow property, or a shared-type entity's indexer — keeps its
    ///     sentinel in the entry, which is where its values live anyway.
    /// </remarks>
    private static void SetSentinel(Replay replay, IProperty property)
    {
        if (property.FieldInfo is { } fieldInfo)
        {
            fieldInfo.SetValue(replay.Entity, property.Sentinel);
        }
        else if (property.PropertyInfo is { CanWrite: true } propertyInfo
                 && propertyInfo.GetIndexParameters().Length == 0)
        {
            propertyInfo.SetValue(replay.Entity, property.Sentinel);
        }
        else
        {
            replay.Shadow.RemoveAll(s => s.Property == property);
            replay.Shadow.Add((property, property.Sentinel));
        }
    }

    /// <summary>
    ///     Gets the entry for an instance, by entity type rather than by CLR type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>DbContext.Entry</c> resolves by CLR type, which cannot identify a shared-type
    ///         entity: several of them have the same <c>Dictionary&lt;string, object&gt;</c> CLR
    ///         type and are told apart only by name. A many-to-many join entity is exactly that.
    ///     </para>
    ///     <para>
    ///         This used to reach for <c>DbContext.Set&lt;T&gt;(name)</c> by reflection whenever
    ///         the type was shared. An <em>owned</em> type is also shared — <c>Owner.Owned#Owned</c>
    ///         — and <c>Set&lt;T&gt;(name)</c> refuses it outright: "must be accessed through its
    ///         owning entity type". Asking the state manager for the entry names the entity type
    ///         directly, which is the identity the request carries, and covers ordinary, shared
    ///         and owned types by one call with no reflection.
    ///     </para>
    /// </remarks>
    private EntityEntry Track(IEntityType entityType, object entity)
        => new(_context.GetService<IStateManager>().GetOrCreateEntry(entity, entityType));

    /// <summary>
    ///     Collects the values the store produced for one entry.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only properties the store could have generated <em>for this state</em>. Returning
    ///         everything marked store-generated sent an inserted row's key back for a
    ///         <c>Modified</c> entry too, and the client then refused it — "the property
    ///         'Blog.Id' is part of a key and so cannot be modified" — because re-keying a
    ///         tracked row is exactly what EF must not allow.
    ///     </para>
    ///     <para>
    ///         A deleted row generates nothing worth returning.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     The principal-key property that <paramref name="property" /> points at through
    ///     <paramref name="foreignKey" />, matched by position — a composite foreign key's third
    ///     column answers to the principal key's third column, not to its first.
    /// </summary>
    private static IProperty? PrincipalPropertyOf(IForeignKey foreignKey, IProperty property)
    {
        for (int i = 0; i < foreignKey.Properties.Count; i++)
        {
            if (foreignKey.Properties[i] == property)
            {
                return foreignKey.PrincipalKey.Properties[i];
            }
        }

        return null;
    }

    private GeneratedValues? ReadGenerated(
        int correlationId,
        EntityEntry entry,
        IEntityType entityType,
        EntityState state)
    {
        if (state == EntityState.Deleted)
        {
            return null;
        }

        var values = new List<DynamicPropertyValue>();

        foreach (IProperty property in entityType.GetProperties())
        {
            bool generated = state == EntityState.Added
                ? property.ValueGenerated is ValueGenerated.OnAdd or ValueGenerated.OnAddOrUpdate
                : property.ValueGenerated is ValueGenerated.OnUpdate or ValueGenerated.OnAddOrUpdate;

            // A store decides a value indirectly as well as directly. A foreign key onto a
            // store-generated principal key is `ValueGenerated.Never` — nothing generates it —
            // and yet it holds a number only the store knows, put there by EF's propagation.
            //
            // **Only when that FK is also part of this row's own key**, which is the case where
            // the client cannot recover it any other way. `CompositeDependent.PrincipalId` is
            // half of its own primary key, the client held a placeholder for it, and without this
            // the read-back `Single(e => e.PrincipalId == id)` looked for the placeholder and
            // found nothing.
            //
            // The wider rule — every propagated FK — is measured and wrong: **1 fixed, 2 broken**
            // (`c42`), two `Save_optional_many_to_one_dependents` parameterizations. An ordinary
            // FK is the client's *own* business, reached by EF's fixup from the principal key
            // that does come back, and sending it re-asserts a relationship the client may have
            // deliberately changed. A key is different: it identifies the row, and a row cannot
            // disagree with the store about its own identity.
            if (!generated && state == EntityState.Added && property.IsKey())
            {
                generated = property.GetContainingForeignKeys().Any(
                    fk => PrincipalPropertyOf(fk, property) is
                    { ValueGenerated: ValueGenerated.OnAdd or ValueGenerated.OnAddOrUpdate });
            }

            if (!generated)
            {
                continue;
            }

            values.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                Value = _mapper.ToDynamicValue(
                    PrimitiveCoercion.ToWireValue(property, entry.Property(property.Name).CurrentValue),
                    PrimitiveCoercion.WireType(property)),
            });
        }

        return values.Count == 0
            ? null
            : new GeneratedValues
            {
                CorrelationId = correlationId,
                SerializedValues = ChangeEntryMapper.Serialize(entityType, values, _mapper),
            };
    }
}
