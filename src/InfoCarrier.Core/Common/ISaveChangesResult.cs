// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore.Update;

    /// <summary>
    ///     A result of a SaveChanges/SaveChangesAsync operation executed on the server-side.
    /// </summary>
    public interface ISaveChangesResult
    {
        /// <summary>
        ///     Applies the server-side result to the client-side state entries.
        /// </summary>
        /// <param name="entries">The client-side state entries.</param>
        /// <returns>The number of state entries written to the database.</returns>
        int ApplyTo(IList<IUpdateEntry> entries);
    }
}
