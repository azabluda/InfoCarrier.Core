# Upstream defects

Defects in somebody else's code that this repository has diagnosed. Each entry names the site, the
evidence it was found by, and **which InfoCarrier functionality it blocks** — because a defect that
blocks nothing is worth reporting and is not worth pricing a route around.

**The file exists because a diagnosis is not a report.** Five of these were traced to a named method
in a named file, written up in `test/known-failures.txt` or in the plan, and then left there. A
reader of those files cannot tell a defect that was sent from one that was not, and one of them was
cited with an issue number that does not describe it.

**An entry leaves §1 only when an issue number exists**, and then it moves to §2 with its link. Do
not delete an entry: a report that was filed is exactly the thing a later reader needs to not file
twice.

## 1. Not reported

### 1.1 The property-bag materializer skips a guard the same method applies eight lines later

**Site.** `StructuralTypeMaterializerSource.AddInitializeExpression`, its local function
`CreateMemberAssignment`, in the branch taken when `IsPrimitiveCollection` is true and the CLR type
is not an array. That branch calls `MakeMemberAccess(parameter, property.GetMemberInfo(…))`, and
`MakeMemberAccess` on a `PropertyInfo` is `Expression.Property`. For a **property-bag** complex
type every member is the `Item[string]` indexer, so the call supplies no index argument and .NET
refuses it. The same local function guards its final `return` with `property.IsIndexerProperty()`
and builds a `MakeIndex` instead.

**Symptom.** `ArgumentException: Incorrect number of arguments supplied for call to method
'System.Object get_Item(System.String)'`, raised on the server during `SaveChanges`.

**Why only this provider reaches it.** EF's own suites construct the object and track it. Nothing
on EF's side materializes such an entity **from a value buffer** — which is what this provider must
do, because the entity reaches the server as values. The server's own stack was printed off
`exception.Data["InfoCarrier.ServerStackTrace"]` (R65) and names every frame down to
`ServerSaveChangesExecutor.Materialize`.

**What it blocks here.** Inserting an entity whose complex property, or complex collection, has a
`Dictionary<string, object>` CLR type and holds a primitive collection. This is the **single entry
under "Not supported"** on [`website/docs/limitations.md`](../website/docs/limitations.md). Two
spec tests, both parameterizations of
`ComplexTypesTrackingInfoCarrierTest.Can_track_entity_with_complex_property_bag_collections(state: Added)`.
The route around it has to avoid `GetOrCreateMaterializer` and reproduce constructor binding, which
was priced in M9 and declined.

**`dotnet/efcore#36175` does not track this, and both files here say it does.** That issue is
*"Support notification change tracking for complex types"* — a Backlog **feature request** opened by
a member in June 2025. EF's SQL Server suite cites it when disabling a neighbouring test, which is
corroboration that the shape is unsupported there and is not a report of this branch. Read on
2026-09-07. `test/known-failures.txt` and `limitations.md` are corrected in the same step that
created this file.

**To report:** a model with `ComplexCollection` over `List<Dictionary<string, object>>` whose
declared members include one primitive collection, plus a materialization from a value buffer. The
fix is one branch applying the guard its own method already applies.

### 1.2 `EnumerableClassKey.Equals(object)` casts to the wrong type

**Site.** `KeysWithConvertersTestBase.EnumerableClassKey.Equals(object)` in
`EFCore.Specification.Tests`:

```csharp
public override bool Equals(object obj)
    => obj == this
        || obj?.GetType() == GetType()
            && Equals((IntClassKey)obj);      // <- should be (EnumerableClassKey)obj
```

The guard passes — both objects really are `EnumerableClassKey` — and the cast then throws
`InvalidCastException`. A copy-paste bug in EF's own test type.

**Who reaches it.** Any caller whose expression tree carries such a value as a
`ConstantExpression`: EF's `ExpressionEqualityComparer.CompareConstant` calls `Equals` on tree
constants to build the compiled-query cache key. EF's own providers never have one there, because a
captured variable is already a parameter by then.

**What it blocks here.** **Nothing today.** J21 removed this provider's path into it — a value the
model maps now travels boxed, so the server's funcletizer lifts it back into a parameter instead of
meeting it as a constant. `KeysWithConvertersInfoCarrierTest` is 47 of 47. It cost two false
clearances and a milestone's worth of confusion before that.

**To report:** one word in one test file. Cheap for EF to take and cheap for us to write.

### 1.3 The owned-JSON structural-equality base has its four comments swapped

**Site.** `OwnedJsonStructuralEqualityRelationalTestBase`. Each of the four `Contains_*` overrides
carries, directly above its assert, the message text belonging to the **other** exception:
*"The given key … was not present in the dictionary"* — a `KeyNotFoundException`'s own message —
sits above `Assert.ThrowsAsync<InvalidOperationException>`, and *"No backing field could be found
for property …"* — an `InvalidOperationException`'s — sits above
`Assert.ThrowsAsync<KeyNotFoundException>`.

**What it blocks here.** Nothing, and that is not the same as costing nothing. It produced two
wrong classifications in this repository (R167 and R174), both of which read the comment as the
statement of cause and concluded that the two products fail identically. They do not, and the
asserts had said so all along.

**To report:** a documentation fix, and worth one sentence saying which half is authoritative.

### 1.4 `Correlated_collection_with_distinct_3_levels` asserts something no answer can satisfy

**Site.** `GearsOfWarQueryTestBase.Correlated_collection_with_distinct_3_levels`. The projection is
an anonymous type whose `Members` member is a lazily evaluated `IEnumerable<>`. The
compiler-generated `Equals` compares members with `EqualityComparer<T>.Default`, which for an
iterator is reference equality, and two iterators are never the same reference.

**The evidence is a self-comparison, so nothing about this provider is involved.** C64 ran the
base's own *expected* query twice over the same in-memory data and compared the two results with
the assertion the base uses:

```
EXPECTED-vs-ITSELF: EqualException
same Squad instance both times: True
```

**What it blocks here.** Two spec tests, permanently red on the InMemory tier —
`GearsOfWarQueryInfoCarrierTest.Correlated_collection_with_distinct_3_levels`, both async values.
They are the whole of this suite's "wrong answer" class, and the answers are right: a side-by-side
dump matched squad for squad, member for member, weapon count for weapon count.

**Why no other provider notices.** Every one of them refuses the query before the assertion runs —
InMemory with `DistinctOnSubqueryNotSupported`, every relational provider with
`DistinctOnCollectionNotSupported`.

**To report:** the collection member needs materializing, or the assertion needs a sequence
comparison.

### 1.5 The parameter half of `Contains` over an owned JSON collection

**Site.** Unlocated. `OwnedJsonStructuralEqualityRelationalTestBase` pins three `Contains_*`
overrides at `KeyNotFoundException`, whose message names the collection's
`__synthesizedOrdinal` shadow key. Its neighbour `Associate_with_parameter_null` carries `// #36401`
and these carry no issue number.

**Whether this is a second defect or one already tracked is not settled here.**
[`dotnet/efcore#36400`](https://github.com/dotnet/efcore/issues/36400) describes the same family and
quotes the *other* exception, raised from
`RelationalSqlTranslatingExpressionVisitor.TryRewriteStructuralTypeEquality`. Two exception types
from two paths through one feature may be one defect or two, and EF's own base does not say.

**What it blocks here.** **Nothing since V8.** This provider used to send a captured owned-type
instance inline, which put all four tests on EF's inline branch; boxing an owned entity type puts
the parameter form back on EF's parameter branch, and the class is 15 of 15. Both products refuse
the query, and they now refuse it identically.

## 2. Already reported

Nothing here needs writing. The list exists so that an entry in §1 is not filed twice, and so that a
reader meeting one of these numbers in a comment can see what it covers.

| Issue | What it is | Where it shows here |
|---|---|---|
| [dotnet/efcore#36400](https://github.com/dotnet/efcore/issues/36400) | A nested owned entity compared to an inline value or a parameter throws, both ways | The `OwnedJsonStructuralEquality` `Contains_*` family. Matched exactly since V8 |
| [dotnet/efcore#36401](https://github.com/dotnet/efcore/issues/36401) | An owned JSON entity compared to a **parameterized** null translates to `WHERE 0 = 1` instead of `IS NULL`, so the rows are wrong | `Associate_with_parameter_null`. This provider answered it correctly until V8, by the accident of sending the parameter inline; it now reproduces EF's answer, and the `limitations.md` entry claiming otherwise is removed |
| [dotnet/efcore#33522](https://github.com/dotnet/efcore/issues/33522) | A `byte[]` inside a JSON document is not comparable by value | `Json_predicate_on_byte_array`. EF's own SQLite override adopted in C85 |
| [dotnet/efcore#30730](https://github.com/dotnet/efcore/issues/30730) | `Array_of_TimeOnly` round-trips wrong on SQLite | Reclassified from "ours" to SQLite's in one run, by reading the row the store actually holds |
| [dotnet/efcore#31751](https://github.com/dotnet/efcore/issues/31751) | Lazy-loading proxies need threads, which WebAssembly has none of | Automatic lazy loading in Blazor WebAssembly. Not a defect this repository found; listed because two documents cite it |
| [FirebirdSQL/NETProvider#1277](https://github.com/FirebirdSQL/NETProvider/issues/1277) | `FbQuerySqlGenerator` wraps a plain table after `LATERAL` and never added the branch for a function, which is the whole of that provider's fourteen "Not supported on Firebird" skips | Tier C. **Reported by this repository on 2026-09-04**, with the repro and the suggested branch. `FirebirdLateralQuerySqlGenerator` in the test harness is the correction, and is deleted when the fix lands |
