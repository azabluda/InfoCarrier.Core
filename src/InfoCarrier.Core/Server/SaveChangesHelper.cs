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
    using Microsoft.EntityFrameworkCore.Internal;
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

            // Materialize entities
            var entityMaterializerSource = this.dbContext.GetService<IEntityMaterializerSource>();
            var entities = new List<object>(request.DataTransferObjects.Count);
            foreach (UpdateEntryDto dto in request.DataTransferObjects)
            {
                IEntityType entityType = typeMap[dto.EntityTypeName];
                var valueBuffer = new ValueBuffer(dto.GetCurrentValues(entityType));
                object entity = entityMaterializerSource.GetMaterializer(entityType).Invoke(valueBuffer);
                entities.Add(entity);
            }

            var entries = new List<IUpdateEntry>(entities.Count);

            // Add entities to dbContext
            foreach (var i in entities.Zip(request.DataTransferObjects, (e, d) => new { Entity = e, Dto = d }))
            {
                InternalEntityEntry internalEntry = this.dbContext.GetInfrastructure<DbContextDependencies>().StateManager.GetOrCreateEntry(i.Entity);
                entries.Add(internalEntry);

                if (internalEntry.EntityState != EntityState.Detached)
                {
                    continue;
                }

                EntityEntry entry = internalEntry.ToEntityEntry();

                // Correlate properties of DTO and node.Entry
                var props = i.Dto.JoinScalarProperties(entry);

                // Set Key values
                foreach (var p in props.Where(x => x.EfProperty.Metadata.IsKey()))
                {
                    p.EfProperty.CurrentValue = p.DtoProperty.CurrentValue;
                    if (p.EfProperty.Metadata.GetOriginalValueIndex() >= 0)
                    {
                        p.EfProperty.OriginalValue = p.DtoProperty.OriginalValue;
                    }
                }

                // Set EntityState after PK values (temp or perm) are set.
                // This will add entities to identity map.
                entry.State = i.Dto.EntityState;

                // Set non key / non temporary (e.g. TPH discriminators) values
                foreach (var p in props.Where(
                    x => !x.EfProperty.Metadata.IsKey() && !x.DtoProperty.IsTemporary))
                {
                    p.EfProperty.CurrentValue = p.DtoProperty.CurrentValue;
                    if (p.EfProperty.Metadata.GetOriginalValueIndex() >= 0)
                    {
                        p.EfProperty.OriginalValue = p.DtoProperty.OriginalValue;
                    }

                    p.EfProperty.IsModified = p.DtoProperty.IsModified;
                }

                // Mark temporary values
                foreach (var p in props.Where(x => x.DtoProperty.IsTemporary))
                {
                    p.EfProperty.IsTemporary = true;
                }
            }

            // Replace temporary PKs coming from client with generated values (e.g. HiLoSequence)
            var valueGeneratorSelector = this.dbContext.GetService<IValueGeneratorSelector>();
            foreach (EntityEntry entry in entries.Select(e => e.ToEntityEntry()))
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

            this.Entries = entries;
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