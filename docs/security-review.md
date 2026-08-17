# Security review — the deserialization path

Milestone **M5** exit criterion. Reviewed **2026-08-10** against commit `f346a63` (plan item C48).

> **Scope.** What a **server** does with bytes a **client** sent it. That direction is the whole
> of the threat model roadmap M5 states — *"an unconstrained resolver is a remote-code-execution
> vector in a product whose entire purpose is accepting serialized expression trees from remote
> clients."* The other direction is reviewed too, but held to a different standard, and §7 says
> why.

---

## 1. The path, in order

Every arrow is a place a hostile payload gets a say.

| # | Stage | Control |
|---|---|---|
| 1 | bytes → `InfoCarrierEnvelope` | `InfoCarrierPayloadLimits.MaxRequestBytes` (64 MiB, before the parse) |
| 2 | envelope → operation | `InfoCarrierEnvelopeServer`: protocol version checked, operation matched by name |
| 3 | payload → request DTO | source-generated `JsonSerializerContext`, no reflection fallback |
| 4 | `SerializedQuery` → `ExpressionNode` | payload bound again; `ExpressionJsonContext` `MaxDepth = 256` |
| 5 | `$kind` → node type | closed set of 15 `[JsonDerivedType]`s; unregistered discriminator fails the parse |
| 6 | `TypeNode` → CLR type | `TypeAllowlist` |
| 7 | `MethodNode` → `MethodInfo` | `ResolveMethod` + `Admit`: public, or two named markers |
| 8 | `Operator` → `ExpressionType` | per-node-kind allowlist of the pure subset |
| 9 | `DynamicValueNode` → object | `TypeNodeResolver` again, then constructor invocation |
| 10 | tree → execution | EF's own query pipeline on the server's model |

Stages 1, 4, 5, 7 and 8 were closed during M5 (C36, C37, C30, and the type allowlist in M2).

## 2. The finding that matters: the bound is a conjunction

`TypeAllowlist` admits more than one might expect, and two entries deserve to be named:

- **`System.Type`, and everything assignable to it.** A payload may therefore call
  `Type.GetType("System.Diagnostics.Process")` — a *public* method on an *admitted* type — and
  obtain, **at run time on the server, after every deserialization-time check has passed**, a type
  the allowlist never saw.
- **Every enum**, by the closing `return type.IsEnum`. So `BindingFlags` is admissible.

Neither is a hole, and the reason is precise and load-bearing:

> **A `Type` obtained that way has nothing to call.** Every reflection entry point that would turn
> it into an invocation is blocked, and each by a *different* clause of the same allowlist:
> `Type.InvokeMember` takes a `System.Reflection.Binder`; `MethodInfo.Invoke` and
> `ConstructorInfo.Invoke` live on declaring types that are not admitted; `Activator`,
> `Assembly` and `AppDomain` are not admitted at all. `ResolveMethod` resolves a method's
> **parameter** types through the same allowlist, so an unadmitted parameter type fails the
> signature lookup before `Admit` is consulted.

So the safety of stage 6 is not one check but a **conjunction across several**, and it would be
broken by adding any of `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`, `PropertyInfo`,
`Activator`, `Assembly` or `AppDomain` to `TypeAllowlist` — none of which looks dangerous on its
own, which is exactly the problem.

**This is asserted, not merely written down.** `DeserializationHardeningTest` builds the pivot end
to end — `Type.GetType(…)` resolves, `.InvokeMember(…)` does not — and pins each blocked type
individually. A review whose conclusions live only in prose goes stale the first time someone adds
a convenience type to a list.

**Recommendation (not taken here):** `typeof(Type)` earns its place only if payloads genuinely
carry `Type` values. If a later audit finds they do not, removing it collapses the conjunction to a
single clause and is worth doing. Removing it now is a change with a full-suite cost and no
demonstrated benefit, so it is recorded rather than made.

## 2a. Amendment — C53's base-class rule, and why it does not widen the surface

Reviewed **2026-08-10**, two commits after §2 was written, because C53 admits the **base classes
of a mapped property's CLR type** and §2's whole point is that adding types is how the conjunction
breaks. The question is fair and the answer is not "trust me".

**The method-reachability delta is nil, and that is the load-bearing fact.** `ResolveMethod` calls
`declaringType.GetMethods(flags)` **without** `BindingFlags.DeclaredOnly`, so inherited public
methods are found by naming the *derived* type. Every public method on a base was already callable
before C53 by saying the subclass. Admitting the base adds no method to the reachable set.

What it does add:

| Added | Bound |
|---|---|
| the base may be **named** (cast target, parameter type, generic argument) | a cast to a base of an admitted type is trivially safe |
| the base may be **constructed** via `NewNode` | it is a base of a type the application itself mapped, whose own constructors were already reachable |

**What it cannot add**, and this is now enforced rather than argued: `AddPropertyBaseTypes` stops
at the reflection invocation surface — `Binder`, `MemberInfo` and everything derived from it,
`Assembly`, `Module`, `AppDomain`. It could only have reached one if an application mapped a
property whose CLR type derived from one, which is absurd but not impossible; the guard means
§2's conjunction does not depend on nobody ever doing it.

It also stops at the **categories** (`ValueType`, `Enum`, `Array`, `Delegate`,
`MulticastDelegate`), which is not a security rule but a correctness one — C23 measured widening
to a category at **145 → 186**. Without it every value-typed property in every model would put
`ValueType` on the list.

`DeserializationHardeningTest` pins both against a **model-derived** allowlist, which is the one a
server actually runs; §2's theory checks the model-free list, which C53 does not touch, so without
the new cases the widening would have been unpinned exactly where it applies.

**Noted while writing this:** `typeof(Delegate)` is admitted, by the pre-existing branch that lets
`Func<,>` travel so a lambda can be serialized — `Delegate.IsAssignableFrom(Delegate)` is true.
Harmless (abstract, constructs nothing) and unrelated to C53, but it was mistaken for C53's doing
on first reading, so it is written down.

## 3. What is genuinely bounded

- **Node kinds** (C36). Not a wire field: `ExpressionNode.Kind` is `[JsonIgnore]` and answered by
  the CLR type. The wire carries `$kind`, a discriminator over 15 registered types; anything else
  fails deserialization before a node object exists.
- **Operators** (C36). Previously `Enum.TryParse` over all 85 `ExpressionType` names — `Assign`
  reached `Expression.MakeBinary` and `Throw` reached `MakeUnary`, building a mutation or a throw
  into a tree the server compiles. Now a per-node-kind allowlist of the pure subset, derived from
  the fact that a C# expression-tree lambda cannot contain an assignment, a throw or a block.
- **Methods** (C30). Public on an admitted declaring type, plus exactly two named non-public
  markers EF's own rewrites produce. Designed from an inventory of 362 methods over 84 declaring
  types across a full run, not from intuition.
- **Payload size** (C37). 64 MiB, checked *before* the parse, because the allocation the parse
  costs is what it bounds. Request direction only — the split is measured, not stylistic.
- **Payload depth.** `MaxDepth = 256`. This also bounds `NodeToExpressionTranslator`'s recursion,
  which is what stops a deeply nested payload exhausting the stack; the test asserts a 400-deep
  payload is refused.
- **AOT/trimming as a control.** `ExpressionJsonContext` is source-generated with **no reflection
  fallback resolver**, so a type not registered there cannot be deserialized at all. That was
  chosen for §4.5 and is a security property as well.

## 4. Weaknesses accepted, with reasons

| # | Weakness | Why it is accepted |
|---|---|---|
| 1 | `NewNode` calls `GetConstructor(Public \| NonPublic \| Instance)` and invokes it | Bounded by the type allowlist. A model entity with a non-public constructor is ordinary — `WithConstructorsTestBase` is built on it — and EF's own materializer does the same. |
| 2 | `MemberNode` binds with `BindingFlags.NonPublic` | A `MemberExpression` reads; the type is allowlisted; EF's own trees name backing fields. |
| 3 | `DynamicValueMapper.RehydrateObject` invokes constructors and sets properties | Type comes from `TypeNodeResolver`, so the allowlist governs. This is the widest construction primitive on the path and is worth re-reading whenever the allowlist grows. |
| 4 | Every enum is admitted | An enum constructs nothing. It can complete a *signature*, which is how it appears in the pivot above — and that is blocked elsewhere. |
| 5 | The server executes the tree against its own `DbContext` | The point of the product. Authorization of *what data* a client may query is the application's, and this library does not attempt it. **Stated so nobody assumes otherwise.** |
| 6 | `Regex` is admitted, so a payload may name `Regex.IsMatch` with a catastrophic-backtracking pattern | **DoS, not code execution — see §4a.** The overloads EF's own tests use take no timeout, so a match runs unbounded. Mitigated by the deployer, not by this library. |
| 7 | `MaxResponseBytes` defaults to `null` | **Narrowed 2026-08-17 (M8-24), and the streaming half of it is closed.** This was recorded ahead of the change as "streaming removes the natural ceiling" (`architecture.md` §6a **D7**), on the reasoning that while the server buffers a ruinous result is at least ruinous *locally* and visible. Streaming shipped **with the bound carried across rather than lost**, which is the part worth being precise about: the bound was already enforced, by `Guard<QueryDataResult>(payload.Length, …)` on the buffered payload, and that shape cannot work on a stream because it needs the bytes already in hand. Had nothing replaced it, streaming would have *removed* an existing control. `HttpInfoCarrierTransport` now counts the response bytes as it reads them and refuses at the point they pass. **The quantity counted did change**: it is now the raw response body rather than the base64-inflated payload, so an unchanged setting admits roughly 1.78× as much row data as before. What remains accepted is only the **default**, which is still `null` for the reason `InfoCarrierPayloadLimits` gives — a result is something the client asked its own server for, and this library has no basis for capping it (C37 measured a legitimate 560 MB result in this repository's own suite). A deployment that exposes this to untrusted callers should set a bound. |
| 8 | **An abandoned enumeration pins server resources** | Also D7, and it is §8a's abandoned-transaction shape reached by ordinary code: a caller that `break`s out of a `foreach` leaves a query, a `DbContext` and a store connection open. Unlike §8a's case, no crash is needed — correct-looking client code does it. |

## 4a. Amendment — `Regex` admitted (M9 J20), and why the conjunction survives

Reviewed **2026-08-17**, because §2's whole point is that adding a type is how the conjunction
breaks, and this adds one. A46 had refused `Regex` since M6; the refusal is now reversed.

**Why it was reversed.** EF's own SQLite provider translates `Regex.IsMatch` to `REGEXP`, and its
InMemory provider evaluates it. So `Query.Translations.StringTranslations.Regex_IsMatch` and
`…_constant_input` were this provider disagreeing with **every reference implementation**, on a
type that is ordinary application code. A46 recorded the refusal as deliberate but never argued
that `Regex` was dangerous — it argued that the allowlist is ADR-008 and that widening it is a
decision. This is that decision, taken with the argument written down.

**The RCE surface is untouched, and the reasoning is §2's own.** §2's bound is a conjunction over
the *reflection invocation surface*: `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
`PropertyInfo`, `Activator`, `Assembly`, `AppDomain`. `Regex` is on none of it, derives from none
of it, and constructs none of it. `RegexOptions` needs no entry either — §2 already records that
every enum is admitted.

**The one member that reaches that surface is `Regex.CompileToAssembly`**, which is still in the
.NET 10 API surface and which writes an assembly. It cannot be named, **by §2's mechanism rather
than by a special case**: `ResolveMethod` resolves every parameter type through this same
allowlist, and the overloads take `RegexCompilationInfo[]`, `System.Reflection.AssemblyName` and
`System.Reflection.Emit.CustomAttributeBuilder[]`. None is admitted, so the signature lookup fails
before the method is found — precisely how `Binder` blocks `Type.InvokeMember` in §2.

**Deliberately not resting on `PlatformNotSupportedException`.** `CompileToAssembly` throws it on
modern .NET, which is true and is the *weaker* argument: it is a property of the runtime and could
change. The signature argument is a property of this allowlist, which is what this document is
about.

**Asserted, not written down.** `DeserializationHardeningTest.Regex_is_admitted_but_CompileToAssembly_cannot_be_named`
pins the premise (`Regex` resolves, and `Regex.IsMatch(string, string)` translates), each of the
three parameter types individually, and the whole call. §2's own standard: *a review whose
conclusions live only in prose goes stale the first time someone adds a convenience type to a
list.*

**What is genuinely accepted is denial of service.** A hostile payload may send
`Regex.IsMatch(input, pattern)` with a pattern whose backtracking is exponential, on an overload
that takes no `matchTimeout`. §5 already excludes "denial of service beyond payload size" on the
ground that an expensive query is not distinguishable from a legitimate one — **that ground is
weaker here**, because ReDoS costs the attacker far fewer bytes than an expensive join and is not
bounded by the size of the data. So it is recorded here rather than left to §5.

**The mitigation is the deployer's and should be named in deployment guidance:** set the
process-wide default with

```
AppContext.SetSwitch / AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(1))
```

The library cannot do it: the timeout belongs to the call the *caller* wrote, and rewriting a
static overload the caller named into a different one server-side would be this provider silently
changing the semantics of a query — the class of thing ADR-006 exists to prevent.

## 5. Not in scope, and stated so

- **Authentication and authorization.** No identity travels in `InfoCarrierEnvelope`. A deployment
  must authenticate the transport and decide what a caller may see. Row-level authorization is an
  application concern — EF query filters on the *server's* model are the natural mechanism, and
  they are applied by the server's own `OnModelCreating`, which the client cannot influence.
- **Transport confidentiality and integrity.** TLS is the transport's business.
- **Denial of service beyond payload size.** A well-formed query that is merely expensive is not
  distinguishable here from a legitimate one. Timeouts and quotas belong in the host.

## 6. Cancellation (W6) — half landed, and the half that touches this path is the open one

**Amended 2026-08-10 (C66).** The *cooperative* half is done and now tested: the caller's
`CancellationToken` reaches every one of the nine server operations, and `CancellationTest` pins
that plus the fault-filter behaviour below. It carries **no** security consequence — the token is
an in-process object and never crosses the wire.

What this section is about is the other half, and it is untouched: a **remote cancel signal**, by
which a caller abandons a request already dispatched over a connection.
`InfoCarrierEnvelope.CorrelationId` exists for it and nothing reads it. `InfoCarrierEnvelopeServer`
deliberately lets `OperationCanceledException` escape rather than reporting it as a fault, because
cancellation is the caller's own token rather than a server-side failure. **When W6 lands, a
correlation id becomes a handle by which one caller can affect another caller's in-flight
request** — so the id must be unguessable and scoped to its connection. Recorded here because it is
the one open M5 item with a security consequence.

## 7. The response direction, held to a different standard

`InfoCarrierFaultMapper` (W5, C46) resolves an exception type **by name** and constructs it. That is
deliberately less strict than stage 6, for the reason C37 established for payload limits: a response
comes from the server the client chose to talk to, not from an unauthenticated stranger. It is still
bounded three ways — the type must already be loaded (no assembly is loaded to satisfy a payload),
it must derive from `Exception`, and it is constructed only through an exception's ordinary
`(string)` / `(string, Exception)` constructor. Anything else degrades to
`InfoCarrierServerException`.

**A client that does not trust its server has a larger problem than this**, since the server
supplies the query results themselves. Recorded so the asymmetry is a decision rather than an
oversight.

## 8. Verdict

**The deserialization path meets ADR-008 constraint 2 as written**, and the three allowlists it
mandates — node kinds, types, methods — are all default-deny and all closed. The one material
finding is §2: the safety of the type allowlist rests on a conjunction that is not obvious from
reading any single part of it, and it is now pinned by tests rather than by this document.

No change is recommended before a network transport ships. §2's recommendation and §6's
correlation-id requirement should be revisited when one does.

## 8a. Amendment — the trigger fired

Amended **2026-08-12**, as part of the M8-8 fix wave (`docs/implementation-plan.md`), because §8's
own condition — "before a network transport ships" — has now been met: `test/InfoCarrier.Core.TransportTests`
and the samples under `samples/` add a real HTTP transport, previously in-process only. This
amendment records what changed character as a result. It recommends no fix; it records that two
properties this review's earlier sections did not need to weigh now carry a cost they did not carry
in process, and that §5's authn/authz exclusion was a decision made against an unreachable path and
should be made again, not inherited.

- **`InProcessInfoCarrierServer._transactions`** (`src/InfoCarrier.Core/InProcessInfoCarrierServer.cs`)
  is an unbounded `ConcurrentDictionary`, keyed by transaction token, with no expiry. In process,
  an abandoned entry died with the test host that created it. Behind HTTP, a client that opens a
  transaction and then vanishes — closes its connection, crashes, never calls commit or rollback —
  pins a service scope, a `DbContext` and a store connection for as long as the server process
  runs. There is no eviction, no timeout, and no check that the caller replaying a transaction
  token is the caller who created it. The one SQLite file triplet leaked per test run (noted
  elsewhere in this repo as a test-cleanup nuisance) is the observable symptom of exactly this
  mechanism, not a separate defect.
- **`InfoCarrierEndpointExtensions.MapInfoCarrier`**
  (`samples/Northwind.Server/Transport/InfoCarrierEndpointExtensions.cs`) reads the request body
  with an unbounded `http.Request.Body.CopyToAsync(buffer, …)` and then copies it again with
  `buffer.ToArray()`, before `InfoCarrierPayloadLimits.Guard` (§1, stage 1 of this document) ever
  sees a length. Kestrel's 30 MB default request-body limit bounds this in a real host, but that
  bound is Kestrel's, not this endpoint's, and it sits *below* the product's own deliberate 64 MiB
  `MaxRequestBytes` — so the limit stage 1 of this review calls out, and the message it produces,
  are unreachable behind a default-configured Kestrel. An application that raises Kestrel's limit,
  or hosts behind a server with no such default, gets none of the bound §1 describes.
- **§5's exclusion of authentication and authorization** was written when nothing in this repo
  could reach the deserialization path over a network. That is no longer true. The exclusion is not
  reversed by this amendment — it is flagged as a decision that was made under a precondition which
  no longer holds, and so should be made again, deliberately, rather than carried forward as if
  network-reachability had already been weighed.

No code change accompanies this amendment. Both properties are recorded in
`docs/implementation-plan.md`'s Phase H "what Phase 1 leaves open" list alongside the three items
already there.
