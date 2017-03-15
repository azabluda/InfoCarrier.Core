namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore.Specification.Tests;

    public class GenericTestStore : TestStore
    {
        private readonly Action deleteDatabase;

        public GenericTestStore(Action deleteDatabase)
        {
            this.deleteDatabase = deleteDatabase;
        }

        public override void Dispose()
        {
            this.deleteDatabase.Invoke();
            base.Dispose();
        }
    }
}
