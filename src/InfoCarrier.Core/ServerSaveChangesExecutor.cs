// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

                if (property.PropertyInfo is { } propertyInfo && propertyInfo.CanWrite)
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

        // Wire relationships before anything is tracked. A dependent whose principal is new has
        // no usable foreign key — the client's was temporary and was not sent — so the link is
        // what tells the server they belong together, and EF's fixup supplies the real key once
        // the principal is inserted.
        var byCorrelationId = pending.ToDictionary(p => p.Change.CorrelationId, p => p.Entity);
        foreach ((ChangeEntry change, object entity, IEntityType entityType, _) in pending)
        {
            foreach (NavigationLink link in change.Navigations ?? [])
            {
                if (FindNavigation(entityType, link.Name) is not { } navigation)
                {
                    continue;
                }

                object?[] targets = [.. link.TargetCorrelationIds
                    .Select(id => byCorrelationId.TryGetValue(id, out object? t) ? t : null)
                    .Where(t => t is not null)];

                Wire(entity, navigation, targets);
            }
        }

        foreach ((ChangeEntry change, object entity, IEntityType entityType, var shadow) in pending)
        {
            EntityEntry entry = _context.Entry(entity);
            EntityState state = Enum.Parse<EntityState>(change.State);
            entry.State = state;

            foreach ((IProperty property, object? value) in shadow)
            {
                entry.Property(property.Name).CurrentValue = value;
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

    private static INavigationBase? FindNavigation(IEntityType entityType, string name)
        => (INavigationBase?)entityType.FindNavigation(name) ?? entityType.FindSkipNavigation(name);

    /// <summary>
    ///     Assigns a navigation on an untracked instance.
    /// </summary>
    /// <remarks>
    ///     A collection navigation is added to rather than replaced: the instance may have been
    ///     constructed with one, and replacing it would discard whatever the entity's own
    ///     constructor put there.
    /// </remarks>
    private static void Wire(object entity, INavigationBase navigation, object?[] targets)
    {
        if (navigation.PropertyInfo is not { } propertyInfo)
        {
            return;
        }

        if (!navigation.IsCollection)
        {
            if (targets.Length > 0 && propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(entity, targets[0]);
            }

            return;
        }

        object? collection = propertyInfo.GetValue(entity);
        if (collection is null && propertyInfo.CanWrite)
        {
            collection = Activator.CreateInstance(
                typeof(List<>).MakeGenericType(navigation.TargetEntityType.ClrType));
            propertyInfo.SetValue(entity, collection);
        }

        if (collection is System.Collections.IList list)
        {
            foreach (object? target in targets)
            {
                list.Add(target);
            }
        }
    }

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
