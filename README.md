# InfoCarrier.Core v2

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

## Why v2?

The [original InfoCarrier.Core v1](https://github.com/azabluda/InfoCarrier.Core) (EF Core 5,
Remote.Linq v6.2.3, Aqua v4.5.3) proved the concept works. But the serialization pipeline
(Remote.Linq + Aqua DynamicObject) caused deep issues:

- Castle.Core proxy serialization failures
- Expression tree PartialEval type mismatches
- DynamicObject mapping for owned/shared entity types
- GeoJSON Z/M coordinate loss
- Many-to-many SaveChanges fixup through the wire

**v2 is a greenfield rewrite** targeting EF Core 10, with a fresh look at the expression
serialization strategy and lessons learned from v1.

## Status

**Phase 0 — Planning.** See [`MEGA_PROMPT.md`](MEGA_PROMPT.md) for the comprehensive
implementation plan. No code yet.

## Repository Structure

```
InfoCarrier.Core-v2/
├── README.md                 ← this file
├── MEGA_PROMPT.md            ← ultra-comprehensive implementation prompt
├── .github/workflows/        ← CI/CD (placeholder)
├── docs/                     ← architecture decision records, design docs
├── subrepos/                 ← 3rd-party source for reference (not submodules, all git-ignored)
│   ├── efcore/               ← EF Core source (for test compliance)
│   ├── rlinq/                ← Remote.Linq source (if adopted)
│   ├── aqua/                 ← Aqua source (if adopted)
│   └── infocarrier-v1/       ← original InfoCarrier.Core v1 (non-authoritative, for inspiration)
└── samples/                  ← sample apps (design docs only for now)
```

## Key Dependencies (Planned)

| Dependency | Version | Purpose |
|------------|---------|---------|
| .NET SDK | 10.0.x | Runtime |
| EF Core | 10.0.x | Database provider framework |
| Expression Serializer | TBD | LINQ expression tree serialization (options: Remote.Linq, custom, gRPC) |
| xUnit.net | latest | Functional test suite |
| ASP.NET Core | 10.0.x | Server hosting |

## License

MIT — see [original license](../license.txt).
