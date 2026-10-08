# Anonymous shape investigation and draft

Date: 2026-10-07. Subject: [issue #209](https://github.com/azabluda/InfoCarrier.Core/issues/209).

Historical investigation snapshot. The owner subsequently chose the bounded protocol. The
[implementation plan](anonymous-shape-implementation.md) replaces the three compiler draft files
with production emission and owned protocol tests; the measurements below describe the original
investigation, not the implemented protocol's final validation.

The owner approved uninterrupted investigation, a report, and a code draft for later review.
This is an experimental draft, not approval to change the production protocol or supersede ADR-010/011.

## Design and implementation plan

Goal: measure whether a separately generated anonymous type preserves backend behavior more closely
than the current tuple substitution, without changing shipped code or admitting new production types.

Architecture: a test-only compiler generates anonymous generic templates from validated ordered member
names. A test-only expression visitor substitutes those templates while recursively retyping dependent
expressions. The backend comparison runs the original expression, the current QuerySplitter's shipped
expressions plus its residual, and the generated expression over the same model and data. A separate
serializer experiment checks transport construction/member mappings and the existing allowlist boundary.

Technology: .NET 10, the repository's EF Core 10.0.1 SQLite and InMemory providers, and the C# compiler
already present in the specification project's dependencies. No added package and no production change.

Files:

- `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeDraft.cs`: generator and expression visitor.
- `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeTransportDraft.cs`: request-local
  shape metadata, JSON reconstruction, and experimental type admission.
- `test/InfoCarrier.Core.FunctionalTests/ProjectionSplit/AnonymousShapeInvestigationTest.cs`: semantic,
  backend, and serializer experiments with independently specified expected results.
- This file: scope, measured evidence, limitations, and recommended production design.

Review focus: malicious member names; nested generic signatures; null versus default structs;
cross-type equality; generated type identities and lifetime; provider-sensitive translation;
constructor/member order; empty and more-than-seven-member shapes; accidental allowlist expansion.

Tasks, executed inline in the existing checkout:

- [x] Write semantic tests against a rejecting generator stub; observe expected failures.
- [x] Implement compiler templates and recursive expression substitution; pass semantic tests.
- [x] Measure original, current splitter, and generated paths on SQLite and InMemory.
- [x] Verify JSON construction/member round trips with explicit test-only registration; verify refusal without it.
- [x] Read provider source, assess security, type identity, caching, deployment, and trimming.
- [x] Run relevant gates, review the draft, and complete the report. Leave all changes for owner review.

Assertions compare live providers rather than committed SQL captures. Diagnostic output belongs in
the test console. The report records dated findings, not permanent current suite counts.

## Recommendation

The hypothesis has useful evidence: a generated C# anonymous template, with a different runtime
identity, preserved the original provider's executed statements and refusal text in the tested cases.
The nine-member join also demonstrates a structural benefit: the current splitter executes two
unfiltered, full-entity reads and joins locally; both the original and generated paths execute one
database join projecting the requested identifier. Results match on the seeded data.

This does not justify replacing every tuple yet. Most measured tuple differences were aliases,
not changed plans or answers. Production reconstruction adds a new protocol vocabulary, generated
type admission, cache/lifetime concerns, and client result reconstruction. Keep the existing production
path while reviewing this draft. If the owner chooses implementation, start with anonymous join keys
and internal anonymous carriers, using a constrained emitter rather than shipping this compiler prototype.

The strongest cheaper alternative is to improve the existing join-key representation for keys wider
than seven members, with its own direct/wire differential tests. That may deliver this measured benefit
without a general shape protocol. It would not reproduce anonymous refusal diagnostics.

## What the draft actually does

The code remains entirely inside the functional test project. No production file, package graph,
allowlist, fixture attribution, override, or public signature changes. The original seven owned boundary
tests were run before changes. The separate draft tests use their own model and do not override a
specification test.

`AnonymousShapeDraft` compiles a small generic method whose body constructs an anonymous object.
Only validated property identifiers enter source; property types are generic arguments, never source
text. This yields actual compiler-generated, immutable reference types with C# equality, hashing, and
formatting. It is an oracle for a future hand-written emitter, not an acceptable production dependency.
The specification project already references the compiler transitively; no dependency was added.

`AnonymousShapeTransportDraft` translates the original expression with the existing expression-node
translator, carries a separate request-local shape table, round-trips both through JSON, and rebuilds
the server expression with the existing reverse translator and a test-only resolver. Generic argument
nodes retain property types; ordered shape members retain names; `NewNode` retains constructor
parameter signatures, argument order, and argument-to-member bindings. Empty shapes work too.
No marker or additional field is projected into query rows.

The shape table uses distinct identifiers for original generic type definitions, including definitions
from different assemblies. It never resolves an anonymous type by scanning for the client's compiler
name. The receiver generates its own definition and the existing query-root factory rebinds model roots.
Explicit registration of `Tile` and `Card` is identical for the splitter and generated paths.

The resolver's `admitGenerated` switch is a trusted test-call option. It is not a field received from the
client and not a proposed public product option. Every component type is checked before generation.
The draft asserts that omission of this opt-in rejects the shape and that a `Binder` component remains
refused. Generated type admission is confined to the draft resolver; production remains default-deny.

The backend experiment compares:

1. The original anonymous expression against the real provider.
2. The actual current `QuerySplitter`, executing each shipped expression on that provider and applying
   its actual residual to materialized queryables. This is not a newly invented tuple rewrite.
3. The original expression after JSON shape reconstruction, executing on the same provider and data.

Executed command text is collected by an interceptor, not `ToQueryString`. Results are checked against
literal fixture answers. Generated command text is compared byte-for-byte with original command text;
refusal messages are compared separately. Tuple aliases are reported rather than normalized into a
claim about plans. InMemory has no SQL, so SQL comparison there is marked not applicable.

This is a real JSON expression reconstruction and backend execution experiment. It is not a complete
InfoCarrier envelope/result-serialization integration: no HTTP request, normal client boundary admission,
generated-row response encoding, identity tracking, or original-client-type reassembly was added.

## Measurements, 2026-10-07

Reference source: EF Core `v10.0.1`, commit `a6217e3438ca1fb430079f2626056c1a11581927`, verified in
the reference checkout. Runtime reported .NET 10.0.12; installed software development kit: 10.0.401.

The matrix contains 19 query cases, each executed through all three paths on both SQLite and InMemory.
All accepted results matched their literal expectations. Generated SQLite statement sequences matched
the original sequences exactly. Generated refusal diagnostics matched the original diagnostics exactly.

| Case | Current splitter relative to original on SQLite | Generated relative to original |
|---|---|---|
| Flat projection, member filter, nested anonymous object | Same command text | Same commands and results |
| Distinct over an anonymous projection | Column aliases and references differ | Same commands and results |
| Anonymous grouping with an aggregate | Projection aliases differ | Same commands and results |
| Two-member join, nullable composite join matching | Projection aliases differ; nullable matching retained | Same commands and results |
| Navigation through an anonymous carrier | Projection aliases differ; database navigation retained | Same commands and results |
| Conditional anonymous/null projection | Projection aliases differ; absence retained | Same commands and results |
| Nested child collection, empty projection | Same command text | Same commands and results |
| Nine-member final projection | Tuple aliases, including nested tuple slots | Same commands and results |
| Nine-member join key | Two full-entity reads, followed by local join/order | One original database join and ordering |
| Registered constructor nested in anonymous projection, followed by Distinct | Same command text | Same commands and results |
| Group First followed by ordering on the projected row | Same translation refusal, tuple-shaped diagnostic | Same refusal and diagnostic |
| Group First with non-null keys and a root limit | Aliases differ; grouping and limit retained | Same commands and results |
| Group First selecting the null-key group | Same SQLite runtime exception after one command | Same command and runtime exception |
| Anonymous object equality in the final projection | Same command text and Boolean results | Same commands and results |
| Projection over the explicitly registered converted list | Same refusal, tuple-shaped diagnostic, no command | Same refusal and original diagnostic, no command |

The null-key Group First case is a provider-specific outcome, not a new InfoCarrier failure: SQLite
executes one command and throws `InvalidOperationException("Sequence contains no elements.")`;
InMemory returns the null-key group's row. All three paths preserve their provider's outcome.
It is recorded here; no upstream issue was filed and no product workaround was attempted.

Separate root-terminal tests cover `FirstOrDefault` and `SingleOrDefault`, each with an existing row
and with no row, on both providers. All three paths return the expected object or null. SQLite executes
one limited command per call. The generated command is identical to the original; tuple column aliases
differ. This tests an absent reference row rather than confusing absence with a default struct value.

Semantic and transport checks also cover:

- Read-only properties, property order and types, null members, equal versus unequal values,
  equal-value hash consistency, C# formatting, and reference equality of separately constructed objects.
- Empty and nested shapes, nine members without a tuple `Rest`, escaped identifiers and Unicode.
- Nested member construction through JSON, with a different server type identity.
- Rejection of duplicate/invalid identifiers, source injection, inconsistent arity, `void`, by-reference
  member types, more than 32 members, and names longer than 128 characters.
- Different original assemblies with matching shapes. The first cache implementation merged these
  identities, and the dedicated test failed. The revised cache preserves their separation. A separate
  JSON expression test preserves unequal objects from distinct original assemblies and equality for
  two constructions of the same original type.
- Generated compiler-name collisions. Two different one-member shapes have the same ordinary
  `TypeNode` text because each compiler assembly starts its anonymous numbering at zero. A Boolean
  marker or original compiler name is therefore insufficient even when property types are identical.

One local timing sample measured a cold two-member template at 143.905 ms and 100 warm lookups at
0.428 ms. These are illustrative prototype costs, not benchmarks or pass/fail thresholds. Compilation
cost and per-request identity churn rule out deploying this implementation directly.

No captured statement baseline is committed. Detailed commands, results, and diagnostic messages are
emitted through xUnit's console output. Ignored `artifacts/` logs are only local command transcripts.

## Why the provider recognizes the replacement

Read through roslyn-codelens at the pinned EF commit:

- `SharedTypeExtensions.IsAnonymousType` recognizes the `<>` prefix, compiler-generated attribute,
  and `AnonymousType` name. The compiler template satisfies all three; shape alone would not.
- `ExpressionPrinter.VisitNew` uses that recognition to print anonymous braces instead of a tuple
  constructor and prints `NewExpression.Members` names. This explains the converter diagnostic result.
- `ReplacingExpressionVisitor.VisitMember` substitutes a constructed member only when that member
  occurs in `NewExpression.Members`. Preserving constructor-to-member bindings remains necessary.
- `RelationalQueryableMethodTranslatingExpressionVisitor.CreateJoinPredicate` decomposes a nonempty
  `NewExpression` into argument comparisons. This supports the measured wide-key improvement without
  proving every provider handles every generated type.
- `RelationalSqlTranslatingExpressionVisitor.VisitNew` does not generally translate arbitrary row-dependent
  object construction. Recognizing an anonymous type does not make an unsupported scalar collection query
  translatable, which is why the converter remains refused.

## Production design questions and constraints

### Original assembly resolution

Loading the caller's assembly would preserve exact identity only if the server used the same type
definition. It changes the shared-assembly deployment contract and accepts client code rather than
data. Current resolution uses already-loaded assemblies and an allowlist; it does not load an assembly
to satisfy a payload. Preserve that restriction. An explicitly shared, registered type is already the
supported answer when an application needs exact type identity or custom behavior.

### Type identity and semantics

Matching names, order, and member types do not preserve `GetType`, `typeof`, casts to the original type,
assembly-qualified names, comparison with original instances, or user methods bound to that original
type. C# anonymous classes have structural `Equals` but reference `==`; a substitute must preserve both.
Hashing must stay consistent within each generated type. The prototype uses the C# compiler rather than
assuming a record or a public mutable class has equivalent metadata or behavior.

Use separate original-definition identifiers even for identical shapes from distinct assemblies. The
server need not execute or load the original assembly to maintain that distinction. The identifiers are
data keys, not assembly-resolution instructions. A production shape format should record constructor
mapping explicitly or restrict itself to the validated canonical C# anonymous constructor layout.

### Security and resource limits

The reasons for ADR-008/010 still hold: unknown names must not become arbitrary runtime constructors,
methods, reflection entry points, or assembly loads. A server-owned generator emitting only immutable
data properties and fixed equality/hash/formatting methods could address that constraint, but admitting
all compiler-generated types would not. Keep `Binder`, reflection invocation types, `Activator`,
`Assembly`, and `AppDomain` refused. Resolve every nested component through the same admitted-type rules.

A production implementation needs whole-payload byte/depth limits, shape/member/name totals, cycle
rejection, validated identifiers and constructor mappings, and a budget for generated code across tenants.
The draft has local 64-shape and 32-member bounds, but it is not a hostile-payload security review.
Returning an exception after allocating many templates is not an adequate resource policy.

### Cache and generated-type lifetime

Per-request generation gives new runtime type identities and can defeat EF's compiled-query cache.
Process-wide canonical shape caching improves reuse but can grow without bound under hostile traffic.
Cache keys must include member names, order, types, nesting, semantics, and original-definition identity
partitioning. Names and generic arity alone collide. Never cache an admission decision across executions.

The prototype uses collectible assembly load contexts and clears its local references on disposal.
This does not prove unloading: EF query caches, compiled delegates, materializer caches, and JSON
metadata can retain generated types. No bounded-memory or unloading guarantee is claimed.

### Deployment and trimming

The prototype depends on compiler assemblies and runtime reference-file discovery. It was run as an
ordinary Windows test process, not trimmed, bundled, browser-hosted, or Native AOT. No such compatibility
claim follows from these tests. The product already excludes Native AOT, but trimming is supported and
remains a requirement for any replacement. A fixed Reflection.Emit implementation avoids a Roslyn product
dependency but still needs dynamic-code annotations, preserved metadata, trim measurement, and tests
in the server's actual deployment environments. Client shape capture must not introduce server emission
into a browser client or make that client depend on compiler packages.

### Locked decisions and the eventual integration footprint

No locked decision was amended in this draft. Production adoption would need a dated amendment to
ADR-010/011: clients would still compute and enforce the boundary and still reassemble client-only
results, but a constrained anonymous-data descriptor would become a new server-known representation.
ADR-008's default-deny bound, ADR-001's dependency restriction, and ADR-014's prohibition on committed
capture baselines remain binding. Investigating a descriptor does not approve changing those decisions.

Minimum integration work, before any proposed merge:

1. Version and bound a shape table in the expression request. Coordinate old-peer refusal/fallback.
2. Add shared shape-aware boundary knowledge without inferring registrations for private helpers,
   converted elements, custom constructors, or application types.
3. Implement the constrained server generator and execution-local admission, with adversarial tests.
4. Preserve stable type identities within the defined cache/lifetime policy.
5. Keep final results type-agnostic on the wire; reconstruct the caller's original types, nested
   collections, missing rows, entities, and tracking identity on the client.
6. Compare complete wire/envelope paths and additional providers, run the whole suite, transport tests,
   strict Release build, trim gate, and package validation if public signatures change.

## Scope limits

The matrix uses SQLite and InMemory. Firebird, MongoDB, SQL Server, inheritance mappings, compiled
queries with captured anonymous values, non-C# anonymous type layouts, and custom tuple projections
were not run through the generated path. Their support cannot be inferred. The standalone expression
visitor intentionally refuses non-null captured anonymous constants. The transport draft does not
implement generated response-row mapping or client reassembly and is not ready for production.

## Validation and review

All results below are local measurements on 2026-10-07, not promises about other environments.

- Before draft changes, the seven existing owned boundary tests passed: Passed 7, Failed 0, Total 7.
- Final focused Release run, filtered to `AnonymousShapeInvestigationTest`: Passed 15, Total 15;
  `Test Run Successful.` The log is `artifacts/anonymous-shape-final.log`.
- `eng/measure.sh anonymous-shape-draft`: FAILING 0, TOTAL 30074. The functional project reported
  Passed 29606, Skipped 234, Total 29840. The document-store project reported Passed 234, Total 234.
  Both project runs reported `Test Run Successful.` Local transcripts are under `artifacts/measure/`.
- Strict `CI=true` Release solution rebuild succeeded with 0 errors and the five known Razor trim
  warnings. After the final transport-identity test, the strict Release solution build succeeded with
  0 warnings and 0 errors. Its transcript is `artifacts/anonymous-shape-release-build.log`.
- File hygiene passed for all five changed files. Document-link validation found 0 broken links in
  this report and the rolling implementation plan.
- An independent read-only code review found no critical or important defects. Its two minor findings
  were addressed: add a transport-specific cross-assembly equality test and list the transport draft.
  The final focused run and whole-suite run include the added test.

The first sandboxed whole-suite command failed before building because Git Bash could not create its
home path. The same authorized command completed under elevated execution. This was an environment
permission failure, not a test failure. No source workaround was made.

No commit, branch, pull request, issue update, package publication, or production change was made.
The draft remains uncommitted on `main` for the owner's review.

## Ranked next steps

1. Review this report and the three test-only draft files. The measured benefit is the nine-member
   join; the broader proposal remains blocked on deliberate protocol, identity, and resource choices.
2. Choose the narrower wide-key improvement or a bounded generated-shape protocol. If choosing the
   latter, approve a dated ADR-010/011 amendment and an implementation plan covering the integration
   footprint above before changing production behavior.
3. After that choice, implement and measure complete request/response paths on the selected providers.
   The compiler oracle is evidence for that work, not the production implementation to merge.
