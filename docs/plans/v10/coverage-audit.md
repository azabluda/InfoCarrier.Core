# Product coverage audit

Date: 2026-10-15

Issue: [#226](https://github.com/azabluda/InfoCarrier.Core/issues/226)

Source run: [Coverage 38720589711](https://github.com/azabluda/InfoCarrier.Core/actions/runs/38720589711)
Updated run: [Coverage 38748189351](https://github.com/azabluda/InfoCarrier.Core/actions/runs/38748189351)

## Scope and method

The merged Cobertura report from the first `main` coverage run identified six unusually low
product classes. The same three product assemblies were collected locally and merged with
`dotnet coverage merge`. The six line rates reproduced exactly.

`eng/coverage.runsettings` includes only `InfoCarrier.Core`, `InfoCarrier.Core.Abstractions`, and
`InfoCarrier.Core.AspNetCore`. Its `**/obj/**` file filter removes compiler-generated source files.
ReportGenerator showed exactly those three assemblies. No test assembly or generated file affected
the six results.

The audit then checked each type's responsibility, the requirement that introduced it, why the
specification suite does or does not reach it, and the effect of removing it. A test is recommended
only when an uncovered behavior belongs to a supported contract. Low coverage alone is not a
reason to preserve behavior or add a test.

The updated run verifies the factory exclusion against the complete workflow. ReportGenerator now
shows 130 classes in 116 files, down from 132 classes in 117 files. Coverable lines fell from 8,130
to 8,120. Uncovered lines fell from 499 to 491. The excluded factory file accounts for all eight
removed uncovered lines. Two covered registration lines also disappeared after their formatting
changed. No new uncovered line was introduced by the factory change.

Line coverage rose from 93.8% to 93.9%. The branch figures are not comparable: the branch
denominator changed from 3,234 to 4,612 although this pull request changed no branching production
logic. The merged Cobertura files assign additional branch points across many unchanged classes.
This audit therefore uses the stable line records and does not interpret that branch-rate movement.

## Classification

| Class | Lines | Classification | Decision |
|---|---:|---|---|
| `InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory` | 0 of 4 | Obsolete version 10 compatibility name | Remove in version 11. Exclude its inherited deliberate throw from coverage. |
| `InfoCarrierShapedQueryCompilingExpressionVisitorFactory` | 0 of 4 | Obsolete version 10 compatibility name | Remove in version 11. Exclude its inherited deliberate throw from coverage. |
| `InfoCarrierDatabaseCreator` | 2 of 10 | Required service with unsupported placeholder behavior | Keep the service in version 10. Do not freeze all ten current results in tests. Review the behavior separately. |
| `InfoCarrierDbContextOptionsBuilder` | 9 of 20 | Shipped compatibility entry points | Keep until version 11. Add direct behavior tests for the two obsolete methods. |
| `InfoCarrierRelationalConventionSetBuilder` | 46.4% rollup | Active model builder plus an unreachable fail-fast stub | Keep. Exclude only the nested structural adapter. |
| `InfoCarrierDatabaseFacadeExtensions` | 6 of 10 | Covered transaction feature plus one uncovered misuse guard | Keep. Existing positive tests cover the requirement. Do not add a message test for coverage. |
| `InfoCarrierLoggerExtensions` | 71.2% | Active diagnostics plus two superseded public overloads | Deprecate the superseded overloads for version 11. Test their compatibility behavior until removal. Test the active Information classification. |
| `InfoCarrierLoggingDefinitions` | 73.9% | Active definition cache plus one definition used only by a superseded overload | Deprecate the old definition with its caller. Keep and test the active Information cache arm. |
| `InfoCarrierTransactionManager` | 73.5% rollup | Active async transactions plus obsolete and requirement-violating sync behavior | Do not add sync coverage. Deprecate the unused public helper and redesign every sync-over-async member. Add focused async misuse coverage. |
| `InfoCarrierMetrics` | 76.4% | Active low-allocation operation-tag mapping | Exercise every supported operation through the public metrics path. Do not test the impossible enum fallback. |

### Query-pipeline factories

#### Responsibility

Entity Framework Core's standard query pipeline genuinely uses both factories.
[`QueryCompilationContext.CreateQueryExecutorExpression`](https://github.com/dotnet/efcore/blob/release/10.0/src/EFCore/Query/QueryCompilationContext.cs)
preprocesses a query, calls the translating
factory, postprocesses the result, and calls the shaped-query factory. The services are not unused
inside Entity Framework Core.

InfoCarrier replaces `IDatabase.CompileQuery`, which is the caller above that standard pipeline.
Its implementation captures and sends the expression without creating a
`QueryCompilationContext`. Therefore, the two factory calls are bypassed as a consequence of the
InfoCarrier design. Their `Create` methods have no supported caller here.

#### Why the specification suite does not cover the bodies

The inherited
`Required_services_are_registered_with_expected_lifetimes` test compares every provider against
the uniform `EntityFrameworkServicesBuilder.CoreServices` list. That dictionary describes every
provider service used by EF Core's standard architecture. It is static framework state, not a
per-provider runtime requirement list. The test is virtual because a provider may have a narrower
architecture.

`TryAddCoreServices` is also virtual, but overriding it is unnecessary here. It registers EF Core's
shared implementations and dependency objects; it does not add these provider implementations.
Mutating the static `CoreServices` dictionary would affect every provider in the process. The
provider-specific test instead filters exactly these two inapplicable interfaces, runs the base
lifetime check for everything else, and separately asserts that neither interface is registered.

Normal query tests use InfoCarrier's earlier capture point and never call either `Create` method.

The factories were added by
[`63793919`](https://github.com/azabluda/InfoCarrier.Core/commit/6379391964bd30212aee894b7986a5d3d65aee22)
after that inherited registration test failed. The commit chose full conformance with the uniform
service list and a descriptive error for manual resolution. It did not restore missing query
functionality, because no query functionality was missing.

#### What happens without them

The exact result depends on what is deleted:

| Change | Build | Specification tests | Normal InfoCarrier execution | Package validation |
|---|---|---|---|---|
| Remove both registrations and keep the unmodified inherited test | Builds | The uniform service-list test fails | Queries, updates, and model construction are unchanged | Passes |
| Remove both registrations and state the two exceptions in the provider test | Builds | Passes, while every applicable lifetime remains checked | Queries, updates, and model construction are unchanged | Passes |
| Also remove the shipped public types in version 10 | Builds | Passes | Unchanged | Fails because version 10.3 contained both types |

A caller that manually resolves an unregistered factory receives a generic missing-service error.
No repository call site resolves either factory. Normal InfoCarrier execution never enters the
standard query pipeline.

Therefore, “Entity Framework Core requires them” is too broad. Entity Framework Core requires them
for its standard query pipeline, and its generic conformance test assumes that pipeline. InfoCarrier
does not use it. The registrations were conformance shims, not production functionality. They are
now absent, with the reason stated beside service registration and in the provider-specific test.

The original public factory types shipped in version 10, so they cannot be removed on this line.
They remain unregistered compatibility stubs. Their `ICC1006` and `ICC1007` diagnostics state that
version 11 will remove them. No replacement factory type is needed because no factory service is
needed.

Both types have `ExcludeFromCodeCoverageAttribute`. This is not a request to hide untested
functionality. The only executable bodies are deliberate throws for a path that InfoCarrier does
not support. The coverage workflow uses Coverlet's VSTest collector. Coverlet
[`always excludes`](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/VSTestIntegration.md)
`ExcludeFromCodeCoverageAttribute`, without extra run-settings configuration. A local collection
used the workflow's `eng/coverage.runsettings` file and the inherited service-registration test.
Its Cobertura file contained neither class name nor their source file. The later merge
and both reporters cannot restore entries that the collector omitted.

Keep only the obsolete public types through version 10. Remove them in version 11. Do not add
direct `Create` tests, replacement types, or unused service registrations.

### Database creator

#### Responsibility

`IDatabaseCreator` is also a required scoped provider service. The interface completes the provider
service graph. However, its documented responsibility is to create, delete, inspect, and connect
to the provider's database. InfoCarrier has no client-side database. The real store and schema are
owned by the server's provider.

The product documentation therefore says migrations and `EnsureCreated` are not part of the
InfoCarrier surface. Schema management must run on the server.

#### Why the specification suite barely covers it

The inherited service-registration test requires an `IDatabaseCreator`, but does not require these
ten results. Some test infrastructure reaches creation helpers while arranging stores. Most
backing-store setup runs against the server provider directly. Normal query, update, and model
specifications have no reason to ask the connectionless client to manage a schema.

The class entered in work-in-progress commit
[`95b63bd6`](https://github.com/azabluda/InfoCarrier.Core/commit/95b63bd67ad5ad7be62673bfe95a3d660bf05aec).
Its own comment says `EnsureCreated` reports success so test seeding can continue. That is a test
harness accommodation, not a product requirement.

#### What happens without it

Removing the service makes the provider registration incomplete. Removing individual methods is
impossible while implementing `IDatabaseCreator`. That justifies an adapter, but not its current
answers.

The current answers claim successful actions that never happened:

| Method family | Entity Framework Core meaning | Current InfoCarrier answer | Actual effect |
|---|---|---|---|
| `EnsureCreated` | Create the database and schema when absent | `true` | Creates nothing |
| `EnsureDeleted` | Delete the database when present | `true` | Deletes nothing |
| `CanConnect` | Check whether the database is reachable | `true` | Performs no check |
| `HasTables` | Check whether tables exist | `true` | Performs no check |
| `CreateTables` | Create model tables | completes | Creates nothing |

Direct tests for these results would convert an old test-harness shortcut into a protected product
contract. That would conflict with the published statement that schema management is unsupported.

Keep the required service in version 10 because the type and members have shipped. Do not add the
ten proposed tests. A separate change should decide how unsupported calls fail clearly, and should
move any remaining test-store dependency out of product behavior.

### Options builder

#### Responsibility

Every uncovered line belongs to obsolete `AllowTypes` or obsolete parameterless
`AllowArbitrarySqlExecution`. Both methods shipped in version 10.1. They configured real client
planning and security-related deployment facts before version 10.3 introduced the shared builder.

The current builder methods cover new applications. The old methods cover binary and source
compatibility for existing version 10 applications. Repository versioning policy forbids their
removal before version 11. Package validation checks that members remain present, but cannot prove
that they still change the correct options.

#### Why the specification suite does not cover them

The tests were migrated to the recommended shared builder. The remaining reflection test checks
the obsolete diagnostic identifiers and replacement names without invoking the old methods. This
is expected for new-feature tests, but it removed the only behavior evidence for shipped entry
points.

#### What happens without them

Removing either member breaks source compatibility and can cause `MissingMethodException` for an
already compiled application. Leaving a member present but inert is also a compatibility break,
and package validation would not detect it.

Two focused compatibility cases are justified:

1. Call obsolete `AllowTypes(typeof(ExampleFunctions))`. Verify the resulting client options allow
   `ExampleFunctions`, exactly as `InfoCarrierDeploymentBuilder.AddAllowedTypes` does.
2. Call obsolete parameterless `AllowArbitrarySqlExecution()`. Verify the resulting client options
   allow raw SQL, exactly as `InfoCarrierDeploymentBuilder.AllowArbitrarySqlExecution(true)` does.

These tests protect shipped behavior, not the coverage percentage. Removal remains version 11 work
associated with [#221](https://github.com/azabluda/InfoCarrier.Core/issues/221).

### Relational convention-set builder

#### Responsibility

The outer builder creates the client model's relational metadata. The specification suite uses
that model and covers 13 of the builder's 18 lines. This is active production behavior.

ReportGenerator also assigns the private nested `NoUpdateSqlGenerator` to the outer class. Entity
Framework Core requires an `IUpdateSqlGenerator` inside
`RelationalConventionSetBuilderDependencies`. InfoCarrier uses selected framework conventions that
need the dependency object, but never generate client-side database commands.

#### Why the specification suite does not cover the nested stub

Thousands of model and query tests build models through the outer builder. Their success proves
that the selected conventions do not read the command-only dependency. The nested methods remain
uncovered because reaching one would mean Entity Framework Core crossed the provider's architecture
boundary.

#### What happens without it

Without some `IUpdateSqlGenerator`, the dependency object cannot be constructed. Passing `null`
would postpone a framework change into an unclear null-reference failure. Registering a real SQL
generator would add a client command pipeline that InfoCarrier must not have.

The throwing stub is therefore a fail-fast structural adapter. If a future Entity Framework Core
version starts using it during model construction, the existing specification suite will fail at
the newly invalid boundary. Calling every stub method directly today would prove only that `throw`
throws.

#### Amendment 2026-10-20: the command-side consumer is provider-specific

The original assessment established that the selected conventions did not call the stub, but it
did not identify which convention needed the dependency. The constructor parameter was added to
EF Core for provider-specific sequence value generation. In EF Core 10,
`SqlServerValueGenerationStrategyConvention` reads
`RelationalConventionSetBuilderDependencies.UpdateSqlGenerator` to obtain SQL Server's sequence
expression. The generic relational conventions selected by InfoCarrier do not read that member.

That distinction makes the boundary exact. InfoCarrier reads its `RelationalDependencies`
property and passes the complete object to generic relational conventions. Those conventions use
the metadata dependency where needed. InfoCarrier never installs a provider-specific convention,
because the client cannot know which provider backs the server. Therefore, no supported
InfoCarrier path can call this particular generator.

The two original reasons against direct tests still hold: the adapter has no supported behavior,
and invoking its methods would test only that `throw` throws. The earlier reason for leaving its
lines visible does not hold after identifying the concrete consumer. The uncovered methods are not
an unresolved architecture warning; they are the deliberately absent SQL Server command path.
`NoUpdateSqlGenerator` is now excluded from coverage, while the active outer builder remains
measured.

### Database facade extension

#### Responsibility

`UseInfoCarrierTransaction` lets a second InfoCarrier context join the first context's server
transaction. This is required by specification tests that use two contexts inside one transaction.
Without it, the second context uses another SQLite connection and fails with a database lock.

That supported path is already covered. It shipped with transaction remoting in
[`aa0a3c05`](https://github.com/azabluda/InfoCarrier.Core/commit/aa0a3c059d4d117661f5aa6c6f252c412f10918f).

#### Why four lines remain uncovered

The public signature accepts `IDbContextTransaction`, matching Entity Framework Core transaction
APIs. The implementation must recover the InfoCarrier server token from the concrete transaction.
The uncovered branch detects a transaction from another provider and returns a clearer
`ArgumentException`.

Specification tests pass the correct provider transaction because they test transaction sharing,
not invalid cross-provider input. A foreign transaction violates the method's documented
precondition.

#### What happens without the guard

The supported transaction-sharing path is unchanged. A direct cast would still reject a foreign
transaction, but with a less useful `InvalidCastException`. Changing the parameter to
`InfoCarrierTransaction` would express the restriction statically, but would break the shipped
version 10 signature.

Keep the guard because it gives a precise public error at negligible cost. Do not add a test for
its exact wording merely to cover four lines. The existing positive transaction tests cover the
actual feature requirement.

### Query-split logging

#### Responsibility

`InfoCarrierLoggerExtensions` publishes the query-split diagnostic through both Entity Framework
Core logging paths. `InfoCarrierLoggingDefinitions` caches the event definitions. This is active
product diagnostics, introduced by
[`2a8f4ec5`](https://github.com/azabluda/InfoCarrier.Core/commit/2a8f4ec53b9a8d6a0a72a5569b2e43a46d929623).

The product call site now uses the four-argument `QuerySplit` overload. That overload reports the
server query count, row-removing client operators, and unregistered key types. The two-argument
overload reports only the count. The three-argument overload omits unregistered key types. No
repository call site invokes either older overload. The old `LogQuerySplit` definition is used
only by the two-argument overload.

#### Why these lines are uncovered

The production call moved forward as the diagnostic gained information. The old public overloads
remained for binary compatibility, but their behavior tests moved to the newest overload. Their
message callback and old event definition therefore remain uncovered.

One unrelated uncovered line is the `Information` cache arm in `LogClientFailure`. Query
preparation failures deliberately use `Information`; caller cancellations use `Debug`, and
unexpected failures use `Error`. Existing tests reached the other classifications but did not
combine a preparation failure with an enabled logger.

#### What happens without them

Deleting either old overload or `LogQuerySplit` in version 10 breaks the shipped public API and
can break an already compiled caller. Deleting them in version 11 changes no InfoCarrier execution
path. Deleting the `Information` cache arm changes a supported preparation failure into the wrong
severity or removes its definition.

Mark the two old overloads and their dedicated definition obsolete for version 11 removal. Keep
their implementation intact through version 10 and add one compatibility test that reaches both
logging delivery paths. Add one active-path test for the `Information` preparation-failure event.
These tests protect public diagnostics behavior, not the percentage.

### Transaction manager

#### Responsibility

The manager owns the client transaction token and sends begin, commit, and rollback requests to
the server. Requirements section 2.9 requires transactions. Section 4.3 requires all I/O to be
asynchronous and says a synchronous overload must never block on asynchronous code.

#### Why these lines are uncovered

The inherited and repository transaction tests use the asynchronous API. Consequently,
`CommitTransaction` and `RollbackTransaction` remain uncovered. Both call their asynchronous
counterparts through `GetAwaiter().GetResult()`. `BeginTransaction` and `ResetState` use the same
blocking pattern but happen to be covered. Coverage of those two lines does not make the design
valid.

`OnSaveChanges` is not a member of `IDbContextTransactionManager`, and Roslyn finds no caller. It
is leftover code, not a framework requirement. The uncovered `Require` branch is the supported
error for an asynchronous commit or rollback when no transaction is open.

#### What happens without them

Deleting `OnSaveChanges` changes no caller, interface, or runtime path. However, the public virtual
method shipped in version 10, so package validation correctly prevents immediate removal. Mark it
obsolete now and remove it in version 11.

Deleting the synchronous interface members does not compile. Testing their current implementation
would preserve behavior that violates section 4.3. They need a separate design decision: either
fail synchronously with a clear unsupported-operation exception, or provide a genuinely
synchronous transport contract. The latter would widen the entire transport surface and conflicts
with the async-first requirement. Direct failure is the smaller consistent design.

Deleting the `Require` guard replaces a precise invalid-state failure with a null-reference
failure. Add focused tests for asynchronous commit and rollback without an open transaction. Do
not add coverage tests for the current blocking wrappers.

The same review must include the covered `BeginTransaction` and `ResetState` wrappers. Limiting
the fix to the uncovered commit and rollback lines would repeat the factory mistake: it would
optimize the report while leaving the requirement violation in place.

### Round-trip metrics

#### Responsibility

`InfoCarrierMetrics.NameOf` maps every wire operation to a stable `operation` tag without calling
`Enum.ToString` on every request. All nine operations use the public transport client, and every
one reaches the same metrics recording method when a listener is enabled. This behavior was added
by [`02f5da28`](https://github.com/azabluda/InfoCarrier.Core/commit/02f5da28c2dd7939f478d6403b36b40ae6c7b0e5).

#### Why these lines are uncovered

The metrics tests enable a listener for `CommitTransaction` and `SupportsSavepoints`. Ordinary
query, save, transaction, and savepoint tests do not enable a process-wide metrics listener, so
their `NameOf` arms remain uncovered. The operations themselves are well covered; their observable
metric tags are not.

The default arm accepts an invalid enum value. No public client method can produce one. The
serializer and server reject invalid protocol operations elsewhere, so a direct test of this
private fallback would not protect a supported scenario.

#### What happens without them

Removing an explicit valid-operation arm does not currently change its text because
`Enum.ToString` returns the same name. It does reintroduce reflection-backed formatting and an
allocation on that operation's measured hot path. Removing the default arm makes the switch
non-exhaustive for malformed internal values, but no supported caller supplies one.

Extend the public metrics test across all nine transport operations. Assert the stable tag once per
operation. Do not reflect into `NameOf`, and do not add an invalid-enum test solely for the final
fallback line.

## Result

The updated report confirms that the coverage filters and factory exclusions are correct. The low
percentages combine eight different situations:

1. Two inapplicable provider services omitted through a documented test exception, while obsolete
   public compatibility types remain until version 11.
2. One required framework service whose current placeholder results are not supported product
   behavior and should not be frozen in tests.
3. Two shipped compatibility methods whose behavior needs direct protection.
4. One active convention builder reported together with an unreachable structural stub.
5. One covered transaction feature reported together with an optional misuse guard.
6. Active logging mixed with superseded public overloads that now exist only for compatibility.
7. An async transaction implementation mixed with dead code and synchronous wrappers that violate
   the async-first requirement.
8. A low-allocation metrics mapping whose operations run broadly, but whose listener-enabled test
   exercises only two tags.

Focused tests are justified for obsolete options behavior, version 10 logging compatibility, the
active Information logging classification, async transaction misuse, and all supported metrics
tags. Each protects a stated or shipped contract. The query-factory throws, update-SQL stub, and
wrong-provider message need no coverage-driven tests.

The new review also found work that tests must not legitimize. `OnSaveChanges` has no caller and
should be deprecated for version 11 removal. The transaction manager's four synchronous wrappers
block on asynchronous I/O, contrary to requirements section 4.3. They need a design correction,
not coverage. The obsolete query-split logging overloads should leave in version 11.
`InfoCarrierDatabaseCreator` remains a separate design problem rather than a test backlog item.
