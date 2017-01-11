namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Remote.Linq;
    using Remotion.Linq.Clauses;

    public class InfoCarrierEntityQueryableExpressionVisitor : EntityQueryableExpressionVisitor
    {
        private readonly IQuerySource querySource;

        public InfoCarrierEntityQueryableExpressionVisitor(
            EntityQueryModelVisitor entityQueryModelVisitor,
            IQuerySource querySource)
            : base(entityQueryModelVisitor)
        {
            this.querySource = querySource;
        }

        protected override Expression VisitEntityQueryable(Type elementType)
        {
            IQueryable stub = MethodInfoExtensions.GetMethodInfo(() => RemoteQueryableStub.Create<object>(null))
                .GetGenericMethodDefinition()
                .MakeGenericMethod(elementType)
                .ToDelegate<Func<IQuerySource, IQueryable>>()
                .Invoke(this.querySource);

            return Expression.Constant(stub);
        }

        internal abstract class RemoteQueryableStub : IRemoteQueryable
        {
            internal RemoteQueryableStub(IQuerySource querySource)
            {
                this.QuerySource = querySource;
            }

            internal IQuerySource QuerySource { get; }

            public abstract Type ElementType { get; }

            protected static dynamic NotImplemented
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public Expression Expression => NotImplemented;

            public IQueryProvider Provider => NotImplemented;

            public IEnumerator GetEnumerator() => NotImplemented;

            internal static IQueryable<T> Create<T>(IQuerySource querySource)
            {
                return new RemoteQueryableStub<T>(querySource);
            }
        }

        private class RemoteQueryableStub<T> : RemoteQueryableStub, IQueryable<T>
        {
            public RemoteQueryableStub(IQuerySource querySource)
                : base(querySource)
            {
            }

            public override Type ElementType => typeof(T);

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => NotImplemented;
        }
    }
}
