namespace InfoCarrier.Core.Common
{
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore.Update;

    public interface ISaveChangesResult
    {
        int Process(IReadOnlyList<IUpdateEntry> entries);
    }
}
