// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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

        foreach (ChangeEntry change in request.Entries)
        {
            IEntityType entityType = _context.Model.FindEntityType(change.EntityTypeName)
                ?? throw new InvalidOperationException(
                    $"Entity type '{change.EntityTypeName}' is not in the server model.");

            object entity = Activator.CreateInstance(entityType.ClrType)
                ?? throw new InvalidOperationException(
                    $"'{entityType.DisplayName()}' has no parameterless constructor, so the server "
                        + "cannot reconstitute it for SaveChanges.");

            // Populate the object *before* it reaches the change tracker. Assigning a key
            // through a tracked entry is refused outright — "the property 'Blog.Id' is part of a
            // key and so cannot be modified" — because EF reads that as re-keying a row rather
            // than as describing one.
            var shadow = new List<(IProperty Property, object? Value)>();
            var generatedKeys = new List<(IProperty Property, object ClientValue)>();
            var references = new List<(IProperty Property, object ClientValue)>();
            var values = new List<(IProperty Property, object? Value)>();
            var temporaryNames = new HashSet<string>(change.TemporaryProperties ?? [], StringComparer.Ordinal);
            bool isAdded = change.State == nameof(EntityState.Added);

            foreach (DynamicPropertyValue value in ChangeEntryMapper.ReadValues(change.SerializedValues))
            {
                if (entityType.FindProperty(value.Name) is not { } property)
                {
                    continue;
                }

                object? clrValue = _mapper.FromPropertyValue(value, property.ClrType);

                if (isAdded
                    && clrValue is not null
                    && temporaryNames.Contains(property.Name)
                    && !property.IsForeignKey())
                {
                    // The client's own placeholder for a key this store issues. Left unset,
                    // because EF only runs value generation for a property still holding its
                    // sentinel — and a backend that generates at `Add` time rather than at save
                    // (EF's InMemory provider, whose
                    // `InMemoryIntegerValueGenerator.GeneratesTemporaryValues` is `false`)
                    // otherwise keeps the client's placeholder and hands it straight back.
                    generatedKeys.Add((property, clrValue));
                    continue;
                }

                values.Add((property, clrValue));
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
        var waiting = pending.Where(p => p.Change.State != nameof(EntityState.Deleted)).ToList();
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

        void TrackOne(Replay replay)
        {
            EntityState state = Enum.Parse<EntityState>(replay.Change.State);

            // Redirect borrowed placeholders *before* tracking, while the key is still an
            // ordinary field on a detached object.
            foreach ((IProperty property, object clientValue) in replay.References)
            {
                if (placeholders.TryGetValue(clientValue, out var principal))
                {
                    SetOnEntity(replay.Entity, property, principal.Value, replay.Shadow);
                }
            }

            EntityEntry entry = Track(replay.EntityType, replay.Entity);
            entry.State = state;

            foreach ((IProperty property, object? value) in replay.Shadow)
            {
                entry.Property(property.Name).CurrentValue = value;
            }

            // What the store will call this row, so references to the client's placeholder can
            // find it. On a backend that generates at save time the value is itself temporary,
            // and that travels with it — every reference stays temporary too and EF replaces the
            // lot when the store answers.
            foreach ((IProperty property, object clientValue) in replay.GeneratedKeys)
            {
                PropertyEntry generated = entry.Property(property.Name);

                // No *real* value appeared, so this store issues the key during `SaveChanges`
                // rather than at `Add` — every relational one does. There the client's
                // placeholder is exactly what the original design wants: put it back, flag it,
                // and let the store replace it and propagate to everything sharing it. Adopting
                // EF's own temporary value instead would be a second placeholder doing the first
                // one's job, and the join row of a many-to-many between two new entities came out
                // pointing at neither.
                //
                // Only when the value is real — EF's InMemory provider assigns one from a value
                // generator at `Add` — is there anything better to redirect references to.
                if (generated.IsTemporary || Equals(generated.CurrentValue, property.Sentinel))
                {
                    generated.CurrentValue = clientValue;
                    generated.IsTemporary = true;
                }

                placeholders[clientValue] = (generated.CurrentValue, generated.IsTemporary);
            }

            // A reference is a placeholder exactly as long as what it points at is one. An
            // unresolved reference keeps the client's flagging, which is what it had before any
            // of this existed.
            foreach ((IProperty property, object clientValue) in replay.References)
            {
                entry.Property(property.Name).IsTemporary =
                    !placeholders.TryGetValue(clientValue, out var principal) || principal.IsTemporary;
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
    ///     One entry of the request, unpacked and waiting to be tracked.
    /// </summary>
    /// <param name="GeneratedKeys">
    ///     Temporary keys the client made up that this store issues itself, deliberately left
    ///     unset on <paramref name="Entity" />. The client's value is kept only so references to
    ///     it can be redirected.
    /// </param>
    /// <param name="References">
    ///     Temporary foreign keys — placeholders belonging to some other entry.
    /// </param>
    private sealed record Replay(
        ChangeEntry Change,
        object Entity,
        IEntityType EntityType,
        List<(IProperty Property, object? Value)> Shadow,
        List<(IProperty Property, object ClientValue)> GeneratedKeys,
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

            if (!generated)
            {
                continue;
            }

            values.Add(new DynamicPropertyValue
            {
                Name = property.Name,
                Value = _mapper.ToDynamicValue(entry.Property(property.Name).CurrentValue, property.ClrType),
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
