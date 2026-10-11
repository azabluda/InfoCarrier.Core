# Anonymous and named query types: comparison audit

Investigation for [issue #218](https://github.com/azabluda/InfoCarrier.Core/issues/218),
2026-10-10, against `be22ae416c8333fa5a69c12487e490d30b9f9949`.
No production behavior change is proposed by this audit. The second investigation pass used
throwaway probes, described below, rather than adding a permanent test mechanism.

## Findings

The asymmetry is real, but final projection construction is only one part of it.
Client admission, server knowledge, server permission, provider translation, and result
reconstruction are separate decisions. Neither registration nor a successful result proves
that filtering, grouping, or joining ran in the database.

`TypeAllowlist.Evaluate` admits original anonymous types by their component types and bounds.
It does not consult a server catalog. Named application classes, records, and structs need
explicit admission unless the model already admits them. Being a record or value type is
not an admission rule. Listed tuple definitions delegate admission to their components;
inspect the exact tuple arity rather than assuming every `System.Tuple` definition is built in.

`QueryExecutor` substitutes parameters before `QuerySplitter.Split`. The splitter normalizes
and rewrites carriers and projections before boundary analysis. It can therefore eliminate
a named type from the shipped tree without admitting that type. Constructor and initializer
forms are not interchangeable: existing tests distinguish an initializer carrier that ships
from a constructor key that is refused for lacking value equality.

The old `JoinKeyRewriter` remains in source, but the current `QueryExecutor` does not invoke
it. Anonymous join keys now use descriptors. Its comments about anonymous denial and the
seven-member limit describe the earlier path, not current anonymous execution.

## Registration matrix

These are source-derived planning outcomes, conditional on the type surviving rewriting.
They are not a new exhaustive runtime test matrix.

| Representation | Neither side | Client only | Server only | Both sides |
|---|---|---|---|---|
| Anonymous shape with admitted components | Client can send; missing server catalog refuses | Client shape registration is not a planning permission; server still refuses | Registered server structure can execute | Same server path; no symmetric client catalog is required |
| Named class, record, or struct outside the model | Eligible projection can reconstruct locally; other uses depend on rewrites and guards | Sent type must resolve, then server permission refuses | Client still lacks admission; server registration alone does not change planning | Can cross; provider translation and value mapping still decide success |
| Built-in tuple with admitted components | Can cross without application registration | Additional registration normally unnecessary | Additional registration normally unnecessary | Admission does not guarantee translation |

Anonymous shape registration and component admission are independent. A prototype does not
authorize an otherwise denied component. A named type's assembly being loaded also does not
authorize that type. Model-admitted types must be excluded from tests intended to measure
explicit application registration.

## Operations and semantics

| Use | Current evidence and comparison constraint |
|---|---|
| Final projection | Anonymous construction normally ships and needs server catalog membership. Eligible unadmitted named construction uses existing local reassembly. Compare columns, original result type, null results, and entity tracking. |
| Filtering | An untransportable method in a predicate is refused. Member reads through a rewritten carrier can remain server-side. Do not infer local filtering from the original carrier type or a historical test name. |
| Ordering | Scalar member ordering can survive carrier rewriting. Relational ordering with an unadmitted key has a dedicated refusal guard. Whole-object ordering also depends on provider translation and comparison semantics. |
| Grouping | Registered named keys have direct statement comparison coverage. Unregistered named keys can group locally after a whole-table transfer. Final grouping over a locally reconstructed element can still use server ordering. These are different shapes. |
| Join and group join | Registered named keys have direct comparison coverage; unregistered value-equality keys can read both tables and join locally. Constructor reference-equality keys can be refused, while initializer carriers can be rewritten. A nine-member anonymous join already has server execution coverage. |
| Distinct and equality | Several constructed-type and nested-anonymous cases already compare database statements. Class reference equality, record equality, struct equality, and tuple equality must remain distinct. Database translation can decompose construction instead of invoking runtime equality. |
| Nested values | Existing anonymous protocol and catalog tests cover nested objects, arrays, lists, component permissions, runtime identities, and original-type restoration. Registration must cover the nested structure. |
| Parameters and constants | Substitution occurs before planning. Runtime payload types and captured-instance validation also matter. An admitted anonymous constant still needs catalog membership if sent. A local anonymous-list filter already has a backend-refusal comparison. |

Tuple syntax, `ValueTuple.Create`, constructor expressions, and constructor expressions carrying
member bindings are different expression trees. A transportable tuple is not proof of a
translatable join key. Likewise, named record constructors can contain behavior that an anonymous
replacement never executes. No universal replacement between these representations is justified.

## Resolution and returned values

`TypeNodeMapper.ToTypeNode` writes anonymous member descriptors and remembers original client
types for the exchange. Named types use names and generic arguments. `TypeNodeResolver.ResolveCore`
requires anonymous catalog membership, resolves components separately, and binds response identities.
Named resolution uses runtime type lookup and loaded assemblies before enforcing permission.

Three failures remain distinct: missing named type resolution, resolved-but-denied named type,
and missing anonymous catalog membership. All can throw `InvalidOperationException`; asserting
only that exception type cannot distinguish them. Anonymous catalog lookup occurs before component
resolution, so a missing catalog entry can mask a second component failure.

`DynamicValueMapper.FromShape` resolves the returned type before object reconstruction.
`QueryExecutor.Materialize` eagerly materializes the shipped element type, then applies the
residual. Execution registrations reach both request planning and response resolution.
`ExecuteAsync` logs and rethrows transport failures; it does not retry with another query split.

Anonymous generated identity is not original compiler identity. Catalog and protocol tests already
cover cross-assembly prototypes, exchange identity restoration, reference-valued components,
structural equality, permission revocation, and refusal of conflicting identities. A separate-process
named-type test remains necessary to prove absence rather than denial.

## Coverage and validation

Existing coverage was inspected through Roslyn CodeLens and member source. Relevant families are
`QuerySplitterTest`, `TypeAllowlistBoundaryTest`, `AnonymousShapeProtocolTest`,
`AnonymousShapeCatalogTest`, `AnonymousShapeStructuralRegistrationTest`,
`ServerParameterizationTest`, and `NorthwindOverHttpTest`.

Focused Release execution on 2026-10-10:

- ProjectionSplit plus ServerParameterizationTest: Passed: 246, Failed: 0, Total: 246.
- NorthwindOverHttpTest: Passed: 5, Failed: 0, Total: 5.

The initial transport build encountered sample-server file locks. A separate artifacts directory
avoided changing or stopping the server. That run then failed during host startup because Windows
Event Log access was denied. A single-case diagnostic run located that failure before query execution.
Setting `Logging__EventLog__LogLevel__Default=None` for the test process allowed all five cases to pass.
The isolated restore reported vulnerability-data connection warnings. These runs establish the
existing tests only; they do not establish every cell in the broader comparison.

## Measured comparison, second pass on 2026-10-10

The final throwaway transport probe collected 1,152 outcomes: 18 expression forms, 16 operations,
and four independently configured registration states. Every outcome includes a plain SQLite
Entity Framework control, request count, response bytes, executed command text, results, and errors.
The collector itself passed one test; its observations include expected refusals and differences,
so this is not a claim that 1,152 behavior tests passed.

Forms were anonymous, constructor class with reference equality, initializer class, constructor
class with explicit value equality, record class, ordinary struct, record struct, reference tuple,
and value tuple. Three additional forms supplied explicit constructor member metadata for a record,
reference tuple, and value tuple. Six forms repeated anonymous, value-equality class, record class,
record struct, and both tuples with a null first component for German customers.

Operations were final projection, member filtering, member ordering, whole-value ordering, aggregate
grouping, final grouping, join, group join, distinct, comparer equality with a literal constant,
object equality with a captured value, constant-list membership, literal constant projection,
captured projection, compiled-query parameter projection, and nested projection.

The seed contained 65 customers. Non-null two-component grouping and distinct returned 51 rows;
the self-join returned 111 rows. All comparisons included original runtime result type names.
Member ordering compared the returned sequence; other result comparisons used multisets.
This does not prove stable ordering between equal keys, reference identity, or constructor side
effects. Existing protocol and materialization tests remain the evidence for those separate concerns.

### Admission and simple projection

| Form | Neither registered | Client only | Server only | Both registered |
|---|---|---|---|---|
| Anonymous | Catalog refusal | Catalog refusal | Same result as control | Same result as control |
| All six named forms | Local construction; same result | Server allowlist refusal | Local construction; same result | Same result as control |
| Reference and value tuples | Same result as control | Same result as control | Same result as control | Same result as control |

Each sent refusal made one request and executed no query command. No refusal in the final matrix
made a second request. Client refusals made zero requests. Registering an anonymous type through
client `AllowTypes` did not make an absent server catalog acceptable. Server-only named registration
did not change client planning. Nested anonymous wrappers had their own server catalog requirement;
permission for the named value alone could not authorize the wrapper.

### Construction metadata and provider translation

Ordinary constructor records and classes were not interchangeable with anonymous constructions.
For example, the direct provider refused filtering through `new RecordCard(c.Country, c.City).Country`.
With registration on both sides, the wire followed that refusal. Without client registration,
InfoCarrier constructed the record locally and filtered the fetched projection instead.

The same record constructor represented by `Expression.New` with explicit property-member mappings
allowed the direct provider to translate the member filter and ordering. Both registered and
unregistered client cases then retained server filtering. The reference tuple showed the same
member-metadata distinction. A value tuple with member metadata gained member filtering and ordering,
but its join remained refused by the direct provider and wire alike.

The initializer class retained server filtering and grouping without application registration
because carrier rewriting removed that type from the shipped tree. Its join and group join were
answered through rewriting when unregistered, while the direct provider refused the original
reference-equality form. Registering both sides let that original form reach the provider and its refusal.

These observations rule out a policy based only on `IsAnonymous`, `IsValueType`, or record syntax.
The exact expression, constructor mapping, equality semantics, and query position all matter.

### Execution location and transferred data

Unregistered constructor records and explicit value-equality classes grouped locally after one
whole-customer-table read. Their joins and group joins made two requests and read both customer
sources in full. Registering both sides retained a single database grouping or join command.
The constructor class with reference equality was refused locally for aggregate grouping and joins
instead of receiving the same value-equality treatment. Its final grouping and distinct still had
working paths; these operations must not be inferred from its join outcome.

Measured response totals for the non-null record class illustrate the cost:

| Operation | Neither registered | Both registered |
|---|---:|---:|
| Final projection | 51,258 bytes | 42,014 bytes |
| Aggregate grouping | 76,218 bytes | 13,274 bytes |
| Join | 152,436 bytes across two responses | 30,018 bytes in one response |

These are dated observations of this seed and serializer, not performance thresholds. They include
the response envelope. Command inspection confirmed the unregistered grouping and joins fetched
customer entities without the requested database grouping or join.

### Nulls change more than cost

The null-component group-join probe returned 65 rows on both paths, but eight counts differed for
unregistered record classes, value-equality classes, and record structs. Direct SQLite Entity
Framework returned zero for the eight affected outer customers. The local group join returned one.
The direct command included a non-null condition on the first computed key component; the local
equality implementation matched the null component. Registration on both sides kept the provider
command and returned the same zero counts. Anonymous and tuple server paths also matched their
direct controls in these null-component group joins.

This is a measured configuration-dependent semantic difference, not grounds for an automatic retry
or a new inference rule. It shows why moving grouping or joins locally cannot be treated as harmless
construction. No issue was filed and no behavior was changed.

### Captured values and compiled parameters

Literal object constants and captured values were tested separately. Literal constant projections
met the provider's constant-reference validation. Captured named values and tuples behaved
differently: the direct provider returned them, while the wire, when the types were transportable,
refused them as constant references. Both an ordinary captured projection and a compiled parameter
projection showed this difference. Unadmitted named types instead used local reconstruction and
returned the same values as their controls. Anonymous captured and compiled values succeeded with
server catalog registration and failed without it.

Source tracing found the relevant distinction in
`QueryExecutor.SubstituteParametersExpressionVisitor.Substitute`: collections, wire primitives,
entities, and mapped property types are boxed; anonymous values are opened into construction;
other unmapped named values become constants. The downstream provider then applies its constant
validation. This is a focused investigation candidate even with recommended explicit registration.
It is separate from the decision about missing anonymous catalog membership. No remedy is selected.

The literal-comparer equality probe refused before sending on every form because captured-constant
validation applied to the literal argument. Repeating equality with a captured value avoided that
particular confound. Several unadmitted value-equality named forms then answered locally where the
direct provider refused translation; admitted forms reached the provider refusal. An exception type
alone therefore still does not identify the failing stage.

### Separate-process knowledge control

A dedicated sample server process loaded the server and shared model assemblies, without the
probe's named types. An unadmitted `RecordCard` projection reconstructed locally and returned 65
rows in one request. Admitting that same type on the client produced `Cannot resolve type` in one
request. The same-process client-only matrix instead produced an allowlist refusal because the
test assembly was loaded there. This establishes missing knowledge separately from missing permission.

The separate server also returned 240 registered anonymous order projections and refused an
unregistered customer shape. It was started for this investigation and stopped afterward.
The pre-existing sample host was not stopped.

### Probe corrections and limits

An initial generic self-group-join reused the identical lambda parameter object on both selectors.
That made the direct control lose its correlation. The final probe creates distinct parameter objects,
matching ordinary source lambdas. The apparent non-null group-join result discrepancy disappeared.
An initial value-equality class inherited its properties from another unregistered class; the final
comparison declares its own properties to isolate equality from permission for a declaring base type.
The null-component result difference remained with the corrected probe.

Final collection: Passed: 1, Failed: 0, Total: 1, with 1,152 recorded outcomes.
Separate-process collection: Passed: 1, Failed: 0, Total: 1, with four knowledge/catalog observations.
These supplement the earlier 246 functional and five transport checks; they replace neither suite.
All runs used Release and the isolated output directory. Windows Event Log output was disabled for
the test processes. Existing vulnerability-data connection warnings remained.

Local reproduction evidence is retained under ignored `artifacts/`: `ShapeEvidenceProbe.cs`,
`shape-evidence-console.log`, and `shape-process-console.log`. Command text is local investigation
output, not a committed statement baseline. The temporary test source was removed from the test
project after collection. The separate-process probe needs its dedicated sample host at port 5298;
the main matrix creates its own test hosts and configures catalog and permissions independently.

This is a representative two-component SQLite matrix, not exhaustive conformance across every
provider, tuple arity, constructor, comparer, nested collection, or async/sync combination.
Provider-independent bounds, component permissions, aliases, and identity conflict cases retain
their existing focused test coverage. Additional provider controls should follow a specific finding,
not duplicate this entire exploratory matrix without a question to answer.

## Ranked follow-up

1. Implement the selected version 10.3 policy from
   [anonymous-shape-planning-design.md](anonymous-shape-planning-design.md): no client declaration
   preserves version 10.2 planning, while matching declarations permit the trusted catalog path.
2. Add only the focused permanent coverage named by that design. Preserve strict server refusal and
   do not add automatic client-evaluation retry.
3. If authorized separately, reduce the captured/compiled named-value refusal into one permanent regression test
   and investigate parameter preservation. It occurs with both sides registered, independently of
   missing-catalog policy. Preserve the actual backend control and do not widen admission.
