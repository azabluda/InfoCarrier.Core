// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.WireContract;

[Flags]
internal enum WireDirection
{
    None = 0,
    Request = 1,
    Response = 2,
}

// Effective contracts are retained in versioned descriptions; comparison inputs use this model.
internal sealed record WireContract(
    int Version,
    int[] AllocatedVersions,
    Dictionary<string, TypeContract> Types,
    Dictionary<string, string[]> Frames,
    Dictionary<string, string> Inventory);

internal sealed record TypeContract(
    string Kind,
    string Encoding,
    WireDirection Direction,
    bool SkipUnknown,
    Dictionary<string, string> Settings,
    Dictionary<string, PropertyContract> Properties,
    Dictionary<string, string> Mappings,
    string[] Children);

internal sealed record PropertyContract(
    string Type,
    bool Required,
    bool Readable,
    bool Writable,
    Dictionary<string, string> Settings);

internal enum ChangeKind
{
    Compatible,
    Incompatible,
    Unclassified,
}

internal sealed record ContractChange(
    string Path,
    WireDirection Direction,
    ChangeKind Kind,
    string Previous,
    string Current,
    string Reason);

internal sealed record ComparisonResult(
    string Reference,
    int PreviousVersion,
    int CurrentVersion,
    IReadOnlyList<ContractChange> Changes,
    IReadOnlyList<string> AllocationErrors)
{
    public bool Passed => AllocationErrors.Count == 0
        && !Changes.Any(c => c.Kind == ChangeKind.Unclassified)
        && (CurrentVersion > PreviousVersion || !Changes.Any(c => c.Kind == ChangeKind.Incompatible));
}
