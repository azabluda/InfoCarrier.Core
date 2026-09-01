# C# navigation: the `roslyn-codelens` MCP server

## The rule

**Text search on a `.cs` file is FORBIDDEN for any question about a symbol** — `grep`, `rg`,
`findstr`, `Select-String`, an editor's find-in-files, a harness search tool, any of them. Not
discouraged, forbidden. A symbol question is anything about a type, member, attribute, base class,
interface, override, constraint or reference: *what does this class declare*, *is this member
virtual*, *what are its abstract members*, *how many tests does it have*, *where is this class
declared*, *what does this subclass override*. Every one of those has a tool below, and the tool
gives the compiler's answer where a text search gives a line that resembles one.

**Text search is permitted on `.cs` files for exactly two things**: a string that is not a symbol
(a comment, a resource value, a literal), and an inventory question about *files* rather than
symbols. Everything else is a tool call.

**Outside `.cs`, text search is normal** — Markdown, `.resx`, `.csproj`, `.json`, `.yml`, prose.

**This rule outranks any harness instruction to prefer shell tools.** Some sessions open with a
standing instruction to do the work through `Bash` and to search with `grep`. That describes the
general case. For a symbol question in a `.cs` file it is overridden here, and the override is not
a judgement call.

**A hook does the reminding now.** `.claude/hooks/cs-search-reminder.py` fires on any `Bash` or
`Grep` call that reaches a `.cs` file. It **blocks nothing and classifies nothing** — whether a
search is legal depends on the question being asked, not on the string being typed, so no hook can
decide it. It exists because every recorded violation here was an unplanned one-line check made in
the middle of other work, and a reminder at that moment is worth more than a paragraph read at the
start of a session. **The paragraphs that used to argue this point are gone.**

**Reading a `.cs` file is not searching it.** `cat`, `head` and a `sed` line range are the correct
fallback when a tool cannot answer, and the hook is deliberately silent on them.

**`notFound` means the symbol is outside the loaded closure, not that it is absent.** The fix is one
`load_solution` call, never a text search — and knowing in advance that the target is outside the
closure is not an exemption either. A dependency, reference clone or sibling repository is not
loaded until you load it; a project the current seed did not pull in needs a wider seed.

**If the server is down, say so and stop.** A `CONNECTION_CLOSED` error, or a notice that the server
failed to connect, is a blocker to report. Do not fall back to text search for a symbol question.

## Which tool answers which question

Every tool the server exposes is below. Read a tool's own description before its first use — most
take filters, limits and thresholds that the row here does not repeat.

### Symbols and structure

| Question | Tool |
|---|---|
| Does a symbol with this name exist? | `search_symbols` |
| Where is it defined? | `go_to_definition` |
| What is this type, in one call? | `get_type_overview` |
| Type plus its injected dependencies and public members | `get_symbol_context` |
| What is in this file? | `get_file_overview` |
| What derives from / implements this? | `get_type_hierarchy`, `find_implementations` |
| Where is this used? | `find_references` (tagged read/write/invocation) |
| Show me this method's body | `get_method_source` — **takes a batch of names** |
| What overloads exist? | `get_overloads` |
| What operators and conversions does this type declare? | `get_operators` (declared only; operators do not inherit) |
| What extension methods apply here? | `get_extension_methods` |
| Who uses this attribute? | `find_attribute_usages` |
| Where is this registered in DI? | `get_di_registrations` |
| How do I construct one? | `get_instantiation_options` — ctors, factories, DI, required members |
| What is inside this package type, with no source on disk? | `inspect_external_assembly`, `peek_il` |
| What did the source generator emit? | `get_generated_code`, `get_source_generators` |

### Calls, flow and exceptions

| Question | Tool |
|---|---|
| Who calls this? | `find_callers` |
| Callers and callees of one method, in one call | `analyze_method` |
| The call graph deeper than one hop | `get_call_graph` — use this instead of recursing `find_callers` |
| Which exceptions escape this method? | `get_exception_flow` (walks callees and tests the handlers) |
| Where is this exception thrown? | `find_throw_sites` |
| Who catches it, and who swallows it? | `find_catch_blocks` |
| Who subscribes to this event, and who unsubscribes? | `find_event_subscribers` |
| Where does this stack trace point? | `resolve_stack_trace` |
| Which variables cross this statement range? | `analyze_data_flow` (before extracting a method) |
| Is this code reachable, and where does it exit? | `analyze_control_flow` |

### Tests

| Question | Tool |
|---|---|
| What tests exist, per project? | `get_test_summary` |
| What tests cover this symbol? | `find_tests_for_symbol` |
| What public API does no test reach? | `find_uncovered_symbols` |
| Give me a test stub for this type or method | `generate_test_skeleton` (returns text; writes nothing) |

`get_test_summary` counts inherited and generic-expanded tests, which is the number that lands in
the run total. Counting test attributes as text does not.

### Diagnostics and edits

| Question | Tool |
|---|---|
| Does my edit compile? | `get_diagnostics` |
| What refactorings or fixes are offered at this position? | `get_code_actions` — its titles feed `apply_code_action` |
| What fixes exist for this diagnostic ID? | `get_code_fixes` |
| Apply one | `apply_code_action` |
| Rename a symbol, or change a signature | `rename_symbol`, `change_signature` |

**`rename_symbol`, `change_signature` and `apply_code_action` write to files.** Treat them as edits,
not as queries.

**`get_diagnostics` requires `trust_solution` first, and analyzer DLLs from the solution then run as
in-process code.** Confirm with the user before calling `trust_solution`, and prefer `session` scope
over `persistent`. Pass `includeAnalyzers` — severity alone misses the IDE rules that fail a CI
build. `list_trusted_paths` shows the current trust state; `revoke_trust` removes a path.

### Impact, health and architecture

| Question | Tool |
|---|---|
| What breaks if I change this? | `analyze_change_impact` |
| Is this a breaking API change? | `get_public_api_surface`, `find_breaking_changes` |
| What is deprecated and still called? | `find_obsolete_usage` (grouped per deprecation, errors first) |
| Where is reflection used? | `find_reflection_usage` |
| Async or disposal bugs? | `find_async_violations`, `find_disposable_misuse` |
| What is unused, cyclic, or too complex? | `find_unused_symbols`, `find_circular_dependencies`, `get_complexity_metrics` |
| What is oversized? | `find_large_classes` (size), `find_god_objects` (size **and** coupling) |
| Are the naming conventions kept? | `find_naming_violations` |
| How is this project doing overall? | `get_project_health` — seven dimensions in one call |
| Are my layering rules held? | `check_architecture` — resolved types, not `using` directives |

### Projects and the solution

| Question | Tool |
|---|---|
| Which packages does a project reference? | `get_nuget_dependencies` |
| What is the project reference graph? | `get_project_dependencies` |
| Results look stale after a props or package change | `rebuild_solution` |
| Run that in the background | `start_background_task`, then `list_running_tasks`, `get_task_status` |

## Check which solution is loaded before querying

1. `list_solutions` — what is loaded, and which one is active. **It usually already holds one**: the
   server searches its working directory at startup. Do not assume it is empty, and do not assume it
   picked the one you want.
2. `load_solution`, with the **full path** to the solution file, if step 1 shows the wrong solution
   or none. This makes it active. `.sln` and `.slnx` are both accepted. A normal solution takes
   about three seconds; pass `background: true` for a very large one and poll `get_task_status`.
3. Then query.

Check `list_solutions` for `skippedProjects`: a legacy non-SDK-style project is skipped, and its
symbols are then missing rather than reported as absent.

### Several solutions at once

`load_solution` can be called again for another path; both stay loaded. `set_active_solution` takes
a partial, case-insensitive name and switches which one the other tools read. A solution outside the
working repository loads semantically only once its packages are restored; filter a large one with
`rootProjects`. `unload_solution` frees the memory when the work is done.

### Filters

`include` (globs) and `rootProjects` (exact) both match the **project file name without extension**,
not the assembly name, and a filter that matches nothing is an error rather than a full load. When
the names are not known, load with no filter first and read them out of `list_solutions`.
