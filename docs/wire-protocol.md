# Wire Protocol — Contract Specification

Status: **PRE-IMPLEMENTATION (contract shape defined; concrete message types are research
output, not final)** · Related: [ADR-001](decisions.md#adr-001--serialization-engine-greenfield-spec-only---locked-2026-07-19), [`expression-serialization.md`](expression-serialization.md), [`architecture.md`](architecture.md).

The wire protocol is the contract between InfoCarrier client and server. It is
**transport-agnostic** (HTTP, gRPC, in-process, or any other transport) and
**format-agnostic** (serializer abstraction per requirements §4.1). This document defines
the required operations, envelope rules, and message contracts. Concrete C# record types
(or `.proto`) are produced during implementation, informed by the open questions below.

---

## 1. Envelope rules (LOCKED direction)

Every message — request and response — carries an envelope with:

| Field | Purpose |
|---|---|
| `protocolVersion` | Wire contract version. Present **from day 1** (rlinq's unversioned `[DataMember(Order)]`-only approach forces lockstep upgrades — we avoid it). |
| `operation` | Discriminator: `Query`, `SaveChanges`, `BeginTransaction`, `CommitTransaction`, `RollbackTransaction`. |
| `payload` | Operation-specific body (below). Serialized via the configured serializer abstraction. |

Versioning policy: additive changes bump minor; breaking changes bump major; a server
rejects an unsupported major with a well-known error payload.

## 2. Operations (from requirements §2.4)

### 2.1 Query

| Direction | Content |
|---|---|
| Client → Server | Serialized expression tree (per [`expression-serialization.md`](expression-serialization.md)) + query parameters + `QueryTrackingBehavior` + async flag |
| Server → Client | Materialized result rows. For entity types: identity-keyed row data with loaded-navigation markers. For non-entity projections: type-agnostic columnar data (the client applies the final projection — see requirements §3) |

Streaming: large result sets flow as `IAsyncEnumerable<T>` (requirements §4.4) rather than
a buffered `List<T>`.

### 2.2 SaveChanges

| Direction | Content |
|---|---|
| Client → Server | Serialized change-tracker entries: entity identity (type + key), state (Added/Modified/Deleted), modified property values, original concurrency-token values. M2M join entries included. |
| Server → Client | Store-generated values per entry: identity keys, computed columns, concurrency tokens, defaults — mapped back to the client's tracked entities. |

### 2.3 Transactions

`BeginTransaction` / `CommitTransaction` / `RollbackTransaction` (+ async variants).
Savepoints supported when the backend provider supports them. Client disposal/rollback must
clean up server-side (requirements §2.9).

## 3. Contract invariants

- **Async-first** (§4.3): every operation has an async variant; sync overloads never block
  on async.
- **Serializer abstraction** (§4.1): no operation hardcodes JSON/protobuf/MessagePack; the
  envelope + payloads serialize through the pluggable `IInfoCarrierSerializer` seam.
- **Reference preservation** (see expression-serialization §2.3): entity graphs with
  circular navigation references must round-trip; the chosen serializer must support
  reference handling.
- **Identity fidelity**: two references to the same entity in one payload arrive as the same
  tracked instance on the client (per-message reference map).

## 4. Message contracts (research output — to be specified concretely in implementation)

v1's DTOs (`QueryDataRequest`/`QueryDataResult`, `SaveChangesRequest`/`SaveChangesResult`)
are the starting shape, modernized:

- Strongly-typed records (not `[DataContract]` classes).
- `System.Text.Json` source-generation contexts for AOT (requirements §4.5) where the
  default serializer is STJ.
- Typed parameter bag instead of rlinq's untyped `object Value` constants.

The exact field lists are **implementation deliverables**, gated by the open questions in
[`expression-serialization.md`](expression-serialization.md) §4 (especially Q1 node set,
Q4 shared types, Q9 entity identity).

## 5. Open research questions (BEFORE locking message contracts)

| # | Question | Study source | Impact |
|---|---|---|---|
| W1 | Minimal payload for entity rows: full property set vs only columns needed by the client projection (requirements §3.3 "minimize data transfer")? | `subrepos/efcore` shaper/materialization | Payload size + correctness of client-side projection |
| W2 | How are store-generated values keyed back to entries when keys are client-generated temporary (Added with temp key)? | `subrepos/efcore` update pipeline, v1 `SaveChanges` handling | SaveChanges correctness |
| W3 | Transaction scope identity across stateless transports: what token represents an open server transaction? | v1 + transport constraints | Transaction support over HTTP |
| W4 | Streaming + identity resolution: can the client resolve identity per-row while streaming, or must include-heavy queries buffer? | `subrepos/efcore` async enumeration | §4.4 streaming vs identity map |
| W5 | Error contract: how do server-side EF exceptions (e.g. `DbUpdateConcurrencyException`) cross the wire with enough fidelity for the client to rethrow the right type? | v1 + `subrepos/efcore` exception surface | Spec-test parity (several bases assert exception types) |
| W6 | Does the envelope need a correlation/cancellation token for async + streaming cancellation? | transport + `IAsyncEnumerable` cancellation | Async-first + streaming |

## 6. Transport bindings (out of scope for the core contract)

The core library defines envelopes + the `IInfoCarrierTransport` seam. Concrete bindings
(ASP.NET Core endpoint, gRPC service, in-process test transport) are separate deliverables;
the in-process JSON round-trip transport (v1's `SimulateNetworkTransferJson` pattern) is the
first, used by the functional-test harness.
