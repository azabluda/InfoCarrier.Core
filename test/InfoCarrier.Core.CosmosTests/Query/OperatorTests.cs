// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query.Translations.Operators;

namespace InfoCarrier.Core.CosmosTests.Query;

// The matching EF Core 11 Cosmos classes add only SQL baselines to these inherited tests.
// Keep the result assertions in the specification bases; do not copy golden SQL text.
public class ArithmeticOperatorTest(BasicTypesFixture fixture)
    : ArithmeticOperatorTranslationsTestBase<BasicTypesFixture>(fixture);

public class ComparisonOperatorTest(BasicTypesFixture fixture)
    : ComparisonOperatorTranslationsTestBase<BasicTypesFixture>(fixture);

public class LogicalOperatorTest(BasicTypesFixture fixture)
    : LogicalOperatorTranslationsTestBase<BasicTypesFixture>(fixture);
