// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Where MongoDB stops allowing an untranslatable method in the final projection, measured
///     against the version that says it allows one.
/// </summary>
/// <remarks>
///     <para>
///         <b>THE ANSWER, MEASURED: THE TRIGGER IS THE KIND OF METHOD, NOT THE PATH TO THE VALUE.</b>
///         A BCL INSTANCE method on a mapped scalar — <c>c.Name.ToArray()</c>, which is
///         <c>EF-250</c>'s own example — is evaluated on the client and the query succeeds. A
///         USER-DEFINED STATIC method is refused, and refused identically whether its argument is a
///         root scalar or one reached through an owned reference. So the owned hop is ruled OUT,
///         which is what the first two arms exist to establish.
///     </para>
///     <para>
///         <b>That was not the expected answer.</b> The hop was the hypothesis, on the grounds that
///         Tier D only ever sees this through <c>e.RequiredAssociate.Int</c>. Three assertions cost
///         less than the paragraph that would have guessed, and the guess would have been wrong.
///     </para>
///     <para>
///         <b>What it says about their fix.</b> EF Core evaluates an untranslatable FINAL
///         projection on the client rather than refusing the query, and MongoDB's tracker carries
///         <c>EF-250</c>, <i>"Allow client evaluation in the final projection"</i>, marked Closed /
///         Fixed in provider versions 10.0.3, 9.1.3 and 8.4.3. <b>This tier measures 10.0.3</b>, the
///         latest published and the version that fix names. The fix is real and it works — for the
///         shape the issue used. It does not reach a user-defined static method, so the behaviour
///         is narrower than the issue TITLE claims. There is no setting to change: 10.0.3 exposes no
///         query-mode option, which was checked against the assembly rather than assumed.
///     </para>
///     <para>
///         <b>No InfoCarrier here on purpose.</b> This runs on the server's own <c>DbContext</c>,
///         so it describes <c>MongoDB.EntityFrameworkCore</c> and nothing of this repository's. The
///         wire answers both shapes either way: ADR-010's projection split cuts the untranslatable
///         node before serialization, which <c>ProjectionPayloadTest</c> measures.
///     </para>
///     <para>
///         <b>These assertions record what the store DOES; they do not say it is right.</b> The day
///         either answer changes, this goes red and somebody reads <c>docs/upstream-defects.md</c>
///         §1.10.
///     </para>
/// </remarks>
public class ClientEvaluatedProjectionTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    /// <summary>A method no store can translate, so the client has to evaluate it.</summary>
    private static string Scramble(string value) => "#" + value;

    /// <summary>No owned hop, and it is refused anyway — which is what rules the hop out.</summary>
    [Fact]
    public async Task A_user_static_method_over_a_root_scalar_is_refused()
    {
        using IServiceScope scope = fixture.ServerProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopServerContext>();

        Exception? thrown = await Record.ExceptionAsync(
            () => db.Customers.AsNoTracking().OrderBy(c => c.Id).Select(c => Scramble(c.Name)).ToListAsync());

        Assert.NotNull(thrown);
        Assert.Equal("ExpressionNotSupportedException", thrown.GetType().Name);
    }

    /// <summary>The same refusal one owned-reference hop away: the hop changes nothing.</summary>
    [Fact]
    public async Task A_user_static_method_over_an_owned_scalar_is_refused()
    {
        using IServiceScope scope = fixture.ServerProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopServerContext>();

        Exception? thrown = await Record.ExceptionAsync(
            () => db.Customers.AsNoTracking().OrderBy(c => c.Id)
                .Select(c => Scramble(c.Address.Postcode)).ToListAsync());

        Assert.NotNull(thrown);
        Assert.Equal("ExpressionNotSupportedException", thrown.GetType().Name);
    }

    /// <summary>
    ///     <c>EF-250</c>'s own example, character for character, so the comparison is theirs rather
    ///     than ours.
    /// </summary>
    /// <remarks>
    ///     <b>THE LAST DIFFERENCE BETWEEN THEIR REPRO AND OURS.</b> Theirs calls an INSTANCE method
    ///     on a mapped scalar — <c>string.ToArray</c>, named in the issue — while the two above
    ///     call a user-defined STATIC method. If this one passes and those fail, their fix covers a
    ///     narrower case than the issue title claims. If it fails too, the fix does not reach a
    ///     final projection at all on this version.
    /// </remarks>
    [Fact]
    public async Task A_BCL_instance_method_is_evaluated_on_the_client()
    {
        using IServiceScope scope = fixture.ServerProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopServerContext>();

        Exception? thrown = await Record.ExceptionAsync(
            () => db.Customers.AsNoTracking().OrderBy(c => c.Id)
                .Select(c => c.Name.ToArray()).ToListAsync());

        Assert.Null(thrown);
    }
}
