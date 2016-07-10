namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    internal interface IInfoCarrierLinqOperatorProvider : ILinqOperatorProvider
    {
        MethodInfo OrderByDescending { get; }

        MethodInfo ThenByDescending { get; }
    }
}
