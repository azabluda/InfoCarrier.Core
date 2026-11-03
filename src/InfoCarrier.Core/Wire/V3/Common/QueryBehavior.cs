// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Wire.V3.Common;

/// <summary>Protocol 3 tracking identities, independent of later framework versions.</summary>
public enum QueryTrackingBehavior
{
    /// <summary>Track returned entities.</summary>
    TrackAll = 0,
    /// <summary>Do not track or identity-resolve returned entities.</summary>
    NoTracking = 1,
    /// <summary>Identity-resolve returned entities without tracking them.</summary>
    NoTrackingWithIdentityResolution = 2,
}

/// <summary>Protocol 3 split-query identities.</summary>
public enum QuerySplittingBehavior
{
    /// <summary>Execute one query.</summary>
    SingleQuery = 0,
    /// <summary>Split included collections across queries.</summary>
    SplitQuery = 1,
}
