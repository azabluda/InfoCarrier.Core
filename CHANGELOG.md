# Changelog

Each release is described on its own page, and this file points at them rather than restating them.
A second copy of release notes goes stale, and the copy people find first is the one that misleads.

| Version | Date | Notes |
|---|---|---|
| `10.0.0-preview.1` | 2026-08-18 | [10.0 release notes](https://azabluda.github.io/InfoCarrier.Core/release-notes/10.0/) |
| `3.1.1` | 2021-05-07 | [Releases](https://github.com/azabluda/InfoCarrier.Core/releases) |
| `3.1.0` | 2020-12-31 | [Releases](https://github.com/azabluda/InfoCarrier.Core/releases) |
| `1.0.0` | 2017-06-20 | [Releases](https://github.com/azabluda/InfoCarrier.Core/releases) |

`10.0` is a rewrite for Entity Framework Core 10 and is not compatible with `3.1.1`. Your
`DbContext` and your entity classes carry over; the wiring around them does not. See
[Upgrading from 3.1](https://azabluda.github.io/InfoCarrier.Core/getting-started/upgrading-from-3-1/).

Every version ever published is listed on
[nuget.org](https://www.nuget.org/packages/InfoCarrier.Core#versions-body-tab). Three versions
between `1.0.0` and `3.1.1` have no GitHub Release although they shipped: `2.1.4`, `2.2.7` and
`3.1.1`.
