// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     A key type that an EF specification base declares privately, resolved so a fixture can name
///     it in <c>AllowTypes</c> the way an application names its own.
/// </summary>
/// <remarks>
///     <para>
///         <b>ONE TYPE, FOR ONE TEST, AND THIS IS NOT THE START OF A SWEEP (the owner,
///         2026-09-21).</b> Registering every private type an EF base declares would turn the
///         specification suite into a tuned configuration and hide the next genuine instance of a
///         type that cannot cross, which is the thing this suite is best at finding. Each entry
///         here is added one at a time, because one named test's drift against EF's own SQL was
///         worth removing, and the comparison exercise is what names it.
///     </para>
///     <para>
///         <b>Why a fixture registers a key type at all.</b> A <c>GroupBy</c> or <c>Join</c> key of
///         a type the boundary cannot ship keeps its operator on the client, so the server sends
///         every row and this client does the work. The answer is right and the payload is the
///         whole table, which <c>ServerParameterizationTest</c> measures both ways. An application
///         fixes that by naming the type on both halves, and this harness is an application: the
///         same fixture already names <c>SqlQueryTestBase</c>'s four unmapped projection types for
///         the same reason.
///     </para>
///     <para>
///         <b>Why reflection.</b> <c>NoGroupByWrapper</c> is <c>private</c>, so no caller can write
///         <c>typeof</c> for it, and it is nested inside a generic base, so reflection gives its
///         generic DEFINITION and the closed type has to be built from the fixture the test runs
///         against. That is three assumptions about somebody else's private shape.
///     </para>
///     <para>
///         <b>So every one of them fails loudly.</b> If the nested type is renamed, moved or made
///         non-generic by an EF version bump, this throws while the fixture is built and every
///         Northwind test says so. It must never return a type that does not match, because the
///         quiet failure is the one that matters: the allowlist would simply not admit the key,
///         the grouping would go back to running here, and the only symptom would be a larger
///         payload that nothing asserts.
///     </para>
/// </remarks>
internal static class UpstreamKeyTypes
{
    /// <summary>
    ///     The grouping key of <c>NorthwindGroupByQueryTestBase.Odata_groupby_empty_key</c>, closed
    ///     over <paramref name="fixtureType" />.
    /// </summary>
    /// <remarks>
    ///     The test is OData's "aggregate the whole set, do not group": the key is an empty class
    ///     whose <c>Equals</c> is true for every instance, so EF's relational providers translate
    ///     it to <c>GROUP BY</c> on a constant and compute the aggregate in the store.
    /// </remarks>
    /// <param name="fixtureType">The fixture the base is closed over.</param>
    /// <returns>The closed key type.</returns>
    public static Type NoGroupByWrapper(Type fixtureType)
        => Nested(typeof(NorthwindGroupByQueryTestBase<>), "NoGroupByWrapper", fixtureType);

    private static Type Nested(Type declaringDefinition, string name, Type fixtureType)
    {
        Type nested = declaringDefinition.GetNestedType(name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"{declaringDefinition.Name} no longer declares a non-public nested '{name}'. "
                + "An EF version bump has moved or renamed it. Find its replacement and name that "
                + "instead; leaving it unregistered is not the same behaviour, it sends the whole "
                + "table (see UpstreamKeyTypes).");

        // A type nested in a generic type carries the enclosing type's parameters, so what
        // reflection hands back is a definition and the expression tree holds the closed type.
        // Guarded rather than assumed, because a future EF could nest it somewhere non-generic.
        if (!nested.IsGenericTypeDefinition)
        {
            return nested;
        }

        Type[] parameters = nested.GetGenericArguments();
        return parameters.Length == 1
            ? nested.MakeGenericType(fixtureType)
            : throw new InvalidOperationException(
                $"'{name}' now takes {parameters.Length} generic arguments rather than the one it "
                + "inherits from its declaring type, so this cannot know what to close it over.");
    }
}
