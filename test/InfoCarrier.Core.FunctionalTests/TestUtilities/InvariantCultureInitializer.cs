// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Pins the test run to the invariant culture.
/// </summary>
/// <remarks>
///     <para>
///         <b>This is an instrument fix, not a way of making tests pass.</b> Without it the
///         suite's failure count is a property of the machine it runs on: this one is
///         <c>en-SE</c>, whose decimal separator is a comma, and that alone accounted for
///         <b>nine</b> failures. A run on a dot-separator machine reported nine fewer with no
///         code change — so the number in <c>test/known-failures.txt</c>, which CI gates on, was
///         only true here. A ratchet whose baseline depends on the runner's locale is not a
///         ratchet.
///     </para>
///     <para>
///         <b>None of the nine was this provider's</b>, and that is why pinning the culture is
///         the right fix rather than a suppression:
///     </para>
///     <list type="bullet">
///         <item>
///             Seven are EF's own <c>JsonGeoJsonReaderWriter</c>, which re-emits a number with
///             <c>StringBuilder.Append(reader.GetDecimal())</c> — culture-sensitive. Under a comma
///             separator <c>[2.0,4.0]</c> comes back as <c>[2,0,4,0]</c> and the point reads as
///             <c>POINT (2 0)</c>. <c>line_string_as_GeoJson</c> passed only by luck, its
///             ordinates being 0 and 1.
///         </item>
///         <item>
///             Two are xUnit failing to convert a decimal <c>InlineData</c> string to its
///             parameter — again the separator, and again nothing to do with the wire.
///         </item>
///     </list>
///     <para>
///         EF's own suite fails these the same way on the same machine (plan item A64), so what
///         is being removed here is an environmental variable EF does not handle, not a defect
///         this repo could fix.
///     </para>
///     <para>
///         A <see cref="ModuleInitializerAttribute" /> rather than a fixture: it runs before xUnit
///         creates any test thread, and <see cref="CultureInfo.DefaultThreadCurrentCulture" /> is
///         inherited by threads that have not set their own — which is every thread in a parallel
///         xUnit run. A test that deliberately sets a culture still overrides it.
///     </para>
/// </remarks>
internal static class InvariantCultureInitializer
{
    [ModuleInitializer]
    internal static void PinInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
