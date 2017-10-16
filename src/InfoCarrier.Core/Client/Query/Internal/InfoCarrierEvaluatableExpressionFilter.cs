// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierEvaluatableExpressionFilter : EvaluatableExpressionFilter
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public override bool IsEvaluatableMember(MemberExpression memberExpression)
            => Remote.Linq.EntityFrameworkCore.ExpressionEvaluator.CanBeEvaluated(memberExpression)
               && base.IsEvaluatableMember(memberExpression);
    }
}
