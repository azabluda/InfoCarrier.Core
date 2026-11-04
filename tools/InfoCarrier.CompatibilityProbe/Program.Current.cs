// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.CompatibilityProbe;

// Excluded by the project when compiling against preserved historical product assemblies.
internal static partial class Program
{
    static partial void RunCurrentCommand(string[] args, ref bool handled)
    {
        if (args is ["verify-retired"])
        {
            Type[] historical = typeof(InfoCarrierEnvelope).Assembly.GetTypes().Where(type =>
                type.Namespace?.StartsWith("InfoCarrier.Core.Wire.V3", StringComparison.Ordinal) == true
                || type.Name is "Protocol3Mapping" or "Protocol3WireProtocolCodec").ToArray();
            if (historical.Length != 0) { throw new InvalidOperationException("Historical product types remain in the retirement build."); }
            Console.WriteLine("PASS physical retirement assembly contains no protocol 3 definitions, mapping, or codec");
            handled = true;
        }
        else if (args is ["metrics", string number] && int.TryParse(number, out int version) && version is 3 or 4)
        {
            Metrics.Run(version);
            handled = true;
        }
    }
}
