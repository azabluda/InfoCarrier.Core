# Contributing

## Build and test

```bash
dotnet build InfoCarrier.Core.slnx

dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj
```

The first project is Microsoft's `EFCore.Specification.Tests`, inherited wholesale: the same suite
EF Core's own SQL Server, SQLite and InMemory providers run. The second drives a real HTTP hop
against an ASP.NET Core server.

Both are green. So is `test/InfoCarrier.Core.DocumentStoreTests`, which runs a few of the same
specification bases against an embedded MongoDB.

**Never override a specification test without saying why.** An override carries an attribute that
says what the store does and where that is shown: EF Core's own test at the release commit, a
tracker issue, or the decision in `docs/decisions.md`. `OverrideAuditTest` fails the run on an
override without one. A skip is allowed only where EF's own suite skips the same test.
[`docs/test-policy.md`](docs/test-policy.md) has the rules.

## Warnings are errors in CI, and only in CI

Locally a warning stays a warning, so half-finished work still builds. To see what the server sees:

```bash
CI=true dotnet build InfoCarrier.Core.slnx --configuration Release
```

`--configuration Release` is not optional. The Blazor sample turns the trim analyzer on in Release
only, so a Debug build cannot produce the diagnostic that fails CI.

Read [`docs/build-warnings.md`](docs/build-warnings.md) before adding a `NoWarn`.

## Which gate to run

| Your change touches | Run |
|---|---|
| `src/` | `eng/measure.sh` and `eng/trim-ratchet.sh` |
| `test/` only | `eng/measure.sh` |
| `docs/`, `website/` or `eng/` text | neither |

`eng/measure.sh <label>` runs the specification suite and prints the count, the tests fixed and
broken by name, and a diff of the failure *reasons*. Read the reasons. A count that did not move
cannot tell "fixed four, broke four" apart from "changed nothing".

`eng/trim-ratchet.sh` publishes the Blazor sample trimmed and compares its IL warnings against
[`eng/trim-baseline.txt`](eng/trim-baseline.txt). It fails when the count goes up. The count does
not have to be zero.

In CI the gate is the test run's own result, and `eng/suite-summary.sh` only collects the numbers.

## Documentation site

```bash
eng/docs-serve.sh            # live reload; mkdocs prints the URL
eng/docs-serve.sh --build    # one-shot strict build, what CI runs
```

Either creates the virtual environment from `website/requirements.txt` if it is missing.
`mkdocs build --strict` is the gate: a broken internal link, or a page missing from the nav, fails
the build.

## Commits

One logical change per commit. Say what changed and why it was worth changing. The diff shows how.

## The documents this repository is developed against

| Doc | Contents |
|---|---|
| [`website/docs/limitations.md`](website/docs/limitations.md) | Every known limitation, with an example. Published to the site; the only copy |
| [`docs/security-review.md`](docs/security-review.md) | The deserialization path, its bound, and what is accepted |
| [`docs/architecture.md`](docs/architecture.md) | Components, test strategy, open questions |
| [`docs/decisions.md`](docs/decisions.md) | ADR log: the decisions and why |
| [`docs/wire-protocol.md`](docs/wire-protocol.md) | Client and server contract |
| [`docs/expression-serialization.md`](docs/expression-serialization.md) | How a LINQ tree becomes bytes |
| [`docs/projection-split.md`](docs/projection-split.md) | What runs on the server, what runs on the client |
| [`docs/plans/v10/roadmap.md`](docs/plans/v10/roadmap.md) | Milestones and CI strategy |
| [`docs/build-warnings.md`](docs/build-warnings.md) | Which warnings are fatal, which are suppressed, where, and why |
| [`docs/versioning.md`](docs/versioning.md) | How a version is decided, and how to cut a release |
