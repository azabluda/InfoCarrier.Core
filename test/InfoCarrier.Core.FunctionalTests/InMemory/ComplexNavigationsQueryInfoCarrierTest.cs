namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;
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

        [Fact(Skip = "Client-side evaluation not fully supported")]
        public override void Complex_query_with_optional_navigations_and_client_side_evaluation()
        {
            base.Complex_query_with_optional_navigations_and_client_side_evaluation();
        }
    }
}
