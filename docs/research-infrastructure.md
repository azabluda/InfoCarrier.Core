# Research Infrastructure

How the pre-implementation study environment is set up: reference clones (`subrepos/`) and
the CodeGraph MCP used to query them. Decisions: [ADR-005](decisions.md), [ADR-007](decisions.md).

---

## 1. Reference subrepos (`subrepos/` — git-ignored, not submodules)

Plain clones for **source-level reference only**; the whole tree is git-ignored with no
un-ignore exceptions (ADR-005). Nothing inside is committed.

| Folder | Repo | Role | Authority |
|---|---|---|---|
| `subrepos/efcore` | https://github.com/dotnet/efcore | EF Core 10 API surface, spec-test sources, query-pipeline internals | **AUTHORITATIVE** |
| `subrepos/rlinq` | https://github.com/6bee/Remote.Linq | Expression-tree serialization prior art | inspiration only |
| `subrepos/aqua` | https://github.com/6bee/aqua-core | `DynamicObjectMapper` / `TypeSystem` prior art | inspiration only |
| `subrepos/infocarrier-v1` | https://github.com/azabluda/InfoCarrier.Core | v1 lessons + test-fixture template | inspiration only |

> The three inspiration repos are **study material, not adoption signals** (ADR-001:
> greenfield serializer). `efcore` is the only authoritative reference.

**Pinned revisions.** Record the checked-out tag/SHA here once captured (kept *outside* the
ignored tree per ADR-005). `efcore` is pinned to the latest `release/10.x` tag for stable
research.

| Folder | Pinned revision |
|---|---|
| efcore | _(record tag/SHA)_ |
| rlinq | _(record tag/SHA)_ |
| aqua | _(record tag/SHA)_ |
| infocarrier-v1 | _(record tag/SHA)_ |

## 2. CodeGraph MCP (structural code queries)

Large reference codebases — especially `efcore` — are queried structurally with
[`@colbymchenry/codegraph`](https://www.npmjs.com/package/@colbymchenry/codegraph) (ADR-007).

**Hard rules**
- Run **exclusively via `npx`** — never `npm install -g`, never the interactive installer,
  never assume it is on `PATH`.
- Do **not** use the `codebase-memory` skill (a different product).
- **No file watcher / no daemon** on the static subrepos — one-shot indexing only.

**Indexing (one-shot per subrepo, already done):**
```powershell
npx -y @colbymchenry/codegraph init subrepos/efcore
npx -y @colbymchenry/codegraph init subrepos/rlinq
npx -y @colbymchenry/codegraph init subrepos/aqua
npx -y @colbymchenry/codegraph init subrepos/infocarrier-v1
```
Each produces a `.codegraph/codegraph.db` (SQLite graph) inside that subrepo.

**MCP wiring — `.vscode/mcp.json` (one server entry serves all projects):**
```json
{
  "servers": {
    "codegraph": {
      "command": "npx",
      "args": ["-y", "@colbymchenry/codegraph", "serve", "--mcp"],
      "env": { "CODEGRAPH_NO_DAEMON": "1" }
    }
  }
}
```
- The stdio entry is `codegraph serve --mcp` (the `serve` subcommand is deliberately hidden
  from `--help`; agents launch it). We substitute `npx -y @colbymchenry/codegraph` for the
  bare `codegraph` binary to honor the npx-only rule.
- **One entry covers every subrepo**: the server holds no per-project state; each tool call
  selects an index via the `projectPath` argument. Do not create one entry per subrepo.
- If the server reports "not initialized" after a reload (launch-cwd issue), add
  `"--path", "${workspaceFolder}"` to `args`.

**Usage:** `codegraph_explore` (default tool) with `projectPath` pointing at a subrepo.
Caveat: C# cross-file resolution is ~85% (DI/reflection ceiling) — CodeGraph complements
grep, it does not replace it.

## 3. Status checklist

- [x] Four subrepos cloned.
- [x] Four CodeGraph indexes built (`.codegraph/codegraph.db` present in each).
- [x] `.vscode/mcp.json` written (single npx-based server).
- [x] `.gitignore` excludes `subrepos/` and `.codegraph/`.
- [ ] CodeGraph MCP smoke test (run `codegraph_explore` per `projectPath`; deferred to a
      session where MCP tools are loaded).
- [ ] Record pinned revisions in the table above.
