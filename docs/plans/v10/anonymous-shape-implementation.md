# Bounded anonymous-shape protocol implementation plan

The owner selected the bounded protocol on 2026-10-07, following the
[investigation](anonymous-shape-investigation.md). Execute inline in the existing checkout.

Goal: retain anonymous construction and member names during server translation, including wide join
keys, and return values as the caller's original immutable reference types.

Architecture: extend TypeNode with ordered shape members and an opaque original-definition identity.
The client keeps its original type bindings per exchange. The server resolves every component through
the existing allowlist before emitting a fixed immutable data class. No compiler dependency, assembly
load from wire names, reflection admission, or implicit registration of application types is added.
Version the envelope so old peers refuse instead of silently losing metadata.

Bounds: 32 members per shape, 128 characters per member name, 64 shapes per exchange, type depth 16,
and 4096 type-generation attempts per process. Generation stops at the process budget, without eviction
and regeneration. Repeated descriptors reuse types. Generated code exposes only getters, a data
constructor, structural Equals/GetHashCode, and formatting. Type identities remain distinct across
original definitions. Client identity comparisons with its actual Type remain outside this guarantee.

Review focus: recursive forbidden component types; collisions and reordered members; concurrency and
budgets; null/empty/nested results; complete response reconstruction and tracking. Existing private
helpers and unregistered application types retain their boundary.

- [x] Pin production serialization/reconstruction and hardening tests, observe missing-feature failures.
- [x] Implement bounded descriptors, fixed emission, original client bindings, and protocol versioning.
- [x] Preserve anonymous shapes through boundary analysis; retain splitting for client-only behavior.
- [x] Add full wire query promises, including wide joins, nesting, equality, absence, and registration.
- [x] Amend ADR-010/011 and security/protocol documentation with the owner-approved distinction.
- [x] Run focused, regular, transport, slow comparison, strict Release, trim, pack, and hygiene gates.
- [x] Remove only attributes whose recorded differences disappear; retain independent owned coverage.
- [x] Obtain independent code review and address findings. Delivery is one reviewed commit and pull request.

ADR reasoning: the server still lacks client assemblies, so custom behavior remains client-only.
Default-deny deserialization still holds; descriptors generate only server-owned data code after
recursive admission. Client-computed boundaries remain necessary. Tuple replacements lose names and
wide join structure, which the measured draft shows the bounded descriptor can retain. Compiler and
reflection invocation dependencies remain refused. Capture baselines remain forbidden. This is the
owner-approved amendment to anonymous-data representation, not general shape-based type admission.

## Implementation review record

The production emitter uses generic templates rather than closed field signatures. This is required
for registered private records and enums: the initial closed emitter reproduced MethodAccessException
and TypeAccessException in owned tests. Framework EqualityComparer<T>.Default is installed before cache
publication through the public IEqualityComparer interface. Its equality contract is preserved;
matching the compiler's exact hash-code integer is not promised.

Recursive, length-prefixed cache identities include model names even on generic components, ordered
member metadata, and array metadata. Actual runtime component type handles distinguish equal assembly
names across load contexts. Original definition tokens also partition nondefault load contexts.
Descriptors are copied recursively on storage and return. Tests reproduce the initial stale model
identity, raw-name admission, private-component access, and mutable metadata risks.

The exchange rejects its sixty-fifth distinct descriptor on every attempt until reset. A locked
process cache counts generation attempts before emission, including failures. Successful identical
descriptors reuse one type under concurrent resolution. An isolated child test host exhausts the
actual 4096-attempt ceiling without poisoning the regular suite's factory. Separate resolvers share
that ceiling: a new legitimate descriptor is then refused, while cached descriptors remain usable.
Its finite capacity can be exhausted by any caller that can send accepted descriptors. There is no
availability claim beyond the documented bounds, and provider caches can retain emitted types indefinitely.

Owned coverage adds what inherited Entity Framework tests cannot establish: separate wire pipelines
restore the original client types; raw original and generated CLR names fail even in mixed generic
descriptors; changing execution registrations rechecks cached component permission; forbidden reflection
components remain denied; malformed descriptors and depth/member/exchange bounds fail; private records
and enums have valid equality, hash, and formatting behavior; case-distinct and Unicode compiler identifiers, empty objects, nested
arrays/lists, null members, and absent synchronous/asynchronous terminal results retain their meanings.
A nine-member anonymous join executes as one database join with the same normalized live statement as direct EF.

Native anonymous projection also restores backend refusal behavior. SQLite and MongoDB refuse tracking
an ownerless owned value. MongoDB additionally refuses the no-tracking DISTINCT collection projection
without its document key. The document-store test compares both exact refusals against its direct
backend; it does not claim that removing tracking makes that query supported. Both client configuration
variants remain covered. Existing configured/unconfigured shared-helper and converted-list tests remain.

The converter override for Composition_over_collection_of_complex_mapped_as_scalar is removed with
its InfoCarrierDesign attribute. The inherited assertion now sees the original H/W member diagnostic.
No additional attribute was removed from source inspection alone. Review found 27 remaining design
attributes and zero defect attributes; their private-helper, cast-target, buffering, or other reasons
are independent of anonymous data representation. Complete slow runs determine their remaining validity.

Trimming retains one new, documented IL2070 in TypeNodeMapper.ToTypeNode: an arbitrary runtime anonymous
Type does not statically prove its public property metadata is preserved. The baseline rises 106 to 107,
with the exact reason recorded in eng/trim-baseline.txt. The fixed runtime-generated templates' IL2055
is annotated because those unconstrained definitions have no trim-time requirements. Native AOT remains
unsupported; the change introduces no runtime compiler dependency or assembly transfer.

## Final validation

All results below are completed summaries, captured on 2026-10-07. The full regular measurement is
`eng/measure.sh bounded-anonymous-shapes-verified`: **FAILING 0, TOTAL 30081**, with an empty failure
snapshot. It includes the full document-store rerun after replacing the mistaken no-tracking support
claim with direct-backend refusal comparisons.

| Run | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Regular functional project | 29612 | 0 | 234 | 29846 |
| Regular document-store project | 235 | 0 | 0 | 235 |
| Final Release owned projection, statement, and envelope checks | 226 | 0 | 0 | 226 |
| Final Release HTTP transport | 29 | 0 | 0 | 29 |
| Complete SQLite live comparison | 27603 | 0 | 167 | 27770 |
| Complete Firebird live comparison | 109 | 0 | 1 | 110 |

Both live comparisons use INFOCARRIER_LIVE_COMPARE=1 and the complete SQLite/Firebird namespace
filters. They are reports against plain EF, not substitute gates. They also detect attributed
statement/outcome differences that disappear; neither complete run reports reason-without-difference.
No SQL captures or test baselines are committed.

The regular and SQLite snapshots preceded the final Unicode-only identifier broadening. A compiler
projection using combining-mark and letter-number identifiers failed once under the earlier validator.
The correction then passed all 226 owned checks in both isolated build outputs and final Release.
The final Firebird run, strict Release rebuild, trim publish, and package validation use the correction.
This is an additional owned test, not a fabricated increase in the earlier complete-run totals.

The strict Release solution rebuild exits zero with no errors. Its five warnings are the existing
generated Razor trimming diagnostics. The production-project strict rebuild has zero warnings and
errors. Final trimmed publish reports **OURS 107, TOTAL 1140; OK (107 <= 107)**. Release packing both
shipping packages with --no-build passes compatibility validation and publishes nothing. Documentation
links, prose budgets, git diff whitespace, and changed-file hygiene pass.

Local evidence is under artifacts/: `measure/bounded-anonymous-shapes-verified.*`,
`anonymous-owned-release-verified.log`, `anonymous-transport-verified.log`, `anonymous-slow-sqlite.log`,
`anonymous-slow-firebird.log`, `anonymous-release-final.log`, `anonymous-release-core-final.log`,
`anonymous-trim-verified.log`, and `anonymous-pack-verified.log`. These diagnostic files remain ignored.

Independent read-only review found no remaining blockers after the identity, raw-name,
budget, snapshot, private-component, trim, and Unicode corrections. Owner review should assess the
protocol-major upgrade and finite process capacity before merging. No release or merge is requested.

## Security follow-up: type-name depth and shared capacity

The owner approved this follow-up on the current branch after discussing hostile client payloads.
The original descriptor-only depth check accepted a raw `List<List<...>>` CLR name at depth 128.
Four owned cases failed before the fix: depths 17 and 128, mixed descriptor/name nesting, and an
array above the depth boundary. Validation now parses raw CLR names without creating runtime types,
then checks combined raw-name/descriptor depth before any resolver lookup or generic construction.
The framework's [TypeName parser](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadata.typename.parse?view=net-10.0)
has a 1,024-node ceiling and raw names have a 16,384-character ceiling.
Valid depth-16 generic types, generic array FullName output, and depth-16 arrays remain supported.
Additional tests cover assembly-qualified arguments with an unavailable assembly, malformed names,
wide names, and constructed names incorrectly paired with structured arguments.

The isolated capacity test uses the real factory through public resolution, not a reduced test budget
or reflection that resets production state. It emits 4,096 one-member integer shapes using fresh
resolvers, demonstrates repeated refusal for a different caller's new shape, and verifies reuse of
the earlier legitimate shape. Its initial local measurement took 1.305 seconds with a 220,418,048-byte
working-set increase. The measurement excludes transport, queries, and retained-memory collection.
No test asserts a machine-specific time or memory number.

This is a fast, persistent cross-caller denial of new-shape service, not only a theoretical cache
ceiling. Arbitrary valid identity tokens consume the shared budget. Authentication and request rate
limits do not prevent cumulative exhaustion by an admitted malicious caller. The cap bounds emission
but does not isolate callers. Trusted quotas, lifecycle isolation, and broader expression-resource
budgets require separate host design; this patch does not claim those protections. Owner review of
that remaining risk is required before treating the protocol as safe for untrusted client availability.
Descriptor limits do not constrain a complex string passed to an admitted runtime `Type.GetType`
expression call. Existing reflection-pivot tests check invocation admission, not execution resources.

Recommended next design: a host-owned resource policy with trusted caller/tenant identity, generation
admission quotas, and recyclable workers for hard memory/time boundaries. A client-provided identity
must not select its own quota. Raising the process cap changes attack cost, not the failure mode;
cache eviction cannot promise release while provider caches retain types. Broader budgets should cover
expression nodes, generic construction, constants, and runtime type-resolution calls. Cooperative
cancellation alone does not stop every admitted framework operation. This is separate architecture
work, not protection claimed by the current patch.

Independent review found no blocking defect in the depth fix or process-isolated capacity test. One
minor follow-up is clearer captured-output diagnostics when the child test host exceeds its deadline.

### Follow-up validation

All following runs completed on 2026-10-07 and include the final Unicode change and the security
follow-up. `eng/measure.sh type-budget-hardening bounded-anonymous-shapes-verified` reports **FAILING 0,
TOTAL 30089**, FIXED none, BROKEN none, REASONS unchanged. The seven new security cases run in the
regular project, including the isolated process-cap test. No additional attribution is removed by
resource validation; it changes rejection bounds rather than translation.

| Run | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Complete regular functional project | 29620 | 0 | 234 | 29854 |
| Complete regular document-store project | 235 | 0 | 0 | 235 |
| Release HTTP transport | 29 | 0 | 0 | 29 |
| Complete SQLite live comparison | 27603 | 0 | 167 | 27770 |
| Complete Firebird live comparison | 109 | 0 | 1 | 110 |

Both complete live comparisons pass without a reason-without-difference failure. The strict Release
solution rebuild passes with zero errors and the five existing generated Razor warnings. The final
functional-test build also uses strict warning rules and passes. Trim publish reports **OURS 107,
TOTAL 1140; OK (107 <= 107)**. Both shipping packages pass compatibility validation. No captured SQL
baselines, package publication, release, or merge are included. Documentation links, prose budgets,
whitespace, and changed-file hygiene pass.

Local evidence remains ignored under artifacts/: `measure/type-budget-hardening.*`,
`type-budget-focused.log`, `type-budget-release.log`, `type-budget-test-release.log`,
`type-budget-transport.log`, `type-budget-trim.log`, `type-budget-pack.log`,
`type-budget-slow-sqlite.log`, and `type-budget-slow-firebird.log`. The focused log preserves the
initial capacity measurement and a subsequent corrected parser-message assertion; the complete
regular gate is the final behavioral result.
