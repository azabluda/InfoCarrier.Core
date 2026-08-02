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
        var pending = new List<(ChangeEntry Change, object Entity, IEntityType EntityType,
            List<(IProperty Property, object? Value)> Shadow)>();

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
            foreach (DynamicPropertyValue value in ChangeEntryMapper.ReadValues(change.SerializedValues))
            {
                if (entityType.FindProperty(value.Name) is not { } property)
                {
                    continue;
                }

                object? clrValue = _mapper.FromPropertyValue(value, property.ClrType);

                // A shared-type entity — a many-to-many join entity is one — stores its values in
                // a dictionary, and EF reports its properties as the `Item[string]` indexer.
                // Handing that to SetValue without an index is a parameter-count mismatch, so it
                // goes down the entry path with the shadow properties instead.
                if (property.PropertyInfo is { } propertyInfo
                    && propertyInfo.CanWrite
                    && propertyInfo.GetIndexParameters().Length == 0)
                {
                    propertyInfo.SetValue(entity, clrValue);
                }
                else if (property.FieldInfo is { } fieldInfo)
                {
                    fieldInfo.SetValue(entity, clrValue);
                }
                else
                {
                    // A shadow property has no CLR member; its value lives in the entry, so it
                    // has to wait until there is one.
                    shadow.Add((property, clrValue));
                }
            }

            pending.Add((change, entity, entityType, shadow));
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
        foreach ((ChangeEntry change, object entity, IEntityType entityType, var shadow) in
                 pending.Where(p => p.Change.State != nameof(EntityState.Added))
                     .Concat(pending.Where(p => p.Change.State == nameof(EntityState.Added))))
        {
            EntityEntry entry = Track(entityType, entity);
            EntityState state = Enum.Parse<EntityState>(change.State);
            entry.State = state;

            foreach ((IProperty property, object? value) in shadow)
            {
                entry.Property(property.Name).CurrentValue = value;
            }

            // Mark the client's placeholders as placeholders here too. EF then treats them the
            // way it treats its own: shared between a principal and its dependents, replaced
            // everywhere by the real key once the store issues it.
            foreach (string name in change.TemporaryProperties ?? [])
            {
                if (entityType.FindProperty(name) is not null && state == EntityState.Added)
                {
                    entry.Property(name).IsTemporary = true;
                }
            }

            tracked.Add((change.CorrelationId, entry, entityType, state));
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
