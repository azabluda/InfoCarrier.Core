// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The construction half of <see cref="F1MaterializationInterceptor" />, and nothing else.
/// </summary>
/// <remarks>
///     <para>
///         The F1 model cannot be materialized without an interceptor: every entity's
///         parameterized constructor is private and ends in
///         <c>Assert.IsType&lt;…Proxy&gt;(this)</c>, so EF's own constructor binding picks it
///         and the assertion fires unless <c>CreatingInstance</c> supplies the proxy. That is
///         a requirement of the *model*, and the server carries this model, so the server
///         needs that half. Measured: registering nothing server-side fails 21 of this
///         class's 33 running tests, 13 on <c>Driver</c>'s <c>Assert.True</c> and 8 on the
///         other types' <c>Assert.IsType</c>, every one of them an
///         <c>InfoCarrierServerException</c>.
///     </para>
///     <para>
///         The other three members are not model requirements — they are the observable
///         behaviour of a hook the *caller* registered. <c>InitializingInstance</c> rewrites
///         <c>Sponsor.Name</c> to <c>"Intercepted: " + name</c>, and that transform is the
///         only non-idempotent thing the interceptor does. Under one <c>DbContext</c> it runs
///         once, which is what <c>Nullable_client_side_concurrency_token_can_be_used</c>
///         asserts; under this provider's two it ran on the server, crossed the wire, and ran
///         again on the client — <c>"Intercepted: Intercepted: New name"</c>, the last of
///         B16's family after C71 closed the other twenty-six.
///     </para>
///     <para>
///         So the server is given what its model demands and not what the caller's hook does,
///         which leaves the transform observable exactly once, on the instance the test
///         registered it against. This is not a statement that the product suppresses a
///         server-side interceptor — B16 stands, a deployment may hook either side or both,
///         and <c>PropertyValuesFixtureBase</c> keeps its server-side one on purpose. It is
///         this fixture declining to hand a one-context spec base's hook to both instances.
///     </para>
///     <para>
///         The base's methods are not virtual, so the interface is re-implemented rather than
///         overridden; <c>CreatingInstance</c> forwards to the base's hundred-line switch so
///         no proxy list is duplicated here.
///     </para>
/// </remarks>
public sealed class ServerSideF1MaterializationInterceptor : F1MaterializationInterceptor, IMaterializationInterceptor
{
    InterceptionResult<object> IMaterializationInterceptor.CreatingInstance(
        MaterializationInterceptionData materializationData,
        InterceptionResult<object> result)
        => CreatingInstance(materializationData, result);

    object IMaterializationInterceptor.CreatedInstance(
        MaterializationInterceptionData materializationData,
        object entity)
        => entity;

    InterceptionResult IMaterializationInterceptor.InitializingInstance(
        MaterializationInterceptionData materializationData,
        object entity,
        InterceptionResult result)
        => result;

    object IMaterializationInterceptor.InitializedInstance(
        MaterializationInterceptionData materializationData,
        object entity)
        => entity;
}
