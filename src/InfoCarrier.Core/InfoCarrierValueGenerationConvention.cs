// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace InfoCarrier.Core;

/// <summary>
///     Tells the client that a property the caller gave a store default is store-generated
///     (M9 J14/J13 follow-up).
/// </summary>
/// <remarks>
///     <para>
///         <b>The same shape as <see cref="InfoCarrierKeyDiscoveryConvention" />, and the same
///         argument.</b> <c>ValueGenerated</c> is not stated by the model builder; it is inferred
///         by a convention, and the one that reads <c>HasDefaultValue</c> is
///         <c>RelationalValueGenerationConvention</c> — which the server's provider runs and this
///         one does not. B6 recorded the consequence for values coming <em>back</em>: a property the
///         server generates is <c>ValueGenerated.Never</c> here, so it has no store-generated slot.
///     </para>
///     <para>
///         <b>This is the same divergence on the way out, and there it is fatal rather than
///         lossy.</b> `SomethingOfCategoryB.CategoryId` has <c>HasDefaultValue(2)</c> and is half of
///         a composite foreign key. Without this convention the client believes nothing will supply
///         it, and EF's own change tracker refuses the save before the wire is reached:
///         <i>"The value of 'SomethingOfCategoryB.CategoryId' is unknown when attempting to save
///         changes"</i>, raised in <c>InternalEntryBase.PrepareToSave</c> with no frame of this
///         provider's in the stack.
///     </para>
///     <para>
///         <b>Read by string name, as J5 decided.</b> `InfoCarrier.Core` does not reference
///         `Microsoft.EntityFrameworkCore.Relational`, so the annotation is named rather than
///         imported — and pinned by a test that asserts the strings still equal EF's constants,
///         which is the whole price of that decision and is paid in one place.
///     </para>
///     <para>
///         <b>Narrow on purpose.</b> Only a property whose <c>ValueGenerated</c> is still
///         <c>Never</c>, and only where the caller declared a default. It never overrides an
///         explicit <c>ValueGeneratedNever()</c> that the model also gave a default to, because
///         such a property has no annotation to find — and it says nothing about computed columns
///         or `rowversion`, which are M7's business and have no test asking for them yet.
///     </para>
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierValueGenerationConvention" /> class.
/// </remarks>
public class InfoCarrierValueGenerationConvention : IModelFinalizingConvention
{
    /// <summary>
    ///     <c>RelationalAnnotationNames.DefaultValue</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string DefaultValueAnnotation = "Relational:DefaultValue";

    /// <summary>
    ///     <c>RelationalAnnotationNames.DefaultValueSql</c>. Pinned by <c>DocumentMappingPinTest</c>.
    /// </summary>
    public const string DefaultValueSqlAnnotation = "Relational:DefaultValueSql";

    /// <inheritdoc />
    public virtual void ProcessModelFinalizing(
        IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (IConventionProperty property in entityType.GetDeclaredProperties())
            {
                if (property.ValueGenerated == ValueGenerated.Never
                    && (property.FindAnnotation(DefaultValueAnnotation) is not null
                        || property.FindAnnotation(DefaultValueSqlAnnotation) is not null))
                {
                    property.SetValueGenerated(ValueGenerated.OnAdd);
                }
            }
        }
    }
}
