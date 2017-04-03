namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class DataAnnotationInfoCarrierFixture : DataAnnotationFixtureBase<TestStore>
    {
        private readonly InfoCarrierInMemoryTestHelper<DataAnnotationContext> helper;

        public DataAnnotationInfoCarrierFixture()
        {
            this.helper = InfoCarrierInMemoryTestHelper.Create(
                this.OnModelCreating,
                (opt, _) => new DataAnnotationContext(opt),
                w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }

        public override ModelValidator ThrowingValidator => new ThrowingModelValidator();

        public override TestStore CreateTestStore()
            => this.helper.CreateTestStore(DataAnnotationModelInitializer.Seed);

        public override DataAnnotationContext CreateContext(TestStore testStore)
            => this.helper.CreateInfoCarrierContext();

        private class ThrowingModelValidator : ModelValidator
        {
            protected override void ShowWarning(string message)
                => throw new InvalidOperationException(message);
        }
    }
}
