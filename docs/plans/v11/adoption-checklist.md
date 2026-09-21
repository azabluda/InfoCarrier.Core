# Adoption checklist

What moves when EF Core 11 is adopted. **Issue
[#99](https://github.com/azabluda/InfoCarrier.Core/issues/99) lists most of it**, and was written on
2026-09-09. This file re-reads it against the repository on 2026-09-21 and adds what it misses. Read
each line against the repository again when the work starts.

## What #99 says that is no longer true

- **"Expect one large, deliberate re-baseline of `test/known-failures.txt`".** The ratchet and both
  of its files were deleted on 2026-09-15 (`b611193`). The suite is green and CI fails on any failing
  test, so every test EF 11 turns red ends as a fix, or as an override with a typed reason that
  `OverrideAudit` accepts ([`docs/test-policy.md`](../../test-policy.md)). There is nothing to
  re-baseline.
- **`Microsoft.EntityFrameworkCore` "at `[10.0.0,11.0.0)`"** is `[10.0.1,11.0.0)`, and the seven
  other EF packages are pinned at `10.0.1`, not `10.0.0`.
- **`subrepos/efcore` "at `release/10.0`, `v10.0.0`"** is at `v10.0.1`, the version of the
  specification packages.

## What #99 does not name

- **Tier D is the second dependency outside Microsoft, with the same schedule risk as Tier C.**
  `test/InfoCarrier.Core.DocumentStoreTests` runs `MongoDB.EntityFrameworkCore` `10.0.3`, which needs
  EF Core `>= 10.0.11`, so that project carries `VersionOverride="10.0.11"` on five packages. It
  cannot move until MongoDB ships a provider for EF Core 11. #99's question for Tier C (pin to EF 10,
  drop for a release, or block on upstream) is the same question here.
- **Every upstream reference is pinned by line, and they all move.** `UpstreamRepository.EfCore`
  names `dotnet/efcore` at `v10.0.1`, and `OverrideAudit` checks that each reference's lines declare
  the overriding test at that commit. Moving `subrepos/efcore` to the EF 11 tag fails the audit
  locally for each reference whose file changed. **CI cannot see this**, because a runner has no
  `subrepos/` and only counts what it could not check. The work is mechanical and large: a script
  that finds each test by name at the new tag and rewrites the line pair is worth writing before
  doing it by hand. `OverrideAuditTest`'s output lists every reference.
- **The same holds for `UpstreamRepository.MongoEfCore`**, pinned at `v10.0.3`, when Tier D moves.
- **The two enum members say their tags in their XML comments** (`OverrideReasonAttributes.cs`), and
  change with the pins.
- **`eng/ef-sql-compare.sh` needs no edit.** It reads the EF version from `Directory.Packages.props`
  and fetches that tag. The figures in `docs/test-policy.md` are dated records of EF 10 and stay as
  they are; the first run on EF 11 is a new record, not a correction.
- **`subrepos/firebird` is at `EFCore-13.0.0.0`**, the tag of the package Tier C runs, and moves
  with whatever Tier C decides.
