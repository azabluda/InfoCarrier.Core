# Cold-read findings, 2026-08-23/24

Seven readers with no knowledge of this repository were each given a slice of the user-facing
documentation, a persona, and one instruction that made the exercise work: **read only your own
files, do not follow links.** A fact they had to click for is a fact on the wrong page.

| Reader | Files | Persona |
|---|---|---|
| Adoption | `index.md`, `release-notes/10.0.md` | Senior dev, WPF client and a REST/DTO layer, 20 minutes, deciding on a spike |
| Getting started | `installation.md`, `first-app.md`, `samples.md` | Day one of an approved spike, building a pair from nothing |
| Daily use | the five `guide/` pages | Six weeks in, writing grids and an atomic wizard over a WAN |
| Configuration | the four `configuration/` pages | Going to production: bearer tokens, `IHttpClientFactory`, payload logging, a `Money` type, multi-tenant |
| Due diligence | `limitations.md`, `security.md`, `api-surface.md` | Tech lead accountable for the dependency, financial records, multi-tenant |
| First contact | both `PACKAGE.md`, `README.md`, the release body | Judged standalone, since a real visitor sees one of the four |
| Upgrade | `upgrading-from-3-1.md` | Runs 3.1.1 in production, wrote a 120-line transport, client on .NET Framework 4.8 |
| Blazor | `blazor-webassembly.md` | Shipped Blazor WASM before, bitten by trimming |

**The method transfers, and it is the finding that matters most.** The humanizer pass over the same
files, run by a reader who had not written them, changed nine files and every change was a sentence.
The cold reads found wrong facts, missing gates, a snippet that does not compile, and a false
security claim. A checklist of writing tells measures style. Only a reader with a task measures
whether the page does its job.

What was fixed immediately is in the commits that follow this file. What is deferred is below.

---

## 1. `IgnoreQueryFilters` crosses the wire and the server honours it

**Status: open, deliberately. Documented as true in `security.md` and `server.md`; not yet decided
in code.**

Two readers challenged the same sentence independently:

> A global query filter defined there is applied to every query, and a client cannot compose past
> it. This is the intended place to decide what a caller may see.

That was false. `IgnoreQueryFilters` is in `QuerySplitter.QueryMarkers`, and the comment at
`QuerySplitter.cs:103` records the case where it failed to travel as the **bug that was fixed**:

> Left alone, `IgnoreQueryFilters(["ActiveFilter", "NameFilter"])` was unshippable whole, so only
> the query root travelled and the marker stayed on the client where it does nothing at all — the
> server applied the filters the caller had just excluded.

So the documented row-level authorization boundary has a client-controlled opt-out. The user-facing
pages have been corrected to say what is true. The design question is open.

### The three strategies

**(a) Respect all.** Today's behaviour. The marker travels and the server honours it. Correct for a
trusted client, wrong for a hostile one, and it means a query filter cannot be an authorization
boundary at all.

**(b) Reject all.** The server refuses the marker. Simple and safe, and it breaks a legitimate use:
an administrative screen that must see soft-deleted rows, which is what `IgnoreQueryFilters` is for.

**(c) Ignore only what the client declared.** *The owner's preference to consider, 2026-08-24.* A
client may switch off a filter its own model declares; a filter the **server** adds in its own
`OnModelCreating` is never bypassed. Multi-tenancy then survives a hostile client while soft-delete
stays usable.

**Why (c) looks implementable.** EF Core 10 has named filters, which is why the comment above shows
`IgnoreQueryFilters(["ActiveFilter", "NameFilter"])`. The client's model and the server's are built
from shared source, so a filter present on both is one the client declared, and a filter present
only on the server is one the server added on its own. The server can therefore honour an
ignore-request for a named filter its client-visible model also has, and refuse it otherwise. **Not
verified.** Before building it, check: what an unnamed (default) filter does under this rule, how
the server learns which filters the client's model declares without trusting the client to say, and
whether `ExecuteUpdate` and `ExecuteDelete` honour filters at all.

### One line that might close it, from the verification read

**Can the serializer's method allowlist simply exclude `IgnoreQueryFilters`?** The reader put it
best: "If it can, the whole problem becomes a configuration line." `security.md` already says the
allowlist governs "the methods it may call", and `QuerySplitter.QueryMarkers` is a fixed set of
seven names, so refusing one of them may be a smaller change than any of the three strategies
above. **Unverified.** Check what the splitter does with a marker the allowlist refuses: whether
the query fails cleanly, or degrades to the shape the `QuerySplitter.cs:103` comment describes,
where the marker stays on the client and does nothing.

If it works, it is also the wrong default on its own. Refusing the marker outright is strategy (b),
which breaks the administrative screen that legitimately wants soft-deleted rows. It would be the
mechanism under strategy (c), not a substitute for deciding.

### The rest of the boundary, unexamined

Raised by the same two readers, not yet checked in source:

- **Writes are not covered by query filters at all.** A client can `SaveChanges` an entity carrying
  another tenant's key. This is ordinary EF behaviour, and the pages pointed at query filters as
  the tenancy answer without saying the write path is a separate problem. `server.md` now says so;
  what the server should *do* about it is undecided.
- **Do `ExecuteUpdate` and `ExecuteDelete` honour query filters?** Unchecked. Cross-tenant bulk
  delete is the worst case in the set.
- **What else in `QueryMarkers` weakens a server-side guarantee?** `IgnoreAutoIncludes` and
  `AsTracking` are in the same set and were not examined.
- **Is there any CPU, wall-clock, node-count or depth bound on evaluating a submitted tree?**
  `security.md` argues that a depth limit is insufficient and never says whether one exists. A
  64 MiB size cap does not bound a small pathological tree.

---

## 2. Gaps that are product work, not writing

Each was raised by a reader with a concrete task it blocked. None is fixed.

**Idempotency of a retried unit of work.** `errors.md` says a transport failure means "the data is
unknown, not wrong, and retrying may work". Retrying a `SaveChanges` whose outcome is unknown
duplicates the insert. There is no request id, no idempotency key, and no "did my last unit of work
commit?" query. The page now states the hazard; the mechanism does not exist. **The reader called
this the one gap that can corrupt a customer's data, and that reading is correct.**

**The abandoned transaction.** Known internally (`roadmap.md` M8, deprioritized 2026-08-16) and now
stated for consumers. A client that vanishes between `BeginTransaction` and the commit pins a
`DbContext`, its connection and its locks until the process exits. There is no timeout and no
reaper.

**Multi-instance servers.** `InProcessInfoCarrierServer._transactions` is a process-local
`ConcurrentDictionary`, so a transaction token only resolves on the instance that minted it. A
load-balanced deployment needs session affinity for the life of a transaction. Now documented;
a shared registry or a token that carries its instance is unbuilt.

**Nothing observable.** No logger category, no event counter, no way to count round trips at
runtime. Two readers wanted to answer "how many requests does this screen cost" and neither could.
`index.md` says round trips are "a round trip you can see", and one reader checked: you cannot see
them, you can only predict them by reading. The wording is fixed; the diagnostics are not.

**Does the shipped execution strategy retry?** `transactions.md` shows `CreateExecutionStrategy()`
in a section headed "Execution strategies and retries" and never says whether this provider ships a
retrying strategy. If the default does not retry, that snippet teaches a no-op.

**Client and server version skew.** The envelope carries a `ProtocolVersion` and the server answers
a mismatch with `400`. There is no stated compatibility policy. A desktop fleet is never all on one
build, and two readers raised it independently.

---

## 3. Documentation gaps left open

Real, and larger than a correction. Ordered by how often a reader hit them.

1. **What happens when the WAN drops mid-unit-of-work.** Each page handles the failure it owns and
   hands the cross-cutting case elsewhere: `transactions.md` defers failure to `errors.md`,
   `errors.md` never mentions an open transaction, `saving-changes.md` never mentions transactions.
   Nobody owns the most common production event.
2. **The split rule covers projections only.** `querying.md` explains an untranslatable projection.
   A helper method in a `Where` is what teams actually write, and the page's own hedge, "the
   projection usually tells you which one you are in", concedes the gap and stops.
3. **Payload shape.** Nothing on cartesian duplication from a nested `Include`, on `AsSplitQuery`
   or its absence, or on buffering versus streaming. Round trips are documented; bytes are not, and
   a WAN charges for bytes.
4. **`blazor-webassembly.md` cannot stand alone.** Six type names, no package, no namespace, no
   server endpoint, and "the sample" named four times and linked zero times. It also never
   mentions AOT, bundle size, or Blazor Web App and `InteractiveAuto`.
5. **Tenancy needs its own page**, covering reads, writes, `IgnoreQueryFilters`, tenant resolution
   against the server's per-request scope, and the model-cache trap when a filter closes over
   per-request state. The last one is a correct-looking snippet that ships a bug, and it is on
   `server.md` today.
6. **`InfoCarrierEnvelope` is described as "a serializable record"** and nothing else, so the
   payload-logging job both configuration readers were given is unreachable from the pages.
7. **`declaredType` is never defined** in `value-mappers.md`, and the worked example matches on the
   runtime value going out and the declared type coming back, without saying why.
8. **`IHttpClientFactory`.** The snippet in `client.md` resolves one `HttpClient` inside an
   `AddSingleton` factory, which defeats handler rotation and captures a scoped token source. It is
   the pattern a reader would copy.
9. **Six "works like any provider" refrains** across five guide pages, each standing where the
   detail was wanted.

---

## 4. False positives, recorded so they are not re-raised

- **License.** A reader wanted one on `index.md`. It is in the site footer on every page, from
  `mkdocs.yml`'s `copyright`.
- **`wasm-tools`.** A reader feared the samples need the workload. CI installs none and the
  solution builds, so a plain build does not.
- **Thread safety.** Asked twice as a doubt; it is real and now documented.
  `TransportInfoCarrierClient`, `HttpInfoCarrierTransport` and `SystemTextJsonInfoCarrierSerializer`
  are each `sealed` with only readonly fields and one `static readonly` options instance.
- **"nine `…Async` methods".** A reader could not derive nine and suspected an error. It is nine:
  `QueryData`, `SaveChanges`, `BeginTransaction`, `CommitTransaction`, `RollbackTransaction`,
  `CreateSavepoint`, `RollbackToSavepoint`, `ReleaseSavepoint`, `SupportsSavepoints`.


---

## 5. The verification read, 2026-08-24

A fifth reader was given the four security-relevant pages and a comprehension test rather than a
review: "can a client see another tenant's rows, and quote every sentence you used to decide."

**It answered correctly**, from the pages alone, and reached the right verdict for its situation:
conditional yes on the library, no on the architecture as documented. What tipped it toward yes is
worth keeping in mind the next time a page is tempted to reassure:

> Documentation that discloses its own holes is documentation I can plan against. The reassuring
> kind is the kind that burns you.

**It also found four contradictions, every one of them introduced by the corrections themselves.**
That is the pattern to watch: each round of fixes created a fresh inconsistency somewhere the fix
did not look. It has now happened twice, which is why a pass that finds contradictions gets re-run
rather than assumed clean.

### What it wants that is not a writing problem

1. **A read-path control that survives `IgnoreQueryFilters()`.** Its adoption gate, and reasonably
   so. Today the honest answer is "keep the type out of the shared model", which is a real control
   but a coarse one: it cannot express "this tenant's rows of this type". §1 above is the design
   question.
2. **The allowlist itself, and any review of it.** "A default-deny allowlist claim is only as good
   as the list, and the list is not here." `security.md` asserts the central security property of
   the product and offers nothing to check it against: no contents, no link, no review record, no
   threat model. `docs/security-review.md` exists in this repository and is not referenced from the
   site at all. **That is the cheapest large win available**, and it is a decision about what to
   publish rather than a writing task.
3. **A security contact and a disclosure policy.** The page says to contact the maintainer through
   the repository, with no address, no window and no advisory history. For a dependency holding
   financial records the reader called this a procurement finding on its own.
4. **The `SaveChanges`-override tenant check, as code.** Both pages now name the hook and neither
   shows it. The step teams get wrong is reading the value you check from the store rather than
   from the entity the client sent, and prose is a weak way to say that.
