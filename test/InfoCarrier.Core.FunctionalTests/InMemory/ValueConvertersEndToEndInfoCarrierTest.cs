// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>ValueConvertersEndToEndTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A value converter is the one thing that makes a property's CLR value and its stored value
///     differ, and this provider has a third value in play: what travels on the wire. ADR-008
///     constraint 1 says the wire reads a scalar through its <c>IProperty</c> accessor precisely
///     so converters are honoured; nothing adopted has measured that across a whole model of them.
/// </remarks>
public class ValueConvertersEndToEndInfoCarrierTest(ValueConvertersEndToEndInfoCarrierTest.InfoCarrierFixture fixture)
    : ValueConvertersEndToEndTestBase<ValueConvertersEndToEndInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ValueConvertersEndToEndFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
