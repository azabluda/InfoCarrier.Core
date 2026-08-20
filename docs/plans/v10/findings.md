# Findings, v10

How each part of the provider was made to work, and what it cost to learn. This is the long form
of what `CLAUDE.md` states as rules. Nothing here is an instruction; the instructions are in
`CLAUDE.md` and they are short on purpose.

Most of what follows is closed. It is kept because the same mistakes are available again: a
classification that was never re-checked, a count that did not move, a price paid for the wrong
obstacle. The plan entries that produced these findings are in `implementation-plan.md` and
`archive/`.

## What was built, and what each thing taught

Not yet implemented, in rough priority order:
- **The HTTP transport works and is tested (M8 Phase 1).** A `DbContext` with no database answers
  queries, saves and runs transactions against a SQLite-backed ASP.NET Core server over a real
  HTTP hop — `test/InfoCarrier.Core.TransportTests`, 17 of 17. **That project is deliberately not
  `InfoCarrier.Core.FunctionalTests`**: the ratchet counts the latter and its number must keep
  meaning "inherited spec tests failing". `eng/measure.sh` was scoped to one project in the same
  phase, because it parses the *last* `Total tests:` block and a second test project in the
  solution would have silently corrupted every measurement.
- **The Blazor WebAssembly client works too (M8 Phase 2, M8-10…M8-17).** `dotnet run --project
  samples/Northwind.Server` serves a browser client whose `DbContext` has no database: three pages,
  a wire inspector that decodes the expression tree out of its base64 layers, a compiled model, and
  a trimmed publish that runs. **Verified by executing it** — headless Edge over the **DevTools
  protocol**, because `--dump-dom` renders a page but cannot press a button, and two of the three
  pages are about what happens when you do.
  **Three things that phase established, all of them about the browser rather than this provider:**
  - **WebAssembly cannot block, and it bit twice.** Automatic lazy loading is impossible there — a
    navigation getter is *synchronous*, so it must block on the round trip, and `order.Customer`
    throws `Cannot wait on monitors on this runtime` **after the request has gone out**.
    `ILazyLoader.Load()` is synchronous too, so the spec's recorded fallback fails identically; use
    `Entry(x).Reference(…).LoadAsync()`. Separately, **a compiled model cannot even be loaded**
    without `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)`, because EF
    initializes it on a 10 MB-stack `Thread`. Proxies themselves are fine — Castle DynamicProxy
    emits types there — but the client no longer enables them (M8-18), because
    configured-but-unusable turns an unloaded navigation into an exception instead of a `null`.
  - **`dotnet ef dbcontext optimize` uses the STARTUP app's DbContext configuration and silently
    ignores an `IDesignTimeDbContextFactory` in the target project (M8-18).** A Blazor WASM project
    emits no `deps.json` and cannot be a startup project, so the server was one — and the
    "client's" compiled model came out annotated `Relational:TableName` and
    `Proxies:LazyLoading`. The browser ran on the **server's** model for two steps and looked fine,
    which is the silent-divergence shape A49/B4/B12 warn about. **A one-GUID regeneration diff is
    the tell that the factory was never consulted**, and it was misread once as "proxies do not
    affect the model". The compiled model is removed; the sample builds its model at start-up.
  - **Trimming: 88 IL warnings are ours and spec §9's "none of ours" criterion is NOT met.** They
    are the premise showing through — the wire carries a type's *name* — so
    `[DynamicallyAccessedMembers]` cannot express them. Gated by direction in `eng/trim-ratchet.sh`
    against `eng/trim-baseline.txt`, exactly as `eng/ratchet.sh` gates the spec suite. **The app
    runs trimmed regardless**; the warnings mean unprovable, not broken.
  - **`eng/trim-ratchet.sh` publishes CLEAN on purpose.** An incremental publish does not re-run
    ILLink, and the script's first version reported `OURS: 0` — a gate that would have passed
    forever. It now wipes `obj/Release` and refuses a log with no ILLink banner. **And classify
    trim warnings by declaring member, never by file path: this repository's own path contains the
    string "InfoCarrier", so a naive grep attributes every warning in the log to this product.**
    **No count is quoted here on purpose — read `ours` and `total` out of `eng/trim-baseline.txt`.**
    They move independently and for unrelated reasons: `ours` went 86 → 88 for a deliberate
    `WireGrouping` fix, while `total` fell 1129 → 853 because EF CORE's own count dropped, which is
    not an improvement of ours and reads like one. The baseline file records every measurement and
    why it moved; a number copied into prose only records when it was copied.
- **Complex types work** (A32) — `ComplexTypesTrackingTestBase` is **249 of 251**, and the two
  left are one shape of one feature: a property-bag complex *collection* on an `Added` entity.
  A complex value cannot ride in the value dictionary an entity is built from — `CreateEntry` and
  `ShadowValuesFactory` are name-keyed and complex leaves collide (`Culture.Species` and
  `Milk.Species` are both `"Species"`) — so both sides set it through its CLR member instead.
  **J22 traced the residual two to an UPSTREAM defect on a path only this provider takes, and
  corrected two wrong readings on the way.** EF's `StructuralTypeMaterializerSource.CreateMemberAssignment`
  calls `Expression.Property(instance, member)` where `member` is the `Item[string]` **indexer** of
  a property-bag complex type, with no index argument — the same hazard
  `ServerSaveChangesExecutor.SetOnEntity` already guards one level down. **The property bag is the
  complex type, not the entity** (`List<Dictionary<string, object>> Teams`), so a fix gated on
  `IEntityType.IsPropertyBag` is inert — measured, and reverted. EF's own InMemory suite passes the
  test **because EF never materializes the entity from a value buffer**; EF's SQL Server suite
  disables it outright (issue #36175). The route — construct the entity without
  `GetOrCreateMaterializer` — has to reproduce constructor binding, so it is priced in J22 and not
  taken. The
  `Query.Associations.ComplexProperties` family is **not** adoptable on Tier A (A77): EF's InMemory
  provider does not translate a complex property access at all, which is why EF ships no InMemory
  complex-type query test. Complex-type *queries* need Tier B.
- **The query residual** — 2, and **neither is a gap**. A40 closed
  `SelectMany_correlated_subquery_hard`, the correlated subquery under a client-side projection
  that **milestone M2-B existed for**, and A43 closed `Select_GroupBy_SelectMany`. The 2 left are
  spec tests asserting a limitation this provider does not have — they run and return the right
  answer, and the query bodies are `private` to the spec base, so the assertion cannot be inverted
  from a derived class.
- **`GraphUpdates` is CLOSED — 1787 of 1787 (C76) — and the residual was never what it was filed
  as.** For phases it read *"tracked-entry count off by one (26 vs 27)"*. Two probes in C42's order
  — what the metadata says, then **read the row the store actually holds** — showed the principals
  were byte-identical between a passing and the failing parameterization and only the *dependents'
  foreign keys* differed: `Optional2MoreDerived#7->6`, and no `Optional1` has key 6. **A wrong
  value written to the store.** The cause is that **a client placeholder is not unique across
  entity types** — EF's temporary generator counts down from `int.MinValue` *per key property*, so
  `Optional1.Id` and `Optional2.Id` issue the same numbers in one request, and the server's
  placeholder map was keyed by the value alone. It is now keyed by `(key property, value)` and
  resolved through `foreignKey.PrincipalKey`. **C34's rule in the SaveChanges direction**: a key
  resolved by value rather than by what declares it. Order-dependence, not rarity, is why it was
  1 of 36 parameterizations.
  **It remains the tripwire for over-returning on SaveChanges**: C42 measured a rule that sent
  every propagated foreign key back to the client and two more parameterizations of this same test
  went red. An ordinary FK is the client's own business; only a key it cannot recover by fixup may
  be asserted at it.
  **C76 filed an open finding here and C79 closed it: there was no gap, and the test was wrong.**
  `alpha.Id` reads `0` right after `Add` because **EF keeps a temporary key on the entry, not on
  the instance** (`Entry(x).Property(p).CurrentValue` is the placeholder) — so the test wrote `0`
  into a required foreign key and earned its `FOREIGN KEY constraint failed`. It failed with and
  without the fix, which is true and which was read as "pre-existing defect" when it meant
  "unrelated". **The pin C76 wanted now exists on Tier A**, and needing Tier A is the reusable
  part: on Tier B every placeholder maps to *itself*, so three separate mutations leave a Tier B
  test green. A collision is only observable where the store issues keys at `Add` **and** the two
  key sequences have drifted apart — both InMemory counters start at 1, so an unseeded store hides
  it too.
- **The remaining spec bases — 0. `InfoCarrierComplianceTest` is GREEN (C82).** Every base EF
  ships that this provider can host is adopted, `AdHocJsonQuery` last and **61 of 61**. B3d/C10
  had priced it at *"626 + 322 lines of relational mirror and seven abstract seeds"*, and both
  halves were priced against the wrong obstacle: the mirror was the cost of **not referencing**
  `EFCore.Relational.Specification.Tests` (ADR-013 now does), and the seeds are ten raw-SQL
  `INSERT`s copied byte-for-byte from `AdHocJsonQuerySqliteTest` and run against the **backend**,
  because the client has no database. **Copying only the seven the compiler demanded cost a run** —
  `Seed21006`, `Seed29219` and `Seed34960` are `virtual`, EF's SQLite class overrides them too, and
  34 of 61 became 56 of 61 once all ten were taken. *"Which does the compiler require"* is not
  *"which does this store need"*.
- **`Scaffolding.CompiledModel` is CLOSED — 4 of 4 (C93)**, and its 42 baselines live in
  `test/InfoCarrier.Core.FunctionalTests/Scaffolding/Baselines/`, written by EF's own
  `EF_TEST_REWRITE_BASELINES=1`. They need `<Compile Remove="Scaffolding\Baselines\**\*" />` and
  its `<None Include>` partner — the same `TestNamespace.DbContextModel` is generated once per
  test, so building them gives **125** duplicate-definition errors. They are the only assertion in
  the suite over the *source* this provider contributes to a compiled model.
- **This provider has design-time services now (C90), and the standing price for them was for a
  package that was never needed.** C8 filed `Scaffolding.CompiledModel` as *"needs
  `Microsoft.EntityFrameworkCore.Design` on the product"* — but `IDesignTimeServices`,
  `DesignTimeProviderServicesAttribute` and `EntityFrameworkDesignServicesBuilder` all live in
  `Microsoft.EntityFrameworkCore` itself. **The namespace is not the assembly.** What shipped is
  one attribute and one class; the tests are about `dotnet ef dbcontext optimize`, not schema, and
  registering nothing schema-related keeps migrate and scaffold-from-database unavailable without
  having to refuse them. Four further obstacles came out behind it, each named by its own error
  and each real: a `Default` property on `InfoCarrierTypeMapping` (a compiled model *clones* a
  mapping, it does not construct one), `<PreserveCompilationContext>`, the product assembly in
  `AddReferences`, and the server's model — `CompiledModelTestBase.Test` builds the context factory
  **twice** and the second call carries no model customization, so the harness carries the last one
  forward. **Two genuine defects are left and both are filed with probe evidence**: C91, where
  `property.GetValueConverter()` is `null` under a compiled model because EF's generator puts the
  converter on the *type mapping* for a converter configured by instance; and C92, where a complex
  value travels by reflective object shape and so carries a member the model says `Ignore` to. The
  baselines are the third, and they are not a defect — C8 read `AssertBaseline` as returning early
  when the **baselines** directory is missing, and it returns early when the **test source**
  directory is missing. It *creates* the baselines directory. C0's "112 generated files" was right
  in kind; `EF_TEST_REWRITE_BASELINES=1` is how EF writes them.
- **`property.GetValueConverter()` is not the effective converter, and a compiled model is where
  the two part company (C91).** EF's generator emits a `valueConverter:` argument only when it can
  name a converter *type*; under `ForNativeAot` — what `dotnet ef dbcontext optimize` produces —
  it puts the converter on the property's **type mapping**, so a converter configured by
  *instance* (`HasConversion(new BoolToStringConverter("A", "B"))`) vanishes from
  `GetValueConverter()` while one configured by *type* survives. `PrimitiveCoercion` falls back to
  `(FindTypeMapping() as InfoCarrierTypeMapping)?.Converter` — the client's own mapping, where a
  converter can only have come from the model. **The first version of that fallback measured 23
  disagreements where it meant to close 3**, and the instrument that found them is the one to
  reach for whenever the two models might disagree: print, from `WireType`, what *each side*
  computes for *every* property — tagged by whether the mapping is an `InfoCarrierTypeMapping`,
  which is the only side marker needed — and diff the two by name. Twenty-one were primitive
  collections (the mapping's `CollectionToJsonStringConverter` is `JsonForm`'s business, B4) and
  two were `HasConversion<string>()` on an enum (a provider CLR type means the model asked for a
  *target*, not for a converter). The guard is `GetProviderClrType() is null && JsonForm(…) is
  null`: fall back only where the existing rule had no answer at all.
- **A complex value travels by reflective object shape, and the shape is not the model (C92).**
  `OwnedType` declares `public DbContext? Context` and its model says `Ignore(e => e.Context)`; the
  walk sent it anyway. `ToComplexValue` hands the `IComplexType` down so the walk can drop what the
  model does not map — **forward only**, because `RehydrateObject` sets exactly what arrived. The
  hard part is that a complex type **descends through three shapes that are not it**, and the first
  version measured **31, five worse than it started**: the items of a complex collection (right), a
  `KeyValuePair` inside a **property bag** (a bag *is* a `Dictionary<string, object>`, so it takes
  the collection branch), and `Nullable<T>` for an **optional** complex property (`ValueNestedType?`
  presents `HasValue`/`Value`, which no complex type declares). Filter only where
  `ClrType.IsInstanceOfType(value)`, and treat `Nullable<>` as transparent. **The probe was one
  line** — print `DROP <complexType>.<member> kept=[…]` at the point of refusal — and it named the
  `Nullable<>` case in one filtered run where reading the five test names had suggested only
  "something about value types".
- **Spatial works, and the shape of how is worth keeping.** Three pieces, landed and measured
  separately because C9's combined attempt aborted the host: the NetTopologySuite branch in
  `InfoCarrierTypeMappingSource` (C15, worth 19 on its own — the long-standing "needs SpatiaLite"
  note was wrong, and the provider that could not map a `Point` was *the client*); **ADR-012's
  value-mapper seam** (C17); and a **WKT** geometry mapper registered **test-side** (C18), which is
  why the product assembly still does not reference NetTopologySuite. Not GeoJSON — it carries no Z
  or M, which is the v1 defect requirements §2.8 records.
- **`SpatialQuery.Item` is CLOSED (C53), and the three diagnoses before it were all wrong in the
  same way.** It was not null semantics in the residual (C43), not a native dependency (C51), not
  a tier question (C52) — it was **a member declared on a base class the model never names**.
  `MultiLineString`'s indexer lives on `GeometryCollection`; the allowlist admitted a property's
  own CLR type and nothing above it, so the analyzer refused the call and the rewriter shipped the
  whole geometry and indexed it client-side, where `null[0]` throws. `AddPropertyBaseTypes` walks
  the base chain — **base classes only, never a category**, because C23 measured `ValueType`/`Enum`
  widening at 145 → 186. **The rule to carry forward: when a projection lands on the client for no
  obvious reason, probe the boundary verdict before theorising about semantics.** Two probes —
  what the split produced, then which type was refused — replaced three sessions of plausible
  reasoning.
- **Spatial stays Tier A, and moving it is measured-worse (C52): 12 failing on Tier B against 2.**
  `mod_spatialite` does arrive from NuGet with no manual install, and EF's fixture pieces port
  cleanly server-side, so the move is *possible* — it is just worse. If it is ever attempted again,
  the two `Intersects_*` overrides become wrong (SQLite passes the base) and six `JsonException`s
  on geometry conversion are the price to diagnose.
- **The seam is the general answer to "a CLR type the wire cannot walk", and it now has three
  consumers.** A geometry's members recurse (C18), `IPAddress.ScopeId` throws for an IPv4 address
  (C23), and `Uri.AbsolutePath` throws for a relative URI (C34). Three unrelated CLR types, one
  mechanism, all reached by the same reflective object-shape walk.
  **DECIDED 2026-08-11 (C89).** `IPAddress` and `Uri` now ship in the product and are registered
  by `AddInfoCarrierStandardValueMappers()`, which `AddEntityFrameworkInfoCarrier` calls for the
  client and which is **public because the server must call it too** — a value mapped on one side
  only is worse than one mapped on neither. Both are BCL types whose members throw for ordinary
  instances, so an application storing one has opted into nothing. **The geometry mapper stays
  test-side**: shipping it would put a NetTopologySuite dependency in this package, which v1 also
  refused (C12). An application registers its own beside the standard two, and the test project's
  `InfoCarrierNetTopologySuiteValueMapper` is the worked example. ADR-012 carries a dated
  amendment.
- **"The wire cannot handle this type" has two answers and they are not interchangeable** (C34).
  The seam decides how a value is *written*; `ExpressionJsonContext` decides whether the wire can
  carry the result at all — a key value lands in `EntityKeyNode.KeyValues`, declared `object` and
  resolved by runtime type, which the seam never sees. A converted key exercises both, and fixing
  only the first moves the failure rather than closing it.
- **C18's `GeometryCollection` gap turned out not to need the type-level probe** it proposed (C24).
  `ProjectionRewriter` was substituting a `List<T>` for a declared type a `List<T>` does not
  satisfy; one clause fixed it and ADR-012 needed no amendment.
- **`MaterializationInterception` is CLEAR (C71), and the route in is the reusable part.** B16
  answered the design question in 2026-08-09 and C58 priced the optional remedy at "a hand-rebuilt
  `CoreOptionsExtension` plus a per-fixture DI change, reaching at most half". Both of C58's facts
  were true and both were about *intercepting* the forwarding; the answer was to **not forward** —
  one argument in the test class's own `CreateContextFactory`, safe because
  `SingletonInterceptorsTestBase.CreateContext` is the family's only entry point and the
  `onConfiguring`/`addServices` it sets carry nothing but interceptors. 26 fixed, 0 broken, product
  untouched, and `PropertyValues` still green because it registers its server-side interceptor
  itself. **When a remedy is priced as expensive, check whether the price is for the route rather
  than the goal.** **The 27th member closed in C72, and it needed the opposite question.**
  `OptimisticConcurrency`'s fixture registers `F1MaterializationInterceptor` on the server *on
  purpose*: every F1 entity's private constructor ends in `Assert.IsType<…Proxy>(this)`, so the
  model cannot be materialized without one — dropping it measured **21 of 33 failing**, all
  server-side. Only `InitializingInstance`'s `Sponsor.Name` rewrite is non-idempotent, so the
  server gets the **construction half** and not the caller's transform. **When the payload cannot
  be dropped, ask which part of it the server is entitled to.** The design answer below is
  unchanged and still the reason the product forwards nothing: This
  provider is **two EF instances**, and a real deployment must be free to define materialization
  hooks on either side or both — so the three routes B16 measured, each of which suppresses one
  side, may none of them be taken. Nothing in `src/` forwards an interceptor: the server sees the
  user's only because `InfoCarrierBackendTestStore.AddProviderOptions` forwards the client's
  `onConfiguring` for model parity (A49) and it rides along. Each side is individually correct —
  `"Intercepted: Intercepted:"` proves two invocations, `Assert.Same` proves they carry different
  contexts, and B15's fix landing on the *client's* materializer proves the client raises it. **The
  A28 family, one level up**: A28's spec tests assert a materialization limitation this provider
  does not have, these assert a *topology* it does not have. Red and classified.
  **C58 attempted the optional harness remedy and priced it.** Two facts came out of it and both
  are load-bearing: `DbContextOptions.WithExtension` keys the map on `extension.GetType()`, so
  **no subclass of `CoreOptionsExtension` can ever replace it** — B16's hand-rebuild from the
  public `With*` setters is the only route, not one of several. And the family arrives on *two*
  channels, because `SingletonInterceptorsTestBase` passes `useServiceProvider: inject`: half
  through options and half through the server's service collection, which the options route does
  not touch at all. A71's ten `AddInterceptors` failures are the **server's** and the same defect
  as the twelve `Assert.Same`, not a separate one.
- **JSON-mapped owned collections work (B12, TAKEN in C80).** A JSON document carries no key for
  its array elements, so every store synthesizes an ordinal. The client kept the CLR `Id` instead —
  a property the document does not contain — so it was `0` for every element and EF's fixup gave
  each of them to all of them. **Wrong data, no exception.** `InfoCarrierKeyDiscoveryConvention`
  now gives the client the same synthesized-ordinal key, which is `RelationalKeyDiscoveryConvention`'s
  JSON branch over the same public core base. **36 fixed, 0 broken**; `JsonQuery` went 40 → 4.
  The rule it states, and it is narrower than "the client is relational": *where a key shape is
  decided by the caller's own model configuration rather than by the store, the client has to reach
  the same answer as the server.* Nothing relational is resolved from the container, and the
  product already referenced `Microsoft.EntityFrameworkCore.Relational`. **A Cosmos backend would
  need its own clause** — Cosmos recognises an ordinal key by the property's *shape*, not by this
  name. **Reads only** — `JsonQueryTestBase` has zero `SaveChanges`. C81 answered the write half as far
  as it can be answered: `ComplexCollectionJsonUpdateTestBase` is adopted and **18 of 18**, so a
  JSON-mapped collection does survive being written; but `JsonUpdateTestBase`, the base that covers
  **owned** JSON collections, is **unreachable** — its `UseTransaction` is `public void` rather than
  virtual and calls `GetDbTransaction()`, so all **142** of its tests fail on *"Relational-specific
  methods can only be used when the context is using a relational database provider"* before
  reaching anything about JSON.
  **C86 covered it directly, C87 fixed half and C95 closed it — `JsonOwnedCollectionUpdate` is
  5 of 5.** `InfoCarrierDatabase.Expand` now yields, for a JSON-mapped entry, both the **ownership
  chain** (C87 — EF writes a JSON column by partial update of the owning row, whose entry is
  `Unchanged` and never travelled) and **the rest of that owner's document** (C95), as `Unchanged`,
  read off the client's change tracker rather than off the owner's navigations because a *removed*
  element is no longer in its collection.
  **C87's account of what was left was wrong, and the way it was wrong is the lesson.** It read the
  message *"another instance with the key value '{OwnerId: 1, __synthesizedOrdinal: 1}' is already
  being tracked"* as the **server** holding a query-tracked element, and raised "should a server
  context carry query-tracked state into a replay at all" as a design question about context
  lifetime. Two probes refuted it in one filtered run: the server's tracker is **empty** on entry,
  and the conflict is raised on the **client**, in `ApplyGeneratedValues`, applying what the server
  sent back. **"Something already holds this key" names a collision, not a side** — and the stack
  trace had said which side all along. The real defect: `__synthesizedOrdinal` is **positional**,
  `ChangeEntryMapper` sends no navigations, so the owner arrived with an empty collection and EF
  numbered the appended element `1` instead of `3`. **The identity conflict was the symptom; a
  wrong ordinal written into the document was the defect** — C76's shape one level along. This is
  not the "send the whole graph" C37 and C42 each paid for: a JSON column is written as one
  document, and the scan is gated on `GetContainerColumnName()`, so a model with no `ToJson()` pays
  nothing. 0 broken across 22,453.
- **`Query.Associations.*` + `BulkUpdates.*` are adopted and green — 322 of 336 and 251 of 257.**
  The standing "no InMemory counterpart, therefore out of scope" note was the A79 mistake again;
  they are Tier B (C0–C4), and C19/C20 took them the rest of the way. What is left is 14 + 6, all
  classified in C20.
- ~~**CI is broken**~~ — **it is not, and had not been since Phase N** (C39, 2026-08-10). The
  `.sln`-vs-`.slnx` and `~InMemory`/`~SqlServer` claims that stood here described the file as it
  was before `51f4684`; the workflow has restored `.slnx`, run two jobs and invoked
  `eng/ratchet.sh` ever since. What *was* broken was `test/known-failures.txt`, eight months stale
  at `111/5215` against an actual `145/22312` — the gate would have failed on the failure count
  while the total quadrupled. **Read the file before repeating a note about it.**

**`ExecuteUpdate` is the cautionary tale of this phase, and it is about pricing rather than code.**
Three plan entries (C0, C3, C4) recorded it as a wire or boundary change and priced it at 136 —
on the strength of reading `UnreachableException: Can't call this overload directly` as proof that
the split evaluated the call on the client. It *was* evaluated on the client, and the cause was one
missing name on the ADR-008 allowlist: `ExecuteUpdate`'s rewritten call names
`IReadOnlyList<ITuple>`, `Tuple<,>` and `IReadOnlyList<>` were both already admitted, and `ITuple`
was not. **A probe in `QuerySplitter.Split` printing the boundary verdict and `Diagnose(query)`
named it in one filtered run** (C19, 153 closed), and the same probe established in the run before
that `ExecuteDelete` had never been broken at all. The standing probe rule is "establish that the
code *ran*"; point it one step earlier — *where is this being cut* — before pricing a gap.

## Two closed investigations

**The runtime culture is pinned to invariant, and that was a ratchet fix rather than a test fix
(C50).** The machine is `en-SE`, whose decimal separator is a comma, and it cost **nine**
failures — seven in the `_as_GeoJson` family, where EF's own `JsonGeoJsonReaderWriter` re-emits a
number with the culture-sensitive `StringBuilder.Append(reader.GetDecimal())` so `[2.0,4.0]` reads
back as `POINT (2 0)`, and two decimal `InlineData` parameterizations xUnit could not convert.
None was this provider's; EF's own suite failed them identically. **The reason to fix it was that
the suite total was a property of the machine** — the `test/known-failures.txt` baseline CI gates
on was true only here. A `[ModuleInitializer]` now pins `CultureInfo.DefaultThreadCurrentCulture`
before xUnit creates a thread. Nothing is skipped and no assertion is inverted.

**The one intermittent is CLOSED (C38, 2026-08-10), and how it was closed is the reusable part.**
`SqliteSmokeTest.A_store_generated_key_comes_back_on_the_client_entity` failed roughly one run in
four and passed 12-of-12 in isolation. It was **instrumented rather than chased** — C27 makes
`ServerSaveChangesExecutor` rethrow an identity conflict with the whole request appended, and
writes nothing on the happy path, which is the design that matters: the previous attempt wrote a
line per tracked entry and cost 194 extra failures through file I/O under parallel collections.
Sightings 3 and 4 arrived already diagnosed, exactly as intended, and two dumps were enough.

**The cause, and note that the standing hypothesis had the right evidence and the wrong
mechanism.** The range coincidence was real — client placeholders and EF's server-side temporaries
both count down from `int.MinValue` — but nothing was ever misidentified as a borrowed placeholder
(`borrowedReferences=[]` in both dumps). The server was letting **EF's own temporary generator**
run for a key it was about to overwrite with the client's placeholder. Entry 0 takes EF's
`-2147482647` and is forced to the client's `-2147482646`; entry 1 then takes EF's *next* value,
`-2147482646`, and the identity map refuses it. It needs the client's counter to be exactly one
ahead of the server's, which is why it was rare, order-dependent and never reproducible in
isolation. The fix is to not run the generator at all where the value is going to be replaced —
`IValueGeneratorSelector.GeneratesTemporaryValues` answers "does this store issue at save time"
*before* anything is tracked. Full reading in C11, fix in C38.

**The lesson worth keeping: an evidenced hypothesis can be right about the evidence and wrong
about the mechanism.** "The ranges coincide" was correct and load-bearing. "Therefore a stored key
is being mistaken for a borrowed placeholder" was one step too far, and the dump that confirmed the
first half refuted the second in the same four lines. Read what the instrument prints, not what it
was expected to print.
