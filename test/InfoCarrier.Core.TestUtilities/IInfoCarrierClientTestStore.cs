// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     What the two client-side test-store shells have in common: the backend they remote to.
/// </summary>
/// <remarks>
///     <para>
///         There are two shells and not one because <c>RelationalTestStore</c> derives from
///         <c>TestStore</c>, so a single class cannot be both it and a plain <c>TestStore</c>.
///         <see cref="InfoCarrierTestStore" /> is the default; <see cref="RelationalInfoCarrierTestStore" />
///         is opted into per fixture by the small number of bases that cast the store to
///         <c>RelationalTestStore</c>.
///     </para>
///     <para>
///         <b>Test classes reach the backend through this interface, not through either concrete
///         type</b>, so which shell a fixture uses stays the fixture's business.
///     </para>
/// </remarks>
public interface IInfoCarrierClientTestStore
{
    /// <summary>
    ///     The backend store this client remotes to.
    /// </summary>
    InfoCarrierBackendTestStore Backend { get; }
}
