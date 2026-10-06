# Implementation plan — the rest of the slow run's triage (#182)

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

**Every milestone closed on 2026-08-24, and the issue-driven phases that followed, Q to H, were done
when H4 merged.** Their plan is archived with the milestones' and never edited again. What is open
is the rest of the slow run's triage, below, which #182 holds since #167 closed with 10.2.0. A new phase starts here too, as a section that names the GitHub
issue it serves, with one checkbox per step, ticked in the commit that does the work. Which release
it lands in is decided on the issue, not here.

| Plan | Archive |
|---|---|
| M5 — wire hardening (Phase P) | [`archive/implementation-plan-m5-phase-p.md`](archive/implementation-plan-m5-phase-p.md) |
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |
| Post-10.0, issue-driven (Phases Q, R, V, S, T, U, Y, Z, X and H) | [`archive/implementation-plan-post-10.0.md`](archive/implementation-plan-post-10.0.md) |

## Phase H, continued — the reds the slow run still shows (#182)

H0 to H4 are in the archive. After H29 the slow run of Tiers B and C was red in 21 methods, each a
decision for the owner and deferred: 8 where this provider answers a query plain EF refuses, and 13
others. A step here takes one of them through the same loop as H5 to H33: a promise of our own, seen
red first, the fix in the product, and the slow run again.

- [x] **H34. A filter on a local list of anonymous objects is refused, as EF refuses it.** On the
      branch `live-comparison-anonymous-contains`, on top of H4. The owner, 2026-09-28, choosing it
      out of the 8 for 10.2.0: "lets include in 10.2.0 and we have good (yet slow :)) tool to
      highlight it and a clear guideline how to pin it before fixing". It was the one of the 8 that
      breaks a documented promise: `Where(o => ids.Contains(new { o.OrderID, o.ProductID }))` over
      a captured list of anonymous objects read all 2156 rows of `"Order Details"` and filtered
      here, where plain EF raises `TranslationFailed` and the guide says a filter the server cannot
      run throws. The other seven are projections that run client code, and each answers right
      from the same rows.

      1. **A promise in `ServerParameterizationTest`, seen red first**:
         `A_filter_on_a_local_list_of_anonymous_objects_is_refused_as_EF_refuses_it`. Before the
         fix it failed with "InfoCarrier answered: SELECT "_"."Id", "_"."Title"", a read of the
         whole table, while plain EF refused.
      2. **The fix**: `QuerySplitter`'s guard against client evaluation refuses a client-side
         operator whose row-deciding lambda calls `Contains` on a local collection of a type the
         wire cannot carry. The anonymous type had passed as the type boundary, the exemption
         that keeps a composite `GroupBy` or join key legal, so the `Where` stayed here. The test
         is the collection, not the construction, and a registered element type is left for the
         server's EF to decide. Relational only, as the guard's ordering-key rule is.
      3. **EF's own SQLite override is adopted** for
         `NorthwindAggregateOperatorsQueryInfoCarrierTest.Contains_with_local_anonymous_type_array_closure`,
         with its line range. Slow run of the class and the promise: **Passed: 423, Failed: 0**,
         and the method is off the red list: 20 left.

      Normal run, `eng/measure.sh anonymous-contains h4-cleanup`: **FAILING 0, TOTAL 29958**, the
      one new promise, FIXED none, BROKEN none, REASONS unchanged. `trim-ratchet.sh` OK at
      106 <= 106. `InfoCarrier.Core.TransportTests` **Passed: 28, Failed: 0, Total: 28**. `CI=true`
      Release build after deleting the product's `obj` and `bin`: 0 errors, once a nullability error
      in the new promise was fixed (`Blog.Title` is `string?`, so the local list declares it too).

- [x] **H35. A root Single places its limit after an optional reference join.**
      Query_filter_with_pk_fk_optimization returned the same answer and read count as plain Entity
      Framework Core, but its LIMIT 2 ran before the join. The provider-owned ServerSqlTest promise
      reproduced that subquery and failed before the product change.

      ProjectionRewriter now sends Single over the server tuple and rebuilds its one result on the
      client. Entity Framework Core puts LIMIT 2 after the join. A second promise checks the empty
      and duplicate-result exceptions. The specification override and its design reason are gone.
      The decision is recorded in docs/projection-split.md section 3.4.

      On 2026-10-01, the measured rewrite changed **FAILING 1 to 0, TOTAL 29969**, with one fixed
      promise, no broken tests, and unchanged reasons. The final normal run reports **FAILING 0,
      TOTAL 29970**. The selected slow-run class reports **Passed: 23, Failed: 0, Total: 23**.
      The wider SQLite slow run has 29 failing cases, one fewer than its preceding capture.
      Trim warnings stay at 106; transport tests report **Passed: 29, Failed: 0, Total: 29**.
      The nonincremental CI Release build reports five known Razor warnings and zero errors.

- [x] **H36. A root SingleOrDefault directly after a projection limits after its reference join.**
      `Using_explicit_interface_implementation_as_navigation_works` returned the same answer and
      read count as plain Entity Framework Core, but its LIMIT 2 ran before the join. A new
      provider-owned SQL promise reproduced that shape and failed before the product change.

      ProjectionRewriter now uses a reference tuple when a root SingleOrDefault directly follows a
      reassembled Select with no predicate. It runs the terminal on the server tuple and rebuilds a
      present result on the client. A second
      promise checks empty, present, and duplicate results. No specification override was needed.

      On 2026-10-01, the measured rewrite changed **FAILING 2 to 0, TOTAL 29972**, fixing both new
      promises with no broken tests. The selected slow-run class reports **Passed: 25, Failed: 0**.
      Trim warnings stay at **106**. The design is recorded in docs/projection-split.md section 3.4.

- [x] **H37. A compiled FromSqlRaw query preserves EF's constant arguments.**
      `FromSqlRaw_queryable_composed_compiled_with_parameter` returned the same answer and read
      count as plain Entity Framework Core, but the server bound a parameter where EF emitted a
      literal. Server execution performs a second, ordinary parameter extraction after the client
      has already made the compiled query's constantization decision.

      The entity-root factory protects only constant argument arrays until EF's invocation-removal
      visitor starts preprocessing. Earlier server visitors and parameter extraction retain that
      protection. EF then receives its genuine raw SQL root and formats literals through its own
      type mapping. Ordinary argument expressions remain bound. No wire contract or permission
      changes, and no specification deviation reason.

      Provider-owned differential tests cover synchronous and asynchronous execution, quoted values,
      all three server collection modes, ordinary bound arguments, and two constants on one model.
      The original four compiled cases failed before the change. The review exposed an early
      collection-mode visitor; eight additional cases failed before the lifecycle correction.
      Focused run: **Passed: 14, Failed: 0, Total: 14**. On 2026-10-01,
      `eng/measure.sh h37-product h37-red-own` changed **FAILING 4 to 0**, with the four original
      compiled cases fixed, no broken tests, and unchanged reasons. Expanded collection-mode coverage
      makes the final **TOTAL 29986**. The selected live class reports **Passed: 149, Failed: 0,
      Total: 149**. The related ad-hoc entity `SqlQueryRaw_queryable_composed_compiled_with_parameter`
      also passes through the same entity-root correction. Its live cases and the Firebird tier
      report **Passed: 111, Failed: 0, Skipped: 1, Total: 112**.
      Trim warnings remain **106**; transport tests report **Passed: 29, Failed: 0, Total: 29**.
      The nonincremental CI Release build reports five known Razor warnings and zero errors.

- [x] **H38. A value-converted private subtype keeps EF's SQL cast.**
      `Comparison_with_value_converted_subclass` returned the same answer and read count, but
      widening the constant's private `IPAddress.ReadOnlyIPAddress` type to its public base turned
      EF's conversion into an identity conversion. The server omitted `CAST(... AS TEXT)`.

      The expression translator preserves only a built-in constant upcast affected by that widening,
      with a conversion through `object` inside the original conversion. EF deliberately preserves
      object conversions during parameter extraction and emits its original SQL cast. No inaccessible
      type name, new wire field, public signature, permission, or provider-specific SQL is introduced.
      Public constants and user-defined conversions keep their existing paths.

      A provider-owned address model reproduces the difference in both execution modes. Public-base
      constants are controls against introducing extra casts. The failing full baseline reports
      **FAILING 2, TOTAL 29990**, exactly the two private-subtype cases. Focused candidate:
      **Passed: 4, Failed: 0, Total: 4**. The targeted live run, including all three inheritance
      variants, reports **Passed: 10, Failed: 0, Total: 10**. On 2026-10-01,
      `eng/measure.sh h38-product h38-red-own` changed **FAILING 2 to 0, TOTAL 29990**, with exactly
      the two private-subtype cases fixed, no broken tests, and unchanged reasons. The selected live
      class reports **Passed: 1173, Failed: 0, Skipped: 4, Total: 1177**.
      Trim warnings remain **106**; transport tests report **Passed: 29, Failed: 0, Total: 29**.
      The nonincremental CI Release build reports five known Razor warnings and zero errors.

- [x] **H39. A root one-column projection preserves EF's inheritance union.**
      `Selecting_only_base_properties_on_derived_type` returned the same rows but wrapped EF's
      pruned table-per-concrete-type union in an outer SELECT for the tuple's `Item1` alias.
      The root plain Select now carries one scalar directly and rebuilds from that value.
      Nested, single-result, entity, collection and multi-slot projections keep their tuples.
      A null scalar remains a present row, not an absent result.

      The provider-owned inheritance regression failed in both execution modes before the change.
      The first full candidate fixed both cases and exposed one structural unit assertion expecting
      a tuple rather than a string. Its minimal-payload contract is unchanged; the assertion now
      checks the direct string carrier. The selected live class reports **Passed: 96, Failed: 0,
      Skipped: 4, Total: 100**. On 2026-10-01, `eng/measure.sh h39-final h39-red-own` changed
      **FAILING 2 to 0, TOTAL 29992**, fixing both new cases with no broken tests and unchanged
      reasons. Trim warnings remain **106**; transport tests report **Passed: 29, Failed: 0,
      Total: 29**. The nonincremental CI Release build reports five known Razor warnings and
      zero errors. Independent review found no blocking defect.

- [x] **H40. Register the custom constructor projection before requiring backend parity.**
      The owner reconsidered the refusal guard on 2026-10-06. The original
      `Member_binding_after_ctor_arguments_fails_with_client_eval` lacked registration for its
      `CustomerListItem` result type. With that type available and admitted on both ends, the
      existing server path reaches the actual provider's translation refusal. The harness now
      declares the type explicitly rather than adding a relational-only client guard.

      Own tests distinguish registered backend refusal from unregistered local ordering. The
      registered result has a recorded server translation stack and executes no SQL; the
      unregistered result is sorted correctly on the client after one statement with no ORDER BY.
      The latter is a configuration edge case outside the owner's parity goal. Anonymous ordering
      remains a direct-server control. No upstream source or deserialization security is changed.
      The focused original-method and own-test run passes eight cases. Broader verification is
      recorded by the rebuilt branch's measurements and commit message.

- [x] **H41. A final multi-column shadow projection preserves the inheritance union.**
      `TPCGearsOfWarQueryInfoCarrierTest.Project_shadow_properties` returns the correct values,
      but tuple member aliases introduce an outer SELECT that plain EF does not need. A new
      provider-owned two-column regression reproduces the statement difference in synchronous and
      asynchronous execution, with a nullable regular column and a nullable shadow column.

      EF's relational projection binder uses index-based binding when a constructor has no member
      metadata. The proposed change removes that metadata only from flat final tuples holding
      row-dependent scalar member reads or `EF.Property` calls. Internal projections, absent-row
      reference tuples, collections, closed values, and other expression shapes retain the existing
      construction. Full baseline reports **FAILING 2, TOTAL 29998**, exactly the two new cases.
      A focused control found that a captured parameter also has a member-read shape; requiring a
      reference to a row parameter preserves that parameter's existing SQL binding. Review also
      found that the argument-count check alone accepts exactly eight values with a nested Rest
      tuple; checking every constructed argument's scalar type restores the intended flatness.
      On 2026-10-02, `eng/measure.sh h41-scoped h41-red-own` reports **FAILING 2 to 0, TOTAL 29998**,
      exactly the two new cases fixed, none broken, and unchanged reasons. Final-code live comparison
      passes **1280 cases, with 4 skips, total 1284**, covering the complete concrete-inheritance
      query class, own regressions, and parameter-handling controls. Transport tests pass 29 cases;
      trim passes at 106 <= 106. Nonincremental `CI=true` Release build reports five known Razor
      warnings and zero errors. Independent review approves the corrected guard.

## Phase D — a client projection that holds a captured object is refused, as EF refuses it (#113)

The first issue phase after 10.2.0, taken up on 2026-09-28 in the owner's order for 10.3.0. Its
letter is the first that no earlier phase used.

- [x] **D1. The client refuses a client projection that holds a constant object, with EF's three
      messages.** A query written inside a `DbContext` that calls one of its instance methods
      captures `this` as a constant. EF refuses it (`ClientProjectionCapturingConstantIn*`), because
      its query cache would keep the context alive; this client answered, and the cache kept every
      disposed context that had run the query until the entry was evicted (measured 2026-09-15).

      1. **Four promises in `ServerParameterizationTest`**, each run through the wire and with
         plain EF on the same SQLite store: the instance, argument and tree forms are refused with
         EF's own message, and a local variable, which EF parameterizes, is still answered. The
         three refusals failed first with "InfoCarrier answered".
      2. **The fix is `CapturedConstantValidator`, in two moments.** `QueryExecutor` replaces each
         parameter with its value before the split, which makes a local variable a constant as
         well, so the constants to refuse are found on the tree as captured and looked for, by
         reference, in the residual after the split. A copy of EF's check run on the split tree
         refused the local variable too: the fourth promise failed that way when tried. The rule
         is EF's (null, a type the client's mapping source maps, or an empty array passes), so it
         refuses on every store; plain EF on InMemory does not, and no Tier A test reaches it.
      3. **EF's own test runs unmodified**: the `[InfoCarrierDefect(113)]` override of
         `Inlined_dbcontext_is_not_leaking` is deleted. **EF's SQLite overrides of the three
         `Client_code_using_instance_*` tests are adopted**, with their line ranges: the base
         expects InMemory's answer, they passed while this client answered, and they failed with
         the fix, which is SQLite's refusal.

      With `src/` reverted to `main`, exactly the three refusals and `Inlined_dbcontext_is_not_leaking`
      fail. Normal run, `eng/measure.sh context-capture-2 release-10.2.0`: **FAILING 0, TOTAL
      29962**, the four new promises, FIXED none, BROKEN none, REASONS unchanged; the spec project
      **Passed: 29494, Skipped: 234, Total: 29728** and Tier D **Passed: 234, Total: 234**.
      `trim-ratchet.sh` OK at 106 <= 106. `InfoCarrier.Core.TransportTests` **Passed: 28, Failed:
      0, Total: 28**. `CI=true` Release build after deleting the product's `obj` and `bin`: 0
      errors, the 5 known Razor warnings.

## Phase E — what 10.3 does about a write whose outcome is unknown (#48)

#48 asked for a request id the server records, so that a retried unit of work cannot write twice.
Its design reached two approved sections and a drafted third on 2026-09-28 and 29, and the owner
then set it aside: an application can already do this itself with EF Core's own documented pattern,
a marker row saved with the changes and looked for after a failure ("Connection Resiliency", option
4), and plain EF leaves the same problem to the application. The design is saved on the issue,
which the owner closed on 2026-09-29 once the errors page showed the pattern (#185). What 10.3 does
instead is the part an application cannot do itself.

- [x] **E1. A commit or a rollback the transport loses is not treated as done.** Found while
      designing #48: `InfoCarrierTransaction` marked itself finished before the round trip. A second
      `Commit` after a lost request then reported success without reaching the server, and the
      dispose sent no rollback, so a commit that never arrived left the server holding the
      transaction, its connection and its locks. It now finishes only once the server answered, as
      EF's `RelationalTransaction` clears its state only after the store did. No test could see it:
      every transport in the suite delivers everything, and EF tests a failed commit only in its
      SQL Server suite.

      Four promises in `InMemory/LostTransactionEndTest`, whose transport loses one request or one
      answer on purpose: a lost commit is sent again by the next `Commit`; a commit whose answer was
      lost makes the next `Commit` throw rather than report success; a lost commit or a lost
      rollback is rolled back on the server by the dispose. With `src/` reverted to `main`, exactly
      these four fail. Normal run, `eng/measure.sh lost-commit context-capture-2`: **FAILING 0,
      TOTAL 29966**, FIXED none, BROKEN none, REASONS unchanged; the spec project **Passed: 29498,
      Skipped: 234, Total: 29732** and Tier D **Passed: 234, Total: 234**. `trim-ratchet.sh` OK at
      106 <= 106. `InfoCarrier.Core.TransportTests` **Passed: 28, Failed: 0, Total: 28**. `CI=true`
      Release build after deleting the product's `obj` and `bin`: 0 errors, the 5 known Razor
      warnings.

The other half, `guide/errors.md` showing the marker pattern instead of saying that an update has no
remedy, corrects what the shipped release does, so it starts on `release/10.2` and is merged up. It
has no checkbox here, because the plan on that branch predates this phase.

## Phase F — an entity with a property-bag complex type is inserted (#52)

Taken up by the owner on 2026-09-29, replacing the handoff's "only if EF fixes it or a user reports
it". J22 had priced the route around EF's materializer defect as reproducing constructor binding.

- [x] **F1. The server inserts an entity whose complex type is a property bag holding a list.** Two
      layers, the second hidden by the first.

      1. **EF's defect** (`docs/upstream-defects.md` 1.1): the full materializer fills a bag's
         primitive-collection member through `Expression.Property` on the `Item[string]` indexer.
         `ServerSaveChangesExecutor.Materialize` now builds such an entity with EF's own
         `GetOrCreateEmptyMaterializer`, which binds the constructor and fills no member, then writes
         the values the full materializer would have. Only where the constructor takes no property
         value, and only for an entity with a property-bag complex type.
      2. **Ours**: a bag is a dictionary of `object`, so the wire walked it as one and a
         `List<string>` member arrived as a `List<object>`; EF's snapshot then threw
         `InvalidCastException`. The server restores each bag member's type from the model, through
         complex collections and nested complex values, rebuilding a list through its JSON form with
         EF's own reader/writer service.

      **Plain EF cannot read such an entity either**: its query shaper takes the same branch. So the
      read stays failing, as in plain EF, and a test asserts the two fail alike.

      Two promises in `Sqlite/PropertyBagComplexTypeTest`: the insert, checked with SQL against the
      columns the model names, and the read failing where EF Core 10 fails. EF's
      `Can_track_entity_with_complex_property_bag_collections` runs with no override, and no
      `[InfoCarrierDefect]` is left in the suite. With `src/` reverted to `main`, exactly the insert
      and EF's two `Added` cases fail. Normal run, `eng/measure.sh property-bag lost-commit`:
      **FAILING 0, TOTAL 29968**, FIXED none, BROKEN none, REASONS unchanged; the spec project
      **Passed: 29500, Skipped: 234, Total: 29734** and Tier D **Passed: 234, Total: 234**.
      `trim-ratchet.sh` OK at 106 <= 106. `InfoCarrier.Core.TransportTests` **Passed: 28, Failed: 0,
      Total: 28**. `CI=true` Release build after deleting the product's `obj` and `bin`: 0 errors,
      the 5 known Razor warnings.

## Phase Q, resumed — cancellation over a real socket (#55)

The owner reopened the real-socket question for 10.3.0 on 2026-09-30. The earlier decision remains
in the archive; this step tests the missing integration without a sleep or a slow database query.

- [x] **Q3. Cancelling a query over a real socket stops server execution.**
      `ServerCancellationOverSocketTest` uses the existing Northwind host with Kestrel bound to a
      dynamically assigned loopback port and HTTP/1.1. An ordinary query succeeds first. A command
      interceptor then signals entry and waits on the real server-side cancellation token. Only
      after that signal does the client cancel. The test checks both command cancellation and exit
      from the real request pipeline before cleanup releases the interceptor. The endpoint,
      envelope dispatch, query executor and Entity Framework command path are not replaced.

      Deadlines prevent hangs; they do not order events. This proves cancellation reaches server
      execution at the command boundary, not that every database driver interrupts a running native
      command. No product behavior, shared fixture or package dependency changes.

      With the query executor changed temporarily to pass `CancellationToken.None`, the test failed
      after reaching the command: it timed out waiting for server-side cancellation. Restored, it
      passed in 2 seconds. `InfoCarrier.Core.TransportTests` **Passed: 29, Failed: 0, Total: 29**.
      `eng/measure.sh cancellation-real-socket`: **FAILING 0, TOTAL 29968**. `CI=true` Release build:
      **0 warnings, 0 errors**.

## Branches that outlive their pull request

**A branch that survives a merge is a branch nobody records, and until 2026-09-20 none of these
was named in any document.** Work branches are deleted when their pull request merges, by the
repository setting rather than by hand, so only two prefixes live here: `park/` for work that may
resume, `archive/` for work that will not. A machine branch keeps whatever name its workflow types.

| Branch | What it holds | What would revive it |
|---|---|---|
| `park/r77-relational-client-store` | A fixture may opt into a relational client test store (#56). Six adopted bases re-declare `TestStore` as `RelationalTestStore` and 25 tests threw `InvalidCastException`; the branch splits that type into its string half, which is harmless, and its connection half, which would reach the database past the wire | Adopting a base whose tests need the string half. The connection half never ships: a green that reaches the store says nothing about this provider |
| `park/d7-streaming-half` | Streaming results over the HTTP transport, complete and measured green, parked 2026-08-17 because its cost is countable and none of its headline benefits was ever measured | A measurement: peak memory, payload size, time to first byte or throughput. Not an argument from reading the code, which is the error its commit message exists to correct. Renamed from `streaming/d7-half-a-parked` on 2026-09-20 |
| `archive/v5` | The v5 line, last touched 2026-06-28. Kept for history only | Nothing. Renamed from `v5/abandoned` on 2026-09-20 |
| `badges` | Written by `build.yml`, which clones it by name and pushes the EF parity figure that shields.io reads for the README | Not a work branch. Do not rename it: the workflow names it in four places |

**The branches of #167 were deleted on 2026-09-28, at the owner's request.** The 26 step branches of
Phase H were merged into `main` with every commit, so nothing of theirs is lost. Three were never
merged, and their last commits are named here so that a clone still holding them can restore them:
`experiment/direct-baseline` at `7b628da`, the prototype #167 was opened on;
`experiment/live-comparison` at `514a691`, the spike ADR-014's amendment of 2026-09-26 measured; and
`sql-capture` at `9f0ef88`, the withdrawn capture files, which PR #170 also keeps.
