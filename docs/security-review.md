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
| 5 | `$kind` → node type | closed set of 16 `[JsonDerivedType]`s; unregistered discriminator fails the parse |
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
- **Every enum**, by the `type.IsEnum` clause of `Evaluate`. So `BindingFlags` is admissible.
  **That clause used to be the closing line, and it was reached less often than this sentence
  claimed** (R72, 2026-09-01): a type *nested in a generic type* is itself a constructed generic
  type, so the generic decomposition above it denied every such enum before the rule ran. The
  clause now sits before that decomposition, which makes the statement in this bullet true rather
  than aspirational. **It widens nothing the conjunction below depends on** — the surface that
  bullet is measured against is `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
  `PropertyInfo`, `Activator`, `Assembly` and `AppDomain`, and an enum is none of them and derives
  from none of them.

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

## 4b. Amendment — the relational operation hosts admitted (R78), and why the conjunction survives

`TypeAllowlist` admitted `EF`, `DbFunctions` and the *core* `DbFunctionsExtensions`, but not the two
classes that actually declare the markers a caller writes:
`Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions` (`EF.Functions.Collate`, `Least`,
`Greatest`) and `Microsoft.EntityFrameworkCore.EFExtensions` (`EF.Constant`, `EF.Parameter`,
`EF.MultipleParameters`). Both live in `EFCore.Relational`, which `InfoCarrier.Core` does not
reference (M9), so neither can be written as `typeof`. They are matched by **full name and assembly
name** instead, the same by-name route `ServerBoundaryAnalyzer` takes for
`FromSqlQueryRootExpression`.

**Why the refusal was wrong.** The server is an ordinary relational provider and translates all six
markers. Refusing them at the client boundary made this provider disagree with every reference
implementation, which is precisely the reasoning that admitted `Regex` in §4a — and it cost six reds
in `NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest` before anyone noticed, because this
repository had no `EF.Functions` coverage at all.

**Why §2's conjunction survives.** That bound is over the reflection *invocation* surface —
`Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`, `PropertyInfo`, `Activator`, `Assembly`,
`AppDomain`. Neither class is on it, derives from anything on it, or constructs anything on it.
Their parameters are `DbFunctions`, scalars, `string` and arrays. The generic markers
(`EF.Constant<T>` and friends) are bounded **by §2's own mechanism rather than by luck**:
`ResolveMethod` resolves every parameter type through this same allowlist, so a `T` bound to an
unadmitted type fails the signature lookup before the method is found — exactly how `Binder` blocks
`Type.InvokeMember`. Naming a host permits the type to be *named*; a method still has to resolve by
signature.

**What is accepted.** Nothing new. These are query-shaping markers with no side effect on the
server beyond the SQL they contribute, and unlike raw SQL they cannot reach a table the model does
not map — the distinction R75 turned on.

## 4c. Amendment — the application may register types explicitly (R85), and why that is the safe shape

Reviewed **2026-09-01**. §2 ends with a sentence that had no API behind it until now:

> *Never admitted by inference — only ever by an application registering one explicitly, which is
> its own decision.*

**What forced the question.** A caller cannot use their own store's `EF.Functions`. `Like` works
because it is declared on EF Core's **core** `DbFunctionsExtensions`; `Glob` is
`SqliteDbFunctionsExtensions`, `DateDiffDay` is `SqlServerDbFunctionsExtensions`, and
`InfoCarrier.Core` references no provider, so it can name neither. Measured, not assumed: a `Glob`
probe was refused by `QuerySplitter.RejectClientEvaluation` while the server translated the same
call to `GLOB` without difficulty. **A list of provider names inside this package was rejected as
the fix** — it cannot enumerate providers it does not reference, and a *pattern* (*"any class named
`*DbFunctionsExtensions`"*) cannot be reviewed, because §2's argument is a per-class conjunction and
a pattern admits classes nobody has seen.

**The shape, and it is two halves that do different jobs.**

| Half | API | What it decides | Security boundary? |
|---|---|---|---|
| client | `UseInfoCarrier(client, o => o.AllowTypes(…))` | what this application's own code may **send** | **no** |
| server | `services.AddInfoCarrierAllowedTypes(…)` | what a **payload** may name | **yes** |

Only the second one is this document's subject. The client's list governs code the application
already controls; widening it can produce a query the server refuses, never a payload the server
accepts. **The server's list is the one to review**, and it is the one `TypeNodeResolver` reads.

**Why this does not weaken §2.** It cannot be reached by inference — there is no rule that guesses,
and every entry is a line an application wrote. The conjunction is unchanged and is still broken by
admitting any of `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`, `PropertyInfo`,
`Activator`, `Assembly` or `AppDomain`; that hazard now has a place to be stated, and both API
members state it. **This is strictly better than the alternative that was actually in front of us**,
which was to widen the *static* list inside this package on behalf of every deployment at once.

**`SqliteDbFunctionsExtensions` itself clears §2's bar**, for the record and as the worked example:
a static class on no reflection surface, deriving from nothing on it, constructing nothing, whose
parameters are `DbFunctions`, `string` and `byte[]`. That is R78's reading applied to a third class.

**Pinned by three tests in `SqliteSmokeTest`**, not by this prose: the call is refused with nothing
registered, it works with both halves registered, and — the one that makes the two registrations
look like something other than duplication — **registering on the client alone still fails on the
server**, with this document's own rejection message.

## 5. Not in scope, and stated so

- **Authentication and authorization.** No identity travels in `InfoCarrierEnvelope`. A deployment
  must authenticate the transport and decide what a caller may see. Row-level authorization is an
  application concern — EF query filters on the *server's* model are the natural mechanism, and
  they are applied by the server's own `OnModelCreating`. **A client cannot influence those
  filters — but it can write a query they are not part of, and §5a is the reading of that.**
- **Transport confidentiality and integrity.** TLS is the transport's business.
- **Denial of service beyond payload size.** A well-formed query that is merely expensive is not
  distinguishable here from a legitimate one. Timeouts and quotas belong in the host.

## 5a. Amendment — raw SQL (#60, R95), and why it is a change of posture rather than a wider list

Written **2026-09-02**, from R94's measurements rather than from an expectation.

**Every other control in this document is about what a payload may *name*.** §2's conjunction,
ADR-008's allowlist, §4a's `Regex`, §4b's operation hosts, §4c's application registration — each
one asks whether some CLR type or method may appear in a tree the server rebuilds, and the server
then executes only trees built from a vocabulary it controls. **`FromSql` is the first construct
where the client hands the server a string to run.** A payload that names nothing dangerous can
still carry `DROP TABLE`, so §2 is not the precedent here and cannot be stretched to be one:
there is no set to enumerate and no per-class conjunction to check, because SQL text is not a
naming question. **§4c rhymes with the API shape and gives no support to the argument.**

**What was measured, and it decides the name of the gate.**
`Sqlite/RawSqlExecutionProbeTest` (R94) answers the two questions this section rests on, and both
answers are in the direction that removes options:

| Question | Answer |
|---|---|
| Does one `CommandText` run more than one statement? | **Yes.** `SELECT 1; DROP TABLE Probe;` drops the table — on `ExecuteNonQuery` and on the `ExecuteReader` path EF itself takes, where the trailing statements run as the reader is advanced or disposed. |
| Does EF pass an uncomposed `FromSqlRaw` through unwrapped? | **Yes.** The caller's string *is* the command, character for character. The `FROM (…)` wrap appears only when something is composed on top, and the caller decides whether to compose. |

**Therefore there is no read-only version of this feature to grant.** The wrap that would have
produced one is optional from the caller's side, and the store executes every statement regardless.
Enabling raw SQL on a server enables **arbitrary SQL execution** on that server's connection, under
whatever rights it holds — `INSERT`, `DROP`, `ATTACH`, anything the store's own grammar allows.

**The shape, and it is R85's two halves doing the same two jobs.**

| Half | API | What it decides | Security boundary? |
|---|---|---|---|
| client | `UseInfoCarrier(client, o => o.AllowArbitrarySqlExecution())` | whether this application's own code may **send** raw SQL | **no** |
| server | `services.AddInfoCarrierArbitrarySqlExecution()` | whether a **payload** may carry SQL this server runs | **yes** |

**Default-deny, and the default is what ships.** A server that does not register refuses a raw-SQL
payload; a client that does not register refuses to send one, with EF's own `TranslationFailed` —
the answer every other provider gives for a construct it cannot translate. **The name says what is
granted rather than which API is unblocked**, because "enable `FromSql`" reads as a query feature
and this is not one.

**What a deployment loses by registering, stated once.** The server's query filters are applied by
EF when it builds a query from the server's model. A raw-SQL query does not go through that: the
client writes its own `FROM`, so no filter is in the statement. Combined with the standing note
that `IgnoreQueryFilters` is not refused (`roadmap.md`, `cold-read-findings.md` §1), **a server
that registers this has no query-filter-shaped tenancy control left at all** — not for reads and
not for `ExecuteUpdate`/`ExecuteDelete`. The controls that still work are the ones outside the
model: a database account with the rights the caller should have, a server-side query interceptor,
and authenticating the transport. **A deployment that relies on query filters for row-level
authorization must not register this.**


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

### 6a. Amendment 2026-08-25 — W6 closed, and not by the mechanism this section describes

**This section is now history and its warning does not apply.** W6 landed on 2026-08-24 through
the transport, not through the envelope: `MapInfoCarrier` hands `HttpContext.RequestAborted` down
the chain, and `ServerQueryExecutor.ExecuteQueryAsync` gives that token to EF, which gives it to
the `DbCommand`. A caller who abandons a request drops the connection, and the store cancels the
command. **Nothing reads `InfoCarrierEnvelope.CorrelationId`**, so no id has become a handle by
which one caller can affect another's in-flight request, and the "unguessable and scoped to its
connection" requirement above has nothing to attach to.

`CorrelationId` still exists on the envelope and is still part of the public surface. What it does
is narrower than this section assumed: the server copies it onto the response, because every
response is built as `request with { ... }`, so a caller that sets one gets it back. It carries no
authority and addresses nothing, which is why it is not a handle. Its own comment said otherwise
until 2026-08-25 and now says this.

**The requirement is not retired, it is relocated.** Should a later release make a correlation id
address an in-flight request, everything §6 says about unguessability and connection scope applies
from that moment. The same property is already live elsewhere and is a real gap: the W3
transaction token addresses server-held state and is not bound to its creator. That is stated for
consumers in `website/docs/security.md` and in `SECURITY.md`, and tracked in `roadmap.md` M8.

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

Amended **2026-08-12**, as part of the M8-8 fix wave (`docs/plans/v10/implementation-plan.md`), because §8's
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
`docs/plans/v10/implementation-plan.md`'s Phase H "what Phase 1 leaves open" list alongside the three items
already there.
