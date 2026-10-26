# Wire versioning: issue 50 completion and migration follow-up

Owner decisions recorded 2026-10-26. This document defines the scope of
[issue #50](https://github.com/azabluda/InfoCarrier.Core/issues/50). Implementation uses wire
contract 3; verification evidence is recorded in the [implementation plan](../v10/implementation-plan.md).

## Accepted completion boundary for issue 50

**Issue 50 ships no migration mechanism.** Its deliverable is a compatibility policy,
explicit version identification, consistent compatibility checks, refusal diagnostics, and tests
of those checks. It does not convert older wire contracts into newer contracts.

**All pre-issue-50 client payloads are rejected explicitly**, including payloads from version 11
development builds made before this change. Old envelope requests and unversioned streamed query
requests must not fall through to current execution. Missing new contract identification is a
compatibility refusal, not permission to assume the current contract. This establishes the initial
version 11 contract boundary without introducing migration. The previous-minor promise applies to
official releases under the policy, not these earlier development builds.

The supported package window is the current minor and one previous minor within version 11.
There is no compatibility requirement between version 11 and version 10. An 11.0 server supports
11.0 clients; an 11.1 server supports 11.1 and 11.0 clients. The window is release-based, not a
fixed number of days.

Package support and wire compatibility are separate. A wire contract includes field meanings,
defaults, execution guarantees, and response behavior, not just a schema. Several package releases
can share one wire contract. A schema comparison or matching version number is not proof of
compatible behavior. Independent integer wire versions identify the contract. Wire contract 3 is
carried in buffered envelopes, streamed query requests, and response start frames. Missing request
identification is zero, never an implicit current version. Clients check response identification
before interpreting faults or results.

Streamed HTTP queries use `InfoCarrier-Stream-Operation: Query`. The pre-policy
`InfoCarrier-Operation` selector is refused. A new selector is necessary because an old streaming
endpoint would otherwise ignore the added body version and execute the request. Without the old
selector, that endpoint attempts to read an envelope and refuses the query body's missing required
operation and payload. This is refusal behavior, not conversion or legacy execution support.
For the new selector, the `InfoCarrier-Protocol-Version` request header is checked before parsing
the query body, then the body version is checked independently. This preserves explicit refusal
even when an unsupported future contract has a query body the current serializer cannot read.

The server accepts only contract 3; it does not infer a package version from that number or require
a package version field. Older packages outside the support window may still interoperate when
they use the same contract, without a support guarantee. A newer client using a different contract
is refused; a newer package using the same contract may work without a forward-compatibility
promise. Changes to structure or semantics that would invalidate a supported minor still need the
separate migration work or an explicit owner decision.

HTTP refusals use status 400, a plain text diagnostic, and `InfoCarrier-Protocol-Version` plus
`InfoCarrier-Client-Protocol-Version` response headers. The updated HTTP transport raises
`InfoCarrierProtocolException` with both versions and upgrade guidance. Buffered dispatch and
stream-frame validation use the same exception. Older clients and servers retain their existing
exception behavior; no diagnostic adapter for legacy servers is introduced.

Issue 50 is complete only when all of the following hold:

1. The written policy defines the package support window, the version 10 boundary, and the
   distinction between supported package releases and accepted wire contracts. It states exactly
   which combinations are accepted or refused, including newer clients and older packages sharing
   a supported wire contract; unapproved proposals are not treated as requirements.
2. Buffered and streamed requests identify their contract consistently. Unsupported contracts are
   refused before operation execution. Clients check the applicable response contract before
   interpreting results or server faults. Refusals are distinguishable from transient connection
   failures and provide the versions and upgrade guidance needed to diagnose the mismatch.
3. Written evolution rules cover semantics as well as structure: preserve existing field names,
   types, and meanings; use new numbers for new node kinds; give additive fields safe defaults;
   change the contract version when a new field or interpretation carries a new promise.
4. Meaningful tests cover accepted current combinations, rejection boundaries, diagnostics, and
   absence of operation effects after refusal, on buffered and streamed paths. Existing sample-host
   and transport-test infrastructure is preferred over a new compatibility framework. Explicitly
   exercise pre-issue-50 buffered envelopes and streamed query payloads; neither may execute. The
   updated client must report the refusal as a permanent compatibility failure. An unchanged older
   client retains its existing exception behavior, but the server's refusal must still be clear.
5. Pre-issue-50 refusal is tested with a bounded set of representative buffered and streamed
   request payloads, including the request headers needed to select each path. Payloads may be
   captured from any suitable pre-change client or constructed from the verified old contract.
   No older client build, separate client process, or published version 10 package is required for
   these refusal tests. Assert the compatibility diagnostic and that no operation executes, rather
   than merely asserting a generic parsing error. Published-client acceptance coverage remains
   required from 11.1, when a published 11.0 client exists; that uses the actual released client
   package and separate processes. Refusal payload fixtures are not acceptance evidence.
6. The design records, implementation plan, and user documentation match the implemented behavior.
   Relevant focused tests and repository-required product, transport, build, package, documentation,
   and hygiene checks pass. The focused pull request is ready for review; merging and publishing
   require separate owner authorization.

This scope excludes request or response conversion adapters, migration chains, version-specific
execution emulation, a general migration framework, speculative adapter extension interfaces,
published schema discovery, and a metadata endpoint. It also excludes a manufactured breaking
change or retained legacy path whose only purpose is to demonstrate migration. Existing paths
needed by the current product are not reclassified as a proven migration design.

The previous-minor guarantee still applies when later wire changes are proposed. Issue 50 does
not authorize accepting an older contract with changed meaning, or refusing a supported minor
merely because conversion is inconvenient. A later change must preserve the supported behavior,
wait for the migration follow-up, or obtain an explicit owner decision revising the promise.

## Why migration design is deferred

There is no representative required migration at this stage against which to evaluate a mechanism.
A synthetic conversion could establish that one example runs, but would not provide sufficient
evidence of semantic correctness, sound software design, or sufficient flexibility for the actual
changes. Issue 50 therefore makes no claim that a migration mechanism has been designed or proven.
Version checks and refusal tests do not establish that future conversions will be possible.

## Follow-up story: design and validate migration against real contract changes

Status: planned, not implemented; no GitHub issue has been created. This is separate work after
issue 50, to be evaluated before the first stable version 11 publication.

**User story:** As a maintainer supporting the approved client window, I need a migration design
tested against actual contract changes, so that older supported clients retain their promised
behavior and impossible conversions are identified explicitly.

### Entry condition and dependencies

A concrete change from [Entity Framework Core 11 adoption, issue #99](https://github.com/azabluda/InfoCarrier.Core/issues/99),
[stricter client evaluation, issue #220](https://github.com/azabluda/InfoCarrier.Core/issues/220),
or another owner-approved change must expose a real compatibility problem. Neither named issue
is assumed to require wire changes, nor is either assumed migratable. Start once a representative
change is available; completion of both issues is not required. Investigation and sequencing can
be planned earlier, but mechanism design and implementation need the concrete change.

Identify the pre-change source commit so that the earlier client can be rebuilt when needed;
there is no requirement to preserve build artifacts in advance. Use separate client and server
processes for migration experiments. A locally built prerelease client is valid experimental
evidence, but does not replace the published-client acceptance obligation or create a public
prerelease compatibility promise. Version 10 support remains excluded.

### Acceptance criteria

1. Identify the actual structural and semantic differences, including response behavior and
   behavior performed on the client. Determine what can be converted, what needs retained execution
   behavior, and what cannot be preserved. A server converter cannot repair arbitrary work already
   performed locally by an older client.
2. Design and implement only the mechanism the real change requires. Locate responsibility using
   the measured change; do not assume generic adapters or migration chains are the answer.
3. Run the unchanged earlier client against the changed server. Verify results, database effects,
   errors, transaction guarantees, and applicable streaming behavior. Include a case that would fail
   if changed semantics were silently interpreted as the current contract.
4. Review correctness, design quality, and flexibility against the actual examples. Account for
   production code size, duplicated execution paths, normal-path overhead, diagnostics, testing,
   response conversion, and removal when the supported window advances. Record evidence and limits;
   one successful conversion does not prove arbitrary future migrations.
5. Define contract retirement and prove its removal behavior. If migration is impossible, document
   the alternatives: retain old behavior, defer the change, or seek an explicit change to the
   compatibility promise. Clear refusal alone does not fulfill support for an older minor.
6. Update the versioning design and release-facing compatibility statements from the findings.
   If neither candidate produces a representative migration, return to the owner before stable
   version 11 publication; do not claim the migration design was validated.

On 2026-10-26 the owner explicitly allowed representative old payload examples for refusal tests.
These small fixed inputs test the rejection boundary; they are not generated output baselines,
do not need recapturing when the current contract changes, and do not replace actual-client
acceptance or migration tests. A failure means fix rejection or its diagnostic, not regenerate the
fixture. This narrowly supersedes the earlier blanket exclusion of captured requests in this plan.
Recorded-request replay remains excluded as proof of compatibility or migration correctness,
consistent with the issue's design history and the relevant output-ownership reasons in
[ADR-014](../../decisions.md#adr-014-the-comparison-with-plain-ef-runs-live-and-a-slow-runs-output-is-never-committed-locked-2026-09-26).
