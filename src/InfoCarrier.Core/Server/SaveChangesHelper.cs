// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Server
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.EntityFrameworkCore.ValueGeneration;

    /// <summary>
    ///     Implementation of <see cref="IInfoCarrierServer.SaveChanges" /> and
    ///     <see cref="IInfoCarrierServer.SaveChangesAsync" /> methods.
    /// </summary>
    internal class SaveChangesHelper
    {
        private readonly DbContext dbContext;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SaveChangesHelper" /> class.
        /// </summary>
        /// <param name="dbContext"> <see cref="DbContext" /> against which the requested query will be executed. </param>
        /// <param name="request"> The <see cref="SaveChangesRequest" /> object from the client containing the updated entities. </param>
        public SaveChangesHelper(DbContext dbContext, SaveChangesRequest request)
        {
            this.dbContext = dbContext;

            var typeMap = this.dbContext.Model.GetEntityTypes().ToDictionary(x => x.DisplayName());
            var stateManager = this.dbContext.GetService<IStateManager>();
            var entityMaterializerSource = this.dbContext.GetService<IEntityMaterializerSource>();

            // Materialize entities and add entries to dbContext
            EntityEntry MaterializeAndTrackEntity(UpdateEntryDto dto)
            {
                IEntityType entityType = typeMap[dto.EntityTypeName];

                object MaterializeEntity()
                {
                    var valueBuffer = new ValueBuffer(dto.GetCurrentValues(entityType, request.Mapper));
                    var materializationContext = new MaterializationContext(valueBuffer, this.dbContext);
                    return entityMaterializerSource.GetMaterializer(entityType).Invoke(materializationContext);
                }

                EntityEntry entry;
                if (entityType.HasDefiningNavigation())
                {
                    IKey key = entityType.DefiningEntityType.FindPrimaryKey();
                    object[] keyValues = dto.GetDelegatedIdentityKeys(request.Mapper, key);

                    // Here we assume that the owner entry is already present in the context
                    EntityEntry ownerEntry = stateManager.TryGetEntry(key, keyValues).ToEntityEntry();

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

                // Set Key properties
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

                // Set non key properties
                foreach (var p in props.Where(x => !x.EfProperty.Metadata.IsKey()))
                {
                    bool canSetCurrentValue =
                        p.EfProperty.Metadata.IsShadowProperty() ||
                        p.EfProperty.Metadata.TryGetMemberInfo(forConstruction: false, forSet: true, out _, out _);

                    if (canSetCurrentValue)
                    {
                        p.EfProperty.CurrentValue = p.CurrentValue;
                    }

                    if (p.EfProperty.Metadata.GetOriginalValueIndex() >= 0
                        && p.EfProperty.OriginalValue != p.OriginalValue)
                    {
                        p.EfProperty.OriginalValue = p.OriginalValue;
                    }

                    if (canSetCurrentValue)
                    {
                        p.EfProperty.IsModified = p.DtoProperty.IsModified;
                    }
                }

                // Mark temporary property values
                foreach (var p in props.Where(x => x.DtoProperty.IsTemporary))
                {
                    p.EfProperty.IsTemporary = true;
                }

                return entry;
            }

            request.SharedIdentityDataTransferObjects.ForEach(dto => MaterializeAndTrackEntity(dto));
            var entries = request.DataTransferObjects.Select(MaterializeAndTrackEntity).ToList();

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

            this.Entries = entries.Select(e => e.GetInfrastructure()).Cast<IUpdateEntry>().ToList();
        }

        /// <summary>
        ///     Gets the corresponding entries after painting the state of the updated entities.
        /// </summary>
        private IList<IUpdateEntry> Entries { get; }

        /// <summary>
        ///     Saves the updated entities into the actual database.
        /// </summary>
        /// <returns>
        ///     The save operation result which can either be
        ///     a SaveChangesResult.Success or SaveChangesResult.Error.
        /// </returns>
        public SaveChangesResult SaveChanges()
        {
            try
            {
                return SaveChangesResult.Success(this.dbContext.SaveChanges(), this.Entries);
            }
            catch (DbUpdateException e)
            {
                return SaveChangesResult.Error(e, this.Entries);
            }
        }

        /// <summary>
        ///     Asynchronously saves the updated entities into the actual database.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the save operation result which can either be
        ///     a SaveChangesResult.Success or SaveChangesResult.Error.
        /// </returns>
        public async Task<SaveChangesResult> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return SaveChangesResult.Success(await this.dbContext.SaveChangesAsync(cancellationToken), this.Entries);
            }
            catch (DbUpdateException e)
            {
                return SaveChangesResult.Error(e, this.Entries);
            }
        }
    }
}
