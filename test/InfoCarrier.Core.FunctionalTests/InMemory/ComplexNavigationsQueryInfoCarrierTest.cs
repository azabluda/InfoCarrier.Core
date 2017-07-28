namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class ComplexNavigationsQueryInfoCarrierTest : ComplexNavigationsQueryTestBase<TestStoreBase, ComplexNavigationsQueryInfoCarrierFixture>
    {
        public ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "issue #4311 (from ComplexNavigationsQueryInMemoryTest)")]
        public override void Nested_group_join_with_take()
        {
            base.Nested_group_join_with_take();
        }
    }
}
