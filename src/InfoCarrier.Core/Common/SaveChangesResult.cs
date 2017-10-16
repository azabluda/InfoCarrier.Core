// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Update;

    /// <summary>
    ///     A serializable object containing the result of a SaveChanges/SaveChangesAsync operation
    ///     executed on the server-side. It can either be Success or Error.
    /// </summary>
    [DataContract]
    [KnownType(typeof(SuccessImpl))]
    [KnownType(typeof(ErrorImpl))]
    public class SaveChangesResult : ISaveChangesResult
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SaveChangesResult"/> class.
        /// </summary>
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public SaveChangesResult()
        {
        }

        private SaveChangesResult(ISaveChangesResult impl)
        {
            this.Impl = impl;
        }

        [DataMember]
        private ISaveChangesResult Impl { get; set; }

        /// <summary>
        ///     Create a Success result object.
        /// </summary>
        /// <param name="countPersisted">The number of state entries written to the database.</param>
        /// <param name="entries">
        ///     State entries after they have been persisted on the server-side. They usually contain
        ///     new values for keys which are to be sent back to the client.
        /// </param>
        /// <returns>The Success result object.</returns>
        internal static SaveChangesResult Success(int countPersisted, IEnumerable<IUpdateEntry> entries)
            => new SaveChangesResult(new SuccessImpl(countPersisted, entries));

        /// <summary>
        ///     Create an Error result object.
        /// </summary>
        /// <param name="exception">The <see cref="DbUpdateException" /> which occurred during SaveChanges operation.</param>
        /// <param name="entries">
        ///     The reference collection of <see cref="IUpdateEntry" />'s of the original <see cref="SaveChangesRequest"/>
        ///     needed for determining the <see cref="ErrorImpl.FailedEntityIndexes" />.
        /// </param>
        /// <returns>The Error result object.</returns>
        internal static SaveChangesResult Error(DbUpdateException exception, IEnumerable<IUpdateEntry> entries)
            => new SaveChangesResult(new ErrorImpl(exception, entries));

        /// <summary>
        ///     Applies the server-side result to the client-side state entries.
        /// </summary>
        /// <param name="entries">The client-side state entries.</param>
        /// <returns>The number of state entries written to the database.</returns>
        public int ApplyTo(IReadOnlyList<IUpdateEntry> entries)
            => this.Impl.ApplyTo(entries);

        [DataContract]
        private class SuccessImpl : SaveChangesRequest, ISaveChangesResult
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public SuccessImpl()
            {
            }

            internal SuccessImpl(int countPersisted, IEnumerable<IUpdateEntry> entries)
                : base(entries)
            {
                this.CountPersisted = countPersisted;
            }

            [DataMember]
            private int CountPersisted { get; set; }

            int ISaveChangesResult.ApplyTo(IReadOnlyList<IUpdateEntry> entries)
            {
                // Merge the results / update properties modified during SaveChanges on the server-side
                foreach (var merge in entries.Zip(this.DataTransferObjects, (e, d) => new { Entry = e, Dto = d }))
                {
                    foreach (var p in merge.Dto.JoinScalarProperties(merge.Entry.ToEntityEntry(), this.Mapper))
                    {
                        // Can not (and need not) merge non-temporary PK values
                        if (p.EfProperty.Metadata.IsKey() && !p.EfProperty.IsTemporary)
                        {
                            continue;
                        }

                        p.EfProperty.CurrentValue = p.CurrentValue;
                    }
                }

                return this.CountPersisted;
            }
        }

        [DataContract]
        private class ErrorImpl : ISaveChangesResult
        {
            [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
            public ErrorImpl()
            {
            }

            internal ErrorImpl(DbUpdateException exception, IEnumerable<IUpdateEntry> entries)
            {
                this.IsConcurrencyException = exception is DbUpdateConcurrencyException;
                this.Message = exception.Message;

                var map = entries
                    .Select((e, i) => new { Index = i, Entry = e })
                    .ToDictionary(x => x.Entry, x => x.Index);
                this.FailedEntityIndexes = exception.Entries.Select(re => map[re.GetInfrastructure()]).ToArray();
            }

            [DataMember]
            private bool IsConcurrencyException { get; set; }

            [DataMember]
            private string Message { get; set; }

            [DataMember]
            private int[] FailedEntityIndexes { get; set; }

            int ISaveChangesResult.ApplyTo(IReadOnlyList<IUpdateEntry> entries)
            {
                IReadOnlyList<IUpdateEntry> failedEntries = this.FailedEntityIndexes.Select(x => entries[x]).ToList();

                if (this.IsConcurrencyException)
                {
                    throw new DbUpdateConcurrencyException(this.Message, failedEntries);
                }

                throw failedEntries.Any()
                    ? new DbUpdateException(this.Message, failedEntries)
                    : new DbUpdateException(this.Message, (Exception)null);
            }
        }
    }
}
