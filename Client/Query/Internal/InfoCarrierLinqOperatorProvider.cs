namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Internal;
    using Utils;

    internal abstract class InfoCarrierLinqOperatorProvider : LinqOperatorProvider
    {
        public abstract MethodInfo OrderByDescending { get; }

        public abstract MethodInfo ThenByDescending { get; }

        protected static MethodInfo GetMethod(Expression<Action> expression)
        {
            MethodInfo mi = SymbolExtensions.GetMethodInfo(expression);
            return mi.IsGenericMethod ? mi.GetGenericMethodDefinition() : mi;
        }
    }
}
