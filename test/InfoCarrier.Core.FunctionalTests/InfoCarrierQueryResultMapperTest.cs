﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Aqua.Dynamic;
    using InfoCarrier.Core.Client.Query.Internal;
    using InfoCarrier.Core.FunctionalTests.InMemory.Query;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class InfoCarrierQueryResultMapperTest : IClassFixture<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>, IDisposable
    {
        private readonly NorthwindContext context;
        private readonly InfoCarrierQueryResultMapper queryResultMapper;
        private readonly IEnumerable<DynamicObject> arrayDto;
        private readonly IEnumerable<DynamicObject> listDto;
        private readonly IEnumerable<DynamicObject> productDto;

        public InfoCarrierQueryResultMapperTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
        {
            this.context = fixture.CreateContext();

            this.queryResultMapper = new InfoCarrierQueryResultMapper(
                this.context.GetService<IQueryContextFactory>().Create(),
                new Aqua.TypeSystem.TypeResolver(),
                new Aqua.TypeSystem.TypeInfoProvider());

            IEnumerable<DynamicObject> Yield(params DynamicObject[] dynamicObjects) => dynamicObjects;

            // Mapped array
            {
                var obj = new[] { 1, 2, 3 };
                var array = obj.Select(x => new DynamicObject(x)).ToArray();
                var mappedArray = new DynamicObject();
                mappedArray.Add("ArrayType", new Aqua.TypeSystem.TypeInfo(obj.GetType(), includePropertyInfos: false));
                mappedArray.Add("Elements", array);
                this.arrayDto = Yield(mappedArray);
            }

            // Mapped list
            {
                var enumerable = new List<int> { 1, 2, 3 };
                var mappedEnumerable = new DynamicObject(typeof(IEnumerable<int>));
                mappedEnumerable.Add(
                    "Elements",
                    new DynamicObject(enumerable.Select(x => new DynamicObject(x)).ToList()));
                this.listDto = Yield(mappedEnumerable);
            }

            // Mapped product entity
            {
                var mappedProduct = new DynamicObject(typeof(Product));
                mappedProduct.Add("__EntityType", "Product");
                mappedProduct.Add("__EntityLoadedNavigations", new DynamicObject(new HashSet<string>()));
                mappedProduct.Add("ProductID", new DynamicObject(1));
                mappedProduct.Add("ProductName", new DynamicObject("Potato"));
                mappedProduct.Add("Discontinued", new DynamicObject(false));
                mappedProduct.Add("UnitsInStock", new DynamicObject(default(ushort)));
                this.productDto = Yield(mappedProduct);
            }
        }

        public void Dispose()
            => this.context.Dispose();

        [Fact]
        public void Can_map_array()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<int[]>(this.arrayDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(3, result.Single().Length);
        }

        [Fact]
        public void Can_map_array_as_object()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<object>(this.arrayDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.IsType<int[]>(result.Single());
            Assert.Equal(3, ((int[])result.Single()).Length);
        }

        [Fact]
        public void Throws_on_array_with_missing_ArrayType()
        {
            // Arrange
            this.arrayDto.Single().Remove("ArrayType");

            // Act / Assert
            Assert.Throws<ArgumentException>(() => this.queryResultMapper.MapAndTrackResults<int[]>(this.arrayDto));
        }

        [Fact]
        public void Throws_on_array_with_missing_Elements()
        {
            // Arrange
            this.arrayDto.Single().Remove("Elements");

            // Act / Assert
            Assert.Throws<ArgumentException>(() => this.queryResultMapper.MapAndTrackResults<int[]>(this.arrayDto));
        }

        [Fact]
        public void Cannot_map_array_with_corrupt_ArrayType()
        {
            // Arrange
            this.arrayDto.Single().Remove("ArrayType");
            this.arrayDto.Single().Add("ArrayType", 1);

            // Act
            var result = this.queryResultMapper.MapAndTrackResults<object>(this.arrayDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.IsNotType<int[]>(result.Single());
        }

        [Fact]
        public void Can_map_list()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<List<int>>(this.listDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(3, result.Single().Count);
        }

        [Fact]
        public void Can_map_ObservableCollection()
        {
            // Arrange
            this.listDto.Single().Add(@"CollectionType", new Aqua.TypeSystem.TypeInfo(typeof(ObservableCollection<int>), includePropertyInfos: false));

            // Act
            var result = this.queryResultMapper.MapAndTrackResults<ObservableCollection<int>>(this.listDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(3, result.Single().Count);
        }

        [Fact]
        public void Can_map_IOrderedEnumerable()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<IOrderedEnumerable<int>>(this.listDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(3, result.Single().ThenBy(x => x).Count());
        }

        [Fact]
        public void Can_map_IQueryable()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<IQueryable<int>>(this.listDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(typeof(int), result.Single().ElementType);
        }

        [Fact]
        public void Throws_on_list_with_missing_Elements()
        {
            // Arrange
            this.listDto.Single().Remove("Elements");

            // Act / Assert
            Assert.Throws<ArgumentException>(() => this.queryResultMapper.MapAndTrackResults<int[]>(this.listDto));
        }

        [Fact]
        public void Throws_on_nongeneric_collection()
        {
            // Arrange
            this.listDto.Single().Add(@"CollectionType", new Aqua.TypeSystem.TypeInfo(typeof(ArrayList), includePropertyInfos: false));

            // Act / Assert
            Assert.Throws<NotSupportedException>(() => this.queryResultMapper.MapAndTrackResults<ArrayList>(this.listDto));
        }

        [Fact]
        public void Can_map_and_track_entity()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<Product>(this.productDto).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal(1, result.Single().ProductID);
            Assert.Equal("Potato", result.Single().ProductName);
            Assert.Single(this.context.ChangeTracker.Entries());
            Assert.Same(result.Single(), this.context.ChangeTracker.Entries().Single().Entity);
        }

        [Fact]
        public void Cannot_track_entity_with_corrupt_EntityType()
        {
            // Arrange
            this.productDto.Single().Remove("__EntityType");
            this.productDto.Single().Add("__EntityType", 0);

            // Act
            this.queryResultMapper.MapAndTrackResults<Product>(this.productDto);

            // Assert
            Assert.Empty(this.context.ChangeTracker.Entries());
        }

        [Fact]
        public void Cannot_track_entity_with_corrupt_EntityType2()
        {
            // Arrange
            this.productDto.Single().Remove("__EntityType");
            this.productDto.Single().Add("__EntityType", "Product2");

            // Act
            this.queryResultMapper.MapAndTrackResults<Product>(this.productDto);

            // Assert
            Assert.Empty(this.context.ChangeTracker.Entries());
        }
    }
}
