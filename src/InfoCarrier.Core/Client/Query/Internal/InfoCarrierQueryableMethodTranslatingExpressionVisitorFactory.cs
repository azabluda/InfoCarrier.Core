﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using InfoCarrier.Core.Properties;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     <para>
    ///         This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///         the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///         any release. You should only use it directly in your code with extreme caution and knowing that
    ///         doing so can result in application failures when updating to a new Entity Framework Core release.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Singleton" />. This means a single instance
    ///         is used by many <see cref="DbContext" /> instances. The implementation must be thread-safe.
    ///         This service cannot depend on services registered as <see cref="ServiceLifetime.Scoped" />.
    ///     </para>
    /// </summary>
    public class InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory : IQueryableMethodTranslatingExpressionVisitorFactory
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies dependencies;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
        {
            this.dependencies = dependencies;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public virtual QueryableMethodTranslatingExpressionVisitor Create(IModel model)
            => throw new InvalidOperationException(InfoCarrierStrings.NotUsingEntityQueryableExpressionVisitor);
    }
}
