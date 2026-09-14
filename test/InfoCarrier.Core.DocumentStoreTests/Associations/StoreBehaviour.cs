// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The one way ADR-009 Tier D says "the store refused this, so there was nothing for the wire
///     to carry".
/// </summary>
/// <remarks>
///     <para>
///         <b>AN OVERRIDE HERE IS A CLAIM, AND THIS IS THE SHAPE OF THE CLAIM.</b> A red must say
///         something about THIS provider; if the backing store refuses a query then this provider
///         cannot answer it either, and the red restates the obvious. The claim is only as good as
///         the evidence, which is why every use is listed in <c>test/tier-d-overrides.txt</c> with
///         a citation and checked by <c>eng/tier-d-control.py</c> against the wire-free control.
///     </para>
///     <para>
///         <b>It asserts the exception by NAME, and the name is the store's own refusal rather
///         than an assertion failure.</b> That distinction decides whether a test may be
///         overridden at all. Where the store's exception escapes — <c>ExpressionNotSupportedException</c>
///         from the driver, EF's <c>could not be translated</c>, an <c>ArgumentException</c> out of
///         the translator — the override records what the store DID, and it goes red the day the
///         store does something else. Where the specification base has already CAUGHT the exception
///         and is comparing its message, what escapes is xUnit's own assertion failure, and an
///         override asserting that says little more than "the base's assertion failed" — it would
///         stay green if the store started returning wrong data. <b>Those are left red instead</b>,
///         because the baseline's name diff is a stronger signal than a contentless assertion.
///     </para>
///     <para>
///         By name and not by type so that this tier names no driver type, which is the idiom
///         <see cref="OwnedNavigationsServerSideControlTest" /> established.
///     </para>
/// </remarks>
public static class StoreBehaviour
{
    /// <summary>
    ///     Runs a specification test that the store refuses, and asserts the refusal.
    /// </summary>
    /// <param name="test">The base test.</param>
    /// <param name="exceptionName">
    ///     The simple name of the exception the STORE raises. Not an xUnit assertion type; see the
    ///     class remarks for why that distinction is the line between an override and a red.
    /// </param>
    public static async Task Refuses(Func<Task> test, string exceptionName)
    {
        Exception? thrown = await Record.ExceptionAsync(test);

        Assert.NotNull(thrown);
        Assert.Equal(exceptionName, thrown.GetType().Name);
    }
}
