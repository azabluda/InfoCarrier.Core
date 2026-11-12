// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

public class JsonTranslationsInfoCarrierTest(JsonTranslationsInfoCarrierTest.JsonTranslationsInfoCarrierFixture fixture)
    : JsonTranslationsRelationalTestBase<JsonTranslationsInfoCarrierTest.JsonTranslationsInfoCarrierFixture>(fixture)
{
    public class JsonTranslationsInfoCarrierFixture : JsonTranslationsQueryFixtureBase
    {
        private ITestStoreFactory? _factory;

        protected override string StoreName => "JsonTranslationsInfoCarrier";

        protected override ITestStoreFactory TestStoreFactory
            => _factory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance, ContextType,
                (model, context) => OnModelCreating(model, context),
                configureConventions: ConfigureConventions);

        protected override string RemoveJsonProperty(string column, string property)
            => $"json_remove({column}, '$.{property}')";
    }
}
