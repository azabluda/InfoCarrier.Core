// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using InfoCarrier.Core.ValueMapping;

if (args.Length != 1)
{
    throw new ArgumentException("Pass the path to LegacyMapperConsumer.dll.");
}

try
{
    Assembly legacyAssembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
    Type legacyType = legacyAssembly.GetType("LegacyMapper", throwOnError: true)!;
    object legacyMapper = Activator.CreateInstance(legacyType)!;
    _ = (IInfoCarrierValueMapper)legacyMapper;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

Console.WriteLine("Version 10.2 mapper cast succeeded.");
return 0;
