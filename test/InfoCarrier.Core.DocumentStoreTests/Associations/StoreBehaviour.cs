// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The ways ADR-009 Tier D states what the store does to a specification test, each exact.
/// </summary>
/// <remarks>
///     <para>
///         <b>EACH HELPER NAMES THE OUTCOME, NEVER "SOMETHING FAILED".</b> An assertion that any
///         failure will do stays green when the store starts returning wrong rows, which is the defect
///         MongoDB's own <c>EF-367</c> describes in their suite and this tier committed on 2026-09-14.
///         So every helper checks a type and, where the base has swallowed the store's exception, the
///         store's own text as well.
///     </para>
///     <para>
///         <b>Three shapes, because the base test decides what escapes.</b> When the query reaches the
///         store unhandled, the store's exception escapes and <see cref="Refuses" /> names it. When
///         the base expected a DIFFERENT exception type, xUnit's <see cref="ThrowsException" />
///         escapes holding the store's exception inside it. When the base compared a message or a
///         value, xUnit's <see cref="EqualException" /> escapes with the store's text or value in its
///         message.
///     </para>
///     <para>
///         By name and not by type, so this tier names no MongoDB driver type.
///     </para>
/// </remarks>
public static class StoreBehaviour
{
    /// <summary>The store's own exception escapes the base test.</summary>
    /// <param name="test">The base test.</param>
    /// <param name="exceptionName">The simple name of the exception the store raises.</param>
    /// <param name="storeText">
    ///     Text from the store's own message. Required when the exception type alone says too little,
    ///     such as <c>InfoCarrierServerException</c>, which wraps every server-side failure.
    /// </param>
    public static async Task Refuses(Func<Task> test, string exceptionName, string? storeText = null)
    {
        Exception? thrown = await Record.ExceptionAsync(test);

        Assert.NotNull(thrown);
        Assert.Equal(exceptionName, thrown.GetType().Name);

        if (storeText is not null)
        {
            Assert.Contains(storeText, thrown.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     The base expected another exception type; the store's is inside xUnit's failure.
    /// </summary>
    /// <param name="test">The base test.</param>
    /// <param name="exceptionName">The simple name of the exception the store raises.</param>
    /// <param name="storeText">Text from the store's own message.</param>
    public static async Task BaseExpectedAnotherException(Func<Task> test, string exceptionName, string storeText)
    {
        ThrowsException thrown = await Assert.ThrowsAsync<ThrowsException>(test);

        Assert.NotNull(thrown.InnerException);
        Assert.Equal(exceptionName, thrown.InnerException.GetType().Name);
        Assert.Contains(storeText, thrown.InnerException.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The base compared the store's message against EF's own and they differ.
    /// </summary>
    /// <param name="test">The base test.</param>
    /// <param name="storeText">
    ///     The start of the store's message. xUnit shortens a long string in its failure, so this has
    ///     to be a prefix that survives the shortening.
    /// </param>
    public static async Task BaseExpectedAnotherMessage(Func<Task> test, string storeText)
    {
        EqualException thrown = await Assert.ThrowsAsync<EqualException>(test);

        Assert.Contains(storeText, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>The base expected one value and the store gave another.</summary>
    /// <param name="test">The base test.</param>
    /// <param name="expected">What the base expects.</param>
    /// <param name="actual">What the store gives.</param>
    public static async Task BaseExpectedAnotherValue(Func<Task> test, int expected, int actual)
    {
        EqualException thrown = await Assert.ThrowsAsync<EqualException>(test);

        Assert.Matches($@"Expected:\s+{expected}\b", thrown.Message);
        Assert.Matches($@"Actual:\s+{actual}\b", thrown.Message);
    }
}
