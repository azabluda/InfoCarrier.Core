namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Utils;

    internal class InfoCarrierLinqOperatorProvider : LinqOperatorProvider
    {
        public override MethodInfo All => Impl.All;

        public override MethodInfo Any => Impl.Any;

        public override MethodInfo Cast => Impl.Cast;

        public override MethodInfo Concat => Impl.Concat;

        public override MethodInfo Contains => Impl.Contains;

        public override MethodInfo Count => Impl.Count;

        public override MethodInfo DefaultIfEmpty => Impl.DefaultIfEmpty;

        public override MethodInfo DefaultIfEmptyArg => Impl.DefaultIfEmptyArg;

        public override MethodInfo Distinct => Impl.Distinct;

        public override MethodInfo Except => Impl.Except;

        public override MethodInfo First => Impl.First;

        public override MethodInfo FirstOrDefault => Impl.FirstOrDefault;

        public override MethodInfo GroupBy => Impl.GroupBy;

        public override MethodInfo GroupJoin => Impl.GroupJoin;

        public override MethodInfo Intersect => Impl.Intersect;

        public override MethodInfo Join => Impl.Join;

        public override MethodInfo Last => Impl.Last;

        public override MethodInfo LastOrDefault => Impl.LastOrDefault;

        public override MethodInfo LongCount => Impl.LongCount;

        public override MethodInfo OfType => Impl.OfType;

        public override MethodInfo OrderBy => Impl.OrderBy;

        public override MethodInfo Select => Impl.Select;

        public override MethodInfo SelectMany => Impl.SelectMany;

        public override MethodInfo Single => Impl.Single;

        public override MethodInfo SingleOrDefault => Impl.SingleOrDefault;

        public override MethodInfo Skip => Impl.Skip;

        public override MethodInfo Take => Impl.Take;

        public override MethodInfo ThenBy => Impl.ThenBy;

        public override MethodInfo Union => Impl.Union;

        public override MethodInfo Where => Impl.Where;

        private static class Impl
        {
            private static readonly Expression<Func<object, bool>> DummyPredicate = null;
            private static readonly Expression<Func<object, IEnumerable<object>>> DummyPredicateEnum = null;

            public static readonly MethodInfo All =
                GetMethod(() => Queryable.All<object>(null, null));

            public static readonly MethodInfo Any =
                GetMethod(() => Queryable.Any<object>(null));

            public static readonly MethodInfo Cast =
                GetMethod(() => Queryable.Cast<object>(null));

            public static readonly MethodInfo Concat =
                GetMethod(() => Queryable.Concat<object>(null, null));

            public static readonly MethodInfo Contains =
                GetMethod(() => Queryable.Contains<object>(null, null));

            public static readonly MethodInfo Count =
                GetMethod(() => Queryable.Count<object>(null));

            public static readonly MethodInfo DefaultIfEmpty =
                GetMethod(() => Queryable.DefaultIfEmpty<object>(null));

            public static readonly MethodInfo DefaultIfEmptyArg =
                GetMethod(() => Queryable.DefaultIfEmpty<object>(null, null));

            public static readonly MethodInfo Distinct =
                GetMethod(() => Queryable.Distinct<object>(null));

            public static readonly MethodInfo Except =
                GetMethod(() => Queryable.Except<object>(null, null));

            public static readonly MethodInfo First =
                GetMethod(() => Queryable.First<object>(null));

            public static readonly MethodInfo FirstOrDefault =
                GetMethod(() => Queryable.FirstOrDefault<object>(null));

            public static readonly MethodInfo GroupBy =
                GetMethod(() => Queryable.GroupBy(null, DummyPredicate, DummyPredicate));

            public static readonly MethodInfo GroupJoin =
                GetMethod(() => Queryable.GroupJoin<object, object, object, object>(null, null, null, null, null));

            public static readonly MethodInfo Intersect =
                GetMethod(() => Queryable.Intersect<object>(null, null));

            public static readonly MethodInfo Join =
                GetMethod(() => Queryable.Join<object, object, object, object>(null, null, null, null, null));

            public static readonly MethodInfo Last =
                GetMethod(() => Queryable.Last<object>(null));

            public static readonly MethodInfo LastOrDefault =
                GetMethod(() => Queryable.LastOrDefault<object>(null));

            public static readonly MethodInfo LongCount =
                GetMethod(() => Queryable.LongCount<object>(null));

            public static readonly MethodInfo OfType =
                GetMethod(() => Queryable.OfType<object>(null));

            public static readonly MethodInfo OrderBy =
                GetMethod(() => Queryable.OrderBy<object, object>(null, null));

            public static readonly MethodInfo Select =
                GetMethod(() => Queryable.Select(null, DummyPredicate));

            public static readonly MethodInfo SelectMany =
                GetMethod(() => Queryable.SelectMany(null, DummyPredicateEnum));

            public static readonly MethodInfo Single =
                GetMethod(() => Queryable.Single<object>(null));

            public static readonly MethodInfo SingleOrDefault =
                GetMethod(() => Queryable.SingleOrDefault<object>(null));

            public static readonly MethodInfo Skip =
                GetMethod(() => Queryable.Skip<object>(null, 1));

            public static readonly MethodInfo Take =
                GetMethod(() => Queryable.Take<object>(null, 1));

            public static readonly MethodInfo ThenBy =
                GetMethod(() => Queryable.ThenBy<object, object>(null, null));

            public static readonly MethodInfo Union =
                GetMethod(() => Queryable.Union<object>(null, null));

            public static readonly MethodInfo Where =
                GetMethod(() => Queryable.Where(null, DummyPredicate));

            private static MethodInfo GetMethod(Expression<Action> expression)
            {
                MethodInfo mi = SymbolExtensions.GetMethodInfo(expression);
                return mi.IsGenericMethod ? mi.GetGenericMethodDefinition() : mi;
            }
        }
    }
}