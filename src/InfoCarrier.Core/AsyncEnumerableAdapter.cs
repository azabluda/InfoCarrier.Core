// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Adapter class which represents <see cref="Task{IEnumerable}" /> as <see cref="IAsyncEnumerable{T}" />.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    internal class AsyncEnumerableAdapter<T> : IAsyncEnumerable<T>
    {
        private readonly Func<IAsyncEnumerator<T>> enumeratorFactory;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AsyncEnumerableAdapter{T}"/> class.
        /// </summary>
        /// <param name="enumerableTask">A <see cref="Task{IEnumerable}"/> to adapt.</param>
        public AsyncEnumerableAdapter(Task<IEnumerable<T>> enumerableTask)
        {
            this.enumeratorFactory =
                () => new AsyncEnumerator(enumerableTask);
        }

        /// <summary>
        ///     Gets an asynchronous enumerator over the sequence.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>Enumerator for asynchronous enumeration over the sequence.</returns>
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => this.enumeratorFactory();

        private class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly Task<IEnumerable<T>> enumerableTask;
            private IEnumerator<T> enumerator;

            public AsyncEnumerator(Task<IEnumerable<T>> enumerableTask)
            {
                this.enumerableTask = enumerableTask;
            }

            public T Current =>
                this.enumerator == null
                    ? default
                    : this.enumerator.Current;

            public ValueTask DisposeAsync()
            {
                this.enumerator?.Dispose();
                return default;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (this.enumerator == null)
                {
                    this.enumerator = (await this.enumerableTask).GetEnumerator();
                }

                return this.enumerator.MoveNext();
            }
        }
    }
}
