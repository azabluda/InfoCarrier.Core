// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Xml;

// Stream XML rather than matching tags: theory names can contain an unescaped '>' attribute value.
internal static class TrxFailures
{
    private const string Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    internal static int CompareCodePoints(string left, string right)
    {
        var a = left.EnumerateRunes().GetEnumerator();
        var b = right.EnumerateRunes().GetEnumerator();
        while (a.MoveNext())
        {
            if (!b.MoveNext()) return 1;
            var difference = a.Current.Value.CompareTo(b.Current.Value);
            if (difference != 0) return difference;
        }
        return b.MoveNext() ? -1 : 0;
    }

    internal static IEnumerable<string> Failing(IEnumerable<string> paths)
    {
        var names = new SortedSet<string>(Comparer<string>.Create(CompareCodePoints));
        foreach (var path in paths)
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            while (reader.Read())
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Namespace && reader.LocalName == "UnitTestResult"
                    && reader.GetAttribute("outcome") == "Failed" && reader.GetAttribute("testName") is { Length: > 0 } name)
                    names.Add(name);
        }
        return names;
    }

    public static int Run(string[] args)
    {
        Console.Out.NewLine = "\n";
        if (args.Length == 0) { Console.Error.WriteLine("usage: TrxFailures.cs <results.trx> [more.trx ...]"); return 2; }
        try
        {
            foreach (var name in Failing(args)) Console.WriteLine(name);
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException)
        {
            Console.Error.WriteLine($"trx-failures: cannot read a TRX: {error.Message}");
            return 1;
        }
    }
}
