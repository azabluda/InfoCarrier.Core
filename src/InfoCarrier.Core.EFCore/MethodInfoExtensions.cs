namespace InfoCarrier.Core
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    public static class MethodInfoExtensions
    {
        public static MethodInfo GetMethodInfo(Expression<Action> expression)
        {
            var outermostExpression = expression.Body as MethodCallExpression;
            if (outermostExpression != null)
            {
                return outermostExpression.Method;
            }

            throw new ArgumentException(@"Invalid Expression. Expression should consist of a Method call only.");
        }

        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo)
        {
            return (TDelegate)(object)Delegate.CreateDelegate(typeof(TDelegate), methodInfo);
        }

        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo, object firstArgument)
        {
            return (TDelegate)(object)Delegate.CreateDelegate(typeof(TDelegate), firstArgument, methodInfo);
        }
    }
}
