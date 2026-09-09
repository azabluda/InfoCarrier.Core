// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Security.Claims;
using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The ASP.NET Core half of #54 part 2: reading the caller out of the request in flight.
/// </summary>
/// <remarks>
///     <para>
///         <b>The interesting property is that a SINGLETON answers a PER-REQUEST question.</b>
///         <c>InProcessInfoCarrierServer</c> is registered as a singleton, because the registry of
///         open transactions has to outlive any one request. So the identity it reads cannot be
///         injected per request; it comes from <see cref="IHttpContextAccessor" />, which holds the
///         context in async-local storage and therefore follows the call rather than the object.
///     </para>
///     <para>
///         <b>These tests drive the accessor directly rather than through a live server</b>,
///         because what is under test is that seam. That the endpoint populates
///         <c>HttpContext.User</c> at all is ASP.NET Core's own behaviour and its authentication
///         middleware's, not this package's.
///     </para>
/// </remarks>
public class HttpCallerIdentityTest
{
    [Fact]
    public void It_reads_the_caller_out_of_the_request_in_flight()
    {
        (IInfoCarrierServerCallerIdentity identity, IHttpContextAccessor accessor) = Build();

        accessor.HttpContext = RequestFrom("alice");

        Assert.Equal("alice", identity.CurrentCallerId);
    }

    /// <summary>
    ///     The property that makes a singleton safe here.
    /// </summary>
    /// <remarks>
    ///     <b>If the answer were cached, the first caller would own every later transaction.</b>
    ///     This asserts the seam is read per call rather than once.
    /// </remarks>
    [Fact]
    public void It_follows_the_request_rather_than_the_object()
    {
        (IInfoCarrierServerCallerIdentity identity, IHttpContextAccessor accessor) = Build();

        accessor.HttpContext = RequestFrom("alice");
        Assert.Equal("alice", identity.CurrentCallerId);

        accessor.HttpContext = RequestFrom("bob");
        Assert.Equal("bob", identity.CurrentCallerId);
    }

    /// <summary>
    ///     No request means no caller, which is the in-process case.
    /// </summary>
    [Fact]
    public void It_answers_null_when_there_is_no_request()
    {
        (IInfoCarrierServerCallerIdentity identity, IHttpContextAccessor accessor) = Build();

        accessor.HttpContext = null;

        Assert.Null(identity.CurrentCallerId);
    }

    /// <summary>
    ///     An unauthenticated request names nobody, and the grant then protects nothing.
    /// </summary>
    /// <remarks>
    ///     <b>This is the documented limit rather than a defect.</b> Binding is a second lock:
    ///     without authentication on the transport every caller answers <see langword="null" />,
    ///     every null matches, and the token is the only credential again.
    /// </remarks>
    [Fact]
    public void An_unauthenticated_request_names_nobody()
    {
        (IInfoCarrierServerCallerIdentity identity, IHttpContextAccessor accessor) = Build();

        accessor.HttpContext = new DefaultHttpContext();

        Assert.Null(identity.CurrentCallerId);
    }

    private static (IInfoCarrierServerCallerIdentity Identity, IHttpContextAccessor Accessor) Build()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddInfoCarrierHttpCallerIdentity(http => http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value)
            .BuildServiceProvider();

        return (
            provider.GetRequiredService<IInfoCarrierServerCallerIdentity>(),
            provider.GetRequiredService<IHttpContextAccessor>());
    }

    private static DefaultHttpContext RequestFrom(string callerId)
        => new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, callerId)], "test")),
        };
}
