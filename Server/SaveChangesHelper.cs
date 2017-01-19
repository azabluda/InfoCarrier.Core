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
    using Microsoft.EntityFrameworkCore.Update;

    public class SaveChangesHelper : IDisposable
    {
        private readonly DbContext dbContext;

        public SaveChangesHelper(Func<DbContext> dbContextFactory, SaveChangesRequest request)
        {
            this.dbContext = dbContextFactory();

            // Materialize entities
            var entities = new List<object>();
            foreach (UpdateEntryDto dto in request.DataTransferObjects)
            {
                IEntityType entityType = this.dbContext.Model.FindEntityType(dto.EntityTypeName);
                object entity = Activator.CreateInstance(entityType.ClrType);
                entities.Add(entity);
            }

            // Add entities to dbContext
            foreach (var i in entities.Zip(request.DataTransferObjects, (e, d) => new { Entity = e, Dto = d }))
            {
                this.dbContext.ChangeTracker.TrackGraph(
                    i.Entity,
                    node =>
                    {
                        // Correlate properties of DTO and node.Entry
                        var props = node.Entry.Metadata.GetProperties()
                            .SelectMany(p => i.Dto.YieldProperty(p))
                            .ToList();

                        // Set PK values
                        foreach (var prop in props.Where(x => x.EfProperty.IsPrimaryKey()))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.CurrentValue = prop.DtoProperty.CurrentValue;
                            propEntry.OriginalValue = prop.DtoProperty.OriginalValue;
                        }

                        // Set EntityState after PK values (temp or perm) are set.
                        // This will add entities to identity map.
                        node.Entry.State = i.Dto.EntityState;

                        // Set non-PK / non temporary (e.g. TPH discriminators) values
                        foreach (var prop in props.Where(x => !x.EfProperty.IsPrimaryKey() && !x.DtoProperty.HasTemporaryValue))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.CurrentValue = prop.DtoProperty.CurrentValue;
                            propEntry.OriginalValue = prop.DtoProperty.OriginalValue;
                            propEntry.IsModified = prop.DtoProperty.IsModified;
                        }

                        // Mark temporary values
                        foreach (var prop in props.Where(x => x.DtoProperty.HasTemporaryValue))
                        {
                            PropertyEntry propEntry = node.Entry.Property(prop.EfProperty.Name);
                            propEntry.IsTemporary = true;
                        }
                    });

                this.Entries = entities.Select(e => this.dbContext.Entry(e).GetInfrastructure());
            }
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