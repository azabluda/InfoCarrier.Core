# Design — Northwind sample: a Blazor WASM client over HTTP

**Date:** 2026-08-11 · **Milestone:** M8 (productization) · **Status:** approved, not yet implemented

The first sample application. A `DbContext` runs **in the browser** with no database and talks to
a SQLite-backed ASP.NET Core server over HTTP.

---

## 1. Why this sample, and why it is not only a demo

The sample exercises four things the provider claims: querying, lazy loading, the unit-of-work
shape of `DbContext`, and transactions.

**It is also the test of an M8 exit criterion.** "AOT/trimming verification" is on the roadmap, and
Blazor WebAssembly is the most demanding host we could pick for it:

- the browser has no dynamic code, so `Expression.Compile()` runs on an interpreter —
  `ClientResultMaterializer.cs:633` and `Query/SplitQuery.cs:74` both reach it;
- a Blazor release build trims by default, and the wire deserializer resolves types **from
  strings** (`TypeAllowlist`, `TypeNodeResolver`), which is exactly what a trimmer cannot see.

**Decision (2026-08-11): a trimming or AOT failure is a product defect and gets fixed in `src/`.**
The sample is not permitted to set `PublishTrimmed=false` to get itself green. That decision is the
reason this sample is worth building rather than merely nice to have.

**The compiled-model work of C90–C93 is what makes it viable, and that was not planned.** Microsoft
documents EF Core as only partly trim-safe and recommends a compiled model as the mitigation.
`dotnet ef dbcontext optimize` works against this provider for the first time as of C90, so the
mitigation is available. See §5.

---

## 2. Scope

**In:** three pages, a wire-inspector panel, an HTTP transport on each side, a seeded SQLite store.

**Out, deliberately:** authentication, a second backing store, streaming (`IAsyncEnumerable<T>` is
its own M8 item), and any runtime-compiled "LINQ playground" — that last one needs a C# compiler in
the browser and fights the trimming goal.

**Approach chosen:** *CRUD plus a wire inspector*, over a plain CRUD app. The extra ~150 lines buy
the only thing this sample can show that no other EF sample can — an expression tree leaving the
browser and rows coming back.

---

## 3. Projects

Three projects under a new `samples/` folder.

| Project | Kind | References |
|---|---|---|
| `Northwind.Shared` | class library | EF Core |
| `Northwind.Server` | ASP.NET Core | `InfoCarrier.Core`, `EFCore.Sqlite`, Shared |
| `Northwind.Client` | Blazor WASM | `InfoCarrier.Core`, Fluent UI Blazor, Shared |

### 3.1 One context type, shared

`Northwind.Shared` holds the POCOs and **one** `NorthwindContext`. Both sides use that same type.

This is a correctness requirement, not a convenience. A49 in `CLAUDE.md`: the two models must
agree, because the wire carries entity type **names**. One shared `OnModelCreating` makes that true
by construction rather than by discipline.

```csharp
// Server                          // Client (browser)
options.UseSqlite(connection)      options.UseInfoCarrier(client)
```

The model is a trimmed Northwind — `Customer`, `Order`, `OrderDetail`, `Product`, `Category`. The
POCOs are **copied**, not taken from `Microsoft.EntityFrameworkCore.Specification.Tests`: a sample
must not reference a test package.

### 3.2 Lazy loading: **automatic (proxies) is the target**, `ILazyLoader` is the recorded fallback

**Revised 2026-08-11.** An earlier draft of this section chose `ILazyLoader` injection outright, on
the grounds that Castle DynamicProxy needs `Reflection.Emit` and the browser has none. **That was
stated with more confidence than the facts support**, and the demo is materially better with
automatic loading — `order.Customer` as a plain navigation, no ceremony in the model.

**What is actually true, and the distinction matters:**

| Blazor WASM build | Dynamic code | Proxies |
|---|---|---|
| default (Mono **interpreter**), including a trimmed release publish | supported | expected to work |
| `RunAOTCompilation=true` | **not** supported | expected to fail |

Trimming and AOT are separate axes, and a Blazor release publish trims **without** AOT by default.
So proxies are likely fine for the target configuration, and the M8 criterion this sample serves is
*trimming* verification.

**Decision:** use `UseLazyLoadingProxies()`. Navigations are `virtual`. Verify it in the browser
early in Phase 2 — this is an **experiment with a known answer if it fails**, not an assumption.

> **AMENDED 2026-08-16 (M8-14). The experiment ran, and both this section and its fallback were
> wrong — about the mechanism, not about the risk.**
>
> The table above is correct as far as it goes: proxies *are* created under the interpreter. The
> Customers page reports a runtime type of `CustomerProxy`, so Castle DynamicProxy emits types
> inside WebAssembly and `RunAOTCompilation` is not the obstacle here.
>
> **Automatic lazy loading still does not work in the browser, for an unrelated reason.** A
> navigation property getter is **synchronous**, so a lazy load has to *block* on the HTTP round
> trip — and a single-threaded WebAssembly runtime cannot block. Touching `order.Customer` throws
> `PlatformNotSupportedException: Cannot wait on monitors on this runtime`, and it throws *after*
> the request has already gone out, so the panel shows the round trip while the value never
> arrives.
>
> **The recorded fallback does not help either**, which is the part worth carrying forward:
> `ILazyLoader.Load()` is synchronous too, so it fails identically. The fallback was cheap for the
> wrong reason — the cost was never in the model, and switching to it would have bought nothing.
>
> **What works is explicit async loading**: `Entry(x).Reference(…).LoadAsync()` and
> `Entry(x).Collection(…).LoadAsync()`. The demonstration is unharmed — the navigation is still
> not fetched by the original query, and asking for it still costs exactly one round trip, which
> is the point the Order page makes. Verified in headless Edge: 1 round trip to load the order, 2
> after the customer, 3 after the lines.
>
> `UseLazyLoadingProxies()` stays configured on **both** halves. It is not load-bearing for the
> browser any more, but the two models must agree about what the wire names (A49), and the server
> genuinely uses it. **This constraint is the browser's, not this provider's**: any EF provider
> whose store is reached over the network has it, and a desktop or server client of this provider
> can lazy-load normally.

**The fallback is cheap by construction.** `virtual` auto-properties are compatible with both
routes; switching to `ILazyLoader` means adding a loader field and changing the getters inside
`Northwind.Shared/Model/`, and nothing outside that folder moves. Both routes are proven in the
suite — `LoadInfoCarrierTest` and `LazyLoadProxyInfoCarrierTest`, 825 of 825 — so neither is a
compromise in correctness terms; only in how the model reads.

`Microsoft.EntityFrameworkCore.Proxies` must be referenced **explicitly** by the sample. The
functional test project gets it transitively through the spec-tests package, which a sample does
not reference.

### 3.3 Shared configuration is the point, not an implementation detail

One shared `NorthwindContext` is the first worked example of a founding idea recorded as **D2** in
[`architecture.md`](../../architecture.md) §6a: *one model configuration that both halves derive
from and augment*.

It is worth reading the sample as **evidence about D2's central expectation** — that the shared part
is small, because EF's conventions already produce most of it. This sample's `OnModelCreating` is
about a dozen lines for five entity types, two relationships and a composite key.

Divergence between the two models is **silent** — it produces wrong answers rather than errors (A49,
B4, B12). That is why the sample shares a context type rather than declaring the model twice.

### 3.4 The server hosts the client

ASP.NET Core serves the Blazor files. One process, one origin. That removes CORS, removes a second
launch profile, and makes `dotnet run --project samples/Northwind.Server` the whole story. The
SQLite file is seeded at start-up if it is absent.

---

## 4. The HTTP transport

The seam already exists and is one method:

```csharp
Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken ct);
```

`InfoCarrierEnvelopeServer` is already the server half — it checks `ProtocolVersion` and dispatches
all nine operations. So the transport is one small class on each side:

| File | Side | What it does |
|---|---|---|
| `HttpInfoCarrierTransport` | client | `POST /infocarrier`, envelope in, envelope out. `System.Net.Http` only, so it is WASM-safe. |
| `MapInfoCarrier()` endpoint | server | Reads the request envelope, calls `InfoCarrierEnvelopeServer`, writes the response. |

Client wiring: `HttpClient` → `HttpInfoCarrierTransport` → `TransportInfoCarrierClient` (already in
the product) → `UseInfoCarrier`.

### 4.1 Packaging — decided, with its cost stated

**Decision (2026-08-11): both files live in the sample for now, and are promoted later** to
`InfoCarrier.Core.Http` and `InfoCarrier.Core.AspNetCore`.

The cost of that choice is that M8's "HTTP transport binding" criterion stays formally open until
the promotion happens. The mitigation is a constraint on the code: **both files contain no
Northwind types and no sample-specific code**, so promotion is a file move rather than a rewrite.
A reviewer should be able to check that by reading their `using` lines.

### 4.2 Open question carried, not answered — `Payload` is `byte[]`

Recorded in full as **D1** in [`architecture.md`](../../architecture.md) §6a. Summary: the outer
envelope is JSON and `Payload` is `byte[]`, so the expression tree crosses as base64 — about 33%
larger and unreadable.

**No action in this work.** `byte[]` stays. The consequence for this sample is concrete and belongs
here: **the wire inspector must base64-decode the payload before it can display it** (§5.2). That
symptom is what raised the question.

---

## 5. The pages

Three pages. The inspector panel is always present, on the right.

| Page | Feature shown | What the inspector shows |
|---|---|---|
| Customers | querying | one `Query` envelope per grid change |
| Order | lazy loading, unit of work | an extra envelope when a navigation is touched; one `SaveChanges` for many edits |
| Transfer | transactions, error fidelity | `BeginTransaction`, `SaveChanges`, then `Commit` or `Rollback` |

### 5.1 What each page proves

**Customers.** A `FluentDataGrid` in server-side mode. Its sort, filter and page state become
`Where`, `OrderBy`, `Skip` and `Take` on `IQueryable`. The page projects to a small named row
record, so only the needed columns cross — which exercises the projection split (M2) and the
minimal-column payload (W1). The inspector makes it visible: the envelope is small and contains no
`Customer`.

**The row record is client-only, and that is the point rather than a problem.** It is never named
on the wire: `ProjectionRewriter` splits the `Select` into a server-side `ValueTuple` projection
plus a client-side reassembly, so the server is asked for columns and the record is built in the
browser. The inspector shows the tuple projection, which is the clearest single illustration of M2
that this sample can offer.

> **Observation, not a task.** `TypeAllowlist.ForModel(model, registeredTypes)` accepts additional
> projection types, but no caller supplies that argument today — `InProcessInfoCarrierServer` calls
> `ExpressionSerializer.CreateForModel(model, ValueMappers)` without it. The sample does not need
> it, because of the split described above. It would be needed by an application that puts a
> client-only type in a **row-deciding** position, which C68 refuses anyway. Recorded so that the
> unused parameter is not mistaken for a gap this sample failed to exercise.

**Order.** `Customer` and `OrderDetails` are not loaded by the query. Touching them makes
`ILazyLoader` fetch them, and a second envelope appears. Then several line quantities are edited
and saved **once**: the panel shows one `SaveChanges` envelope carrying several change entries.
That is the unit-of-work point in one screenshot.

**Transfer.** Move an order to another customer and adjust stock, inside `BeginTransactionAsync`. A
checkbox forces a failure part-way, which demonstrates rollback and W5 exception fidelity together.

### 5.2 The inspector panel

Shows, for the **last 20** operations: operation kind, envelope size in bytes, round-trip time, and
the decoded payload. Twenty is a fixed ring buffer, so a long session cannot grow the browser's
memory without bound. It is cheap because `HttpInfoCarrierTransport.SendAsync` holds both envelopes
already. It doubles as a debugging tool for the rest of M8.

---

## 6. Error handling

W5 already carries a fault as data, so the server's exception is raised again on the client with
its type, message and inner chain. Two limits are known and are **stated rather than worked
around**:

- a `SqliteException` cannot be rebuilt by a client that does not reference the driver, so it
  degrades to its nearest public base (C83);
- `DbUpdateException.Entries` are the *server's* entries; the client is given its own.

Pages show a `FluentMessageBar`. No page shows a stack trace. A transport-level failure (network
down, HTTP 500, a non-envelope body) must surface as a clear exception and must not be swallowed.

The Transfer page's forced failure is what tests all of this.

---

## 7. Trimming and AOT plan

> **AMENDED 2026-08-16 (M8-18). Steps 1 and 2 are withdrawn: the sample has no compiled model.**
> Not because one cannot work in WebAssembly — it can, with
> `AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true)`, since EF otherwise
> initializes the model on a 10 MB-stack `Thread` — but because **this sample has no correct way to
> generate one.** `dotnet ef dbcontext optimize` needs a startup project it can load, a Blazor WASM
> project emits no `deps.json`, and with the *server* as startup project EF takes the configuration
> from the startup application's own service provider and silently ignores the client's
> `IDesignTimeDbContextFactory`. The generated "client" model came out carrying
> `Relational:TableName` and `Proxies:LazyLoading = true` — it was the server's. The browser ran on
> it and appeared to work, which is precisely the silent model divergence A49 exists to prevent.
> Steps 3 and 4 stand and were carried out; see M8-17.

1. ~~Generate a compiled model for `NorthwindContext` (`dotnet ef dbcontext optimize`, available
   since C90).~~ Withdrawn — see the amendment above.
2. ~~The client calls `options.UseModel(NorthwindContextModel.Instance)`, so no model is built by
   reflection at start-up.~~ Withdrawn.
3. Publish with `PublishTrimmed=true` and **treat IL2xxx warnings as errors**.
4. Fix what is ours. Document what is EF Core's.

Expected warning sites, both because they resolve types from strings: `TypeNodeResolver` and the
reflective object walk in `DynamicValueMapper`. The fix vocabulary is `[DynamicallyAccessedMembers]`
and, where it is honest, `[RequiresUnreferencedCode]`.

**A residue of EF-owned trim warnings is an acceptable outcome; a residue of ours is not.** If step
4 turns out to be large, it is reported with evidence rather than absorbed silently.

---

## 8. Tests

The sample is not a spec test, but two parts of it are product code.

| Test | Where | What it covers |
|---|---|---|
| HTTP round-trip | **a new project**, `test/InfoCarrier.Core.TransportTests` | `WebApplicationFactory` hosts the endpoint; `HttpInfoCarrierTransport` points at it; query, save and transaction assertions run **over a real HTTP hop**. Nothing in this repo has ever crossed one. |
| Trimmed publish | build gate | `PublishTrimmed=true` with IL warnings as errors. |

**A new test project, not the existing one, and the reason is the ratchet.**
`InfoCarrier.Core.FunctionalTests` is what `eng/ratchet.sh` counts and what
`test/known-failures.txt` describes. Adding ASP.NET test hosting to it would put non-spec tests
inside a number that means "spec tests failing", and would drag `Microsoft.AspNetCore.Mvc.Testing`
into a 22,453-test assembly. A separate project keeps both numbers meaning what they say.

**CI.** Both go in the **fast gate**, which must stay green. `build.yml`'s fast gate currently runs
one project by filter, so it needs a second `dotnet test` step for the new project — a small,
deliberate workflow edit. The spec ratchet job is untouched, and `test/known-failures.txt` does not
move: no spec test is added or removed by this work.

---

## 9. Success criteria

1. `dotnet run --project samples/Northwind.Server` serves a working app at one URL.
2. All three pages work, and the inspector shows a decoded envelope for each operation.
3. The HTTP round-trip test passes in the fast gate.
4. The client publishes with `PublishTrimmed=true` and **no IL warning attributable to
   `InfoCarrier.Core`**.
5. The spec suite is unchanged at `Failed: 13, Total: 22453`.

---

## 10. Implementation order — two phases, and phase 1 stands alone

The plan splits at a natural seam. **Phase 1 has no UI and is fully testable**, which is what
de-risks the rest.

**Phase 1 — the transport.** `HttpInfoCarrierTransport`, the `MapInfoCarrier` endpoint, the shared
model and context, the SQLite seed, and the round-trip test project. Exit: a `DbContext` with no
database answers a query, saves, and runs a transaction **over HTTP**, asserted by tests, with no
browser involved.

**Phase 2 — the browser.** The Blazor WASM client, the three pages, Fluent UI, the inspector panel,
the compiled model, and the trimmed publish gate. Exit: §9's five criteria.

If phase 2 uncovers a large trimming problem, phase 1 has already delivered the HTTP transport and
its tests, and the milestone is not stalled behind a browser.

## 11. Follow-on work this unblocks

Not in scope here; listed so the ordering is visible.

| Next | Why it needs this first |
|---|---|
| Remote cancel signal (closes **M5**) | Needs a real transport. Over in-process, the token *is* the signal. |
| Streaming `IAsyncEnumerable<T>` | The HTTP transport is where a streamed response is expressible. |
| Promote the two transport files into packages | Closes M8's transport criterion; see §4.1. |
| Decide **D1** (`byte[]` payload) | Best decided with a real transport in hand; see §4.2. |
