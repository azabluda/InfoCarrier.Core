// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierQueryContext : QueryContext
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierQueryContext(
            QueryContextDependencies dependencies,
            Func<IQueryBuffer> queryBufferFactory,
            IInfoCarrierBackend infoCarrierBackend)
            : base(dependencies, queryBufferFactory)
        {
            this.InfoCarrierBackend = infoCarrierBackend;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Entity Framework Core internal.")]
        public IInfoCarrierBackend InfoCarrierBackend { get; }
    }
}