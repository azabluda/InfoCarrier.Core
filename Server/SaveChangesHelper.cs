namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;

    public class SaveChangesHelper
    {
        private readonly DbContext dbContext;

        public SaveChangesHelper(DbContext dbContext, SaveChangesRequest request)
        {
            this.dbContext = dbContext;

            var entities = new List<object>();
            var dtos = new Dictionary<object, DataTransferObject>();

            // Materialize entities and add them to dictionary
            foreach (DataTransferObject dto in request.DataTransferObjects)
            {
                IEntityType entityType = dbContext.Model.FindEntityType(dto.EntityTypeName);
                object entity = Activator.CreateInstance(entityType.ClrType);

                entities.Add(entity);
                dtos[entity] = dto;
            }

            // Add entities to dbContext
            foreach (object entity in entities)
            {
                dbContext.ChangeTracker.TrackGraph(
                    entity,
                    node =>
                    {
                        DataTransferObject dto;
                        if (!dtos.TryGetValue(node.Entry.Entity, out dto))
                        {
                            node.Entry.State = EntityState.Detached;
                            return;
                        }

                        // Correlate properties of DTO and node.Entry
                        var props = node.Entry.Metadata.GetProperties()
                            .SelectMany(p => dto.YieldPropery(p))
                            .ToList();

                        // Set PK values
                        foreach (DataTransferObject.Property prop in props.Where(x => x.EfProperty.IsPrimaryKey()))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.CurrentValue = prop.CurrentValue;
                            propEntry.OriginalValue = prop.OriginalValue;
                        }

                        // Set EntityState after PK values (temp or perm) are set.
                        // This will add entities to identity map.
                        node.Entry.State = dto.EntityState;

                        // Set non-PK / non temporary (e.g. TPH discriminators) values
                        foreach (DataTransferObject.Property prop in
                            props.Where(x => !x.EfProperty.IsPrimaryKey() && !x.HasTemporaryValue))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.CurrentValue = prop.CurrentValue;
                            propEntry.OriginalValue = prop.OriginalValue;
                            propEntry.IsModified = prop.IsModified;
                        }

                        // Mark temporary values
                        foreach (DataTransferObject.Property prop in props.Where(x => x.HasTemporaryValue))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.IsTemporary = true;
                        }
                    });

                this.Entities = entities;
            }
        }

        public IEnumerable<object> Entities { get; }

        public SaveChangesResult SaveChanges()
        {
            return this.BuildResult(this.dbContext.SaveChanges());
        }

        public async Task<SaveChangesResult> SaveChangesAsync()
        {
            return this.BuildResult(await this.dbContext.SaveChangesAsync());
        }

        private SaveChangesResult BuildResult(int countPersisted)
        {
            var result = new SaveChangesResult { CountPersisted = countPersisted };
            result.DataTransferObjects.AddRange(
                this.Entities.Select(
                    e => new DataTransferObject(this.dbContext.Entry(e).GetInfrastructure())));
            return result;
        }
    }
}