// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     Gives a JSON-mapped owned collection the synthesized-ordinal key its backing store gives
///     it, so that the client's model and the server's agree about what identifies an element.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a key convention is a wire concern.</b> This provider is two EF instances over
///         one <c>OnModelCreating</c>, and anything either side computes from the model has to be
///         computed the same way on both or the wire means different things at each end. A key is
///         the sharpest case: the client resolves identity with it.
///     </para>
///     <para>
///         A JSON document does not carry a key for its array elements — the element's identity
///         <em>is</em> its ordinal. Every store that maps an owned collection to JSON therefore
///         synthesizes one, and EF's relational conventions do it in
///         <c>RelationalKeyDiscoveryConvention</c>: the key becomes
///         <c>[…ownership foreign key…, …any keys the application declared…, __synthesizedOrdinal]</c>.
///         Without this, the client kept the CLR <c>Id</c> property instead — a property the
///         document does not contain, so it is <c>0</c> for every element, every element of every
///         owner shares one key, and EF's fixup hands each of them to all of them. Wrong data, no
///         exception, and 36 of `JsonQueryTestBase` (B12, priced in C78, taken in C80).
///     </para>
///     <para>
///         <b>What makes this safe to state on a non-relational provider.</b> The trigger is the
///         caller's own <c>ToJson()</c>, which is an annotation and is therefore already on the
///         client's model — both sides run the same <c>OnModelCreating</c>. Nothing relational is
///         resolved from the service provider; only <c>RelationalTypeBaseExtensions</c>'s
///         <c>GetContainerColumnName()</c> is read, and it walks the ownership chain so a nested
///         owned type inherits its container from the type that declared it. Where no
///         <c>ToJson()</c> was called the convention is inert and the core behaviour is unchanged.
///     </para>
///     <para>
///         <b>What it does not cover.</b> A store that maps to JSON by some other route
///         synthesizes its ordinal by its own convention: the Cosmos provider recognises one by
///         the property's <em>shape</em> rather than by this name, and does not use the relational
///         container annotation at all. A Cosmos backend would need its own clause here. Every
///         backend in scope today is relational (ADR-009 Tier B) or InMemory, which cannot map to
///         JSON at all.
///     </para>
///     <para>
///         The body below is <c>RelationalKeyDiscoveryConvention</c>'s JSON branch, which derives
///         from this same public core convention. It is duplicated rather than inherited because
///         inheriting it requires <c>RelationalConventionSetBuilderDependencies</c>, which only a
///         relational provider registers.
///     </para>
/// </remarks>
public class InfoCarrierKeyDiscoveryConvention(
    ProviderConventionSetBuilderDependencies dependencies,
    Metadata.IInfoCarrierDocumentMapping documentMapping)
    : KeyDiscoveryConvention(dependencies), IEntityTypeAnnotationChangedConvention
{
    /// <summary>
    ///     EF's own name for the synthesized ordinal, taken from EF rather than repeated, so the
    ///     two models cannot drift apart on it.
    /// </summary>
    public virtual string SynthesizedOrdinalPropertyName =>
        documentMapping.SynthesizedOrdinalPropertyName;

    /// <inheritdoc />
    /// <remarks>
    ///     A JSON-mapped owned collection discovers no key of its own, so that a property the
    ///     application happens to call <c>Id</c> is persisted as an ordinary value instead of
    ///     being mistaken for the element's identity.
    /// </remarks>
    protected override List<IConventionProperty>? DiscoverKeyProperties(IConventionEntityType entityType)
    {
        IConventionForeignKey? ownership = entityType.FindOwnership();
        if (ownership?.DeclaringEntityType != entityType)
        {
            ownership = null;
        }

        if (ownership?.IsUnique == false
            && documentMapping.FindContainerName(entityType) is not null)
        {
            return [];
        }

        return base.DiscoverKeyProperties(entityType);
    }

    /// <inheritdoc />
    protected override void ProcessKeyProperties(
        IList<IConventionProperty> keyProperties,
        IConventionEntityType entityType)
    {
        bool isMappedToJson = documentMapping.FindContainerName(entityType) is not null;
        IConventionProperty? synthesizedProperty =
            keyProperties.FirstOrDefault(p => p.Name == SynthesizedOrdinalPropertyName);
        IConventionForeignKey? ownershipForeignKey = entityType.FindOwnership();

        if (ownershipForeignKey?.IsUnique == false && isMappedToJson)
        {
            // The composite key, in order: the foreign key back to the owner, then whatever the
            // application declared, then the ordinal. The list is manipulated in place because
            // that is the contract of this method.
            List<IConventionProperty> declared = [.. keyProperties];
            keyProperties.Clear();

            foreach (IConventionProperty ownershipProperty in ownershipForeignKey.Properties)
            {
                keyProperties.Add(ownershipProperty);
            }

            synthesizedProperty ??= entityType.Builder
                .CreateUniqueProperty(typeof(int), SynthesizedOrdinalPropertyName, required: true)
                is { } builder
                    ? (builder.ValueGenerated(ValueGenerated.OnAdd) ?? builder).Metadata
                    : null;

            foreach (IConventionProperty keyProperty in declared)
            {
                if (keyProperty != synthesizedProperty && !keyProperties.Contains(keyProperty))
                {
                    keyProperties.Add(keyProperty);
                }
            }

            if (synthesizedProperty is not null)
            {
                keyProperties.Add(synthesizedProperty);
            }
        }
        else
        {
            // No longer a JSON-mapped owned collection: the ordinal, if one was synthesized
            // earlier, is not part of the key any more.
            if (synthesizedProperty is not null)
            {
                keyProperties.Remove(synthesizedProperty);
            }

            base.ProcessKeyProperties(keyProperties, entityType);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The synthesized ordinal must not itself trigger key discovery, or adding it would
    ///     re-enter the pass that added it.
    /// </remarks>
    public override void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        if (propertyBuilder.Metadata.Name != SynthesizedOrdinalPropertyName)
        {
            base.ProcessPropertyAdded(propertyBuilder, context);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    ///     <c>ToJson()</c> may be called after the key has already been discovered, and it changes
    ///     the answer — for the type it names and for every owned type beneath it, which inherit
    ///     the container.
    /// </remarks>
    public virtual void ProcessEntityTypeAnnotationChanged(
        IConventionEntityTypeBuilder entityTypeBuilder,
        string name,
        IConventionAnnotation? annotation,
        IConventionAnnotation? oldAnnotation,
        IConventionContext<IConventionAnnotation> context)
    {
        if (documentMapping.ContainerAnnotationNames.Contains(name, StringComparer.Ordinal))
        {
            Reconfigure(this, entityTypeBuilder);
        }

        static void Reconfigure(InfoCarrierKeyDiscoveryConvention convention, IConventionEntityTypeBuilder builder)
        {
            convention.TryConfigurePrimaryKey(builder);

            foreach (IConventionForeignKey referencing in builder.Metadata.GetReferencingForeignKeys())
            {
                if (referencing.IsOwnership)
                {
                    Reconfigure(convention, referencing.DeclaringEntityType.Builder);
                }
            }
        }
    }
}
