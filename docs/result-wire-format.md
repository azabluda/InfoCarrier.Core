# Result Wire Format — Design

Status: **IMPLEMENTED (2026-08-01)** · Milestone [M2](plans/v10/roadmap.md) · Implements
[`wire-protocol.md`](wire-protocol.md) §2.1 and [ADR-008](decisions.md) constraints 1 and 5.

**Result: 1,109 → 3,692 passing of 4,247.** All three target error classes eliminated —
"a possible object cycle was detected" 821 → 0, "could not be converted to `List<…>`" 226 → 0,
and `Dangling wire reference` 54 → 0.

## Closed — entity nodes nested in a projection

`DynamicValueMapper.Materialize` dispatched on `IsNull` / `TypeValue` / `PrimitiveValue` /
`Items` / object-shape and had **no `EntityKey` branch**. A top-level entity row went through
`ClientResultMaterializer.MaterializeEntity` (identity resolution, shadow state, `SetIsLoaded`),
but an entity reached *through a projection member* — `Select(c => new { c, o })` — fell to
`RehydrateObject`: reflection-constructed, detached, shadow properties lost.

The dangling references were a second-order consequence. `RehydrateObject` selects the
lowest-arity constructor whose parameters all match members, so an entity with a parameterless
constructor took the **ctor** branch — and that branch never called `RegisterMaterialized`. The
projected entity's wire id was therefore never registered, and every back-reference to it from
its own loaded navigations dangled. All 54 failures were `Include`-family tests, which is
exactly the shape that produces the back-reference.

Fixed in two parts:

1. `DynamicValueMapper.EntityMaterializer` — an optional hook, set on the client only, that
   routes any entity-keyed node to `ClientResultMaterializer.MaterializeEntity` wherever it
   appears in the graph. `FromShape` is the bypass for an entity type the client model does not
   know, since routing that back through `FromDynamicValue` would re-enter the hook.
2. `RehydrateObject` now registers the wire id on **every** branch. Only a genuinely
   parameterized constructor still reads its arguments before the instance exists, which is
   safe: a type reachable only through its constructor cannot be mutated into a cycle back to
   itself.

---

## 1. What is broken

Both ends serialize **live CLR object graphs** with reflection-based `System.Text.Json`:

```csharp
// ServerQueryExecutor.SerializeResult
JsonSerializer.SerializeToUtf8Bytes(list, list.GetType());

// ClientResultMaterializer.DeserializeRows
JsonSerializer.Deserialize(result.SerializedResults, typeof(List<>).MakeGenericType(rowType));
```

Three consequences, all observed:

| Symptom | Count | Cause |
|---|---|---|
| `JsonException: A possible object cycle was detected` | 821 | `Customer → Orders → Customer`. Every entity with a loaded navigation back to its parent. |
| `JsonException: The JSON value could not be converted to List<…>` | 226 | Whatever does serialize cannot be rebuilt into the declared row type. |
| `MissingMethodException`, `NotSupportedException` on interfaces | ~10 | Reflection instantiation of types with no parameterless ctor, or interface-typed results. |

It also violates two **LOCKED** constraints of ADR-008:

- **Constraint 1** — "EF-metadata-driven mapper … never blind public-reflection walks."
  Reflection JSON reads public properties, so shadow properties are lost and lazy-loading
  proxies are walked as if they were data.
- **Constraint 5** — "Reference-preserving serializer — circular nav refs must survive."

## 2. Why not just `ReferenceHandler.Preserve`

It is a one-line change and it does stop the cycle exception, so it deserves an explicit
rejection: it keeps serializing CLR objects by reflection, which leaves constraint 1 violated
— shadow properties still missing, proxies still walked, AOT still unreachable — and makes the
wire format depend on STJ's `$id` traversal order rather than on anything we control
(constraint 6, canonical serialization). **Not adopted.**

## 3. Design

Entity rows travel as `DynamicValueNode` graphs produced by the existing
`DynamicValueMapper`, which is already EF-metadata-driven and already has per-message
reference maps. Two changes are required to make it usable for results.

### 3.1 Wire-level reference identity

Today reference preservation returns *the same node instance* for a repeated object. That is
in-memory identity, not wire identity: the serializer simply writes the subtree again, so a
cycle still fails. Nodes need identity **on the wire**.

```csharp
public sealed record DynamicValueNode
{
    public int Id { get; init; }     // unique within the message; 0 = not referenceable
    public int? Ref { get; init; }   // when set, this node IS a back-reference to that Id
    …
}
```

- **Forward:** assign the id and register it **before** mapping members. The current code
  registers *after* `MapToNode` returns, which is only safe because entities short-circuit to
  a key. The moment an entity carries navigations, a cycle recurses forever. This ordering bug
  must be fixed as part of the change, not after it.
- **Reverse:** materialize the instance, register it by id, **then** populate members — same
  ordering requirement, mirrored. A back-reference encountered while its target is still being
  populated must resolve to the partially-built instance.

### 3.2 Two entity mapping modes

`MapToNode` currently maps every entity to `EntityKey` alone. That is correct for an entity
appearing as a *constant in a query* (identify it, don't ship it) and wrong for a *result row*
(the client needs the data).

| Mode | Used for | Emits |
|---|---|---|
| `Reference` (current) | entity constants inside a query tree | `EntityKey` only |
| `Row` (new) | entity result rows | `EntityKey` + scalars via `IProperty` accessors (shadow properties included) + loaded navigations as nested nodes |

Scalars are read through `IProperty.GetGetter()`, never `PropertyInfo.GetValue`, so shadow
state and value converters are honoured (ADR-008 constraint 1).

### 3.3 Loaded-navigation markers

Only navigations EF actually loaded are emitted, and each carries that fact so the client can
call `entry.SetIsLoaded()` (requirements §2.5 step 5). An unloaded navigation must be
distinguishable from an empty one, otherwise the client marks an unloaded collection as loaded
and silently returns wrong results.

### 3.4 Client materialization order

1. Deserialize to `DynamicValueNode` rows.
2. Per row: resolve identity via `IStateManager.TryGetEntry` on the wire key — reuse the
   tracked instance if present (existing `ClientResultMaterializer` behaviour, unchanged).
3. Register the instance under its wire id **before** populating, so cycles resolve.
4. Populate scalars through `IProperty` setters.
5. Wire navigations from the nested nodes; `SetIsLoaded` for those marked loaded.

## 4. Scope boundary

**In scope:** cycles, shadow properties, identity/reference preservation, loaded markers,
source-generated (AOT-safe) serialization of the node graph.

**Out of scope, deliberately:** wire-protocol **W1** (minimal column payload — returning only
the columns a client projection needs, requirements §3.3). Correctness first; W1 is an
optimization over this same format and does not change its shape.

## 5. Affected files

| File | Change |
|---|---|
| `Expressions/DynamicValueNode.cs` | `Id`, `Ref` |
| `Expressions/DynamicValueMapper.cs` | id assignment, register-before-recurse (both directions), `Row` mode |
| `Expressions/ExpressionJsonContext.cs` | register `List<DynamicValueNode>` |
| `ServerQueryExecutor.cs` | `SerializeResult` → node graph |
| `ClientResultMaterializer.cs` | `DeserializeRows` → node graph; populate + fix up |

## 6. Verification

- Cycle exceptions reach zero (821 → 0).
- `List<…>` conversion exceptions reach zero (226 → 0).
- `NorthwindIncludeQueryInfoCarrierTest` — the Include family is the direct test of §3.3.
- Existing `ExpressionRoundTripTest` stays green: the query-tree path uses `Reference` mode and
  must be unaffected.
- No regression in the 1,109 currently passing.

## 7. Follow-on

Unblocks **SaveChanges** (M3): the change-tracker payload has the same entity-graph shape and
the same cycle problem, and will reuse this format rather than inventing a second one.
