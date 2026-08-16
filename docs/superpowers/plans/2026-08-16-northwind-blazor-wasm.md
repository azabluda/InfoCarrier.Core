# Northwind Sample — Phase 2: the browser

**Goal:** A Blazor WebAssembly client with no database answers queries, lazy-loads navigations,
saves a unit of work and runs a transaction against the Phase 1 server — in a browser, with a
wire inspector showing every envelope, and publishing trimmed with no IL warning attributable to
`InfoCarrier.Core`.

**Spec:** [`../specs/2026-08-11-blazor-wasm-sample-design.md`](../specs/2026-08-11-blazor-wasm-sample-design.md).
This plan implements its **Phase 2** (spec §10); Phase 1 landed as M8-1 … M8-9.

**Exit:** the spec's §9 success criteria, all five.

## What Phase 1 already established, and therefore what this phase is not about

Phase 1 proved the wire works — 17 transport tests over `WebApplicationFactory` and a console
demo over a real TCP socket. **So a page that fails here fails on the browser, not on the
protocol**, and that is the whole reason the spec split the phases where it did. Two consequences
for how this plan reads:

- **No new product behaviour is planned**, with exactly one exception: M8-11's source-generated
  JSON context, which Phase 1 recorded as *"Phase 2's first task"* because a trimmed WASM build
  sets `JsonSerializerIsReflectionEnabledByDefault=false` and the envelope is serialized
  reflectively today.
- **The pages are wiring, not mechanism.** If one needs a product change to work, that is a
  finding and gets written down with evidence, not absorbed.

## Environment facts established before planning

Both were probed, not assumed, because each would have changed the plan's shape:

| Question | Answer | How |
|---|---|---|
| Does this SDK have the Blazor WASM template? | Yes | `dotnet new blazorwasm` in a scratch directory |
| Does a trimmed publish need the `wasm-tools` workload, which is not installed? | **No.** ILLink runs and the publish succeeds; the workload buys AOT and native relinking, which spec §3.2 explicitly does not want. | `dotnet publish -c Release` of that scratch app |

The second is load-bearing. Spec §3.2 chose `UseLazyLoadingProxies()` over `ILazyLoader` on the
reasoning that *trimming and AOT are separate axes* and a Blazor release publish trims **without**
AOT. The probe confirms the toolchain agrees: the publish reports
`Publishing without optimizations … we strongly recommend using wasm-tools`, which is about AOT,
while ILLink's own `Optimizing assemblies for size` line shows trimming ran.

## Global Constraints

Every task's requirements implicitly include these.

- **Target framework `net10.0`**, set once in `Directory.Build.props`.
- **Central package management.** A new package needs a `<PackageVersion>` in
  `Directory.Packages.props` **and** a versionless `<PackageReference>`.
- **Sample projects** set `<IsPackable>false</IsPackable>` and
  `<GenerateDocumentationFile>false</GenerateDocumentationFile>`.
- **Never edit anything under `subrepos/`.**
- **No NuGet dependency on Remote.Linq or Aqua** (ADR-001).
- **`HttpInfoCarrierTransport.cs` and `InfoCarrierEndpointExtensions.cs` carry no sample types**,
  so promoting them is a file move (spec §4.1). The inspector must therefore be a **decorator**
  over `IInfoCarrierTransport` living in the client project — not a change to either file.
- **One task per commit**, message prefixed `Step M8-<n>:`, with this plan's checkbox and the
  matching `docs/implementation-plan.md` entry in the **same** commit.
- **`eng/measure.sh` only where a task says so.** It costs 6–9 minutes. Only M8-11 and M8-16 touch
  anything the spec suite can see; the rest are `samples/` and cannot move it.
- **Report test results as `Passed: N, Failed: M, Total: T`** read from actual output.

---

## File Structure

| File | Responsibility |
|---|---|
| `src/InfoCarrier.Core/InfoCarrierJsonContext.cs` *(new)* | Source-generated metadata for the envelope and every payload type. The **only** product change planned. |
| `samples/Northwind.Client/Northwind.Client.csproj` *(new)* | The Blazor WASM project. |
| `samples/Northwind.Client/Program.cs` | The whole client wiring: `HttpClient`, transport, `UseInfoCarrier`. |
| `samples/Northwind.Client/Wire/InspectingTransport.cs` | `IInfoCarrierTransport` decorator feeding the inspector. Keeps the promotion constraint intact. |
| `samples/Northwind.Client/Wire/WireLog.cs` | The 20-entry ring buffer, and the decode of an envelope for display. |
| `samples/Northwind.Client/Pages/Customers.razor` | Querying + the projection split. |
| `samples/Northwind.Client/Pages/Order.razor` | Lazy loading + unit of work. |
| `samples/Northwind.Client/Pages/Transfer.razor` | Transactions + error fidelity. |
| `samples/Northwind.Server/Program.cs` *(modify)* | Serve the client's files from the same origin (spec §3.4). |

---

### Task M8-10: Open this plan

- [x] **Step 1:** Write this file and the Phase I section of `docs/implementation-plan.md`.
- [x] **Step 2:** Commit. No code, so no test run.

---

### Task M8-11: A source-generated JSON context for the envelope

**Why first.** Phase 1's own closing note: `SystemTextJsonInfoCarrierSerializer` sets no
`TypeInfoResolver`, so the envelope and the nine operation payloads serialize reflectively. That is
fine untrimmed and **fails in a trimmed WASM build**. The expression tree is already safe — it goes
through `ExpressionJsonContext` — so the fix is bounded.

**The set of types is closed and was enumerated from the call sites, not guessed.** Every use of
`IInfoCarrierSerializer` resolves to one of: `InfoCarrierEnvelope`, `QueryDataRequest`,
`QueryDataResult`, `SaveChangesRequest`, `SaveChangesResult`, `SavepointRequest`,
`TransactionResult`, `string`, `bool`, and `object` — the last from
`_serializer.Serialize<object?>(null)`, which is how a void operation and a fault both fill
`Payload`. `InfoCarrierFault`, `ChangeEntry` and `GeneratedValues` come along as members.

**The instrument that proves closure already exists**, and `ExpressionJsonContext`'s own comments
record it working: a type absent from a context whose resolver is set fails hard with
*"JsonTypeInfo metadata for type 'X' was not provided"* — it does **not** silently fall back to
reflection. So the 22,453-test spec suite, which drives this serializer on every hop, is the
verification. A missing type cannot pass it.

- [x] **Step 1:** Add `InfoCarrierJsonContext`, `GenerationMode = JsonSourceGenerationMode.Metadata`.
      **Amended during the work:** the step as written said to carry over all three settings the
      old `Options` had, including `ReferenceHandler.Preserve`. That one **cannot** come along, and
      it turned out not to have been doing anything — see M8-11's entry in
      `docs/implementation-plan.md`.
- [x] **Step 2:** Point `SystemTextJsonInfoCarrierSerializer` at `InfoCarrierJsonContext.Default.Options`.
- [x] **Step 3:** Build; run `test/InfoCarrier.Core.TransportTests`.
- [x] **Step 4:** `bash eng/measure.sh m8-11 c96`. **Expected: `FAILING: 13  TOTAL: 22453`, with
      empty FIXED, BROKEN and REASONS diffs.** Anything else means the type set is not closed, and
      the failure message names the missing type outright.
- [x] **Step 5:** Commit.

---

### Task M8-12: The client project, the shell, and the inspector

The first task with a browser in it. It ends with an app that loads, shows the inspector, and has
one working page proving the wire reaches the browser — the rest are content.

- [x] **Step 1:** Package versions for `Microsoft.AspNetCore.Components.WebAssembly`, its
      `.DevServer`, and `Microsoft.FluentUI.AspNetCore.Components`.
- [x] **Step 2:** The project, referencing `Northwind.Shared` and `Northwind.Client.Transport`.
- [x] **Step 3:** `WireLog` + `InspectingTransport`. Ring buffer of 20, per spec §5.2: operation
      kind, envelope size, round-trip time, decoded payload.
- [x] **Step 4:** `Program.cs` — the whole client wiring, and short enough to read as the sample's
      centrepiece.
- [x] **Step 5:** The shell: Fluent UI layout, nav, and the inspector panel on the right.
- [x] **Step 6:** `Northwind.Server` serves it (`UseBlazorFrameworkFiles`, `UseStaticFiles`,
      `MapFallbackToFile`) so there is one origin and one command (spec §3.4).
- [x] **Step 7:** Run it and confirm in a browser that a query returns rows and the inspector shows
      the envelope. **This is where spec §3.2's proxy question gets its answer** — an experiment
      with a known fallback, not an assumption.
- [x] **Step 8:** Commit, recording what the proxy experiment showed.

---

### Task M8-13: The Customers page

Querying, and the projection split made visible. Grid state becomes `Where`/`OrderBy`/`Skip`/`Take`;
the page projects to a client-only row record, which the split turns into a server-side tuple
projection plus a client-side reassembly.

- [x] **Step 1:** The page.
- [x] **Step 2:** Confirm in the browser that the inspector's decoded payload carries the projected
      columns and no `Customer`.
- [x] **Step 3:** Commit.

---

### Task M8-14: The Order page

Lazy loading and unit of work. Touching `Customer` and `OrderDetails` issues further envelopes;
several quantity edits then save as **one** `SaveChanges`.

- [x] **Step 1:** The page.
- [x] **Step 2:** Confirm in the browser: extra envelopes on touch, exactly one on save.
      **This step is what refuted spec §3.2**, and it needed real clicks — `--dump-dom` renders a
      page but cannot press a button, so the check moved to the DevTools protocol.
- [x] **Step 3:** Commit.

---

### Task M8-15: The Transfer page

Transactions and error fidelity together. Move an order to another customer inside
`BeginTransactionAsync`; a checkbox forces a failure part-way, which shows rollback and W5 in one
action.

- [x] **Step 1:** The page, with a `FluentMessageBar` for the fault. No stack trace on screen.
- [x] **Step 2:** Confirm both paths in the browser, and that the inspector shows
      `BeginTransaction` → `SaveChanges` → `Commit`/`Rollback`. Observed exactly that.
- [x] **Step 3:** Commit.

---

### Task M8-16: The compiled model

`dotnet ef dbcontext optimize` for `NorthwindContext` (available since C90), and
`options.UseModel(...)` on the client so no model is built by reflection at start-up.

- [x] **Step 1:** Generate it. **Amended during the work:** the step said "against the server's
      configuration", which was wrong — the compiled model is the *client's*, and must be generated
      against the client's provider. The server is only the `--startup-project`, because a Blazor
      WASM project emits no `deps.json` and `dotnet ef` cannot load one.
- [x] **Step 2:** Wire `UseModel` on the client; confirm the app still works. **It did not, at
      first** — see the `Issue31751` finding in `docs/implementation-plan.md`.
- [x] **Step 3:** `bash eng/measure.sh m8-16 m8-11` — expected unchanged.
- [x] **Step 4:** Commit.

---

### Task M8-17: The trimmed publish gate, CI, and the honest record

- [x] **Step 1:** `PublishTrimmed=true`, and **not** warnings-as-errors — see step 3.
- [x] **Step 2:** Publish. **Triage every warning by which assembly owns it.** Spec §7: a residue
      of EF-owned warnings is acceptable; a residue of ours is not. Expected sites are
      `TypeNodeResolver` and `DynamicValueMapper`'s reflective walk, both of which resolve types
      from strings.
- [x] **Step 3:** **Not done, and reported instead.** 86 are ours and they are the provider's
      premise rather than an oversight; the honest annotation would be `[RequiresUnreferencedCode]`
      on the public query API, which is a product decision. Gated by `eng/trim-ratchet.sh`.
- [x] **Step 4:** Add the publish to CI's fast gate.
- [x] **Step 5:** Record what Phase 2 did **not** close, in `docs/implementation-plan.md`, with the
      same candour Phase 1's M8-7 used.
- [x] **Step 6:** Final measurement and commit.
