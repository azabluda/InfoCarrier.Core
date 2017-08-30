namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.EntityFrameworkCore.ValueGeneration;

    public class SaveChangesHelper : IDisposable
    {
        private readonly DbContext dbContext;

        public SaveChangesHelper(Func<DbContext> dbContextFactory, SaveChangesRequest request)
        {
            this.dbContext = dbContextFactory();

            var typeMap = this.dbContext.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
            IStateManager stateManager = this.dbContext.GetService<IStateManager>();

            // Materialize entities and add entries to dbContext
            var entityMaterializerSource = this.dbContext.GetService<IEntityMaterializerSource>();
            var entries = request.DataTransferObjects.Select(dto =>
            {
                IEntityType entityType = typeMap[dto.EntityTypeName];

                object MaterializeEntity()
                {
                    var valueBuffer = new ValueBuffer(dto.GetCurrentValues(entityType, request.Mapper));
                    return entityMaterializerSource.GetMaterializer(entityType).Invoke(valueBuffer);
                }

                EntityEntry entry;
                if (entityType.HasDefiningNavigation())
                {
                    object[] keyValues = dto.GetDelegatedIdentityKeys(request.Mapper);

                    // Here we assume that the owner entry is already present in the context
                    EntityEntry ownerEntry = stateManager.TryGetEntry(
                        entityType.DefiningEntityType.FindPrimaryKey(),
                        keyValues)?.ToEntityEntry();

                    // If not, then we create a dummy instance, set only PK values, and track it as Unchanged
                    if (ownerEntry == null)
                    {
                        var pkProps = entityType.DefiningEntityType.FindPrimaryKey().Properties.ToList();
                        var ownerValueBuffer = new ValueBuffer(
                            entityType.DefiningEntityType.GetProperties().Select(p =>
                            {
                                int idx = pkProps.IndexOf(p);
                                return idx < 0
                                    ? p.ClrType.GetDefaultValue()
                                    : keyValues[idx];
                            }).ToArray());
                        object ownerEntity = entityMaterializerSource.GetMaterializer(entityType.DefiningEntityType).Invoke(ownerValueBuffer);
                        ownerEntry = stateManager.GetOrCreateEntry(ownerEntity, entityType.DefiningEntityType).ToEntityEntry();
                        ownerEntry.State = EntityState.Unchanged;
                    }

                    ReferenceEntry referenceEntry = ownerEntry.Reference(entityType.DefiningNavigationName);
                    if (referenceEntry.CurrentValue == null)
                    {
                        referenceEntry.CurrentValue = MaterializeEntity();
                    }
                    entry = referenceEntry.TargetEntry;
                }
                else
                {
                    entry = stateManager.GetOrCreateEntry(MaterializeEntity()).ToEntityEntry();
                }

                // Correlate properties of DTO and entry
                var props = dto.JoinScalarProperties(entry, request.Mapper);

                // Set Key values
                foreach (var p in props.Where(x => x.EfProperty.Metadata.IsKey()))
                {
                    p.EfProperty.CurrentValue = p.CurrentValue;
                    if (p.EfProperty.Metadata.GetOriginalValueIndex() >= 0)
                    {
                        p.EfProperty.OriginalValue = p.OriginalValue;
                    }
                }

                // Set EntityState after PK values (temp or perm) are set.
                // This will add entities to identity map.
                entry.State = dto.EntityState;

                // Set non key / non temporary (e.g. TPH discriminators) values
                foreach (var p in props.Where(
                    x => !x.EfProperty.Metadata.IsKey() && !x.DtoProperty.IsTemporary))
                {
                    p.EfProperty.CurrentValue = p.CurrentValue;
                    if (p.EfProperty.Metadata.GetOriginalValueIndex() >= 0)
                    {
                        p.EfProperty.OriginalValue = p.OriginalValue;
                    }

                    p.EfProperty.IsModified = p.DtoProperty.IsModified;
                }

                // Mark temporary values
                foreach (var p in props.Where(x => x.DtoProperty.IsTemporary))
                {
                    p.EfProperty.IsTemporary = true;
                }

                return entry;
            }).ToList();

            // Replace temporary PKs coming from client with generated values (e.g. HiLoSequence)
            var valueGeneratorSelector = this.dbContext.GetService<IValueGeneratorSelector>();
            foreach (EntityEntry entry in entries)
            {
                foreach (PropertyEntry tempPk in
                    entry.Properties.Where(p =>
                        p.IsTemporary
                        && p.Metadata.IsKey()
                        && p.Metadata.RequiresValueGenerator()).ToList())
                {
                    ValueGenerator valueGenerator = valueGeneratorSelector.Select(tempPk.Metadata, entry.Metadata);
                    if (!valueGenerator.GeneratesTemporaryValues)
                    {
                        tempPk.CurrentValue = valueGenerator.Next(entry);
                        tempPk.IsTemporary = false;
                    }
                }
            }

            this.Entries = entries.Select(e => e.GetInfrastructure()).ToList();
        }

        public IEnumerable<IUpdateEntry> Entries { get; }

        public void Dispose()
        {
            this.dbContext.Dispose();
        }

        public SaveChangesResult SaveChanges()
        {
            return new SaveChangesResult(this.dbContext.SaveChanges(), this.Entries);
        }

        public async Task<SaveChangesResult> SaveChangesAsync()
        {
            return new SaveChangesResult(await this.dbContext.SaveChangesAsync(), this.Entries);
        }
    }
}