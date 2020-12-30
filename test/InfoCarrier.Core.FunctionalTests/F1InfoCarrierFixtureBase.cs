// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata;

    public abstract class F1InfoCarrierFixtureBase : F1FixtureBase
    {
        protected IModel CreateModelExternal()
        {
            var builder = this.CreateModelBuilder();
            this.BuildModelExternal(builder);
            builder.FinalizeModel();
            return builder.Model;
        }
    }
}
