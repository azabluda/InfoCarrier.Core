namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Utils;

    internal class InfoCarrierEnumerableLinqOperatorProvider : LinqOperatorProvider,
        IInfoCarrierLinqOperatorProvider
    {
        private static readonly Func<object, bool> DummyPredicate = null;
        private static readonly Func<object, IEnumerable<object>> DummyPredicateEnum = null;

        private static readonly Lazy<IInfoCarrierLinqOperatorProvider> Inst =
            new Lazy<IInfoCarrierLinqOperatorProvider>(() => new InfoCarrierEnumerableLinqOperatorProvider());

        private InfoCarrierEnumerableLinqOperatorProvider()
        {
        }

        public static IInfoCarrierLinqOperatorProvider Instance => Inst.Value;

        public override MethodInfo All { get; } =
            GetMethod(() => Enumerable.All<object>(null, null));

        public override MethodInfo Any { get; } =
            GetMethod(() => Enumerable.Any<object>(null));

        public override MethodInfo Cast { get; } =
            GetMethod(() => Enumerable.Cast<object>(null));

        public override MethodInfo Concat { get; } =
            GetMethod(() => Enumerable.Concat<object>(null, null));

        public override MethodInfo Contains { get; } =
            GetMethod(() => Enumerable.Contains<object>(null, null));

        public override MethodInfo Count { get; } =
            GetMethod(() => Enumerable.Count<object>(null));

        public override MethodInfo DefaultIfEmpty { get; } =
            GetMethod(() => Enumerable.DefaultIfEmpty<object>(null));

        public override MethodInfo DefaultIfEmptyArg { get; } =
            GetMethod(() => Enumerable.DefaultIfEmpty<object>(null, null));

        public override MethodInfo Distinct { get; } =
            GetMethod(() => Enumerable.Distinct<object>(null));

        public override MethodInfo Except { get; } =
            GetMethod(() => Enumerable.Except<object>(null, null));

        public override MethodInfo First { get; } =
            GetMethod(() => Enumerable.First<object>(null));

        public override MethodInfo FirstOrDefault { get; } =
            GetMethod(() => Enumerable.FirstOrDefault<object>(null));

        public override MethodInfo GroupBy { get; } =
            GetMethod(() => Enumerable.GroupBy<object, object, bool>(null, null, DummyPredicate));

        public override MethodInfo GroupJoin { get; } =
            GetMethod(() => Enumerable.GroupJoin<object, object, object, object>(null, null, null, null, null));

        public override MethodInfo Intersect { get; } =
            GetMethod(() => Enumerable.Intersect<object>(null, null));

        public override MethodInfo Join { get; } =
            GetMethod(() => Enumerable.Join<object, object, object, object>(null, null, null, null, null));

        public override MethodInfo Last { get; } =
            GetMethod(() => Enumerable.Last<object>(null));

        public override MethodInfo LastOrDefault { get; } =
            GetMethod(() => Enumerable.LastOrDefault<object>(null));

        public override MethodInfo LongCount { get; } =
            GetMethod(() => Enumerable.LongCount<object>(null));

        public override MethodInfo OfType { get; } =
            GetMethod(() => Enumerable.OfType<object>(null));

        public override MethodInfo OrderBy { get; } =
            GetMethod(() => Enumerable.OrderBy<object, object>(null, null));

        public virtual MethodInfo OrderByDescending { get; } =
            GetMethod(() => Enumerable.OrderByDescending<object, object>(null, null));

        public override MethodInfo Select { get; } =
            GetMethod(() => Enumerable.Select(null, DummyPredicate));

        public override MethodInfo SelectMany { get; } =
            GetMethod(() => Enumerable.SelectMany<object, object, object>(null, DummyPredicateEnum, null));

        public override MethodInfo Single { get; } =
            GetMethod(() => Enumerable.Single<object>(null));

        public override MethodInfo SingleOrDefault { get; } =
            GetMethod(() => Enumerable.SingleOrDefault<object>(null));

        public override MethodInfo Skip { get; } =
            GetMethod(() => Enumerable.Skip<object>(null, 1));

        public override MethodInfo Take { get; } =
            GetMethod(() => Enumerable.Take<object>(null, 1));

        public override MethodInfo ThenBy { get; } =
            GetMethod(() => Enumerable.ThenBy<object, object>(null, null));

        public virtual MethodInfo ThenByDescending { get; } =
            GetMethod(() => Enumerable.ThenByDescending<object, object>(null, null));

        public override MethodInfo Union { get; } =
            GetMethod(() => Enumerable.Union<object>(null, null));

        public override MethodInfo Where { get; } =
            GetMethod(() => Enumerable.Where(null, DummyPredicate));

        private static MethodInfo GetMethod(Expression<Action> expression)
        {
            MethodInfo mi = SymbolExtensions.GetMethodInfo(expression);
            return mi.IsGenericMethod ? mi.GetGenericMethodDefinition() : mi;
        }
    }
}