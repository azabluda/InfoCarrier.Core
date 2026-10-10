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

## Phase I — ordinary type-descriptor complexity ([#211](https://github.com/azabluda/InfoCarrier.Core/issues/211))

- [x] **I1. Bound descriptor complexity before runtime type construction.** Independent delivery
      for [issue #211](https://github.com/azabluda/InfoCarrier.Core/issues/211), based on merged main.
      Preflight the entire descriptor before cache-key traversal and runtime lookup, with combined
      depth 16, cumulative parser nodes 1024, and names up to 16,384 UTF-16 characters. Preserve
      whole-type registration and cache permission checks. Owned adversarial tests cover raw and
      structured nesting, total breadth, malformed input, element modifiers, and boundary behavior.
      No anonymous-type generation or wire-version changes belong to this step.

      Validated 2026-10-08: regular measurement **FAILING 0, TOTAL 30079**, no fixed or broken tests,
      unchanged reasons. Slow SQLite **Passed: 27599, Failed: 0, Total: 27766**; slow Firebird
      **Passed: 109, Failed: 0, Total: 110**. Transport **Passed: 29, Failed: 0, Total: 29**.
      Nonincremental strict Release build: zero errors, five known Razor warnings. Clean trim gate:
      106 <= 106. Focused descriptor/security tests **Passed: 50, Failed: 0, Total: 50**; the pre-fix
      run failed 13 of 14 cases. Independent review found no blocking issue.

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

- [x] **H42. Separate registered JSON index parity from private-helper behavior.**
      Rebuilt on 2026-10-06 after the owner's configuration clarification. Public shared index
      helpers registered on both ends reach the backend's translation refusal, with a recorded
      server stack and no SQL. Private helpers remain local; own tests pin correct root and nested
      results and the statement count. Row-computed and mapped-function indexes retain direct
      provider controls. The JSON-specific early refusal guard is not replayed.

      The two inherited upstream helpers are private. Their unchanged bodies retain upstream
      refusal references plus ADR-008 `AnswerNotRefusal` reasons. This attributes two recorded
      methods, fixes none, and leaves three known unresolved methods before the final census.
      Verification is recorded by the rebuilt measurement and commit message.

- [x] **H43. Separate shared public UNION parity from private local projections.**
      Rebuilt on 2026-10-06. Registered public helpers reach the actual backend: SQLite refuses
      before executing SQL. InMemory instead produces its own operand-dependent failures, preserved
      through the wire with a recorded server stack rather than a client-side relational diagnostic.
      Private unregistered helpers retain local execution, pinned by own operand and sync/async
      cases with two SELECT statements and no server UNION. Valid constructed scalar inputs retain
      a server UNION in one statement.
      The relational-only client-method UNION guard is not replayed, and existing filtering and
      collection-identity guards remain unchanged.

      The upstream helper is private. Its unchanged inherited body retains SQLite's refusal
      reference and an ADR-008 `AnswerNotRefusal` reason. This attributes one recorded method,
      fixes none, and leaves two known unresolved methods before the final census. Verification
      is recorded by the rebuilt measurements and commit message.

- [x] **H44. Changes to separate owned branches write their shared row once.**
      `Save_changed_owned_one_to_one` sends two UPDATEs where plain SQLite EF sends one.
      A provider-owned two-branch model reproduces the difference for edits and replacements,
      synchronously and asynchronously: all four cases fail before a product change.
      EF's `SharedTableEntryMap.GetMainEntry` follows tracked principals, so omitting the
      unchanged common owner gives the branches separate modification commands.
      Send only each changed entry's same-table ownership chain, not all owned siblings.
      Verify the own cases, the graph-update class live, the full suite, Release build,
      trim, transport, and review. No attribution is planned.

      On 2026-10-06, the first full candidate passes **FAILING 0, TOTAL 30023**, with no
      broken cases or changed failure reasons against the committed H43 measurement. The own
      red evidence is the focused four-case run, not a fabricated full-suite red snapshot.
      The final own matrix has ten passing cases: shared-row edits and replacements, separate
      table controls, and leaf-only changes through unchanged intermediate owners. It checks
      SaveChanges counts, stored values, and untouched owner values. Live comparison passes
      **1777, failed 0, skipped 6, total 1783**; transport passes 29; trim passes at 106 <= 106;
      the nonincremental Release build has five known framework warnings and no errors.
      Review approves the bounded expansion, leaf-only controls, and architecture amendment.
      Final full measurement with both added controls reports **FAILING 0, TOTAL 30025**,
      with no broken cases or changed failure reasons. This fixes one recorded slow-red method,
      attributes none, and leaves one known method before a fresh full slow-run census.

- [x] **H45. An ad hoc raw query does not inherit the mapped type's query filter.**
      `Ad_hoc_query_for_shared_type_entity_type_works` adds a mapped filter where plain EF
      reads the raw view directly. A populated provider-owned keyless model exposes missing
      rows: four ad hoc cases fail, while four mapped FromSql controls pass, covering sync,
      async, and composition. Preserve EF's ad hoc root identity in an optional wire field
      and rebuild that root through the existing ad hoc mapper, even when its CLR type is
      also mapped. Keep both SQL execution grants and type admission unchanged. Measure the
      full red baseline and candidate, run live shared-type and raw-query controls, and check
      Release, trim, transport, package validation, and review. Then run a fresh full slow
      census for SQLite and Firebird before claiming the complete queue is resolved.

      On 2026-10-06, the full baseline reports **FAILING 4, TOTAL 30033**, exactly the four
      populated ad hoc cases. The first candidate fixes all four with no broken cases.
      Review found the sibling constant-query-root path also needed the marker. Its own
      ad hoc serialization case fails before that correction; the mapped control passes.
      Both paths corrected, all ten focused cases pass. Final full measurement reports
      **FAILING 0, TOTAL 30035**, fixing the same four populated regressions with no broken
      cases; the four collection-equality failures disappear from the reason summary.
      Broad live comparison passes **1532, failed 0, total 1532**; final-code focused live
      comparison passes **11, failed 0, total 11**. Final transport passes 29;
      trim passes at 106 <= 106; package validation passes; the final nonincremental Release
      build has five known framework warnings and no errors. Review approves both paths.
      This fixes one recorded slow-red method and attributes none. The fresh SQLite census
      reports **passed 27571, failed 2, skipped 167, total 27740**; both failures name
      `Select_distinct_Select_with_client_bindings`. Firebird reports **passed 109, failed 0,
      skipped 1, total 110**. One newly observed method remains, to investigate next.

- [x] **H46. Preserve the DISTINCT boundary before a client projection.**
      The fresh H45 census finds sync and async `Select_distinct_Select_with_client_bindings`:
      identical rows, but the carrier removes the outer SELECT over the distinct subquery.
      A provider-owned computed-year model reproduces both failures; non-DISTINCT controls
      pass. EF's relational TranslateSelect pushes a distinct source into a subquery unless
      the selector is an identity. H39's direct scalar carrier turns the client projection
      into exactly that identity. Preserve a nonidentity carrier when its fragment is the
      scalar row parameter; keep direct scalar column carriers elsewhere. Measure red baseline and candidate, verify
      focused and full live comparisons, Release, trim, transport, and review. No attribution
      is planned. A fresh complete SQLite and Firebird census must establish the final queue.

      On 2026-10-06, the four-case focused run reports **failed 2, passed 2, total 4**;
      the full red baseline reports **FAILING 2, TOTAL 30039**, exactly the own DISTINCT cases.
      Two added ordered-DISTINCT controls pass before the change. With the single guard
      correction, all six focused cases pass, and live comparison passes **8, failed 0,
      total 8**, including the selected upstream method's two cases. Review approves the
      bounded nonidentity carrier without adding a source-operator scan. Transport passes 29;
      trim passes at 106 <= 106; the nonincremental Release build has five known framework
      warnings and no errors. The full candidate reports **FAILING 0, TOTAL 30041**, fixing
      exactly the two baseline DISTINCT cases, with no broken cases and unchanged reasons.
      The final full SQLite live census reports **passed 27581, failed 0, skipped 167,
      total 27748**. Firebird reports **passed 109, failed 0, skipped 1, total 110**.
      Both complete comparison runs have no failures. This fixes one newly observed slow-red
      method, attributes none, and leaves zero slow-red methods in the fresh census.

### Helper documentation, 2026-10-06

- [x] **H48. Explain shared-helper registration and local-only differences to consumers.**
      The querying guide replaces its blanket refusal claim with the actual backend boundary,
      shows registration on both ends, and distinguishes public shared helpers from private or
      client-only helpers. The limitations page names the JSON-index and UNION examples without
      promising backend parity for them. Registration still never admits private methods.
      The querying word budget moves to 1200 for these required facts. These pages remain unpublished
      until the owner explicitly deploys the documentation from a release branch.

### Fixture configuration review, 2026-10-07

- [x] **H49. Remove attribution only where correct fixture configuration removes the difference.**
      The owner authorized this follow-up after H48. The concurrency-detector fixtures and the
      nonshared miscellaneous-query harness now grant raw SQL on both halves. Their inherited
      `FromSql` and namespace-collision tests run unchanged. The Northwind bulk-update fixture
      explicitly registers the public `TestExtensions` helper on both halves, so the backend
      refuses the invalid setter with Entity Framework Core's own diagnostic. These four design
      reasons and their overrides are removed after observing the unconfigured tests fail and
      the configured tests pass the live comparison.

      The nonshared harness also accepts explicit allowed types. The advanced-mapping fixture
      registers `Context18087.IDummyEntity`, which moves the cast refusal to the backend. Its
      reason remains because the diagnostic names `@Value` where direct EF names `@id`; it no
      longer claims an earlier refusal. The test checks the carried backend stack. The converted
      collection's existing `Layout` registration still leaves a tuple-versus-anonymous diagnostic
      difference, so that reason remains too.

      The remaining reasons were reviewed for configuration remedies. Private helpers, queryable
      result validation, buffering, client model services, command-cache and schema ownership,
      absent client connections, nonvirtual transaction helpers, tier compliance, and interceptor
      object identity do not become equivalent through additional type registration. Existing
      shared-public-helper and private-helper regression coverage is retained. No production
      behavior, allowlist inference, upstream reference clone, or release workflow changes.

      Validation on 2026-10-07: the full measured suite, the focused live comparisons, the Release
      warnings-as-errors build, transport tests, trimming ratchet, document links, and file hygiene
      all passed. The Firebird live comparison required an unrestricted native-filesystem retry.
      Review also corrected an obsolete client-refusal comment without changing production code.

      Owner review added an explicit requirement to retain owned configuration-boundary tests.
      `ServerParameterizationTest.Configuration.cs` now pins the client setter diagnostic without
      helper registration and the backend selector diagnostic with registration. It also pins the
      earlier cast refusal without target registration and the backend filter refusal with it.
      Existing `SqliteSmokeTest` raw-SQL grant tests and the registered/unregistered converted-list
      tests already cover the other boundaries; those remain unchanged. The owned pins complement
      the restored inherited tests rather than restoring their obsolete overrides.

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

## Phase G: bounded anonymous-shape reconstruction ([#209](https://github.com/azabluda/InfoCarrier.Core/issues/209))

- [x] **G1. Preserve anonymous data through backend translation and restore original client types.**
      The owner selected the bounded generated-shape protocol on 2026-10-07 after the
      [investigation](anonymous-shape-investigation.md). The
      [implementation and validation report](anonymous-shape-implementation.md) records bounds,
      security review, owned promises, removed attribution, and final gate results.
      Protocol major 2 refuses old peers. Application types still require explicit registration;
      custom behavior still belongs on the client. Regular measurement reports FAILING 0, TOTAL 30081;
      final owned checks pass 226 and transport passes 29. Complete SQLite comparison reports
      Passed 27603, Failed 0, Skipped 167, Total 27770; Firebird reports Passed 109, Failed 0,
      Skipped 1, Total 110. The report distinguishes the later Unicode-only correction and records
      the documented trim increase 106 to 107. No release work is included.

- [x] **G2. Close raw-name type-depth bypass and measure process-capacity exhaustion.**
      Continue on the same branch at the owner's request. Validate CLR names before lookup and
      runtime type construction, preserving valid generic arrays and exact whole-type registrations.
      Pin raw, structured, mixed and malformed-name cases. Exhaust the real factory in an isolated
      child test host and document consequences for another caller without poisoning the suite.
      Shared capacity remains an availability risk; host quotas/isolation are a separate design.
      Run regular, transport, complete slow comparisons, strict Release, trim and documentation gates.
      All gates pass; the [report](anonymous-shape-implementation.md#follow-up-validation) records
      complete results. The shared-cap attack took 1.305 seconds locally and remains a documented
      cross-caller availability risk, not protection delivered by this depth fix.

- [x] **G3. Restrict generated anonymous data to trusted server catalogs.**
      The owner chose finite server registration on 2026-10-08 for
      [issue #212](https://github.com/azabluda/InfoCarrier.Core/issues/212).
      The [catalog contract and validation report](anonymous-shape-catalog.md) records the
      compatibility change, admission boundaries, configuration, and measured gates.
      Requests cannot spend generation capacity, reuse another catalog's global entries,
      or register themselves through original-type mapper bindings. Component permissions
      and actual model/runtime identities remain independent requirements. Preserve the
      original investigation notes until the owner deleted them. The separate ordinary descriptor
      hardening from [pull request #213](https://github.com/azabluda/InfoCarrier.Core/pull/213) is merged.

- [x] **G4. Register anonymous structures using server prototypes across client assemblies.**
      The owner approved `AddInfoCarrierAnonymousShapes(params object[] shapes)` on 2026-10-08.
      The [structural registration plan](anonymous-shape-structural-registration.md) records
      structural membership, per-exchange identity restoration, startup construction, and tests.
      Named records retain the existing type permission registration. Component permissions,
      recursive limits, model binding, and request-independent generation remain mandatory.

## Phase LOG: safe server failure diagnostics ([#214](https://github.com/azabluda/InfoCarrier.Core/issues/214))

The owner approved the audited issue and requested implementation in this session on 2026-10-08.

- [x] **LOG1. Add safe request diagnostics and observe transaction cleanup.** Initial implementation
      committed in the logging pull request. Superseded by the owner's simplification below.
- [x] **LOG2. Replace shared diagnostics with injected class loggers.** Approved on 2026-10-09.
      Remove ambient context, exception markers, counters, suppression, caller hashing, and custom
      registration. Keep safe boundary messages, existing contracts, independent resource cleanup,
      and pending-cleanup shutdown coordination. Preserve server SQL logging. The
      [logging policy](server-failure-diagnostics.md) records ownership, severity, and the explicitly
      narrowed scope. Run transport, specification, strict Release, trim, package, and document gates.
      Update the existing pull request and stop while checks run.
- [x] **LOG3. Surface logger configuration failures.** The owner rejected silent logger acquisition
      fallbacks on 2026-10-09. Require adapter logging services and propagate resolution/creation
      failures. Preserve explicit legacy constructors and protect fault delivery/resource cleanup
      from logging emission failures. Reverse both acquisition regressions, run the required gates,
      and update the existing pull request.

- [x] **LOG4. Add bounded reasons and exception type diagnostics.** Approved on 2026-10-10 after
      the owner tested the missing-registration sample failure. Identify that refusal with a
      fixed reason and explanation without changing its exception or wire fault. Other mapped
      faults report an unclassified reason and runtime exception type, without exception contents.
      Test diagnostic output, privacy, and preserved contracts. Preserve the owner's temporary
      sample failures uncommitted, verify normal samples separately, and update the existing pull request.

## Phase CL: client failure logging ([#215](https://github.com/azabluda/InfoCarrier.Core/issues/215))

The owner requested an implementation plan on 2026-10-10. The
[audit](client-logging-audit.md) records requirements; the
[implementation plan](client-logging-plan.md) records the approved work and the revision following
comparison with Microsoft's provider diagnostic conventions.
Chained deployments use ordinary loggers and may retain duplicate logs rather than add coordination.

- [x] **CL1. Add safe client exchange observations.** Preserve constructors and metrics; add a typed
      logger overload with bounded local phases and privacy tests.
- [x] **CL2. Observe client preparation and result processing.** Use existing injected Entity Framework
      diagnostic loggers and dispatch through ordinary logging, simple logging, and diagnostic sources.
      Cover synchronous/asynchronous paths, deferred projections, mapping, generated values, warning
      configuration, and original exception preservation.
- [x] **CL3. Validate chaining and wire sample loggers.** Exercise actual chained store contexts,
      forwarding grants, scope isolation, and normal console and Blazor behavior.

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
