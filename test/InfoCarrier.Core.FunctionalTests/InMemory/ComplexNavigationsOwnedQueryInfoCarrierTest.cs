namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class ComplexNavigationsOwnedQueryInfoCarrierTest : ComplexNavigationsOwnedQueryTestBase<TestStoreBase, ComplexNavigationsOwnedQueryInfoCarrierFixture>
    {
        public ComplexNavigationsOwnedQueryInfoCarrierTest(ComplexNavigationsOwnedQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "issue #4311 (from ComplexNavigationsOwnedQueryInMemoryTest)")]
        public override void Nested_group_join_with_take()
        {
            base.Nested_group_join_with_take();
        }
    }
}
