// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public struct SharedTestStoreProperties
    {
        public Type ContextType;

        public Action<ModelBuilder, DbContext> OnModelCreating;

        public Func<DbContextOptionsBuilder, DbContextOptionsBuilder> OnAddOptions;
    }
}
