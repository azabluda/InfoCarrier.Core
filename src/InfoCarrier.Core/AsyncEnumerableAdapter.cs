namespace InfoCarrier.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class AsyncEnumerableAdapter<T> : IAsyncEnumerable<T>
    {
        private readonly Func<IAsyncEnumerator<T>> enumeratorFactory;

        public AsyncEnumerableAdapter(Task<IEnumerable<T>> asyncResult)
        {
            this.enumeratorFactory =
                () => new AsyncEnumerator(asyncResult);
        }

        public IAsyncEnumerator<T> GetEnumerator() => this.enumeratorFactory();

        private class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly Task<IEnumerable<T>> asyncResult;
            private IEnumerator<T> enumerator;

            public AsyncEnumerator(Task<IEnumerable<T>> asyncResult)
            {
                this.asyncResult = asyncResult;
            }

            public T Current =>
                this.enumerator == null
                    ? default(T)
                    : this.enumerator.Current;

            public void Dispose()
            {
                this.enumerator?.Dispose();
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (this.enumerator == null)
                {
                    this.enumerator = (await this.asyncResult).GetEnumerator();
                }

                return this.enumerator.MoveNext();
            }
        }
    }
}