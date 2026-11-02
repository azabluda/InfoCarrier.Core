// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

// A case counts against parity only when an InfoCarrier reason covers its theory arguments.
internal static class SpecParity
{
    internal sealed record Reason(string Case, string Side, string Label);
    internal sealed record Result(string Class, string Method, string Name, string Outcome);
    private const string Namespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    internal static (HashSet<string> Controls, Dictionary<(string, string), List<Reason>> Reasons) ReadReasons(IEnumerable<string> paths)
    {
        var controls = new HashSet<string>(StringComparer.Ordinal);
        var reasons = new Dictionary<(string, string), List<Reason>>();
        foreach (var path in paths)
            foreach (var line in File.ReadLines(path))
            {
                var fields = line.Split('\t');
                if (fields.Length == 2 && fields[0] == "control") controls.Add(fields[1]);
                else if (fields.Length == 6 && fields[0] == "reason")
                {
                    var key = (fields[1], fields[2]);
                    if (!reasons.TryGetValue(key, out var entries)) reasons[key] = entries = [];
                    entries.Add(new(fields[3], fields[4], fields[5]));
                }
                else if (line.Length != 0) throw new InvalidDataException($"{path}: not a reasons line: {line}");
            }
        return (controls, reasons);
    }

    internal static IEnumerable<Result> ReadResults(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var outcomes = new Dictionary<string, (string Name, string Outcome)>(StringComparer.Ordinal);
            var definitions = new Dictionary<string, (string Class, string Method)>(StringComparer.Ordinal);
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            string? testId = null;
            while (reader.Read())
            {
                if (reader.NamespaceURI != Namespace) continue;
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "UnitTestResult")
                    outcomes[reader.GetAttribute("testId") ?? ""] = (reader.GetAttribute("testName") ?? "", reader.GetAttribute("outcome") ?? "");
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "UnitTest") testId = reader.GetAttribute("id") ?? "";
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "TestMethod" && testId is not null)
                    definitions[testId] = (reader.GetAttribute("className") ?? "", reader.GetAttribute("name") ?? "");
                else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "UnitTest") testId = null;
            }
            foreach (var (id, value) in outcomes)
            {
                if (!definitions.TryGetValue(id, out var definition)) throw new InvalidDataException($"{path}: result '{value.Name}' has no test definition");
                yield return new(definition.Class, definition.Method, value.Name, value.Outcome);
            }
        }
    }

    internal static bool Covers(string @case, string name)
        => @case.Length == 0 || Regex.IsMatch(name.Contains('(') ? name[name.IndexOf('(')..] : "", @"\b" + Regex.Escape(@case) + @"\b");

    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var split = Array.IndexOf(args, "--");
        if (split < 1 || split == args.Length - 1)
        { error.WriteLine("usage: SpecParity.cs <reasons.tsv> [...] -- <results.trx> [...]"); return 2; }
        try
        {
            var (controls, reasons) = ReadReasons(args[..split]);
            long control = 0, skipped = 0, ran = 0, infocarrier = 0;
            foreach (var result in ReadResults(args[(split + 1)..]))
            {
                if (controls.Contains(result.Class)) { control++; continue; }
                if (result.Outcome == "NotExecuted") { skipped++; continue; }
                ran++;
                if (reasons.GetValueOrDefault((result.Class, result.Method), []).Any(r => r.Side == "infocarrier" && Covers(r.Case, result.Name))) infocarrier++;
            }
            if (ran == 0) throw new InvalidDataException("no test case ran through InfoCarrier; there is nothing to measure.");
            var defects = reasons.Values.SelectMany(r => r).Select(r => r.Label)
                .Where(label => label.StartsWith("INFOCARRIER DEFECT ", StringComparison.Ordinal)).Select(label => label[19..])
                .Distinct(StringComparer.Ordinal).OrderBy(issue => int.Parse(issue.TrimStart('#'), CultureInfo.InvariantCulture)).ToArray();
            var hundredths = (ran - infocarrier) * 10000 / ran;
            var parity = $"{hundredths / 100}.{hundredths % 100:D2}";
            output.Write($"parity={parity}\nparity_ran={ran}\nparity_infocarrier={infocarrier}\nparity_skipped={skipped}\nparity_control={control}\ninfocarrier_defects={defects.Length}\n");
            error.WriteLine("| EF parity | Ran through InfoCarrier | InfoCarrier differs | Skipped | Wire-free control | Open InfoCarrier defects |");
            error.WriteLine("|--:|--:|--:|--:|--:|--|");
            error.WriteLine($"| {parity}% | {ran} | {infocarrier} | {skipped} | {control} | {(defects.Length == 0 ? "none" : string.Join(", ", defects))} |");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or XmlException or FormatException)
        { error.WriteLine($"spec-parity: {exception.Message}"); return 1; }
    }

    public static int Run(string[] args) => Run(args, Console.Out, Console.Error);
}
