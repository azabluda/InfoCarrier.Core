// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Update;

/// <summary>
///     <c>PropertyValuesRelationalTestBase</c> on ADR-009 <b>Tier B</b> — current values, original
///     values, store values and <c>Reload</c>. This is the coverage the concurrency-token work
///     needs: <c>SaveChangesRequest.SerializedOriginalValues</c> carries exactly what these tests
///     read.
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the class it replaces asked for exactly this.</b> Its remark
///         read "Re-test each against Tier B and delete it where it passes". Every one of its
///         twenty-odd overrides existed because the backing store was InMemory, which supports
///         neither complex types nor complex collections — and its fixture went further, ignoring
///         two <c>Building</c> properties and the whole <c>School</c> entity so the model would
///         build at all. On SQLite none of that is true, so the overrides are gone, the model is
///         whole, and the tests that were returning <c>Task.CompletedTask</c> now run.
///     </para>
///     <para>
///         <b>No <c>UseTransaction</c> override, and that is checked rather than assumed.</b>
///         Neither <c>PropertyValuesRelationalFixture</c> nor the test base declares one, and EF's
///         own <c>PropertyValuesSqliteTest</c> is a bare one-liner. The bulk-update family taught
///         that this hook can live on the fixture, so both were looked at.
///     </para>
/// </remarks>
public class PropertyValuesInfoCarrierTest(PropertyValuesInfoCarrierTest.InfoCarrierFixture fixture)
    : PropertyValuesRelationalTestBase<PropertyValuesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The property-values fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class InfoCarrierFixture : PropertyValuesRelationalFixture
    {
        private ITestStoreFactory _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),

                // The seed asserts that the materialization interceptor ran, and the seed runs
                // against the server — so the server provider needs it too.
                onAddServices: services =>
                    services.AddSingleton<ISingletonInterceptor, PropertyValuesMaterializationInterceptor>(),
                configureConventions: ConfigureConventions);
    }
}
