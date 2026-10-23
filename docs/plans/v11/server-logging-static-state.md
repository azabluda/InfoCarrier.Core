# Server logging static-state cleanup

Issue [#216](https://github.com/azabluda/InfoCarrier.Core/issues/216) authorizes a version 11
breaking cleanup of server log capture, forwarding, and replay.

## Audit

`ServerLogCapture.Current` is mutable process-wide state. Every registered capture provider reads
the same `AsyncLocal<Sink?>`. A scope opened for one host therefore activates every capture
provider reached by that async flow. Nested hosts can collect each other's events, and independent
service providers are not isolated by their dependency-injection lifetimes.

`ServerLogCapture.Begin` is public and static. It lets a caller start collection without owning the
provider that receives logs. The contract cannot express which host or provider owns the scope.

`ServerLogReplay` is stateless, but it hides a logging dependency. It resolves `ILoggerFactory`
from a `DbContext` during replay. This delays logging configuration failures until a response has
already crossed the transport. It also prevents direct lifetime and failure tests.

The forwarding grants are immutable singleton values. The excluded category list and replay
formatter are immutable or stateless. They remain static where appropriate.

The assembly-wide field audit found these other mutable static stores:

| Owner | State | Decision |
|---|---|---|
| `AnonymousShapeTypes` | Generated type definitions, descriptors, trusted identities, scope table, and emission count | Retain. ADR-010 requires one locked process cache and a process-wide generation ceiling. Type emission cannot have a service lifetime. |
| `ClientResultMaterializer` | Materializer delegates keyed by runtime entity type and tracking behavior | Retain. Values are pure compiled delegates, keys contain every input, and the concurrent cache holds no host configuration. |
| `DynamicValueMapper` and `PrimitiveCoercion` | Type capability and core JSON-form memoization | Retain. Values derive only from Common Language Runtime types and framework metadata. They contain no request or provider instance. |
| `InfoCarrierFaultMapper` | Exception types resolved from fixed wire names | Retain. Resolution is a pure type-name memoization with a closed allowlist check on every use. |
| `ModelDbFunctions` | Database functions keyed weakly by model | Retain. The weak key binds each value to its model and permits model collection. |
| `SubstituteParametersExpressionVisitor` | Owned and mapped type sets keyed weakly by model | Retain. Values derive only from the keyed model and cannot cross model lifetimes. |
| `InfoCarrierMetrics` | Process meter and instruments | Retain. .NET metrics use process-wide instruments by name. They contain observations, not host configuration or request state. |

The remaining static fields are immutable metadata, generated serializer tables, cached reflection
handles, immutable empty objects, stateless singleton helpers, or compiler-generated delegate
caches. The audit covered all three production assemblies and ignored compiler-generated fields
when classifying application state.

No test-wide logging lock, collection, global reset, or disabled parallelization exists. The lock
inside one capture sink protects concurrent writes to that request's event list. It remains
necessary and is not test coordination.

## Risks to preserve

- Each server must require its own forwarding grant before opening capture.
- Sensitive server contexts must still require the separate sensitive-data grant.
- Replayed downstream events must enter an active upstream capture in chained deployments.
- Logger creation and delivery failures must still propagate from replay.
- Server faults and transport faults must keep their current shapes.
- Query projection and result materialization must not change.
- Concurrent requests must not exchange events or leak capture state after disposal.
- A capture scope must stay valid through streamed enumeration and transaction-held contexts.

## Bounded design

1. Make `ServerLogCapture.Begin` an instance member and make its ambient sink an instance field.
2. Register one capture instance as both the host logger provider and the server dependency.
3. Inject capture and both forwarding grants into `InProcessInfoCarrierServer`.
4. Replace static replay with an instance helper that receives `ILoggerFactory` in its constructor.
5. Carry that helper on `InfoCarrierQueryContext` for cached query delegates.
6. Add focused transport tests for provider isolation, concurrent requests, disposal leakage,
   chained replay, and scoped replay lifetime.

The change does not add cross-hop identifiers, deduplication state, or new forwarding policy. It
does not change the wire model or projection pipeline.
