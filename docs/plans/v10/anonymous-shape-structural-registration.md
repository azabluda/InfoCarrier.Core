# Structural anonymous-shape registration

Owner-approved follow-up to the trusted catalog in [PR #210](https://github.com/azabluda/InfoCarrier.Core/pull/210), 2026-10-08.

## Contract

`AddInfoCarrierAnonymousShapes(params object[] shapes)` accepts anonymous prototypes. Values are
ignored. Names, member order, component types, arrays, and nested structures determine membership;
client compiler identity tokens do not. Named records retain `AddInfoCarrierAllowedTypes`.
Repeated registrations compose. Registration never grants component permissions.

All replacement types are generated from trusted configuration before request execution. Requests
cannot emit types, consume generation slots, or import another server's catalog. Model identity and
actual component runtime identity remain separate checks. Responses retain the request's original
identity within an exchange. Different original identities for the same structure in one exchange
are rejected, because collapsing them could change equality behavior.

## Execution

1. Add failing tests for cross-assembly structural reuse, identity restoration, conflicting identities,
   nested aliases, and unknown structures. Run the focused tests and verify failure causes.
2. Canonicalize structural catalog keys. Deduplicate trusted emission using actual component runtime
   identities; preserve atomic capacity reservation. Bind response descriptors per exchange.
3. Add the prototype registration overload and startup construction. Test repeated calls, input
   snapshots, validation, component permissions, and the transport host.
4. Update user documentation and capacity tests. Explain test-only compiled-code discovery and
   rejection of unregistered shapes. Amend the decision and rolling plan.
5. Run focused tests, the complete regular suite, both slow relational comparisons, transport tests,
   strict Release build, trim gate, package build, documentation checks, and file hygiene. Request one
   fresh review, address material findings, commit, push, and update the existing pull request.

## Review focus

Check equality and identity conflicts, nested descriptor restoration, runtime component identities,
concurrent catalog construction, capacity accounting, startup timing, and unchanged allowlist checks.
No request-controlled generation, client assembly loading, or automatic discovery in application setup.

## Progress

- Planning: complete. Existing branch is authorized; no second checkout is needed.
- Implementation: complete. Cross-assembly reuse and token aliases failed under exact-template
  admission, then passed with structural admission. Prototype API tests first failed compilation
  before the overload existed. An initial cross-assembly assertion compared array references;
  it was corrected to assert original runtime types and contained values.
- Focused anonymous checks: Passed: 35, Failed: 0, Total: 35, including the isolated process ceiling.
- Strict Release build: zero errors and five existing generated Razor warnings.
- Release transport: Passed: 30, Failed: 0, Total: 30. Trim gate: 107 product warnings against 107.
- Both shipping package builds and documentation links/budgets passed. The complete regular measurement reports
  FAILING 0, TOTAL 30120: functional Passed: 29651, Failed: 0, Total: 29885 (234 skipped); document
  store Passed: 235, Failed: 0, Total: 235. No fixed or broken cases; reasons unchanged.
- Complete SQLite live comparison (Release): Passed: 27603, Failed: 0, Total: 27770 (167 skipped).
- Complete Firebird live comparison (Debug): Passed: 109, Failed: 0, Total: 110 (one skipped).
  Debug and Release outputs were separate; no functional process shared an output directory.
- Strict documentation build and changed-file hygiene passed. Implementation and verification are
  complete; the existing pull request will receive the final commit and continuous integration checks.
- Fresh independent review: no confirmed material defect. Minor deferred: mixed prototype and
  explicit catalog registrations depend on call order. Use either prototype registrations or one
  complete explicit catalog per model; startup does not yet reject mixing these modes.

## Delivery

The prototype API matches the owner-approved syntax. Response tokens are exchange metadata, not
permission grants. Unknown structures are rejected; existing client projection reassembly is unchanged.
The test-only manifest reads trusted compiled instructions, rather than running tests or examining
requests. Server catalogs are finite and immutable. Generated types persist for the process lifetime;
exchange membership caches reset automatically and recheck permissions on every resolution.

No additional InfoCarrier diagnostic attributes became unnecessary through structural registration.
The representation feature's single converted-list attribution removal remains the only justified
removal. Regular and both slow comparisons found no further change to results, refusals, or reasons.
