namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class DataAnnotationInfoCarrierFixture : DataAnnotationFixtureBase<TestStoreBase>
    {
        private readonly InfoCarrierTestHelper<DataAnnotationContext> helper;

        public DataAnnotationInfoCarrierFixture()
        {
            this.helper = InMemoryTestStore<DataAnnotationContext>.CreateHelper(
                this.OnModelCreating,
                opt => new DataAnnotationContext(opt),
                DataAnnotationModelInitializer.Seed,
                true,
                w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }

        public override ModelValidator ThrowingValidator => new ThrowingModelValidator();

        public override TestStoreBase CreateTestStore()
            => this.helper.CreateTestStore();

        public override DataAnnotationContext CreateContext(TestStoreBase testStore)
            => this.helper.CreateInfoCarrierContext(testStore);

        private class ThrowingModelValidator : ModelValidator
        {
            protected override void ShowWarning(string message)
                => throw new InvalidOperationException(message);
        }
    }
}
