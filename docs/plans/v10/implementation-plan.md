# Implementation plan — M5 (wire hardening), remaining criterion

Rolling checkbox detail for the **current** milestone only. Milestone-level scope lives in
[`roadmap.md`](roadmap.md). Do not put scope here.

Closed milestones are archived and never edited again:

| Milestone | Plan |
|---|---|
| M6 — spec-base adoption (Phases A–C) | [`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) |
| M8 — productization (Phases H–N) | [`archive/implementation-plan-m8-phases-h-n.md`](archive/implementation-plan-m8-phases-h-n.md) |
| M9 — provider neutrality (Phase J) | [`archive/implementation-plan-m9-phase-j.md`](archive/implementation-plan-m9-phase-j.md) |

**M8 closed 2026-08-24.** Every one of its exit criteria has a resolution: three done, two out of
scope for v10, and requirements §4.5 answered in two halves. That is why this file was rewritten.

The suite stands at `Total tests: 22658, Passed: 22472, Failed: 9, Skipped: 177`. All nine are
classified in the archived M9 plan and stated for consumers in
[`limitations.md`](../../../website/docs/limitations.md).

## What is open, across the whole roadmap

Two milestones, and this file holds the first:

- **M5, one criterion: the remote cancel signal (W6).** Below.
- **M7, whole: SQL Server as Tier C.** The spatial half is already complete. Not started, and not
  in this file until it is the current milestone.

Nothing else is open. M1's `N5`/`N6` documentation tail closed with the documentation rewrite.

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

- [ ] **P1. Execute the server query asynchronously and pass the token.** The sequence path needs
      async enumeration instead of the `foreach` that fills an `ArrayList`; the single-result path
      needs EF's `IAsyncQueryProvider.ExecuteAsync<TResult>(Expression, CancellationToken)` instead
      of `Execute`. **This is not streaming and must not turn into it**: the server still buffers a
      whole result set, because Q6/W4 is out of scope for v10 and identity resolution depends on
      the buffer.
- [ ] **P2. Do the same for the write path.** `ServerSaveChangesExecutor` should be checked for the
      same drop before this criterion closes.
- [ ] **P3. Do not add a Cancel operation to the wire to achieve this.** A tenth operation carrying
      a request id needs a server-side registry of live requests, and that registry is the
      abandoned-token problem again: unbounded, process-local, keyed by something a vanished client
      never mentions again (`roadmap.md`, M8, the transaction registry). **The transport's own
      disconnect signal costs nothing, needs no protocol version, and cannot leak**, because
      ASP.NET Core owns its lifetime. Reach for a wire message only if a transport turns up that
      has no disconnect signal of its own, and record why before building it.
- [ ] **P4. Decide what a transport without a disconnect signal should do.** `IInfoCarrierTransport`
      is one method, `SendAsync(InfoCarrierEnvelope, CancellationToken)`, and a transport may be
      stateless. The in-process transport already shares the caller's token directly. Nothing else
      ships today, so this is a question to answer rather than code to write.

### What must be true before this closes

- The suite is unchanged: `eng/measure.sh` FIXED none, BROKEN none, REASONS unchanged.
- `eng/trim-ratchet.sh` does not rise.
- `guide/errors.md` says what a caller can now expect, and says nothing about what is not built
  (`doc-style.md` rule 6).
