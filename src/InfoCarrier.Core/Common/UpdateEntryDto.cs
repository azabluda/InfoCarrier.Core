// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Aqua.Dynamic;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.ChangeTracking;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Internal;
    using Microsoft.EntityFrameworkCore.Update;

    /// <summary>
    ///     A serializable object containing mapped property values of a <see cref="IUpdateEntry" />
    ///     which are necessary for painting its state on the remote end.
    /// </summary>
    [DataContract]
    public class UpdateEntryDto
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="UpdateEntryDto"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public UpdateEntryDto()
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UpdateEntryDto"/> class.
        /// </summary>
        /// <param name="entry">The state entry of an entity.</param>
        /// <param name="mapper">The <see cref="IDynamicObjectMapper" /> used for mapping the property values.</param>
        public UpdateEntryDto(IUpdateEntry entry, IDynamicObjectMapper mapper)
        {
            DynamicObject CreateValueDto(IProperty propertyBase, object value)
            {
                value = Utils.ConvertToProvider(value, propertyBase);
                return mapper.MapObject(value);
            }

            this.EntityTypeName = entry.EntityType.DisplayName();
            this.EntityState = entry.EntityState;
            this.PropertyDatas = entry.ToEntityEntry().Properties.Select(
                prop => new PropertyData
                {
                    Name = prop.Metadata.Name,
                    OriginalValueDto = prop.Metadata.GetOriginalValueIndex() >= 0 ? CreateValueDto(prop.Metadata, prop.OriginalValue) : null,
                    CurrentValueDto = CreateValueDto(prop.Metadata, prop.CurrentValue),
                    IsModified = prop.IsModified,
                    IsTemporary = prop.IsTemporary,
                }).ToList();

            if (entry.EntityType.HasDefiningNavigation())
            {
                var ownership = entry.EntityType.GetForeignKeys().Single(fk => fk.IsOwnership);
                this.DelegatedIdentityDatas = ownership.Properties.Select(
                    prop => new PropertyData
                    {
                        Name = prop.Name,
                        OriginalValueDto = CreateValueDto(prop, entry.GetOriginalValue(prop)),
                        CurrentValueDto = CreateValueDto(prop, entry.GetCurrentValue(prop)),
                        IsModified = entry.IsModified(prop),
                        IsTemporary = entry.HasTemporaryValue(prop),
                    }).ToList();
            }
        }

        /// <summary>
        ///     Gets the (display) name of the entity type of the <see cref="IUpdateEntry" />.
        /// </summary>
        [DataMember]
        public string EntityTypeName { get; private set; }

        /// <summary>
        ///     Gets the <see cref="EntityState" /> of the <see cref="IUpdateEntry" />.
        /// </summary>
        [DataMember]
        public EntityState EntityState { get; private set; }

        /// <summary>
        ///     Gets or sets the scalar properties of the <see cref="IUpdateEntry" /> mapped to <see cref="PropertyData" />.
        /// </summary>
        [DataMember]
        private List<PropertyData> PropertyDatas { get; set; } = new List<PropertyData>();

        /// <summary>
        ///     Gets or sets the values of the delegated identity key of the <see cref="IUpdateEntry" />
        ///     mapped to <see cref="PropertyData" /> (for owned entities only).
        /// </summary>
        [DataMember]
        private List<PropertyData> DelegatedIdentityDatas { get; set; } = new List<PropertyData>();

        /// <summary>
        ///     Matches the <see cref="PropertyEntry" />'s of the given <see cref="EntityEntry" /> with corresponding <see cref="PropertyData"/>'s.
        /// </summary>
        /// <param name="entry">The reference <see cref="EntityEntry" />.</param>
        /// <param name="mapper">The <see cref="IDynamicObjectMapper" /> used for mapping the property values.</param>
        /// <returns>
        ///     The list of matching scalar properties alongside their Origianl and Current values
        ///     mapped back to their original types.
        /// </returns>
        public IReadOnlyList<(PropertyEntry EfProperty, PropertyData DtoProperty, object OriginalValue, object CurrentValue)> JoinScalarProperties(
            EntityEntry entry,
            IDynamicObjectMapper mapper)
        {
            return (
                from ef in entry.Properties
                join dto in this.PropertyDatas
                on ef.Metadata.Name equals dto.Name
                select (ef, dto,
                    Utils.ConvertFromProvider(mapper.Map(dto.OriginalValueDto), ef.Metadata),
                    Utils.ConvertFromProvider(mapper.Map(dto.CurrentValueDto), ef.Metadata))).ToList();
        }

        /// <summary>
        ///     Returns the current values of scalar properties of the entity.
        /// </summary>
        /// <param name="entityType">The type of the entity.</param>
        /// <param name="mapper">The <see cref="IDynamicObjectMapper" /> used for mapping the property values.</param>
        /// <returns>
        ///     The current values of scalar properties of the entity mapped back to their original types.
        /// </returns>
        public object[] GetCurrentValues(IEntityType entityType, IDynamicObjectMapper mapper)
        {
            return entityType
                .GetProperties()
                .Select(p => Utils.ConvertFromProvider(
                    mapper.Map(this.PropertyDatas.SingleOrDefault(pd => pd.Name == p.Name)?.CurrentValueDto),
                    p))
                .ToArray();
        }

        /// <summary>
        ///     Returns the values of the delegated identity key of the entity
        ///     (for owned entities only).
        /// </summary>
        /// <param name="mapper">The <see cref="IDynamicObjectMapper" /> used for mapping the key values.</param>
        /// <param name="key"> The primary key of the defining entity. </param>
        /// <returns>
        ///     The values of the delegated identity key of the entity mapped back to their original types.
        /// </returns>
        public object[] GetDelegatedIdentityKeys(IDynamicObjectMapper mapper, IKey key)
        {
            return this.DelegatedIdentityDatas.Zip(
                key.Properties,
                (d, p) => Utils.ConvertFromProvider(mapper.Map(d.CurrentValueDto), p)).ToArray();
        }

        /// <summary>
        ///     A serializable object containing property information as well as its
        ///      original and current values mapped to <see cref="DynamicObject" />.
        /// </summary>
        [DataContract]
        public class PropertyData
        {
            /// <summary>
            ///     Gets or sets property name.
            /// </summary>
            [DataMember]
            public string Name { get; set; }

            /// <summary>
            ///     Gets or sets the original property value (mapped to <see cref="DynamicObject" />).
            /// </summary>
            [DataMember]
            internal DynamicObject OriginalValueDto { get; set; }

            /// <summary>
            ///     Gets or sets the current property value (mapped to <see cref="DynamicObject" />).
            /// </summary>
            [DataMember]
            internal DynamicObject CurrentValueDto { get; set; }

            /// <summary>
            ///    Gets or sets a value indicating whether the value of this property has been modified
            ///    and should be updated in the database when <see cref="DbContext.SaveChanges()" />
            ///    is called.
            /// </summary>
            [DataMember]
            public bool IsModified { get; set; }

            /// <summary>
            ///     Gets or sets a value indicating whether the value of this property is considered a
            ///     temporary value which will be replaced by a value generated from the store when
            ///     <see cref="DbContext.SaveChanges()" /> is called.
            /// </summary>
            [DataMember]
            public bool IsTemporary { get; set; }
        }
    }
}
