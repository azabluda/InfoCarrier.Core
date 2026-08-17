// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Metadata;

/// <summary>
///     Answers, for the client's model, the one question this provider asks about how a backing
///     store lays out a nested structure: <b>is this type stored inside one document belonging to
///     something else?</b>
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this is a seam at all (M9, D3 answer (c)).</b> This provider is not relational
///         and its client is never a relational context (ADR-013) — yet two components have to
///         reach the same answer the <em>backing store</em> reaches, or the two models disagree
///         silently. B12 is the worked example and its symptom was wrong data with no exception:
///         a JSON document carries no key for its array elements, every store synthesizes an
///         ordinal, and a client that kept the CLR <c>Id</c> instead gave every element the same
///         key.
///     </para>
///     <para>
///         The question is genuinely store-shaped, which is why it is asked through an interface
///         rather than answered inline. A relational store answers it from a container column; a
///         document store such as Cosmos recognises an ordinal key by the property's <em>shape</em>
///         rather than by this name, and would answer both members differently. This is the same
///         arrangement <c>ServerSaveChangesExecutor.IssuedAtSave</c> uses one level down — ask the
///         capability, never test for a store family.
///     </para>
///     <para>
///         Resolved from the client's service provider; <see cref="AnnotationDocumentMapping" /> is
///         registered by default and an application replaces it for a store that answers
///         differently.
///     </para>
/// </remarks>
public interface IInfoCarrierDocumentMapping
{
    /// <summary>
    ///     The name of the document container <paramref name="type" /> is stored in, or
    ///     <see langword="null" /> when it is not stored inside one.
    /// </summary>
    /// <remarks>
    ///     Only <see langword="null" /> versus not-null is load-bearing today. The name is returned
    ///     rather than a <see cref="bool" /> because two types in one document is a distinction a
    ///     later caller may need, and widening a <see cref="bool" /> afterwards would be a breaking
    ///     change.
    ///     <para>
    ///         <b>An implementation must walk the ownership chain</b>: a nested owned type inherits
    ///         its container from whichever ancestor declared it, and answering only for the type
    ///         that carries the configuration reports the wrong thing for everything beneath it.
    ///     </para>
    /// </remarks>
    /// <param name="type">The entity or complex type to ask about.</param>
    /// <returns>The container name, or <see langword="null" />.</returns>
    string? FindContainerName(IReadOnlyTypeBase type);

    /// <summary>
    ///     Model annotations whose change can alter what <see cref="FindContainerName" /> answers.
    /// </summary>
    /// <remarks>
    ///     A convention has to re-run when one of these changes, because the configuration that
    ///     decides this — <c>ToJson()</c> on a relational store — may be applied <em>after</em> a
    ///     key has already been discovered. An implementation that decides from the model's shape
    ///     rather than from an annotation returns nothing here.
    /// </remarks>
    IEnumerable<string> ContainerAnnotationNames { get; }

    /// <summary>
    ///     The name the backing store gives the synthesized ordinal of an element inside a
    ///     document.
    /// </summary>
    /// <remarks>
    ///     Part of this seam rather than a constant, because it is exactly the part that varies:
    ///     a relational store names the property, and Cosmos recognises the ordinal by the
    ///     property's shape instead.
    /// </remarks>
    string SynthesizedOrdinalPropertyName { get; }
}
