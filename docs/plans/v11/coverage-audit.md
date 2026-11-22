# Product coverage audit for version 11

Date: 2026-11-22. Work: [issue #288](https://github.com/azabluda/InfoCarrier.Core/issues/288).

## Evidence and limits

The complete starting measurement is [Coverage 45007344393](https://github.com/azabluda/InfoCarrier.Core/actions/runs/45007344393),
for source revision `76cf7fe16007b2e2ce4eb89fc6b5423ffc6cb1dc`. That revision was the
checkout's HEAD when this investigation started. Both coverage matrix jobs and the report job
succeeded. The workflow runs the functional project, including InMemory, SQLite and Firebird,
and the Cosmos, transport and engineering projects. The disabled MongoDB project is not an
active tier. This audit does not claim Windows-only SpatiaLite execution from this Linux run.

Downloaded artifacts: `coverage-raw-functional`, `coverage-raw-cosmos`, and `coverage-report`.
The local working copies are under `artifacts/coverage-audit-v11/`; they are not committed.
The Cosmos artifact contains three collector reports because its job runs three projects.
Its empty engineering report is expected: engineering tests do not load the product assemblies.
The other two reports contain product execution. Their directory identifiers, rather than a
guessed project assignment, identify them below.

| Report | Covered / coverable lines | Covered / reported branches |
|---|---:|---:|
| Functional, `482ff9c1` | 8,359 / 10,224 | 5,428 / 7,113 |
| Cosmos-job collector, `03c273ad` | 7,548 / 10,449 | 3,823 / 7,197 |
| Cosmos-job collector, `8909348b` | 7,184 / 10,449 | 4,492 / 7,197 |
| Engineering, `ff43520a` | 0 / 0 | 0 / 0 |
| Merged report | 9,817 / 10,449 | 4,829 / 5,292 |

The union of raw records, keyed by assembly, source filename and line number, has 10,449
coverable lines and 9,817 covered lines. Comparing its hit/non-hit outcomes with the merged
report produces zero differences. A covered line requires a positive hit in at least one
collector. Hit counts are not summed into a claim about test frequency.

ReportGenerator reports three assemblies, 217 classes, 166 files, and 632 uncovered lines.
The assemblies are exactly `InfoCarrier.Core`, `InfoCarrier.Core.Abstractions`, and
`InfoCarrier.Core.AspNetCore`, matching `eng/coverage.runsettings`. There are no test paths,
`obj` paths, or `.g.cs` source entries in the merged report. Async state machines and nested
classes still contribute to their original product source files. They must not be mistaken
for unwanted generated-source files and excluded wholesale.

Branch data is a separate limitation. Merging reduces the denominator below either nonempty
Cosmos-job collector and even reduces the covered numerator below the functional collector.
Therefore, the merged 91.2% branch figure does not describe a reliable union of branch outcomes.
Octocov also does not preserve Cobertura branch detail; see [the review procedure](../../coverage-review.md).
Use raw condition records and source controls to investigate individual decisions. Do not
interpret a changed aggregate branch rate as a regression or improvement. No threshold or
new coverage exclusion is introduced by this audit.

Roslyn CodeLens loaded all fourteen solution projects. It reported newer compiler versions
for several framework analyzers and source generators. Source-defined references remain useful,
but generated entry-point absence is not established by those queries. Generated serializer
metadata is corroborated by the actual serialization tests and collector execution instead.

## Decisions carried forward from version 10

The [earlier audit](../v10/coverage-audit.md) is historical evidence, not today's classification.

| Earlier finding | Version 11 verification | Decision |
|---|---|---|
| Two query-pipeline factory compatibility names | Neither source file remains in tracked production files. The complete loaded solution contains no implementation to audit. | Removal completed. Do not recreate factories or throw-only tests. |
| `InfoCarrierDatabaseCreator` returns fictitious success | The class is gone. `InfoCarrierRelationalFacadeDependencies.DatabaseCreator` now throws; ordinary store setup uses the backend context. | The obsolete no-op service is not a test backlog. Preserve the explicit client schema boundary. |
| `InfoCarrierDbContextOptionsBuilder` compatibility methods | The old builder is absent. `InfoCarrierDbContextOptionsBuilderExtensions` has 45/45 lines; current role/options registration uses the new configuration objects. | Do not restore obsolete methods to satisfy a historical coverage target. |
| Relational convention builder's structural adapter | The outer builder has 13/18 lines. `NoUpdateSqlGenerator` remains the only product type with `ExcludeFromCodeCoverage`. Remaining uncovered lines construct its boundary error. | Keep the adapter and the narrow exclusion. No direct throw-only tests. |
| Database-facade foreign-transaction guard | `InfoCarrierDatabaseFacadeExtensions` has 10/10 lines. `TransactionRemotingTest` now explicitly rejects a foreign transaction. | Earlier missing guard coverage has been addressed. No further wording test. |
| Superseded query-split logging overloads | The active `QuerySplit` takes a residual expression. The old count-only overloads and definition are absent. Remaining logging misses concern different behavior. | Removal completed; classify current misses separately below. |
| `OnSaveChanges` and blocking transaction wrappers | `OnSaveChanges` is absent. Current synchronous members call synchronous client methods, not asynchronous methods followed by blocking waits. | The old design objection no longer applies. Current manager/facade entry points need behavioral coverage. |
| Listener-enabled operation tags | `InfoCarrierMetrics.cs` has 65/65 lines. `Every_operation_has_its_stable_metric_tag` exercises every operation through the client and listener. | Supported metric-tag coverage exists. Do not add private switch-arm tests. |

The synchronous conclusion follows the current interfaces and the version 11
[upgrade review](documentation-structure-proposal.md), not the version 10 audit's proposed
async-only remedy. `IInfoCarrierClient`, `IInfoCarrierTransport`, and `IInfoCarrierServer`
now require synchronous operations. Removing manager methods would violate framework contracts.

## File-by-file decisions

Counts below belong only to the starting revision and include nested classes. Paths are relative
to `src/InfoCarrier.Core/`, except the explicitly named ASP.NET Core file. Line numbers refer
to that revision. Every file with uncovered lines is included. Fully line-covered files are
not evidence of complete branch coverage; the branch-only candidates follow the table.

Classifications:

- **Dead**: corroborated obsolete implementation, with its callers and removal effects checked.
- **Supported**: active behavior whose uncovered scenario belongs to a supported contract.
- **Boundary**: deliberate refusal or an adapter member outside supported client execution.
- **Unresolved**: insufficient evidence to claim dead code or a supported missing scenario.

Removing a **Supported** row would affect the behavior described in its evidence column.
The concrete action is to retain it and add or evaluate the named scenario, rather than delete
unexecuted lines. **Unresolved** rows also stay in place until the stated investigation finishes.
These are bounded follow-up decisions under this issue, not new defect reports.

| File | Lines | Classification | Source/runtime evidence and why existing tests miss it | Removal impact and action |
|---|---:|---|---|---|
| `Query/StreamingResidualPlan.cs` | 40/111 | Supported concat gap / unresolved operator arms | `SelectorStep` executes. The existing concat regression ships one query. A new client-constructed concat produces two projected slots that the old plan cannot stream. Paging/distinct probes move those operations to the server; local filtering is refused. | Fixed projected-slot streaming with a regression observed failing first. Keep other arms until reachability is established beyond these examples. See the follow-up evidence below. |
| `Query/QuerySplitter.cs` | 786/845 | Supported / unresolved branches | Query preparation executes broadly. Misses include paging reassembly at 604–640, correlated-fragment refusal at 1074–1078, navigation traversal, and alternative diagnostic construction. Successful inherited queries do not exercise every rejected or structurally rewritten tree. | Retain splitting and refusal guards. Review paging/reassembly with streaming reachability; use public query outcomes, not private visitor tests. |
| `ServerSaveChangesExecutor.cs` | 394/448 | Supported / unresolved branches | Replay executes across stores. Misses include incomplete-document refusal at 947–984, property-bag coercion at 1535–1601, and generated-sentinel restoration at 1655–1665. Existing graphs do not cover each metadata/storage combination. | Removing replay or repair can lose stored fields or generated values. Add a property-bag collection round trip and a mixed-key conflict/repair control before narrowing code. Follow-up 3. |
| `InfoCarrierDatabase.cs` | 267/297 | Supported / Boundary | Framework-dispatched compilation and saves execute. `CompileQueryExpression` explicitly refuses precompiled queries. Conflict entry matching at 792–832 is unexecuted: ordinary transport faults use correlation information instead of live server entries. | Keep the public refusal. Retain live-entry translation until an in-process conflict control establishes its contract; do not infer obsolescence from zero direct calls. Follow-up 3. |
| `QueryExecutor.cs` | 491/518 | Supported / unresolved branches | Streaming residual and concat execution accounts for many misses; other misses report cancellation or projection/materialization failures. Ordinary successful stream tests do not inject each failure phase. | Keep error reporting and query execution. Resolve with the residual plan and buffered-materialization controls, follow-ups 1–2. |
| `../InfoCarrier.Core.AspNetCore/InfoCarrierEndpointExtensions.cs` | 189/215 | Supported | `BufferedFrames`, 314–349, is entirely absent from the baseline. The registered server and existing `RecordingServer` both implement streaming. A valid replacement implementing only `IInfoCarrierServer` selects the uncovered fallback. | Removing it breaks buffered-only replacements. Added endpoint-selection tests verify rows and remote refusals for default, name, handle and type selection. Forwarded logs and cancellation remain follow-up 2. |
| `Query/ProjectionRewriter.cs` | 723/745 | Supported / unresolved branches | Reassembly runs broadly; uncovered cases include terminal predicate rewrites at 986–987 and `SequenceSlotFinder`'s alternate member chain. Not every carrier shape appears in current application projections. | Keep the final-projection contract. Test supported application shapes after confirming the rewrite is reachable; coordinate with follow-up 1. |
| `ClientResultMaterializer.cs` | 280/301 | Supported | Stream log/fault and failure callbacks at 149–187, deferred join attachment, and writable complex/navigation-property alternatives are unexecuted. Ordinary streams and selected graph shapes do not reach these combinations. | Removal loses forwarded diagnostics, failure classification or graph state. Add fallback log/fault and graph controls, follow-ups 2–3. |
| `Wire/V3/Expressions/TypeNode.cs` | 6/25 | Dead helper; retained public formatting | The historical internal `CacheIdentity` has zero source references. `Protocol3Mapping` converts descriptors to live types; current cache callers all target `Expressions.TypeNode.CacheIdentity`, with thirteen references as a positive control. Remaining misses are `ToString` variants. | Remove only the unused internal helper. Preserve historical serialized properties and public `ToString`; removal changes neither contract. No formatting-only coverage test. |
| `HttpInfoCarrierTransport.cs` | 102/118 | Supported | Missing synchronous non-success/invalid-envelope handling at 57–70 and stream acquisition cleanup at 120–123. Async HTTP failures do not exercise the synchronous adapter. | Keep status/body diagnostics and response disposal. Add symmetric synchronous HTTP refusal and malformed-success controls, follow-up 2. |
| `InfoCarrierServerRegistrationCatalog.cs` | 67/81 | Supported | `DefaultInfoCarrierServer` forwards all operations and adapts buffered results at 114–119. Endpoint mapping deliberately unwraps this facade, so HTTP tests cannot cover its custom-hosting fallback or synchronous aliases. | Removing facade methods breaks service consumers. Add custom-hosting parity tests; do not duplicate HTTP tests hoping to cover unwrapped methods. Follow-up 2. |
| `InfoCarrierEnvelopeServer.cs` | 171/185 | Supported / Boundary | Synchronous savepoint dispatch at 99–103 and its payload helper at 157–159 are unexecuted; normal async transport tests use another dispatch method. Unknown operation and version refusals are separate guards. | Keep dispatch and protocol refusals. Exercise synchronous savepoint effects through a custom host, follow-up 2. |
| `Expressions/NodeToExpressionTranslator.cs` | 205/218 | Supported / Boundary | Contextless function receiver refusal at 95–98 and method/constructor resolution fallbacks at 259–364 are not normal admitted query execution. Current mapped-function tests supply a server context. | Keep admission and resolution checks. Add a historical mapped-function round trip; avoid synthetic private resolver tests. Follow-up 4. |
| `Expressions/DynamicValueMapper.cs` | 414/427 | Supported / Boundary | Missing plugin-contract failure at 404–406, construction fallbacks and missing metadata paths. Existing plugins return valid nodes and ordinary models have the required members. | Retain plugin validation and reconstruction. A malformed plugin output control is meaningful; do not remove fallback construction without model evidence. Follow-up 3. |
| `Wire/V3/Query/QueryStreamFrameCodec.cs` | 88/100 | Supported | Historical reads/writes run, but malformed frame order, missing terminal and unknown tags do not. Current-protocol codec tests do not execute the historical codec. | Removal weakens historical stream validation. Add historical malformed-stream and log-frame acceptance controls, follow-up 4. |
| `InProcessInfoCarrierServer.cs` | 336/348 | Supported / Boundary | Missing synchronous savepoints at 571–587 and scope/disposal failure branches. Successful asynchronous transaction tests bypass those members. | Retain server transactions and cleanup. Cover synchronous savepoint effects alongside custom-hosting dispatch, follow-up 2. |
| `Query/QueryableProjectionValidator.cs` | 94/105 | Supported / Boundary | Missing fallback printing, nullable/array/nested type formatting at 271–293 and some validation alternatives. Existing refusals use simpler type shapes. | Retain rejection behavior. Add a user-facing invalid projection only if its accepted/rejected boundary differs; do not test formatting solely for percentage. |
| `Expressions/ExpressionToNodeTranslator.cs` | 289/300 | Supported / unresolved branches | Scalar captured-SQL translation at 200–207 is absent although ordinary SQL query roots execute. Extension-node refusal and null visitation are separate defensive paths. | Retain serializer translation. Reproduce a scalar captured root through supported configuration before deciding whether this alternative is obsolete. Follow-up 4. |
| `Expressions/TypeNodeResolver.cs` | 131/142 | Supported security boundary | Depth/name/shape and malformed generic/array guards at 289–364 are not all reached. Normal admitted descriptors cannot establish hostile-input refusal. | Removing guards widens admission/resource use. Add malformed descriptors through the serializer, including depth and name bounds; follow-up 4. |
| `InfoCarrierPayloadLimits.cs` | 47/57 | Boundary / Supported | Uncovered lines are read-stream adapter capabilities, unsupported seek/write members and synchronous/array-read alternatives. Async JSON decoding exercises memory-based reads. | Keep the non-seekable, non-writing wrapper. No throw-only stream tests. Add synchronous custom-serializer limit enforcement if that public path selects these reads; follow-up 2. |
| `Query/TransparentIdentifierRewriter.cs` | 394/404 | Supported / unresolved branches | Nullable carrier member substitution at 1043–1051 and alternate tuple/terminal shapes are missing. Current relational query shapes reach other carrier paths. | Keep query semantics. Add nullable final-projection behavior before simplifying alternate rewrites; follow-up 1. |
| `Query/GroupJoinFlattener.cs` | 129/139 | Supported / Boundary | Misses mark nonmatching or correlated query forms, not an unused flattener. Positive joins reach the actual rewrite. | Keep the rewrite and refusal checks. Test an actual supported correlated join only after comparing backend behavior. |
| `InfoCarrierTransactionManager.cs` | 32/41 | Supported | Existing transaction tests end the returned transaction directly. Database-facade commit/rollback, missing-current guards and active async reset remain absent. Current wrappers call genuinely synchronous client members. | Removing them breaks framework transaction management. Added synchronous/asynchronous facade success/misuse tests and an open-token reset test. |
| `InfoCarrierRegistrationAccess.cs` | 21/29 | Supported | Endpoint handle/context overloads and `MediaType` are unexecuted. Mapping overloads are different methods, while current direct-host tests resolve by name. | Keep all public selection methods. Add direct-host selection and media-type parity to follow-up 2. |
| `Query/JoinKeyRewriter.cs` | 50/58 | Supported / Boundary | Four-to-seven component tuple carriers, field members and malformed-member alternatives are missing. Existing join tests use smaller property keys. | Keep key equality reconstruction. Add a wide composite application join before changing tuple arities; follow-up 1. |
| `InfoCarrierFaultMapper.cs` | 52/59 | Supported / Boundary | Alternate exception constructors and throwing-constructor fallbacks at 138–197 are unexecuted. Existing errors use common constructors. | Retain safe exception reconstruction. Use a remotely raised exception with a supported alternate constructor if compatibility needs it; no private reflection tests. |
| `ServerQueryExecutor.cs` | 205/212 | Supported / Boundary | Exception unwrapping, missing model entity refusal and metadata fallbacks are absent. Two trailing throws are unreachable compiler flow aids after `ExceptionDispatchInfo.Throw`. | Keep execution/refusal boundaries. Do not test unreachable throws. A mapped-function failure control can cover real unwrapping, follow-up 4. |
| `TransportInfoCarrierClient.cs` | 208/215 | Supported | Buffered-to-stream log frames, collected log frames and stream failure measurement are absent. Most transport tests use native streams without forwarded logs. | Retain diagnostics and metrics. Add buffered/native forwarded-log parity and cancellation, follow-up 2. |
| `InfoCarrierRoleServiceCollectionExtensions.cs` | 184/191 | Supported | Configured synchronous commit/savepoint wrappers and stream acquisition/iteration failure measurements are unexecuted. Direct client tests and async role tests use different wrappers. | Keep configured role policy and measurements. Use registered clients for synchronous custom-hosting and failure controls, follow-up 2. |
| `Expressions/AnonymousShapeTypes.cs` | 230/237 | Supported security boundary | Generation-budget exhaustion and ambiguous descriptor/component bindings are missing. Normal trusted catalogs do not exhaust process-wide state. | Retain resource/admission guards. Add isolated-process boundary scenarios, not suite-wide budget exhaustion; follow-up 4. |
| `Query/QueryStreamFrameCodec.cs` | 91/97 | Supported | Writer-side invalid ordering, absent terminal and unknown frame types are missing. Reader validation and valid round trips do not exercise writer refusal. | Keep framing checks. Add contract-level invalid sequence tests where exposed, alongside historical comparison; follow-up 4. |
| `Wire/Protocol3Mapping.cs` | 172/178 | Supported / Boundary | Historical `ServerContextStubNode` conversion is absent in both directions, as are unknown enum/variant refusals. Ordinary historical queries do not call context-bound functions. | Keep compatibility mappings. Add historical mapped-function acceptance, follow-up 4; unknown inputs must still fail. |
| `Relational/InfoCarrierRelationalQueryRoots.cs` | 25/30 | Supported / unresolved callbacks | The constant-argument SQL root adapter's `UpdateEntityType`, printing and equality alternatives are absent. Ordinary root rebuilding does not invoke every framework callback. | Keep the adapter. Establish a real framework callback scenario before removing overrides or adding framework-shaped unit tests. |
| `Relational/InfoCarrierRelationalConventionSetBuilder.cs` | 13/18 | Boundary | Remaining uncovered source is the `NoDatabase` error factory, called by the excluded command-only structural adapter. Outer model construction executes across the suite. | Keep the narrow exclusion and error. Removing the dependency object would break construction; no throw-only tests. |
| `Query/ServerBoundaryAnalyzer.cs` | 200/205 | Supported / Boundary | Diagnostic property getters, extension/IQueryable alternatives and the `NodeFacts.IsShippable` convenience property are unexecuted. Other analyzer decisions execute extensively. | Retain admission logic. Check consumers before removing convenience properties; zero hits alone do not establish dead code. |
| `InfoCarrierTypeMappingSource.cs` | 24/28 | Obsolete compatibility host with active helper | The complete solution has no construction, subclass or registration site. `InfoCarrierRelationalTypeMappingSource` calls only its static helper. Its recorded retention reason is version 10 binary compatibility. | Removed the obsolete public host on the version 11 development line; moved unchanged scalar rules into the active service. Custom subclasses must move to the relational source. No wire change. |
| `ValueMapping/ModelConverterValueMapper.cs` | 47/51 | Supported / Boundary | Boolean provider-value conversion and unsupported JSON kind refusal are missing. Existing conversions exercise other primitive representations. | Keep conversion and refusal. Add a model converter with a Boolean provider value and a malformed-wire control; follow-up 3. |
| `Query/ClientPropertyReader.cs` | 19/22 | Supported / Boundary | Null instance, direct navigation getter and missing navigation alternatives are absent. Existing client reconstruction reads scalar/shadow metadata instead. | Keep model-based reads. Test a supported client projection needing navigation access before changing this fallback; follow-up 1. |
| `Query/SplitQuery.cs` | 16/19 | Boundary | Wrong result-slot count guard and a compiler-only throw after exception rethrow are unexecuted. Executors supply the planned slot count. | Keep the invariant and rethrow behavior. No direct throw-only test. |
| `IInfoCarrierClient.cs` | 12/15 | Supported | Buffered result adaptation misses logs and `BufferedResultMaterializationException`. Native stream tests bypass that compatibility adapter. | Keep default interface behavior and failure classification. Test a buffered-only client with malformed rows and logs; follow-up 2. |
| `InfoCarrierKeyDiscoveryConvention.cs` | 40/43 | Supported / unresolved metadata alternatives | Synthetic owned-key merging/removal branches at 128–145 are absent. Current model configurations choose other key layouts. | Keep model conventions until a metadata control establishes which layouts remain supported; follow-up 3. |
| `ValueMapping/IPAddressValueMapper.cs` | 14/17 | Supported / Boundary | Direct string input and invalid non-text input are absent; serialized JSON input executes. In-process plugin use can supply strings. | Keep both representations and refusal. Add a real in-process value round trip if used by a replacement serializer; follow-up 3. |
| `ValueMapping/UriValueMapper.cs` | 14/17 | Supported / Boundary | Same direct-string/non-text distinction as the address mapper. Current serializer supplies JSON elements. | Keep interoperability and validation; evaluate together with replacement-serializer scenarios, follow-up 3. |
| `Expressions/PrimitiveCoercion.cs` | 121/123 | Supported / Boundary | Null and string-element alternatives remain absent while the coercion implementation runs broadly. Current primitive graphs do not select both fallbacks. | Keep coercion; include nullable/string members in the property-bag round trip, follow-up 3. |
| `Metadata/AnnotationDocumentMapping.cs` | 13/15 | Supported / Boundary | Nested complex owner-container lookup and unknown metadata-kind fallback are absent. Current root documents select entity metadata directly. | Keep mapping ownership. Add a nested complex/document model only where the backend supports it; follow-up 3. |
| `Query/ProjectionShape.cs` | 87/89 | Boundary | Metadata lookup returns null for nonmatching shapes. Existing positive mappings find their expected members. | Keep shape discrimination. No isolated null-return coverage test. |
| `InfoCarrierTransaction.cs` | 48/50 | Supported idempotence | Repeated synchronous commit/rollback no-op branches are absent. Existing transaction disposal and async tests take other paths. | Keep completed-token protection. Add repeated public synchronous completion to the custom-hosting transaction scenario, follow-up 2. |
| `InfoCarrierTypeMapping.cs` | 52/54 | Supported / Boundary | Mapping clone and unsupported literal fallback are missing. Query serialization uses normal value mappings instead of arbitrary client SQL generation. | Keep framework mapping surface. Confirm an actual model/literal consumer before testing or removing alternatives. |
| `ChangeEntryMapper.cs` | 111/113 | Boundary | Missing/unknown property metadata returns null at 305 and 328. Ordinary tracked entries have those members. | Keep defensive mapping. No fabricated missing-metadata tests solely for line coverage. |
| `InfoCarrierServiceReplacementExtensions.cs` | 81/83 | Boundary | Invalid builder and nonconcrete replacement checks are missing. Current public replacement tests use valid builders/classes. | Keep configuration rejection. Add only when validating a concrete supported replacement misuse, not private descriptors. |
| `Wire/V3/Expressions/ServerContextStubNode.cs` | 0/2 | Supported historical variant | Source mappings explicitly support this variant, but no historical context-bound function test serializes it. Current Firebird function coverage executes the live node. | Do not delete the variant. It is a historical wire contract; add the mapped-function case in follow-up 4. |
| `InfoCarrierOptionsExtension.cs` | 62/64 | Boundary | Validation rejects absent `IInfoCarrierClient`. Normal `UseInfoCarrier` configures one before framework validation. | Keep the early configuration failure. No fabricated malformed extension solely for percentage. |
| `Wire/Protocol3WireProtocolCodec.cs` | 84/86 | Supported / Boundary | Historical forwarded log-frame conversion and unrepresentable frame refusal are missing. Compatibility tests do not forward logs. | Keep protocol support. Add log-frame round trip through the selected historical codec, follow-up 4. |
| `Relational/InfoCarrierRelationalFacadeDependencies.cs` | 25/27 | Boundary | Relational command logger and raw SQL command builder getters deliberately throw. Client raw-SQL query construction uses query provider/type mapping, not a command connection. | Keep explicit refusal. Removing interface members is impossible; returning a real command pipeline violates the client boundary. |
| `Expressions/TypeAllowlist.cs` | 219/220 | Supported admission alternative | The generic-parameter admission return at 550 is absent; the rest of admission is exercised. Normal runtime descriptors name closed types. | Keep the permission boundary. Confirm which supported generic method metadata can select this case before adding a serializer-level acceptance test; follow-up 4. |
| `Expressions/TypeNodeMapper.cs` | 63/64 | Supported security boundary | Exchange shape-budget exhaustion is absent. Ordinary exchanges stay within the budget. | Keep the resource bound; test in an isolated exchange, follow-up 4. |
| `Common/QueryDataRequest.cs` | 10/11 | Supported source compatibility | Encoded query getter is absent. Protocol 4 serializes `Query`, and protocol 3 has explicit boundary conversion. | Keep the documented in-process compatibility accessor. Add a custom adapter round trip only if validating that supported contract, follow-up 2. |
| `Common/QueryDataResult.cs` | 7/8 | Supported source compatibility | Encoded result setter is absent for the same typed-protocol reason. | Keep the documented adapter accessor; include it with the buffered-only adapter scenario, follow-up 2. |
| `Expressions/WireGrouping.cs` | 23/24 | Boundary | `MoveNext`'s exhausted return remains uncovered for a particular adapter. Other grouping paths execute. | Keep enumerator termination. Do not reflect into the private enumerator to improve coverage. |
| `Query/CapturedConstantValidator.cs` | 34/35 | Boundary | The extension-node passthrough does not execute in current captured constant shapes. Validation still runs. | Keep visitor behavior. No unsupported synthetic extension solely for percentage. |
| `ServerLogCapture.cs` | 47/48 | Boundary | The logger's no-scope return is absent. Forwarding deliberately captures individual events rather than transporting arbitrary scope objects. | Keep the logger interface implementation; no null-scope test. |
| `Wire/InfoCarrierWireProtocol.cs` | 52/53 | Supported protocol boundary | Header/body version disagreement refusal is absent at this entry point. Other version checks reject earlier in normal clients. | Keep validation for custom/malformed clients. Add a mismatched streamed body/header control, follow-up 4. |
| `ExpressionSerializer.cs` | 34/35 | Supported public adapter entry | Explicit `UseAnonymousShapeCatalog` is absent, while pipeline setup supplies trusted catalogs through other construction paths. | Keep custom-pipeline configuration. Verify a replacement pipeline using this method before removal. |
| `InfoCarrierLoggerExtensions.cs` | 36/37 | Boundary | `QuerySplit` returns when another provider's logging definitions are supplied. Correct InfoCarrier registrations install its own definitions. | Keep defensive cross-provider behavior. No foreign-definition test just for a return line. |
| `InfoCarrierLoggingDefinitions.cs` | 23/24 | Supported diagnostics | Remaining miss is the query `Debug` classification/cache arm at 41, not the old Information arm. Existing successful/cancelled runs do not enable this exact diagnostic path. | Keep cancellation severity. Add listener-enabled caller-cancellation classification to follow-up 2. |

### Branch-only review

The raw reports also identify partially covered conditions in fully line-covered files.
`InfoCarrierQueryFilterRewritingConvention.cs` has 13/13 lines, but its nested visitor has
partially covered conditions in the merged view. The visitor preserves `FromSql` calls while
other calls go to Entity Framework's context `DbSet` rewriting; line completion cannot establish
every expression alternative. Keep the visitor and compare filter method/receiver shapes before
considering simplification.

Other important partially covered conditions are already represented above: transaction reset
and current-token guards; stream first/terminal-frame checks; type-descriptor depth and shape
validation; document completion; and stream cancellation/failure classification. The audit uses
their source conditions rather than the unreliable merged branch denominator. It does not
classify every unobserved Boolean alternative as a separate missing feature.

## Applied changes and compatibility

1. Removed only historical `TypeNode.CacheIdentity`. It is internal, has no references in the
   complete loaded solution, and duplicates the live descriptor helper. Historical mappings
   read properties directly; serializers describe properties, not this method. Public signatures,
   serialized members, protocol identifiers and admission policy are unchanged.
2. Added eight public-query cases for buffered-only server replacements, across all four endpoint
   selection methods and both successful rows and remote refusal. They use a real SQLite-backed
   server behind an implementation that deliberately omits the optional streaming interface.
   This protects a supported replacement contract rather than asserting a private fallback body.
3. Added eight database-facade transaction cases for synchronous/asynchronous completion,
   commit/rollback and missing/open transaction state. They verify the transmitted token and
   cleared current state. Added active asynchronous manager-reset rollback coverage. Existing
   transaction tests completed the transaction object and could not establish these paths.

Focused transport/compatibility validation: **Passed: 79, Failed: 0, Total: 79**.
Focused transaction validation: **Passed: 18, Failed: 0, Total: 18**.
These runs collect fresh coverage separately from the complete starting report. Their percentages
are not substituted for a complete-suite measurement. Hosted validation of the changed revision
and structural checks are recorded in the pull request.

The focused reports execute all nine previously missed transaction-manager lines and 22 of the
26 previously missed endpoint lines. The endpoint's forwarded-log yield at 345 remains unexecuted,
consistent with the explicit follow-up. A strict Release rebuild passed with zero errors and the
five existing Razor framework trimming warnings. The wire structural gate passed against the
starting revision, with wire version 4 unchanged. Documentation checks found zero broken links in
the two changed documents; file hygiene and whitespace checks passed.

## Service necessity and test ownership follow-up, 2026-11-22

The initial audit classified suspicious files. A second pass asks whether their production
responsibility still exists before treating a missing line as a request for more tests.

### Obsolete mapping service hidden by helper coverage

`InfoCarrierTypeMappingSource.cs` illustrates the danger of a file-level total. Its 24/28 covered
lines come from shared scalar-classification logic. Its constructor and service override are
unexecuted. `AddEntityFrameworkInfoCarrier` registers `InfoCarrierRelationalTypeMappingSource`
unconditionally, and the relational interface resolves that same instance. Roslyn finds no
construction, subclass or registration of the older host anywhere in the complete solution.
Its only external code reference calls the internal static helper.

The active class's remarks and architecture section D3 record exactly why the old class survived:
it shipped in version 10, so changing its base or removing it would break that release line.
That compatibility requirement does not apply to the unpublished version 11 major. The helper
has moved unchanged into the registered service; the obsolete public host has been removed.
The upgrade guide explains the replacement for application-defined subclasses. This is a public
major-version removal, not a claim of binary compatibility. No live registration was removed,
and no serialized member, wire identifier or scalar mapping decision changed.

Official specification tests should not construct an obsolete, unregistered service merely to
cover its constructor. They reach the active mapping source through model construction and
application queries. The existing SQL-query, mapping, compiled-model, complex-tracking and
spatial controls passed after removal: **Passed: 537, Failed: 0, Total: 587**, with 50 skips
reported by that run. No replacement test was added for the discarded host.

### Projected concatenation exposes a real streaming gap

The temporary application investigation compared plain Entity Framework and InfoCarrier and
inspected the resulting split for six shapes: final construction, paging, indexed paging,
concatenation, distinct and filtering. The investigation file was removed after diagnosis;
the committed tests express the application contract.

Simple paging and distinct over the projection moved to the server, leaving only a client
`Select`. Indexed paging reached the backend and was refused by both clients. Filtering over
the unregistered constructed result was refused on both paths. These observations explain why
adding direct tests of every streaming operator would preserve implementations without first
establishing a supported caller.

Concatenating two client-constructed results exposed a different problem. The split contains
two server parameters, each wrapped by its projection, rather than the bare parameters accepted
by `StreamingConcatPlan`. The executor therefore buffered the queries. The existing concat
regression used anonymous projections that ship as one server query, so it never exercised that
plan. Its passing first-row assertion did not establish progressive delivery across result slots.

The new regression blocks completion of the first source and requires its first projected result
before release. Before the fix, the streaming class reported **Passed: 4, Failed: 1, Total: 5**:
the new case selected buffered execution. After the fix, concat slots can retain an independent
row-local residual plan. The executor streams and projects each slot in order, using the existing
materialization, cancellation and failure-reporting paths. A leaf reading multiple result slots,
or a residual requiring the same slot more than once, retains buffered execution.

An additional application test preserves empty inputs, order, duplicate values and non-tracking
behavior across nested concatenation. The focused streaming, framing, processing-diagnostic and
replacement checks reported **Passed: 82, Failed: 0, Total: 82**. The baseline report's uncovered
concat implementation was therefore a real delivery gap, not merely an untested private parser.

Plain Entity Framework refuses concat after this client-only construction. InfoCarrier already
accepts that row-preserving composition; its splitter does not classify concat as forbidden
client row selection or equality, and the streaming guide promises progressive concatenation.
This change preserves that existing query result and acceptance boundary while correcting its
delivery behavior. It does not establish parity with plain Entity Framework for that query shape.

### Why official bases miss these cases

| Area | What official bases establish | Why additional coverage is absent | Correct action |
|---|---|---|---|
| Old mapping-source host | Model and query behavior through the registered source | They resolve the relational replacement, not a retained compatibility class | Remove the obsolete host; retain its mapping rules |
| Streaming concat | Query results, including concat, under each base's tracking and execution choices | Answer assertions do not establish HTTP first-row timing; the old own test also used one shipped query | Add a gated application test and fix progressive projected-slot execution |
| Buffered-only server replacement | General database behavior | Entity Framework does not own InfoCarrier's optional streaming interface or endpoint selection | Keep the replacement tests added by this audit |
| Database-facade transaction completion | Store transactions through the applicable inherited bases | The relational transaction base requires a client connection; existing own tests completed returned transaction objects | Exercise the supported facade and manager routes with token/state assertions |
| Historical wire and admission failures | Current-provider application behavior | Official Entity Framework bases cannot produce InfoCarrier's historical messages or enforce its decoder budgets | Use actual historical clients/codecs and serializer-boundary controls where supported |

### Covered code called only by tests

Reference counts were split by production and test projects for selected apparently test-heavy
helpers. `WireTypeCollector.CollectOwn` is called by `QuerySplitter` and `ServerBoundaryAnalyzer`;
`RewriteVerifier.Verify` is called by `QuerySplitter`; `BoundaryAnalysis.FactsFor` is called by
the projection rewriter; and `ModelDbFunctions.ForModel` is called by type admission and parameter
evaluation filtering. Their direct tests do not establish obsolescence because their production
call paths are present too. Likewise, `InfoCarrierTypeMapping<T>.Default` has no ordinary caller
but is the generated compiled-model entry point, while `ClientResultMaterializer.Materialize`
is selected through reflection from `QueryExecutor`. These are false positives to avoid.

The subsequent adapter review found a stronger example: `BufferedQueryFrameAdapter.DeserializeRows`
had one caller, in `ServiceReplacementTest.Pipeline`, and no production caller. Protocol 4 changed
the default client adapter to read `QueryDataResult.Rows`, leaving the old byte decoder covered
only because the test called it. The test now reads the public typed result, and the obsolete
internal decoder is removed. Its exception wrapper still has a supported responsibility: errors
from the retained `SerializedResults` compatibility accessor must be classified as materialization,
not exchange or projection failures. The live default adapter now wraps that read, and an
application query using a buffered-only client verifies the original JSON exception and one safe
materialization event. This closes a diagnostic gap rather than preserving the dead decoder.

This is a selected necessity review, not an exhaustive proof that every covered method is needed.

## Completed follow-up decisions, 2026-11-22

### Streaming operators

The version 11 `ClientEvaluationFinder.VisitMethodCall` rejects every client-side `Where` and
`Distinct` before `SplitQuery` is returned. The exemptions do not reopen those cases: a server-side
node is substituted by its result parameter, and every registered reassembly is a `Select`.
The reassembly additions in `ProjectionRewriter`, including preserved root rebuilds from
`TransparentIdentifierRewriter.RebuildAtRoot`, all construct or retain `Select` calls. Therefore
neither operator can reach `StreamingResidualPlan`. Its private predicate and distinct steps
are removed. Registered server filtering and distinct remain unchanged.

Paging is different. A second, indexed projection over a client construction can remain above
the registered first projection. That source is not a directly registered reassembly, so paging
does not move below it. The new application regression uses this route and verifies indexes,
customer identities, non-tracking behavior and first-row delivery before source completion.
Focused coverage executes every line in `SelectorStep`, `SkipStep` and `TakeStep`. Retain these
steps and their yield/skip/complete states. Simple paging probes alone would have led to an
incorrect deletion.

### Adapter and custom-hosting behavior

Buffered-only replacement tests now check forwarded log identity as well as rows and failures
for each endpoint selection. A gated cancellation test waits for server execution, cancels the
client request and requires the replacement to observe cancellation. Direct service resolution
tests the default facade with both streaming and buffered-only implementations. Real SQLite
writes, synchronous savepoint rollback/release and commit establish its forwarding contract;
stream frames establish its optional streaming adaptation and log forwarding.

Synchronous HTTP refusal and malformed-success tests verify status/body diagnostics, synchronous
pipeline selection and response-content disposal. The buffered-only client test exercises default
interface adaptation, log replay and malformed encoded rows. A custom serializer cancellation
during request construction verifies the query-specific Debug diagnostic and safe simple logging.
Official store tests cannot establish these adapter, hosting and diagnostic contracts.

Focused replacement/streaming validation: **Passed: 48, Failed: 0, Total: 48**.
Focused HTTP/historical codec validation: **Passed: 28, Failed: 0, Total: 28**.
Focused admission/historical/client diagnostics validation: **Passed: 45, Failed: 0, Total: 45**.
The later client diagnostics run, including cancellation, reported **Passed: 29, Failed: 0, Total: 29**.
These overlapping runs are separate evidence, not an aggregate suite count.

### Save/model behavior and retained metadata boundaries

`CoverageModelRoundTripTest` adds Boolean-provider conversion of a nonprimitive model value,
including query constants, nullable values and both Boolean outcomes. It reads stored SQLite
values independently. A separate entity has a property-bag complex collection containing string
and nullable-integer primitive collections. Its save is verified from stored JSON, independently
of the upstream property-bag collection materializer that still rejects its indexer branch.
The ordinary property-bag read controls remain in place. Focused model validation reported
**Passed: 2, Failed: 0, Total: 2**.

A direct-server save test bypasses envelope fault correlation while retaining real SQLite
execution. After another client deletes one row, two pending updates fail atomically. The thrown
concurrency exception names the original client entity, and an independent read proves that the
other update was rolled back. Thus live-entry translation is supported and must remain.

The remaining incomplete-document and sentinel alternatives are retained boundaries. Document
completion refuses shared CLR types or unknown keys when it cannot form a safe read; removing
those refusals would permit omitted owned values to be lost. Sentinel restoration distinguishes
fields, writable non-indexer properties and entry-only shadow/indexer state. Existing store-
generated, many-to-many and document tests establish the surrounding behavior. This audit does
not prove every metadata combination. It makes no deletion or unsupported-model extension from
those unobserved alternatives.

### Historical messages and admission

Protocol 3 expression acceptance now verifies a mapped instance method's receiver in both
mapping directions. Evaluation uses the server context's marker, not the client's marker.
The historical stream codec also preserves all forwarded-log fields and rejects empty streams,
rows before start, missing terminal frames, data after termination and unknown tags. Both
header/body version disagreements are refused at the HTTP boundary.

Malformed current descriptors cross serialization and the real streaming endpoint before
rejection. Cases cover excessive name length, structured nesting, invalid array rank and an
oversized shape member list. Each produces a fault without rows. Entity Framework's official
bases cannot create these protocol-specific hostile inputs.

Exchange-shape and process-wide generated-type exhaustion guards remain. This audit deliberately
does not exhaust the process-wide emission budget in the shared test host. Doing that requires a
separate process and is a bounded future admission stress test, not evidence of dead code or a
reason to loosen the limit. Ambiguous catalog bindings and malformed plugin output likewise stay
guarded; their absence from normal model tests is expected.

## Completion and bounded further work

The file table records the starting counts and review candidates; the dated follow-up sections
supersede its initial pending actions. All confirmed deletions and behavior fixes are applied.
Final complete coverage, raw/merged reconciliation and hosted normal/slow results are attached to
[pull request #291](https://github.com/azabluda/InfoCarrier.Core/pull/291). The source revision and
run links there identify the actual measured result; focused percentages do not replace it.

The remaining candidates are retained defensive or public compatibility boundaries, with no
confirmed defect or justified removal. Further useful work, in order:

1. Isolated admission stress tests for exchange and process generation budgets, without exhausting
   global state in the shared suite. Keep all current bounds in the meantime.
2. Additional supported document/sentinel metadata combinations if an application requires them.
   Keep current refusal and repair behavior; validate stored data before narrowing either.
3. Broader covered-code necessity review using production references, framework callbacks and
   generated entry points. The test-only decoder demonstrates why a high file percentage is
   insufficient, but this audit is not a whole-program proof.

These are bounded extensions, not new issues or release blockers established by this audit.
The audit does not authorize publication, merging its pull request, or closing its issue.
