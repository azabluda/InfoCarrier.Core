// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Infrastructure
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     <para>
    ///         Allows InfoCarrier specific configuration to be performed on <see cref="DbContextOptions" />.
    ///     </para>
    ///     <para>
    ///         Instances of this class are returned from a call to
    ///         <see
    ///             cref="InfoCarrierDbContextOptionsExtensions.UseInfoCarrierBackend(DbContextOptionsBuilder, IInfoCarrierBackend, System.Action{InfoCarrierDbContextOptionsBuilder})" />
    ///         and it is not designed to be directly constructed in your application code.
    ///     </para>
    /// </summary>
    public class InfoCarrierDbContextOptionsBuilder
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="InfoCarrierDbContextOptionsBuilder" /> class.
        /// </summary>
        /// <param name="optionsBuilder"> The options builder. </param>
        public InfoCarrierDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            this.OptionsBuilder = optionsBuilder;
        }

        /// <summary>
        ///     Clones the configuration in this builder.
        /// </summary>
        /// <returns> The cloned configuration. </returns>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Entity Framework Core internal.")]
        protected virtual DbContextOptionsBuilder OptionsBuilder { get; }
    }
}
