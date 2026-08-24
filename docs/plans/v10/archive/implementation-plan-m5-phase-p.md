# Implementation plan — M5 (wire hardening), Phase P

**ARCHIVED 2026-08-24, when M5 closed. Never edited again.** Only the relative links changed, repointed one directory deeper by the move.

Rolling checkbox detail for the **current** milestone only. Milestone-level scope lives in
[`roadmap.md`](../roadmap.md). Do not put scope here.

Closed milestones are archived and never edited again:

| Milestone | Plan |
|---|---|
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](implementation-plan-m9-phase-j.md) |

**M8 closed 2026-08-24.** Every one of its exit criteria has a resolution: three done, two out of
scope for v10, and requirements §4.5 answered in two halves. That is why this file was rewritten.

The suite stands at `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177`. All nine are
classified in the archived M9 plan and stated for consumers in
[`limitations.md`](../../../../website/docs/limitations.md).

## What is open, across the whole roadmap

**One criterion, in one milestone: M5's remote cancel signal (W6).** It is below, and it is the only
work left in the roadmap.

M7's SQL Server tier was dropped on 2026-08-24 by the owner's decision, so M7 has nothing open: its
spatial half completed early, in M6. The preferred direction instead is a non-relational backend
tier, which is recorded in `roadmap.md` as future scope with nothing committed.

M1's `N5`/`N6` documentation tail closed with the documentation rewrite.

## Phase P — the remote cancel signal (W6, M5's last criterion)

**The design is not chosen.** This section holds the task and what is already true about it. It
does not hold an approach, because picking one before the owner has is how a plan becomes a
decision nobody made.

### What exists

- [x] **The cooperative half shipped in C66.** Every async method on `IInfoCarrierClient` and
      `IInfoCarrierTransport` takes a `CancellationToken` and passes it to the transport. A
      cancelled request raises `OperationCanceledException` on the client and is never reported as
      a transport failure, which `guide/errors.md` states for consumers.

### What is missing

**The signal already reaches the server and is dropped at the last step.** Traced 2026-08-24, and
it changes what this task is. It is not "build a way to tell the server"; it is "stop discarding
what the server is already told".

- `MapInfoCarrier` passes `http.RequestAborted` into `InfoCarrierEnvelopeServer.DispatchAsync`
  (`InfoCarrierEndpointExtensions.cs`). ASP.NET Core raises that token when the client disconnects
  or cancels, so the signal is real and costs nothing to obtain.
- `DispatchAsync` passes it to `IInfoCarrierServer.QueryDataAsync`, which passes it to
  `ServerQueryExecutor.ExecuteAsync`, which passes it to `ExecuteQueryAsync`.
- **`ExecuteQueryAsync` takes the token and never reads it.** The query runs synchronously:
  `QueryProvider.Execute(query)` for a single result, and a plain `foreach` over
  `BuildQueryable(query)` for a sequence. Neither can be interrupted, so the store keeps working
  for a client that has gone.

**Why no test caught this, and it is not an oversight in the suite.** EF ships two cancellation
tests, `ToListAsync_can_be_canceled` and `ToListAsync_with_canceled_token` in
`NorthwindMiscellaneousQueryTestBase`, and **both are green here**. Read what they assert: the
first cancels a token and accepts *either* an `OperationCanceledException` *or* a complete
nine-row result; the second passes an already-cancelled token and expects
`OperationCanceledException` plus a `QueryCanceled` log event. Every assertion is about what the
**caller** sees, and this provider gives the caller exactly the right thing. Neither test can ask
whether any work continued afterwards, because in EF's model there is no separate process for it
to continue in.

**That is the standing shape of this repository's blind spots**, and it has now cost twice. The
type allowlist "passed" for months because the in-process transport shared an `AppDomain`, so an
assembly scan found types no network server could have (ADR-008's note, ~31% of the suite). Same
cause: **a one-process suite cannot see a two-process defect.** Where a gap is on the far side of
the wire, the spec suite is silent by construction and a test of ours is the only thing that will
speak.

- [x] **P1. Execute the server query asynchronously and pass the token.** `<this commit>`
      The sequence path enumerates `IAsyncEnumerable<T>` with the token instead of the `foreach`
      that filled an `ArrayList`; the single-result path calls EF's
      `IAsyncQueryProvider.ExecuteAsync<Task<TResult>>(Expression, CancellationToken)` instead of
      `Execute`. **Passing the token to EF's async path is what stops the store**, not merely the
      loop: EF gives it to the `DbCommand`, so the provider can cancel the command.
      **Still buffered, deliberately.** The whole result set is materialized before anything is
      written to the wire, because identity resolution needs it and streaming is out of scope
      (Q6/W4). What changed is only that the buffering can be abandoned partway.
      **Cost: one trim warning, 88 to 89, and it was priced before it was accepted.** Both paths
      close a generic over the query's own runtime type, which only the caller's model knows, so
      both dispatch through `MakeGenericMethod`; they sit in one method and cost one unique
      IL2060. There is no reflection-free shape: `IInfoCarrierServer.QueryDataAsync` is non-generic
      by contract, because the wire cannot know the element type at compile time. The alternative,
      polling the token between rows in the old synchronous loop, needs no reflection and costs no
      warning, and was rejected: it stops the loop but not the store command, which is the cost the
      fix exists to remove. `eng/trim-baseline.txt` carries the reasoning.
- [x] **P2. The write path was already correct.** `<this commit>` Checked rather than assumed:
      `ServerSaveChangesExecutor` calls `_context.SaveChangesAsync(cancellationToken)`. Nothing to
      change. **The fault was only ever in the read path**, and the reason is visible in the two
      methods: the write path was async from the start, and the read path executed synchronously
      inside an `async` method that awaited `Task.CompletedTask`.
- [x] **P3. Do not add a Cancel operation to the wire to achieve this.** A tenth operation carrying
      a request id needs a server-side registry of live requests, and that registry is the
      abandoned-token problem again: unbounded, process-local, keyed by something a vanished client
      never mentions again (`roadmap.md`, M8, the transaction registry). **The transport's own
      disconnect signal costs nothing, needs no protocol version, and cannot leak**, because
      ASP.NET Core owns its lifetime. Reach for a wire message only if a transport turns up that
      has no disconnect signal of its own, and record why before building it.
- [x] **P4. Decide what a transport without a disconnect signal should do.** `IInfoCarrierTransport`
      is one method, `SendAsync(InfoCarrierEnvelope, CancellationToken)`, and a transport may be
      stateless. The in-process transport already shares the caller's token directly. Nothing else
      ships today, so this is a question to answer rather than code to write.

- [x] **P5. A test that asks the server, because nothing EF ships can.** `<this commit>`
      `InMemorySmokeTest.The_server_stops_a_query_when_the_caller_cancels` captures a real query
      envelope through the in-process transport, then replays that request straight into
      `InProcessInfoCarrierServer.QueryDataAsync` with an already-cancelled token and expects
      `OperationCanceledException`. **Replaying server-side is the whole design of the test**: a
      client-side cancel aborts the transport first and proves nothing about the far side.
      **Proved failable rather than assumed so**: with `ServerQueryExecutor.ExecuteQueryAsync`
      stashed back to its synchronous form the test fails with `Assert.ThrowsAny() Failure: No
      exception was thrown`, which is the old loop ignoring the token and returning both rows. With
      the fix restored it passes. A test that cannot fail is not evidence.

### What must be true before this closes

- [x] The suite is unchanged: `eng/measure.sh` FIXED none, BROKEN none, REASONS unchanged.
- [x] `eng/trim-ratchet.sh` **rose by one, deliberately, and the reason is recorded** in
  `eng/trim-baseline.txt`. The criterion as written said "does not rise"; the rise is the cost of
  reaching the store with the token at all, and the cheaper shape was priced and rejected in P1
  rather than overlooked.
- [x] `guide/errors.md` says what a caller can now expect: the token reaches the server, so
  cancelling stops the work there and not only the wait here. It says nothing about what is not
  built (`doc-style.md` rule 6).

**What is left in P3 and P4 is a decision, not code.** P3 is a rule for the future rather than a
task. P4 asks what a transport with no disconnect signal should do, and nothing that ships today is
one: HTTP has `RequestAborted`, and the in-process transport shares the caller's token directly. So
M5's remaining criterion is met by the work above, and whether M5 closes is the owner's call.
