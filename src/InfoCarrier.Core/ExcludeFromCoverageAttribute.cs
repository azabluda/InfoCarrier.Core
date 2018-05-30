// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core
{
    using System;

    /// <summary>
    /// Specifies that the attributed code should be excluded from code coverage
    /// collection.  Placing this attribute on a class/struct excludes all
    /// enclosed methods and properties from code coverage collection.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event,
        Inherited = false)]
    internal sealed class ExcludeFromCoverageAttribute : Attribute
    {
    }
}
