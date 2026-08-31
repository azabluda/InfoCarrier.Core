# C# navigation: the `roslyn-codelens` MCP server

## The rule

**Grep on a `.cs` file is FORBIDDEN for any question about a symbol.** Not discouraged — forbidden.
A symbol question is anything about a type, member, attribute, base class, interface, override,
constraint or reference: *what does this class declare*, *is this member virtual*, *what are its
abstract members*, *how many tests does it have*, *where is this class declared*, *what does EF's
own provider override*. Every one of those has a tool below, and the tool gives the compiler's
answer where grep gives a line of text that resembles one.

**Grep is permitted on `.cs` files for exactly two things**: a string that is not a symbol
(a comment, a resource value, a literal), and an inventory question about *files* rather than
symbols. Everything else is a tool call.

**Outside `.cs`, grep is normal** — Markdown, `.resx`, `.csproj`, `.json`, `.yml`, plain prose.

### If the tool cannot answer, the fix is to load the code, not to grep

**The failure mode this rule exists to stop is not forgetting the tools — it is meeting a
`notFound` and treating grep as the fallback.** `notFound` almost always means the symbol is
outside the loaded closure, and the fix is one `load_solution` call:

- `subrepos/efcore` is **not loaded by default**, and its spec bases are the most common symbol
  questions in this repository. Load it (see *Several solutions at once* below) before reading a
  single EF base class. A session that greps `subrepos/efcore/test/**/*.cs` for `protected abstract`
  or `UseTransaction` has skipped this step.
- A project in *this* solution that the active seed did not pull in: re-load with a wider seed.

**If the server is down, say so and stop** — see *Setup* below. Do not silently fall back.

## Why it matters here

Before reaching for grep on a `.cs` file, ask whether the question is about a **symbol**; if it is,
a tool here answers it better. It also resolves into NuGet metadata assemblies that have no source
on disk, which grep cannot do at all.

There are **sixty-odd tools**, and the reflex is to use the same six. The ones below are the ones
that most often replace a worse method. Read the server's own catalogue when a question does not fit
one of these — there is probably a tool for it.

| Question | Tool, not grep |
|---|---|
| Who calls this? | `find_callers` |
| Where is this used? | `find_references` (tagged read/write/invocation) |
| What derives from / implements this? | `get_type_hierarchy`, `find_implementations` |
| What is this type, in one call? | `get_type_overview` |
| Show me this method's body | `get_method_source` — **takes a batch of names** |
| What overloads exist? | `get_overloads` |
| Where is this registered in DI? | `get_di_registrations` |
| How do I construct one? | `get_instantiation_options` — ctors, factories, DI, required members |
| Does a symbol with this name exist? | `search_symbols` |
| What is in this file? | `get_file_overview` |
| What extension methods apply here? | `get_extension_methods` |

**Adopting an EF spec base asks the same five symbol questions every time, and every one of them is
a tool call.** These are written out because they were got wrong by grep in the session that added
this section:

| Question about a spec base | Tool, not grep |
|---|---|
| What does it declare — abstract members, constraints, base? | `get_type_overview` |
| Is `UseTransaction` virtual, and what does it call? | `get_method_source` (**batch the names**) |
| How many tests does it have? | `get_test_summary`, or `find_attribute_usages` for `ConditionalFact`/`ConditionalTheory` |
| What does EF's own SQLite class override? | `get_type_overview` on that class, then `get_method_source` |
| Which of our classes derive from it? | `get_type_hierarchy`, `find_implementations` |

**`grep -c 'ConditionalFact\|ConditionalTheory'` is the tell.** It counts attribute *text*,
including inside comments and `#if` blocks, and it cannot see inherited or generic-expanded tests —
which is the number that actually lands in the suite total. `get_test_summary` can.

And these are the ones that get forgotten entirely, each replacing something slower:

| Question | Tool | Replaces |
|---|---|---|
| Does my edit compile? | `get_diagnostics` | a 60-second build (needs `trust_solution` — ask first) |
| What throws this exception / message? | `find_throw_sites` | grepping for a resource string |
| Who catches it, and who swallows it? | `find_catch_blocks` | reading call sites by hand |
| What tests cover this symbol? | `find_tests_for_symbol` | guessing from test names |
| What tests exist at all, per project? | `get_test_summary` | counting `[Fact]` by eye |
| What breaks if I change this? | `analyze_change_impact` | hoping the build finds it |
| Is this a breaking API change? | `get_public_api_surface`, `find_breaking_changes` | judgement |
| What is inside this NuGet type? | `inspect_external_assembly`, `peek_il` | a decompiler, or giving up |
| Where does this stack trace point? | `resolve_stack_trace` | reading mangled frames |
| What did the source generator emit? | `get_generated_code`, `get_source_generators` | digging in `obj/` |
| Is anything unused / cyclic / oversized? | `find_unused_symbols`, `find_circular_dependencies`, `find_god_objects` | — |
| Async or disposal bugs? | `find_async_violations`, `find_disposable_misuse` | — |

`rename_symbol`, `change_signature` and `apply_code_action` write files. Treat them as edits.

Grep stays right for exactly two things: text that is not a symbol (comments, prose, config,
Markdown, build files), and an inventory question about files rather than symbols (`ls` a directory,
does any file mention this string).

**A `.cs` file outside the loaded closure is not one of them.** Extend the load instead —
`load_solution` takes a `rootProjects` array, and a filtered load is seconds. The trap that keeps
recurring is subtler than forgetting the tools: the repository *is* loaded, but the seed chosen
earlier did not pull in the project this question is about, `get_method_source` says `notFound`, and
grep looks like the only option. It is not. Re-load with a seed that covers it.

Reaching for grep because it is familiar is the habit to break. It reads one file, so it answers
questions about inheritance wrongly rather than not at all — and `grep -B12 … | grep "public
virtual"` to find which method contains a call is `get_method_source` written the hard way.

## Setup

The server is registered in [`.mcp.json`](../.mcp.json), so there is nothing to install: Claude Code
offers it on your first session here and you approve it once. If you are editing `.mcp.json` itself,
every argument in it is load-bearing — a session that starts with the server reporting
`CONNECTION_CLOSED` is the sign one was dropped.

**If the server is down, stop and say so — do not fall back to grep for a symbol question.** A
`CONNECTION_CLOSED` error or a session notice that the server failed to connect is a blocker to
report, not to work around: grep reads one file, so it answers an inheritance or cross-project
reference question *wrongly*, and a confident wrong answer is worse than none. Grep stays right only
for the two cases it was always right for.

## Check which solution is loaded before querying.

1. `list_solutions` — what is loaded, and which one is active. **It usually already holds one**: the
   server searches its working directory at startup, so here it has normally loaded
   `InfoCarrier.Core.slnx` before the first call. Do not assume it is empty and do not assume it
   picked the one you want.
2. `load_solution`, with the **full path** to the `.sln` or `.slnx`, if step 1 shows the wrong
   solution or none. This makes it active. A normal solution takes about three seconds; pass
   `background: true` for a very large one and poll `get_task_status`.
3. Then query.

Loading is cheap and it accepts `.slnx` directly. Check `list_solutions` for `skippedProjects`: a
legacy non-SDK-style project is skipped and its symbols will simply be missing rather than reported
as absent.

### Several solutions at once

`load_solution` can be called again for another path; both stay loaded. `set_active_solution` takes
a partial, case-insensitive name and switches which one the other tools read. Use this when a
question crosses from this repository into `subrepos/efcore`, which loads semantically after one
`dotnet restore` — filter it with `rootProjects`. `unload_solution` frees the memory when the work
is done.

### Two things that need care

- **`get_diagnostics` requires `trust_solution` first, and analyzer DLLs from the solution then run
  as in-process code.** Confirm with the user before calling `trust_solution`, and prefer `session`
  scope over `persistent`. Pass `includeAnalyzers` — severity alone misses the IDE rules that fail
  the CI build.
- **`rename_symbol`, `change_signature` and `apply_code_action` write to files.** Treat them as
  edits, not as queries.

### Filters

`include` (globs) and `rootProjects` (exact) both match the **project file name without extension**,
not the assembly name, and a filter that matches nothing is an error rather than a full load. When
the names are not known, load with no filter first and read them out of `list_solutions`.
