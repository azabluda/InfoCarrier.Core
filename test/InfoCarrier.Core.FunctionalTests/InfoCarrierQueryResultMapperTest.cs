// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Collections.Generic;
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
        private readonly IEnumerable<DynamicObject> groupingDto;
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

            // Mapped grouping
            {
                var key = "hello";
                var elements = new List<int> { 1, 2, 3 };
                var mappedGrouping = new DynamicObject(typeof(IGrouping<string, int>));
                mappedGrouping.Add("Key", new DynamicObject(key));
                mappedGrouping.Add(
                    "Elements",
                    new DynamicObject(elements.Select(x => new DynamicObject(x)).ToList()));
                this.groupingDto = Yield(mappedGrouping);
            }

            // Mapped product entity
            {
                var mappedProduct = new DynamicObject(typeof(Product));
                mappedProduct.Add("__EntityType", "Product");
                mappedProduct.Add("__EntityLoadedNavigations", new DynamicObject(new List<string>()));
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
            Assert.Equal(1, result.Count);
            Assert.Equal(3, result.Single().Length);
        }

        [Fact]
        public void Can_map_array_as_object()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<object>(this.arrayDto).ToList();

            // Assert
            Assert.Equal(1, result.Count);
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
            Assert.Equal(1, result.Count);
            Assert.IsNotType<int[]>(result.Single());
        }

        [Fact]
        public void Can_map_list()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<List<int>>(this.listDto).ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal(3, result.Single().Count);
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
        public void Can_map_grouping()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<IGrouping<string, int>>(this.groupingDto).ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal("hello", result.Single().Key);
            Assert.Equal(3, result.Single().Count());
        }

        [Fact]
        public void Throws_on_grouping_with_missing_Key()
        {
            // Arrange
            this.groupingDto.Single().Remove("Key");

            // Act / Assert
            Assert.Throws<Exception>(() => this.queryResultMapper.MapAndTrackResults<IGrouping<string, int>>(this.groupingDto));
        }

        [Fact]
        public void Throws_on_grouping_with_missing_Elements()
        {
            // Arrange
            this.groupingDto.Single().Remove("Elements");

            // Act / Assert
            Assert.Throws<Exception>(() => this.queryResultMapper.MapAndTrackResults<IGrouping<string, int>>(this.groupingDto));
        }

        [Fact]
        public void Can_map_and_track_entity()
        {
            // Act
            var result = this.queryResultMapper.MapAndTrackResults<Product>(this.productDto).ToList();

            // Assert
            Assert.Equal(1, result.Count);
            Assert.Equal(1, result.Single().ProductID);
            Assert.Equal("Potato", result.Single().ProductName);
            Assert.Equal(1, this.context.ChangeTracker.Entries().Count());
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
            Assert.Equal(0, this.context.ChangeTracker.Entries().Count());
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
            Assert.Equal(0, this.context.ChangeTracker.Entries().Count());
        }
    }
}
