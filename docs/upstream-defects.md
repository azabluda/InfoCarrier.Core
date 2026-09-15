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

**`dotnet/efcore#36175` does not track this, and the corroboration this repository claimed for it
does not exist either.** That issue is *"Support notification change tracking for complex types"* —
a Backlog **feature request** opened by a member in June 2025, read on 2026-09-07.

**What the archive said, and why it is wrong.** M9's J22 recorded that *"EF's SQL Server suite does
disable the test outright (`=> Task.CompletedTask`, Issue #36175), which is the corroboration"*.
Checked in full on 2026-09-07: every one of the ~30 `#36175` overrides in
`ComplexTypesTrackingSqlServerTest.cs` sits in **`ComplexTypesTrackingProxiesSqlServerTest`**, whose
fixture sets `UseChangeTrackingProxies()`. **The plain `ComplexTypesTrackingSqlServerTest` carries no
overrides at all**, so EF's SQL Server suite *runs* `Can_track_entity_with_complex_property_bag_collections`
and it passes there.

**That makes the case stronger, not weaker.** EF passes this test on its own store, because EF never
materializes the entity from a value buffer. No EF suite reaches the branch, no EF issue describes
it, and the one number attached to it describes a different feature under a different fixture.

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

### 1.6 Projecting through a null optional owned reference crashes instead of yielding null

**Found by ADR-009 Tier D, 2026-09-14, and the first entries here against
`MongoDB.EntityFrameworkCore` rather than EF Core itself.**

**Site.** Not located. Their translator and serializers are not in `subrepos/`, and nothing in the
message names a method. **That is stated rather than guessed**, because this file already carries the
cost of one citation that did not describe the defect it was attached to. Locating it means reading
their source; the repro below is what makes that cheap.

**Symptom.** Six tests, one shape: a projection that reads THROUGH an owned reference which is null
on some rows. Five raise `NullReferenceException`; the sixth raises
`InvalidOperationException: Field 'OptionalAssociate' required but not present in BsonDocument for a
'RootEntity'`. A relational provider yields `null` for these, and EF's Cosmos suite records the same
family as its issue #36403.

| Test (EF's `OwnedNavigations` bases) | Raised |
|---|---|
| `Select_optional_nested_on_optional_associate` | `NullReferenceException` |
| `Select_required_nested_on_optional_associate` | `NullReferenceException` |
| `Select_required_associate_via_optional_navigation` | `NullReferenceException` |
| `Select_value_type_property_on_null_associate_throws` (both tracking arms) | `NullReferenceException` |
| `Select_nested_collection_on_optional_associate` | `Field … not present in BsonDocument` |

**Evidence.** `DirectProjectionTest` in `test/InfoCarrier.Core.DocumentStoreTests`, which runs EF's
bases on plain EF Core over the same embedded MongoDB **with InfoCarrier removed**. Identical
failures with and without the wire, so none of this is ours. Each Tier D test that meets it carries
`[StoreDefect("1.6", …)]` naming that control test, and `OverrideAudit` checks the two agree.

**What it blocks.** Nothing in InfoCarrier. **It bounds what Tier D can PROVE**, which is the honest
cost: where the store crashes, this repository cannot tell whether the wire would have carried the
query correctly. Those reds mean "we cannot tell", not "the wire is fine".

### 1.7 `Distinct` over a projected filtered nested collection silently returns the wrong rows

**Site.** Not located; see 1.6.

**Symptom.** `Where(e => e.AssociateCollection.Select(r => r.NestedCollection.Where(n => n.Int == 8)).Distinct().Count() == 2)`
returns **3 roots where 5 are correct**. No exception, no warning.

**Evidence.** `OwnedNavigationsServerSideControlTest.Server_side_Distinct_over_projected_filtered_nested_collection`
asserts the observed 3 directly against the server's own `DbContext`, and
`DirectCollectionTest.Distinct_over_projected_filtered_nested_collection` fails the specification
assertion the same way without the wire.

**What it blocks.** Nothing in InfoCarrier, and **this is the one to report first even so.** A
refusal is visible to a caller; a query that quietly returns three rows of five is not. It is also
the defect that nearly became ours: an override once asserted the 3 as expected, which turned the
suite green over a wrong answer.

### 1.8 Three nested aggregates collide in the store's own subquery alias table

**Site.** Not located; see 1.6. The name `o0` is generated by their translator for a subquery, and is
generated twice for this shape.

**Symptom.** `Select(e => e.AssociateCollection.Select(r => r.NestedCollection.Select(n => n.Int).Max()).Sum())`
raises `ArgumentException: An item with the same key has already been added. Key: o0`.

**Evidence.** `DirectCollectionTest.Select_within_Select_within_Select_with_aggregates`, and
`OwnedNavigationsServerSideControlTest.Server_side_Select_within_Select_within_Select_with_aggregates`.

**What it blocks.** Nothing in InfoCarrier; see 1.6 on what it bounds.

### 1.9 `Concat` of two owned collections emits `$size` against something that is not an array

**Site.** Not located; see 1.6.

**Symptom.** `Where(e => e.RequiredAssociate.NestedCollection.Concat(e.OptionalAssociate!.NestedCollection).Count() == 4)`
raises `Command aggregate failed: PlanExecutor error during aggregation :: caused by :: The argument
to $size must be an array`.

**POSSIBLY THE SAME DEFECT AS 1.6 AND NOT MEASURED EITHER WAY.** The second operand is an owned
collection reached through an OPTIONAL reference that is null on some rows, which is 1.6's shape, and
a null where an array was expected is exactly what `$size` is complaining about. This repository's own
rule is that two failures of the same shape are one defect until measured otherwise — so this is
filed separately only so the evidence is not lost, and whoever reports it should try the same query
with a REQUIRED second operand first.

**Evidence.** `DirectSetOperationsTest.Over_different_collection_properties`.

**What it blocks.** Nothing in InfoCarrier; see 1.6.

### 1.10 A top-level client-evaluable projection is refused instead of evaluated on the client

**Site.** Not located; see 1.6.

**Symptom.** A **user-defined static** method in the final projection is refused:
`Select(c => Scramble(c.Name))` raises `ExpressionNotSupportedException: Expression not supported:
Scramble(c.Name)`. EF Core evaluates an untranslatable final projection on the client rather than
refusing it, and EF's Cosmos provider does exactly that.

**NARROWED BY MEASUREMENT, AND THE FIRST HYPOTHESIS WAS WRONG.** This entry first said the trigger
was the OWNED-REFERENCE HOP, because Tier D only ever meets the shape as
`UntranslatableMethod(e.RequiredAssociate.Int)`. `ClientEvaluatedProjectionTest` settles it with
three assertions on one row:

| Projection | Result |
|---|---|
| `c.Name.ToArray()` — a BCL **instance** method, and `EF-250`'s own example | **evaluated on the client, query succeeds** |
| `Scramble(c.Name)` — a user **static** method, no hop | **refused** |
| `Scramble(c.Address.Postcode)` — a user **static** method, owned hop | **refused** |

**So the hop is ruled out and the kind of method is the trigger.** The middle row is what does it:
same value, same row, no owned reference anywhere, still refused.

**AND THEIR FIX IS REAL — IT IS THE TITLE THAT IS BROADER THAN THE BEHAVIOUR.** `EF-250`, *"Allow
client evaluation in the final projection"*, is Closed / Fixed in provider `10.0.3`, `9.1.3` and
`8.4.3`. This tier measures **`10.0.3`**, and the shape the issue used passes. What does not reach is
a user-defined static method. There is no setting that would change it: `10.0.3` exposes no
query-mode option, checked by inspecting the shipped assembly rather than assumed — `MongoQueryMode`
appears in their tracker and not in the package, so it belongs to the `EF-322` rebuild.

**Evidence.** `DirectProjectionTest.Select_untranslatable_method_on_associate_scalar_property`, both
tracking arms.

**What it blocks.** Nothing, and **InfoCarrier is FASTER than the raw store here**, which is the
reason this entry exists at all. ADR-010's projection split cuts the untranslatable node before
serialization, so the server receives only `e.RequiredAssociate.Int` and never sees what it cannot
translate. Those two tests pass over the wire and fail without it — the only such pair in the tier,
and `ClientEvaluatedProjectionTest` measures which methods it covers. `ProjectionPayloadTest` proves it is a genuine
push-down rather than whole documents crossing behind a green test.

### Their tracker, searched 2026-09-14, and what it changes

**Not GitHub: issues are disabled on `mongodb/mongo-efcore-provider`.** Their `CONTRIBUTING.md` sends
bugs to the **Jira `EF` project** (<https://jira.mongodb.org/projects/EF/issues/>), which is public
and readable through its REST API without credentials. 427 issues at the time of searching.

**That it is the right project was verified rather than inferred from the link.** Project `EF` is
named *"Entity Framework"* on MongoDB's own Jira, and its version list is the decisive part: `10.0.3`,
`9.1.3` and `8.4.3` published, `10.0.4`, `9.1.4` and `8.4.4` still open — **exactly the provider's
three NuGet release lines**, and exactly the `fixVersions` set on `EF-250`. A newer provider is
therefore already in preparation, which is where the three In Code Review entries below would land.

**NOTHING ABOVE HAS BEEN FILED AND NOTHING WILL BE WITHOUT THE OWNER ASKING.** What follows is a
search result, not a report.

**`EF-X001` AND ITS SIBLINGS ARE NOT ISSUE NUMBERS.** `EF-430` — *"Replace the EF-X001-EF-X020
placeholder keys in spec-test Fails tags with real JIRA issues"* — says they are placeholders their
own suite is still carrying. So the `EF-X001 "subquery selection"` tag that appears throughout
`NorthwindSelectQueryMongoTest` cites nothing, and a Tier D override must not cite it either.

**THE PROVIDER IS BEING REBUILT UNDERNEATH ALL OF THIS.** `EF-322`, *"Native LINQ query provider
(ground-up rebuild)"*, is In Progress, and the issues around it run to `EF-453`. Several entries
above may be answered wholesale rather than one at a time, which is a reason to re-measure this tier
against each release rather than to price a route around any of them.

| Ours | Closest on their tracker | State |
|---|---|---|
| 1.6 null optional owned reference | `EF-358` *a missing or explicitly-null embedded array materializes as null instead of an empty collection* | In Code Review |
| 1.7 `Distinct` returns 3 of 5 | **nothing found** | — |
| 1.8 alias collision `Key: o0` | `EF-357` *bare embedded-collection `.Count` projection throws `ArgumentException`* | In Code Review |
| 1.9 `$size` on a non-array | `EF-359` *filtered `Count(pred)` in a projection throws `InvalidOperationException`* — and its quoted message is the same `The LINQ expression 'o' could not be translated` four of our reds carry | In Code Review |
| 1.10 client-evaluable projection refused | `EF-250` *allow client evaluation in the final projection* | **Closed, Fixed in 10.0.3 — and 10.0.3 still refuses a user static method.** See 1.10. |

**1.10 WAS THE ONE WORTH READING TWICE, AND IT HAS NOW BEEN MEASURED.** The paragraph here guessed
that the owned-reference hop was the trigger and said so as a hypothesis. It is not: a user static
method over a plain root scalar is refused just the same, while `EF-250`'s own instance-method
example passes on the same version. **The kind of method is the trigger and the hop is irrelevant.**
Three assertions in `ClientEvaluatedProjectionTest` cost less than the paragraph that guessed, and
the guess was wrong — which is the argument for writing the test rather than the sentence.

**1.7 IS THE ONE NOBODY APPEARS TO HAVE.** No issue in 427 matches a `Distinct` over a projected
filtered nested collection returning the wrong rows. They clearly do care about the class of defect
— `EF-356`, `EF-366` and `EF-367` are all silent-wrong-data issues, and `EF-367` is theirs finding
exactly what this repository found on 2026-09-14: *"Include specification suites mask wrong-data
failures behind a bare-catch AssertTranslationFailed"*. **An override that swallows a wrong answer is
a mistake both projects made independently**, which is the strongest argument yet for the rule that a
crash or a wrong answer is never overridden here.

**The matches above are CLOSEST, not CONFIRMED.** None was verified by reading their fix or
reproducing their exact shape, so no entry has moved to §2. Doing that verification is the work that
would turn any of these into a report, and it is the owner's call whether it is worth it.

### 1.11 EF's InMemory provider crashes where its own suite pins the crash

**Recorded 2026-09-15, when ADR-009 Tier A's overrides were given reasons, and not found by this
repository.** EF's own `EFCore.InMemory.FunctionalTests` overrides each test below to assert the
crash, so EF knows. None carries an issue number, and a crash is never a store limit here, so each
Tier A override that copies one carries `[StoreDefect("1.11", …)]` with the link to EF's override.

**Site.** Not located, and not looked for. The evidence is EF's own assertion at the `v10.0.1` tag.

**Symptom.**

| Tests | Raised |
|---|---|
| `JsonTypesTestBase.Can_read_write_point`, `…_with_M`, `…_with_Z`, `…_with_Z_and_M`, `Can_read_write_line_string`, `Can_read_write_multi_line_string`, `Can_read_write_polygon`, `Can_read_write_polygon_typed_as_geometry` | `NullReferenceException`; EF's comment: *"No built-in JSON support for spatial types in the in-memory provider"* |
| `SpatialQueryTestBase.Intersects_equal_to_null`, `Intersects_not_equal_to_null` | `NullReferenceException` |
| `SpatialQueryTestBase.GetGeometryN_with_null_argument` | skipped by EF, whose comment is *"Sequence contains no elements"* |
| `GearsOfWarQueryTestBase.Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation_complex`, `Find_underlying_property_after_GroupJoin_DefaultIfEmpty` | `InvalidOperationException: Nullable object must have a value.` EF's sibling override on the non-complex test cites its issue #13721, *"Null protection"* |
| `GearsOfWarQueryTestBase.Select_StartsWith_with_null_parameter_as_argument`, `OrderBy_…`, `Group_by_on_…`, `Group_by_with_having_…` | `ArgumentNullException: Value cannot be null. (Parameter 'value')`: `string.StartsWith(null)` evaluated in .NET rather than with database null semantics |
| `GearsOfWarQueryTestBase.Include_after_SelectMany_throws` | `NullReferenceException` where the base expects EF's own refusal |
| `GearsOfWarQueryTestBase.Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result4`, `…_with_complex_projection_result` | `TargetInvocationException` |

**What it blocks.** Nothing in InfoCarrier. Tier A's store is EF's InMemory provider, so these reach
the wire as the store's answer and cross it unchanged. **It bounds what Tier A can prove**, as 1.6
does for Tier D: where the store crashes, the tier cannot tell whether the wire would have carried
the correct answer.

### 1.12 EF's relational pipeline leaves a primitive-collection parameter without a type mapping

**Recorded 2026-09-15, first diagnosed in R31.** EF's `PrimitiveCollectionsQueryRelationalTestBase`
overrides three core tests to assert that the query is refused. **This provider answers all three**,
and its overrides assert the rows, so each carries `[StoreDefect("1.12", …)]` with the link to EF's
refusal.

**Site.** Named by EF itself, on one of the three, in a comment at the `v10.0.1` tag: *"The array
indexing is translated as a subquery over e.g. OPENJSON with LIMIT/OFFSET. Since there's a CAST over
that, the type mapping inference from the other side (p.String) doesn't propagate inside to the
subquery. In this case, the CAST operand gets the default CLR type mapping, but that's object in this
case. We should apply the default type mapping to the parameter, but need to figure out the exact
rules when to do this."*

**Symptom.**

| Test | EF asserts |
|---|---|
| `Parameter_collection_in_subquery_and_Convert_as_compiled_query` | `InvalidOperationException` containing *"in the SQL tree does not have a type mapping assigned"*, with the comment above |
| `Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query` | `RelationalStrings.SetOperationsRequireAtLeastOneSideWithValidTypeMapping("Union")` |
| `Column_collection_equality_inline_collection_with_parameters` | a translation failure |

**Only the first is EF's attribution.** R31 read the other two as the same missing inference, from
their messages; EF says nothing about them, and nobody has read a fix, because none exists.

**What it blocks.** Nothing. **Why this provider does not reach that state is not established**; the
overrides measure that the rows are right, which is the claim that matters to a caller.

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
