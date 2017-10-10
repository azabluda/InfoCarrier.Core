// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore.Update;

    public interface ISaveChangesResult
    {
        int Process(IReadOnlyList<IUpdateEntry> entries);
    }
}
