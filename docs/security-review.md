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

## 5. Not in scope, and stated so

- **Authentication and authorization.** No identity travels in `InfoCarrierEnvelope`. A deployment
  must authenticate the transport and decide what a caller may see. Row-level authorization is an
  application concern — EF query filters on the *server's* model are the natural mechanism, and
  they are applied by the server's own `OnModelCreating`, which the client cannot influence.
- **Transport confidentiality and integrity.** TLS is the transport's business.
- **Denial of service beyond payload size.** A well-formed query that is merely expensive is not
  distinguishable here from a legitimate one. Timeouts and quotas belong in the host.

## 6. Cancellation (W6) — open, and it touches this path

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
