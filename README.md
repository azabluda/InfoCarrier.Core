# InfoCarrier.Core

A next-generation **Entity Framework Core database provider** that remotes LINQ queries and
change-tracking over a wire protocol — enabling true multi-tier applications with EF Core.

## Core Idea

```
┌──────────────────────┐         ┌──────────────────────┐
│   Client (Blazor,     │         │   Server (ASP.NET)   │
│   MAUI, Console...)  │ ◄─────► │                      │
│                       │  Wire   │   Real EF Core       │
│   DbContext with      │  Proto  │   DbContext against  │
│   InfoCarrier provider│         │   SQL Server,        │
│   (no real DB)        │         │   PostgreSQL, etc.   │
└──────────────────────┘         └──────────────────────┘
```

1. **Client** writes LINQ queries against a normal `DbContext` — but the provider serializes
   the expression tree instead of executing SQL.
2. **Wire protocol** transports serialized expressions + entity change-tracking state.
3. **Server** deserializes, executes against a real EF Core context, and returns results.
4. **Entity identity, fixup, and change-tracking** operate normally on the client —
   but the data source is remote.

## Why a Rewrite?

The [original InfoCarrier.Core v1](https://github.com/azabluda/InfoCarrier.Core) (EF Core 5,
Remote.Linq v6.2.3, Aqua v4.5.3) proved the concept works. But the serialization pipeline
(Remote.Linq + Aqua DynamicObject) caused deep issues:

- Castle.Core proxy serialization failures
- Expression tree PartialEval type mismatches
- DynamicObject mapping for owned/shared entity types
- GeoJSON Z/M coordinate loss
- Many-to-many SaveChanges fixup through the wire

InfoCarrier.Core is **a greenfield rewrite** targeting EF Core 10, with a fresh look at the
expression serialization strategy and lessons learned from v1.

## Status

**Implementation — query pipeline.** The vertical slice is green end-to-end: client capture →
serialize → transport → server rebind → EF execute → client materialization with identity
resolution. Phases A–E complete; spec-test fixture F1–F7 online.

The functional suite inherits Microsoft's `EFCore.Specification.Tests` (ADR-004) —
**413 inherited tests discovered, 141 passing** as of 2026-08-01. See
[`docs/implementation-plan.md`](docs/implementation-plan.md) for the live task list and
failure triage.

Not yet implemented: the client/server projection split (requirements §3), `SaveChanges`
(incl. many-to-many), and transactions. Build order:
[`docs/decisions.md`](docs/decisions.md) ADR-003.

### Documentation

| Doc | Contents |
|---|---|
| [`docs/decisions.md`](docs/decisions.md) | ADR log — LOCKED vs PROVISIONAL design decisions |
| [`docs/roadmap.md`](docs/roadmap.md) | Milestone plan M1–M8 + CI strategy |
| [`docs/implementation-plan.md`](docs/implementation-plan.md) | Current-milestone task detail |
| [`docs/architecture.md`](docs/architecture.md) | System architecture + test strategy + open questions |
| [`docs/expression-serialization.md`](docs/expression-serialization.md) | Serializer research & design direction + open questions |
| [`docs/wire-protocol.md`](docs/wire-protocol.md) | Client↔server contract + open questions |
| [`docs/research-infrastructure.md`](docs/research-infrastructure.md) | Subrepos + CodeGraph MCP setup |
| [`docs/infocarrier-core-requirements.md`](docs/infocarrier-core-requirements.md) | Authoritative requirements spec |
| [`docs/ci-cd.md`](docs/ci-cd.md) | CI/CD strategy |

## Repository Structure

```
InfoCarrier.Core/
├── README.md                 ← this file
├── .github/workflows/        ← CI/CD (placeholder)
├── docs/                     ← architecture decision records, design docs
├── subrepos/                 ← 3rd-party source for reference (not submodules, all git-ignored)
│   ├── efcore/               ← EF Core source (for test compliance)
│   ├── rlinq/                ← Remote.Linq source (if adopted)
│   ├── aqua/                 ← Aqua source (if adopted)
│   └── infocarrier-v1/       ← original InfoCarrier.Core v1 (non-authoritative, for inspiration)
└── samples/                  ← sample apps (design docs only for now)
```

## Key Dependencies

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET SDK | 10.0.x | Runtime |
| EF Core | 10.0.x | Database provider framework |
| Expression Serializer | in-tree | Greenfield — no Remote.Linq/Aqua dependency (ADR-001) |
| System.Text.Json | 10.0.x | Default wire serializer (source-generated, AOT-friendly) |
| xUnit.net | 2.9.x | Functional test suite |
| ASP.NET Core | 10.0.x | Server hosting (planned) |

## License

MIT — see [original license](../license.txt).
