# Wire-contract migration research

Research date: 2026-11-02. Repository inspected at
`a91c364915ca9c05c860e507bd77ea7caf448ca1`.

Status: research for [issue #262](https://github.com/azabluda/InfoCarrier.Core/issues/262),
with the owner's initial implementation choices recorded below. No migration mechanism is implemented.
The amended [entry condition](wire-versioning.md#entry-condition-and-dependencies) permits artificial
experiments while preferring a useful change that could remain as protocol version 4.

## Owner decisions, 2026-11-02

Use explicit AI-written C# mappings initially. Keep a complete physical copy of the entire contract
type set for each supported protocol version, including unchanged types. Do not share contract
declarations between versions merely to reduce duplication. Package releases can still share one
protocol version; these copies follow protocol versions, not package releases.

An artificial change is now permitted for an experiment. Prefer a useful, approximately real
change that can justify remaining in production as protocol version 4. This supersedes the earlier
research restriction against artificial changes. Label experimental evidence accurately; an
artificial change does not establish compatibility with unrelated real changes.

The comparisons below remain the evidence behind the discussion. Their selective-copy alternative
was not chosen. A separate canonical model, exact version 4 payload design, and permanent migration
architecture remain to be evaluated against the selected example.

### Owner clarification and implementation approval, 2026-11-03

The owner approved the typed-query candidate. The existing data classes become live protocol 4
contracts and also serve as the initial canonical representation. Add only the complete frozen
protocol 3 copy. There are two complete sets in total; no redundant protocol 4 copy or third
complete model is intended. Handwritten mappings connect protocol 3 directly to the live classes.
This supersedes the earlier folder suggestion showing separate `Wire/V3` and `Wire/V4` trees.

## Initial research recommendation, before the owner's choices

Preserve compatible evolution first. When a real incompatibility requires conversion, start with
explicit C# conversion at the affected boundary. Retain a complete logical definition of each
supported wire contract, including its codecs and behavior. Physical duplication of every class
is a separate choice, and package releases do not each require another contract definition.

Prefer direct conversion to a chosen execution representation over an automatic adjacent-version
chain for the first experiment. Use current representations where they preserve every supported
meaning. Introduce a separate internal representation only where it removes demonstrated coupling
or preserves information the current wire model cannot represent.

Handwritten mappings provide the clearest initial reference. Mapperly is the first generated
mapping candidate to compare if repetitive assignments justify a dependency. Mapster.Tool remains
credible, but inspected generated code disproves an unconditional reflection-free claim. Runtime
contract generation has no demonstrated requirement here.

These recommendations rank hypotheses for an experiment. They do not authorize a framework,
dependency, release, support-policy change, or production implementation.

## 1. Verified starting point and evidence limits

The local and remote `main` commit matched the handoff. The working tree was clean before this
note. [Pull request #273](https://github.com/azabluda/InfoCarrier.Core/pull/273) is merged at that
commit, [issue #264](https://github.com/azabluda/InfoCarrier.Core/issues/264) is closed, and no
open pull requests were returned. The migration issue and both candidate issues remain open.

The implementation's
[protocol constant is 3](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Common/InfoCarrierEnvelope.cs#L22).
Admission accepts only that contract. There is no implemented legacy conversion dispatch.
The [wire specification](../../wire-protocol.md) and
[release policy](../../versioning.md#client-and-server-compatibility) distinguish accepted contracts
from supported package releases. Version 11.0 supports 11.0 clients. From 11.1, support includes
the preceding minor and requires published-client acceptance in separate processes. Version 10
and pre-policy development contracts remain excluded.

The latest GitHub stable release returned was
[10.3.0, published 2026-10-13](https://github.com/azabluda/InfoCarrier.Core/releases/tag/v10.3.0).
No stable version 11 release was found. Direct NuGet queries failed in this environment, so package
publication was not independently verified there. No published test client was selected. Verify
the package feed again when selecting actual acceptance clients.

Evidence here comprises repository source inspection, tagged upstream source, generated examples,
release records, and primary documentation. No fresh mapper generation, migration experiment,
performance benchmark, or production test ran. Generated examples establish particular execution
paths, not every possible configuration. Library versions below are GitHub release facts.

## 2. The contract extends beyond ordinary properties

The current implementation already has strongly typed wire records. A new versioning design
would organize and preserve these definitions; it would not introduce typing for the first time.

| Boundary | Verified implementation | Consequence for migration |
|---|---|---|
| Buffered envelope | Version checked before operation-payload decoding | Select an admitted version before interpreting its operation bytes. |
| Operation records | Default serializer uses `InfoCarrierJsonContext` | Versioned classes need matching serializer metadata and options. |
| Query expression | `QueryDataRequest.SerializedQuery` contains separately encoded expression nodes | Copying the byte array does not convert the expression. |
| Buffered query result | `QueryDataResult.SerializedResults` contains encoded dynamic values | Response conversion must reach the inner representation. |
| Save changes | Encoded values, entry states, concurrency values, and generated-value responses | Property copying cannot establish preserved database effects. |
| Streaming | A handwritten positional frame codec uses two serializer contexts | Class attributes alone do not define the frame protocol. |
| Materialization | Dynamic values encode identity, references, nullness, tracking, and navigation state | Preserve graph meaning, not merely individual values. |

Sources: [buffered dispatch](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/InfoCarrierEnvelopeServer.cs#L56),
[outer serializer](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/SystemTextJsonInfoCarrierSerializer.cs),
[registered operation context](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/InfoCarrierJsonContext.cs),
[expression decoding](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L817),
[result encoding](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L595),
and [dynamic values](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Expressions/DynamicValueNode.cs).

The [frame codec](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Query/QueryStreamFrameCodec.cs#L129)
writes start `[0, version, slot, isEntityResult, elementTypeName]`, row `[1, value]`, log `[2, event]`,
complete `[3]`, and fault `[4, fault]`. Its reader enforces exact lengths and sequence rules.
Adding a class property and appending a frame element therefore have different compatibility rules.

The streamed endpoint checks the version header before parsing, then checks the body version.
Buffered clients check response versions before faults or results. Stream readers check the start
version before yielding. Supporting an old request consequently requires an old-readable response,
including its version, nested encodings, faults, and frame vocabulary.
[Endpoint checks](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core.AspNetCore/InfoCarrierEndpointExtensions.cs#L137),
[client checks](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/TransportInfoCarrierClient.cs).

## 3. Compare evolution strategies before choosing tooling

| Strategy | Strongest case | Main cost or limit | Appropriate evidence |
|---|---|---|---|
| Compatible additions and tolerant readers | Least new machinery when old meanings remain valid | An ignored field may contain an essential new promise | Old clients omit the field and still get the promised behavior. |
| Narrow converters | Small footprint for one localized representation change | Multiple hidden converters can become an undocumented version system | The complete affected request and response path remains bounded and understandable. |
| Version-specific contracts with explicit conversion | Historical formats are explicit and independently inspectable | Duplicated definitions and serializers; semantics still require manual decisions | The changed format cannot be safely read through the current definition alone. |
| Version-specific execution behavior | Preserves behavior that data conversion cannot express | Continued code, security, dependency, and retirement obligations | The old result or effect cannot be obtained through current execution. |
| Adjacent conversion chain | Localizes each newly introduced representation change | Repeated conversion, cumulative information loss, and intermediate-version retention | Several real changes establish composable, information-preserving transitions. |
| Replace the encoding with a schema-based protocol | Could provide stronger standardized evolution rules | A new protocol migration with its own compatibility obligations | A broader product requirement justifies changing the existing serializer. |

Microsoft explicitly distinguishes protocol compatibility from behavioral compatibility. Its
versioning guidance also supports serving separate contracts over shared implementation logic.
These principles apply here without requiring a gRPC transport.
[Microsoft service versioning](https://learn.microsoft.com/en-us/aspnet/core/grpc/versioning?view=aspnetcore-10.0).

Protobuf recommends preserving field identities and defaults. Its presence rules distinguish
missing values from explicit defaults. However, its binary compatibility rules cannot simply be
applied to this JSON protocol: ProtoJSON has different name and unknown-field rules.
[Evolution guidance](https://protobuf.dev/best-practices/dos-donts/),
[presence](https://protobuf.dev/programming-guides/field_presence/),
[ProtoJSON compatibility](https://protobuf.dev/programming-guides/json/#json-wire-safety).

Avro illustrates a different established approach: resolve a writer schema against a reader
schema with explicit missing-field defaults and error rules. That validates the need for defined
resolution rules; it does not establish that InfoCarrier should adopt Avro.
[Avro schema resolution](https://avro.apache.org/docs/1.12.0/specification/#schema-resolution).

The repository already ignores unknown object fields, but rejects unknown executable node kinds
and frame tags. Preserve that distinction. Several releases sharing contract 3 does not imply
every client recognizes every later optional response variant. A new promise needs the existing
version-change procedure; structural tolerance alone cannot establish feature support.
A shared integer cannot identify support for such a later variant. Emission needs evidence from
an already defined request capability, or a new contract when the variant changes the promise.
This is not a proposal to add generic capability negotiation.

## 4. The owner's proposal: strongest arguments on both sides

The strongest argument for complete versioned C# contract sets is control over historical
interpretation. Each reader can retain its names, discriminators, defaults, constructors, and
codec selection. Compiler diagnostics expose many accidental changes. Version-specific response
writers become explicit, and removing a retired version can remove an identifiable group of code.
This is a credible design when several materially different contracts must coexist.

The strongest argument against unconditional complete copies is that they duplicate the visible
shape while leaving the hardest compatibility problems unresolved. Equal property types do not
establish equal units, defaults, query semantics, transaction effects, or local client behavior.
An automatic mapper can successfully copy opaque bytes that still require an older decoder.
It can also map enum numbers correctly as integers while changing their meaning.

A complete logical contract does not require a physical copy of every unchanged class. Shared,
unchanged definitions can reduce duplication, but editing one then affects multiple versions.
If shared definitions are used, their historical serialized behavior needs explicit protection.
Full copies offer stronger isolation at the cost of maintenance. Decide between these options
using the actual changed type graph and the expected support window.

There is no requirement to duplicate definitions for every patch or minor. Compatible revisions
may share an integer and use one compatible superset of fields. Conversely, two identical schemas
can need distinct integers when their meaning changes. The support window concerns package
releases; the number of active wire definitions follows actual distinct supported contracts.

## 5. Direct conversion and the meaning of canonical

Kubebuilder provides an established example of converting versions through one selected
representation. Its tutorial uses an existing served version as that representation.
A separate internal model and the latest wire version are therefore choices, not prerequisites.
[Conversion model](https://book.kubebuilder.io/multiversion-tutorial/conversion-concepts),
[implemented tutorial](https://book.kubebuilder.io/multiversion-tutorial/conversion).

| Property | Direct to chosen representation | Through adjacent versions |
|---|---|---|
| Conversion definitions | One relationship per supported version | One relationship per adjacent version pair |
| Target changes | May require updates to several mappings | Often adds one new adjacent mapping |
| Request and response work | Usually one conversion in each direction | Can traverse several mappings in each direction |
| Allocations | Can avoid intermediate version objects | Can allocate intermediate graphs |
| Retirement | Old mappings can often be removed independently | A retired intermediate type may remain necessary |
| Correctness | Each supported version has an explicit relationship | Every composed path must preserve required information |

Both approaches can have a linear number of conversion definitions. Comparing direct conversion
with a quadratic all-pairs design would not justify rejecting adjacent conversion. The relevant
arguments here are execution cost, information preservation, diagnostics, and removal.

Current `ExpressionNode` and operation records already connect serialization to execution.
Using them initially avoids an immediate duplicate model. The disadvantage is that future changes
to those records affect older adapters. A separate decoded internal model is worthwhile when it
expresses stable execution concepts or preserves distinctions the current wire no longer carries.
It also adds mappings and potentially allocations for current clients.
[Current preparation path](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L148).

Whichever representation is selected, preserve the incoming contract identity whenever execution
or response behavior depends on it. Do not erase historical meaning during conversion and later
try to reconstruct it from current property values. Request and response conversions need separate
specifications: an inverse mapping is not automatically valid or possible.

## 6. Keep five design decisions independent

| Decision | Credible choices | Initial preference, subject to the real example |
|---|---|---|
| Authoritative definitions | Handwritten C#; authored schema; another explicit versioned model | Retain the existing C# definitions and historical behavior. |
| Generation time | Before compilation; during compilation; explicit maintenance command; runtime | Compile known contracts before deployment. |
| Generated-file storage | Temporary build output; reviewed generated files in Git | Avoid a second source of truth; commit only where review or reproducibility benefits justify it. |
| Internal representation | Current decoded models; separate internal models; retained old execution | Reuse current models where they preserve meaning. |
| Production conversion | Explicit methods; generated methods; runtime mapping engine | Explicit methods first, then selective generation. |

These choices combine. Generated files can be committed and checked for reproducibility during
builds. Handwritten contracts can use generated serializers and generated mappings. Build-time
reflection can be acceptable while production mapping remains static.

| Contract maintenance option | Benefit | Principal risk |
|---|---|---|
| Handwritten, committed C# | Direct control over serializer details; familiar review | Repetition and omissions when related definitions change |
| Generate during builds from frozen inputs | Consistent output without large committed diffs | Generator upgrades can change historical readers indirectly |
| Commit generated C# from frozen inputs | Exact shipped definitions remain reviewable | Stale output, noisy changes, and two artifacts requiring clear ownership |
| Generate types at runtime | Handles definitions genuinely unavailable at build time | Runtime failures, startup work, caches, deployment constraints, and security review |

An old definition cannot be reconstructed from current classes after information has been removed.
Preserve authoritative historical inputs or retained output. Never regenerate all old versions
from the current model and assume their original behavior survived.

Mapperly documents that Roslyn generators cannot consume another generator's output in the same
compilation. Contract generation followed by mapping generation therefore needs prior generated
source, a separate compilation stage, or one generator owning both operations. Mapster.Tool
already consumes compiled assemblies and needs explicit staging.
[Mapperly generation constraints](https://mapperly.riok.app/docs/getting-started/faq/),
[Mapster.Tool workflow](https://mapstermapper.github.io/Mapster/articles/tools/mapster-tool/Mapster-Tool-Overview.html).

### Can the existing schema generate the classes?

The committed catalogue uses JSON Schema Draft 2020-12 plus `x-infocarrier` annotations. Its
purpose is structural checking. It does not define arbitrary codec implementations, application
JSON, or semantic conversion. Making it authoritative generation input would be a separate design
change, with an ownership and coverage decision. The current protected descriptions must remain
protected; generation must not reinterpret the checker as a migration generator.
[Current guarantee](../../wire-protocol.md#structural-compatibility-check),
[recorded decision](../../decisions.md#amendment-2026-11-01-standard-schema-descriptions).

NJsonSchema is a maintained C# generation candidate. Its latest GitHub release was
[11.6.1, dated 2026-04-20](https://github.com/RicoSuter/NJsonSchema/releases/tag/v11.6.1), under the
[MIT license](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/LICENSE.md).
Its [documented generator](https://github.com/RicoSuter/NJsonSchema) emits C# from schemas.
However, the tagged
[settings](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/src/NJsonSchema.CodeGeneration.CSharp/CSharpGeneratorSettings.cs)
default to Newtonsoft.Json and describe System.Text.Json support as incomplete. Its
[class template](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/src/NJsonSchema.CodeGeneration.CSharp/Templates/Class.liquid)
renders polymorphic discriminator values as strings. It also distinguishes validation attributes
from emitted C# required members. None of this proves suitability for InfoCarrier's numeric
discriminators, positional frames, custom annotations, and generated serializer contexts.

Schema-first generation remains credible if a broader requirement justifies that work. The
existing standard schema alone is insufficient evidence for selecting NJsonSchema or a custom
generator. No schema-to-contract generation was tested during the initial research. The later
[bounded evaluations and abandonment](#15-abandoned-single-representation-trials-2026-11-03) record
the actual generation failures and the owner's decision not to pursue this approach.

## 7. Mapping libraries: verified capabilities and execution paths

| Option | Release inspected | License | Fit for this task |
|---|---|---|---|
| Explicit mappings | Repository C# toolchain | Repository code | Best initial correctness reference; no new mapper dependency |
| Mapperly | Stable 4.3.1; 5.0 prereleases remain separate | Apache-2.0 | Strong generated-code candidate, with strict configuration |
| Mapster.Tool | Stable 10.0.14 | MIT | Credible generation tool, with helper and dispatch audit required |
| Mapster runtime | Stable 10.0.14 | MIT | Runtime compilation conflicts with the proposed static mapping objective |
| AutoMapper | Stable 16.2.0 | Reciprocal Public License 1.5 or commercial | Runtime execution plans add machinery without a demonstrated benefit here |

Mapster released [10.0.14 on 2026-11-02](https://github.com/MapsterMapper/Mapster/releases/tag/v10.0.14),
including a Mapster.Tool fix. Its [tagged license](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/LICENSE)
is MIT. Mapperly's [4.3.1 release](https://github.com/riok/mapperly/releases/tag/v4.3.1) dates from
2025-12-22; [5.0.0-next.12](https://github.com/riok/mapperly/releases/tag/v5.0.0-next.12) dates from
2026-10-28. Its [stable package](https://github.com/riok/mapperly/blob/v4.3.1/src/Riok.Mapperly/Riok.Mapperly.csproj)
declares Apache-2.0. These release records show maintenance activity, not a future support guarantee.

### Mapster: generated C# does not guarantee static execution

Mapster.Tool loads a compiled assembly, discovers configuration, builds mapping expressions, and
writes C#. The generator enables `SelfContainedCodeGeneration`. That build-time process can use
reflection without requiring it for a simple generated production mapping.
[Generator implementation](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster.Tool/Generators.cs).

The committed [StudentMapper example](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Sample.CodeGen/Mappers/StudentMapper.g.cs)
uses direct assignments. However, the generated
[UserMapper test output](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster.Tool.Tests/Mappers/UserMapper.cs)
calls `GetProperty(...).SetValue(...)` when updating init-only properties. The
[associated test](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster.Tool.Tests/WhenMappingWithExistingObjectAndInitProperties.cs)
explicitly exercises reflective mapping. This is direct generated-code evidence.

Other tagged implementation paths matter:

- [EnumAdapter](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster/Adapters/EnumAdapter.cs)
  can generate calls to `Mapster.Utils.Enum<T>` for names and strings. That
  [helper](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster.Core/Utils/Enum.cs)
  uses reflection and four expression compilations during static initialization.
- [ObjectAdapter](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster/Adapters/ObjectAdapter.cs)
  can generate dynamic mapping lookup for object-to-concrete conversion. The
  [configuration implementation](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster/TypeAdapterConfig.cs)
  compiles missing mappings and defaults to `LambdaExpression.Compile()`.
- Runtime [`Adapt`](https://github.com/MapsterMapper/Mapster/blob/v10.0.14/src/Mapster/TypeAdapter.cs)
  uses mapping delegate lookup. Configuration scanning uses reflection and instance creation.
  Calling `Compile()` at startup changes when compilation happens, not whether it happens.

The enum and object conclusions follow source inspection; no new generator run tested those
configurations. A valid trial would call generated methods directly, use explicit enum handling,
avoid reflective init-only updates and runtime mapper dispatch, and inspect called helpers.
Mapster.Tool should be named explicitly: a project named `Mapster.SourceGenerator` at this tag
does not establish a shipped Roslyn implementation. Its inspected directory contained no generator
implementation source.
[Tagged directory](https://github.com/MapsterMapper/Mapster/tree/v10.0.14/src/Mapster.SourceGenerator).

### Mapperly: a stronger initial fit, with defaults that need review

The [official generated example](https://mapperly.riok.app/docs/getting-started/generated-mapper-example/)
contains ordinary constructors, assignments, loops, and an enum switch. Tagged
[integration output](https://github.com/riok/mapperly/blob/v4.3.1/test/Riok.Mapperly.IntegrationTests/_snapshots/MapperTest.SnapshotGeneratedSource_NET8_0.verified.cs)
and [external helper output](https://github.com/riok/mapperly/blob/v4.3.1/test/Riok.Mapperly.IntegrationTests/_snapshots/UseExternalMapperTest.SnapshotGeneratedSource.verified.cs)
show direct generated calls. Handwritten methods can handle semantic conversion alongside
mechanical generated assignments.

The stable [default configuration](https://github.com/riok/mapperly/blob/v4.3.1/src/Riok.Mapperly.Abstractions/MapperAttribute.cs)
still needs deliberate changes for a protocol boundary. Enum mapping defaults to numeric values;
automatic conversions and mapping-method discovery are enabled. Compatible references can be
assigned without copying. Null mismatches can skip property assignments, and unmapped members
can be warnings. These choices can preserve a constructor default or alias a mutable graph when
the protocol requires something else.

Use explicit enum semantics, intentional null rules, bounded polymorphic mappings, and errors
for relevant unmapped-member diagnostics. Audit application helpers too. Mapperly generally has
no runtime mapping engine, but reference-handling and preserved-attribute configurations can
retain runtime assets. Avoid an unconditional no-runtime-dependencies claim.
[Diagnostics](https://mapperly.riok.app/docs/configuration/analyzer-diagnostics/),
[enum options](https://mapperly.riok.app/docs/configuration/enum/),
[runtime considerations](https://mapperly.riok.app/docs/getting-started/faq/).

### AutoMapper and handwritten mappings

AutoMapper [16.2.0](https://github.com/LuckyPennySoftware/AutoMapper/releases/tag/v16.2.0)
was released on 2026-07-02. Its
[license](https://github.com/LuckyPennySoftware/AutoMapper/blob/v16.2.0/LICENSE.md)
is no longer accurately described as MIT. Its
[mapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/v16.2.0/src/AutoMapper/Mapper.cs)
selects execution plans using requested and runtime types, and its
[configuration](https://github.com/LuckyPennySoftware/AutoMapper/blob/v16.2.0/src/AutoMapper/Configuration/MapperConfiguration.cs)
compiles delegates. `CompileMappings()` prepares plans earlier; it is not build-time generation.
This is a capable general mapper, but a weak fit for the stated static conversion objective.

Handwritten methods make historical defaults, enum translation, decoder selection, and failures
visible. Their weakness is omission when types evolve. Required construction, focused completeness
checks, and behavioral tests can reduce that risk. Generated methods can later replace repetitive
assignments while retaining explicit semantic functions.

## 8. Correctness requirements that no mapper supplies

| Concern | Required contract decision and evidence |
|---|---|
| Missing and required fields | Define presence separately from non-nullness. Preserve historical omitted-value behavior. |
| Null, empty, zero, false | Keep distinctions wherever they affect execution or materialization. |
| Numeric conversion | Check range, precision, signedness, and units. Successful casts do not prove equivalent meaning. |
| Enums and discriminators | Preserve assigned identities and reject unsupported executable meanings. Never silently cast between unrelated enums. |
| Collections and references | Preserve ordering, duplicates, `Id`/`Ref`, entity keys, and relevant aliasing. |
| Nested payloads and codecs | Identify each affected inner format and its version-specific decoder and writer. |
| Custom value mappings | Preserve registered application conversions; generic property mapping cannot infer their semantics. |
| Responses and faults | Preserve readable faults, generated values, concurrency information, logs, and client materialization expectations. |
| Transactions | Verify enlistment, ownership, savepoints, rollback, cancellation, and database effects after conversion failures. |
| Streaming | Preserve order, partial rows, terminal faults, cancellation, disposal, and bounded memory. |

System.Text.Json documents required presence and nullable enforcement as separate features.
Nullable annotations do not validate every collection element or generic value.
Unknown property handling also differs from unknown polymorphic type handling.
[Required properties](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties),
[nullable enforcement](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/nullable-annotations),
[unmapped members](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members),
[polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism).

A round trip through two mappings can still conceal two matching mistakes. Test externally
observable results and effects. A newer result may have no truthful older representation. In that
case, evaluate retained execution, deferral, or an explicit support-policy decision. Clear refusal
does not satisfy an existing support promise.

## 9. Security and runtime behavior

Treat incoming versions as selections among trusted, compiled contracts and codecs. They must not
select arbitrary runtime types, assemblies, mapping configurations, or generators. Bound old-format
parsing and conversion before execution. Check converted descriptors through current security
controls; retaining an old representation must not restore a revoked permission.

The current [type resolver](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Expressions/TypeNodeResolver.cs#L111)
validates descriptor complexity and checks permissions even on cache hits. Anonymous structures
require the trusted server catalog. Migration must not infer new type registrations. The
[security review](../../security-review.md#2-the-finding-that-matters-the-bound-is-a-conjunction)
explains why several type and method restrictions jointly prevent reflective invocation.

The current serializer uses generated metadata. A reflection-free migration method would be a
narrower property than a reflection-free product. Existing query execution and materialization
still use reflection, including
[generic stream invocation](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L162)
and [dynamic collection construction](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Expressions/DynamicValueMapper.cs#L792).
Generated mapping therefore establishes neither native ahead-of-time compilation support nor
whole-product reflection freedom.

Serializer generation also has distinct modes. Falling from optimized writing to generated
metadata is not automatically reflection fallback. Verify the configured resolver and custom
converters. Microsoft's reflection-disable switch can expose accidental default reflection in a
bounded experiment, but does not audit every helper or custom converter.
[Source-generation modes](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation-modes),
[explicit context selection and reflection defaults](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation).

Runtime generation adds cache and resource questions without a current need. System.Text.Json's
own threat model discusses untrusted type selection, converter behavior, and metadata growth.
The existing trusted anonymous-shape mechanism is separately bounded and does not justify
request-driven contract generation.
[System.Text.Json threat model](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/docs/ThreatModel.md).

## 10. The real-change candidates and the evidence still missing

[Entity Framework Core 11 adoption, issue #99](https://github.com/azabluda/InfoCarrier.Core/issues/99),
may expose changes in expression interpretation, dependency behavior, or encoding. Its open status
does not establish any particular wire change. Identify an actual difference before selecting a
migration mechanism.

[Stricter client evaluation, issue #220](https://github.com/azabluda/InfoCarrier.Core/issues/220),
is especially useful for testing the limits of server conversion. The
[query splitter](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Query/QuerySplitter.cs#L195)
creates server fragments and a client residual computation. An older client can fetch two sources
and join them locally. Its server requests do not necessarily describe that retained join.
A server converter cannot reconstruct an operation absent from its input.

That is an implementation-based limitation, not a completed migration experiment. The stricter
rule might require only a client change and no wire conversion. Moreover, version 10 is outside
the promised window. A version 10 difference can inform research without becoming a version 11
support obligation. Rebuilt development clients can test mechanisms, but cannot replace eventual
published-client acceptance.

The unanswered questions are concrete:

1. Which approved change actually produces a different request, response, or execution guarantee?
2. Which behavior occurred locally before transmission or after the response?
3. Can the chosen execution model preserve every required distinction in both directions?
4. How much of the affected graph is repetitive copying versus semantic conversion?
5. Can generated code preserve those rules without runtime fallback or hidden allocation costs?

## 11. Proposed experiment, diagnostics, and retirement

This is a bounded investigation sequence, not an implementation plan for a generic framework.

1. Identify the selected change, earlier source revision, old meaning, new meaning, and supported
   combinations. Label an artificial example explicitly. Record affected nested formats and the
   client's local work.
2. Run the unchanged earlier client in its own process against the changed server. Demonstrate
   the compatibility failure without conversion. Where the real change risks silent reinterpretation,
   test its actual old meaning against the new interpretation. Any additional artificial semantic
   case must be labelled separately and cannot establish a benefit for the proposed production change.
3. Implement only the required compatibility behavior. Use explicit mappings as the initial
   reference. Test responses and database effects as carefully as request decoding.
4. Inspect every explicit mapping and its helpers. Verify all source variants, required target
   values, enum meanings, reference identities, and response paths. Generated mapping comparisons
   are deferred under the owner's initial choice.
5. Measure cold conversion setup, steady-state conversion, allocations, representative graph sizes,
   malformed input, and streaming memory. Separate mapping from parsing and database execution.
6. Remove the expired contract in the experiment. Prove supported combinations still work and
   retired contracts cannot execute. Preserve version allocation history.

A useful diagnostic identifies the received contract, selected decoder, operation, nested field
path, and conversion failure category. It must distinguish unsupported contracts from malformed
data, impossible conversion, execution faults, and transient transport failures. Avoid logging
payload values or connection data. Do not convert compatibility errors into automatic retries.

No performance ranking is established. Explicit and generated mappings can both be ordinary
compiled calls. Their allocation behavior depends on copying, collection materialization, reference
handling, and helper calls. Adjacent conversions can multiply that work. A separate canonical
model can impose it on current clients too. Measure the actual path before paying that cost.

Retirement follows the package support window, not the age of a class. A wire contract remains
necessary while any supported release uses it. Several releases may share it. When the obligation
ends, remove its admission, serializers, mappings, retained execution, and active test targets
together. Keep historical evidence and allocated integers. Test that no conversion chain still
depends on an allegedly retired intermediate version.

Kubernetes distinguishes serving a version from retaining decoding for stored data. That is useful
precedent, but InfoCarrier must first establish whether any relevant deployment persists protocol
payloads. Do not invent a stored-message migration requirement.
[Kubernetes deprecation policy](https://kubernetes.io/docs/reference/deprecation-policy/),
[version removal procedure](https://kubernetes.io/docs/tasks/extend-kubernetes/custom-resources/custom-resource-definition-versioning/#overview).

From 11.1, use actual published previous-minor clients in separate processes, covering the promised
patch range. One selected patch does not redefine the policy. Keep refusal fixtures, structural
descriptions, mapper tests, and actual-client acceptance as different evidence. The existing
structural checker remains necessary, but teaching it about multiple served contracts would itself
need a reviewed design; a passing current-contract comparison is not migration proof.

## 12. Discussion status after the owner's choices

The owner chose complete physical contract copies and explicit mappings. This resolves the
selective-copy question. The cost is duplicated definitions; the benefit is direct historical
isolation. Neither choice alone resolves semantic conversion.

Next, choose the source of that example. The stricter client-evaluation work is useful for exposing
server-only limits, but may not provide a migratable wire change. Entity Framework Core 11 adoption
may provide a better structural example, but no such difference has been demonstrated here.
The owner also permits an artificial example, while preferring a useful change worth retaining.

Use the selected example to decide whether current models suffice as canonical and whether retained
execution is necessary. Mapping generation is deferred; explicit mappings are the initial choice.
No production implementation follows from this note alone.

## 13. Recommended protocol 4 candidate: typed query content

Assessment completed 2026-11-03 against the same repository revision. This is a concrete candidate
for the next experiment, not a protocol allocation or an implemented compatibility claim.

Remove the redundant inner base64 encoding from query requests and buffered query results.
This addresses the recorded [architecture question D1](../../architecture.md#d1-infocarrierenvelopepayload-is-byte-and-the-outer-envelope-is-json).
That record identifies both size overhead and tests that accidentally inspected encoded text
without decoding the contained data. The benefit therefore has an existing repository basis.
No new feature needs to be invented solely to require conversion.

### Proposed before and after

| Surface | Protocol 3 | Proposed protocol 4 |
|---|---|---|
| Query request | `SerializedQuery: byte[]` containing expression JSON | `Query: V4.ExpressionNode` |
| Buffered query response | `SerializedResults: byte[]` containing a dynamic-value array | `Rows: IReadOnlyList<V4.DynamicValueNode>` |
| Outer envelope | Serializer-produced `Payload: byte[]` | Retain arbitrary serializer-produced bytes |
| Streamed request | Encoded expression inside the request body | Same typed `Query` as the buffered request |
| Streamed response | Positional frames already contain node JSON | Copy the complete frame contract; preserve its layout initially |
| Save operations | Encoded value graphs | Copy the complete contracts; preserve their layout initially |

The default JSON representation would embed the query object and row array directly in the
operation body. It would no longer turn those JSON bytes into base64 strings. The outer envelope
retains its own encoding layer. Ordinary binary application values, such as binary keys, remain
binary values and must not be treated as embedded JSON.

This is a legitimate incompatible replacement: a required string containing base64 becomes a
required object or array, with a different field name. Old readers cannot use the replacement.
Supporting them requires an actual protocol 3 decoder, mappings, and a protocol 3 response writer.
An optional duplicate property could avoid an immediate replacement, but would retain duplicate
representations and require precedence rules. The proposed experiment deliberately evaluates
the useful replacement rather than maintaining both fields indefinitely.

For long inputs, base64 contributes approximately one third more bytes than its source data.
Removing that layer reduces the affected encoded contribution by approximately one quarter.
This arithmetic excludes padding, other fields, and compression. It is not a measured request-size,
latency, or allocation result. Streamed response rows already lack this layer, so they do not
receive the same size benefit.

Sources: [request construction](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/QueryExecutor.cs#L503),
[buffered result encoding](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L595),
and [stream frame writing](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Query/QueryStreamFrameCodec.cs#L129).

### Why retain the outer byte payload?

The outer payload belongs to `IInfoCarrierSerializer`, which applications can replace. Its bytes
need not contain JSON. The existing `PrefixSerializer` test prepends byte 127 to serialized
operation bodies and envelopes. Unconditionally embedding those operation bytes as JSON would
break that supported composition. Keeping `Payload: byte[]` preserves this boundary.
Custom serializers still need to support the new versioned contract types; preserving an interface
does not prove every old serializer implementation understands them.
[Replacement test](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/test/InfoCarrier.Core.TransportTests/ServiceReplacementTest.cs#L261).

A converter could instead retain byte-array properties and write their contents as raw JSON.
That is a smaller implementation, but preserves inner encoding work and can add copying or
parsing on reads. It also makes structural inspection depend on new converter-specific coverage.
Typed fields better exercise the selected complete-copy and explicit-mapping design and expose
the real operation structure to generated metadata.

### Constraints to resolve in the experiment

The existing operation and expression contexts have different options. The expression context
omits null fields, uses string enum conversion, supports named floating-point literals, and permits
depth 256. The outer context has different defaults. Simply nesting the node types under the
outer context does not preserve those rules. Surrounding object depth also counts when nodes are
inline. Test named floating values, enum identities, omitted nulls, discriminators, and expressions
near the existing depth boundary before selecting a combined configuration or nested codec.
[Operation context](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/InfoCarrierJsonContext.cs),
[expression context](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Expressions/ExpressionJsonContext.cs).

The current executor bounds expression bytes before creating expression nodes. Direct typed
decoding moves that allocation earlier. Checking a reserialized node afterward would not retain
the protection. Version 4 needs a bounded decoding boundary before constructing its typed graph.
Retain the streamed header check, bounded input handling, and the applicable inner size guarantee.
Evaluate byte limits around custom decoders separately from any expanded-data limits.
[Expression limit](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/ServerQueryExecutor.cs#L817).

Typed copied requests must also retain correct request-direction classification. Existing generic
limits use the `IInfoCarrierRequest` marker. An unrecognized copied marker could accidentally apply
response limits. A shared marker with no wire data is infrastructure, not a shared payload
definition; alternatively, codecs can select request limits explicitly.
[Limit classification](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/InfoCarrierPayloadLimits.cs#L113).

### Complete physical-copy scope

The 2026-11-02 audit found 37 project-owned operation, expression, and value records, three owned
enums, and six frame declarations. There are also two Entity Framework Core enum dependencies:
tracking behavior and query splitting behavior. Copying their fixed vocabularies into version-owned
enums avoids making historical definitions depend on later framework enum additions.

The resulting proposed scope is 48 typed declarations per version, plus two serializer contexts
and one frame codec. This is a dated scope assessment, not an ongoing repository counter or an
implementation estimate. Framework primitives remain framework primitives; freeze their admitted
vocabulary and encoding settings instead of attempting to copy runtime types.

Selected placement uses the existing product project and existing live data classes:

```text
Wire/V3/Common/          Common/       (live protocol 4)
Wire/V3/Expressions/     Expressions/  (live protocol 4)
Wire/V3/Query/           Query/        (live protocol 4)
Wire/V3/Protocol3JsonContext.cs
Wire/V3/Expressions/Protocol3ExpressionJsonContext.cs
Wire/Protocol3Mapping.cs
Wire/InfoCarrierWireProtocol.cs
```

Every recursive property, collection element, discriminator registration, frame payload, and
serializer reference must name that version's definitions. Version 3 must not derive from or
reference version 4 payload types. Each frame codec uses its fixed version identity. Conversion
methods live outside frozen definition folders and explicitly translate enum meanings.

The live data classes also form the initial execution representation. Change those existing query
and row fields directly. Historical mappings decode or encode version 3's nested bytes at the
boundary; current execution must not require a third complete model merely to enable conversion.

Physical copies do not freeze framework serializer behavior. Retain protected effective contract
descriptions and old-client acceptance. The inspector discovers contexts across shipped assemblies,
so additional contexts need explicit version-aware inventory and comparison rules.

### Why this candidate ranks first

| Rank | Candidate | Benefit | Cost and uncertainty |
|---|---|---|---|
| 1 | Typed inline query and buffered rows | Removes a recorded encoding cost; exercises nested request and response conversion | Requires careful limits, serializer options, and complete graph mappings |
| 2 | Explicit dynamic-value variants | Makes null, reference, scalar, collection, and entity forms more explicit | Current historical mistakes are fixed; no present valid-client defect is demonstrated |
| 3 | Distinct request, success, fault, and acknowledgement envelopes | Removes ambiguous combinations and null placeholder payloads | Mainly buffered exchanges; smaller demonstrated user benefit |

The second candidate follows the existing overlapping optional fields and decoding precedence in
[dynamic values](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Expressions/DynamicValueNode.cs).
The third follows the existing shared
[envelope](https://github.com/azabluda/InfoCarrier.Core/blob/a91c364915ca9c05c860e507bd77ea7caf448ca1/src/InfoCarrier.Core/Common/InfoCarrierEnvelope.cs)
and fault payload construction. Both are credible alternatives, not additional work proposed for
the first experiment. Neither has a new measured performance benefit in this research.

### Bounded experiment and acceptance

1. Preserve the unchanged current development client and its source identity. Build it before
   changing the server; no second clone or worktree is necessary. It is experimental protocol 3
   evidence, not a published-client substitute.
2. Preserve the complete physical version 3 set and use the existing classes as live version 4.
   Change only the query and row representations initially. Keep version allocation and protected descriptions under their
   existing review rules; this research note itself allocates nothing.
3. Demonstrate the unchanged earlier client failing against the changed server without compatibility.
   Add explicit mappings and demonstrate success in separate client and server processes. Select
   protocol-specific response codecs, including the stream start version and faults.
4. Verify current and older query results, references, nulls, tracking, faults, cancellation, partial
   streaming, transactions, writes, and generated values. Save formats need full copies and mapping
   coverage even though their layout is not the selected representation change.
5. Compare encoded sizes and mapping allocations using actual queries, including large graphs and
   the prefix serializer. Measure any temporary canonical re-encoding. Avoid claiming improvement
   from base64 arithmetic alone.
6. Remove version 3 support in a bounded retirement check. Verify refusal before effects and continued
   version 4 operation. Preserve allocation history and release-policy obligations.

This example primarily validates structural conversion while preserving existing meaning. It
does not prove that arbitrary semantic changes can be repaired. The useful outcome is a measured
encoding improvement and a demonstrated compatibility boundary, with its limits recorded.

## 14. Implementation experiment, 2026-11-03

### Implemented boundary

Protocol 4 uses the existing data classes with typed `Query` and buffered `Rows`. Protocol 3 has
its complete frozen declarations, independent enum vocabularies, serializer contexts, and frame
codec under `Wire/V3`. `Protocol3Mapping` contains handwritten bidirectional conversions.
`InfoCarrierWireProtocol` selects the representation at the transport boundary. There is no mapping
package, generated mapper, migration chain, or third complete execution model.

The final design injects `IEnumerable<IWireProtocolCodec>` from the ordinary host container into
`InfoCarrierWireProtocol`. Each codec owns one protocol. `Protocol3WireProtocolCodec` calls the
historical mapping helpers; `CurrentWireProtocolCodec` uses live classes directly. Selection,
byte limits, and identification checks are shared. Historical representation changes remain local
to their codec and mappings. The initial static-registry implementation was superseded by
the explicit dependency-injection decision on 2026-11-03.

Role registration adds the built-in singleton services idempotently. Advanced hosts can replace
the collection after registering their roles. Keep exactly one codec per supported version and
retain a codec for the current version. The selector and default serializer share host-specific
generated metadata. Selection caches are not shared between dependency injection containers.
No assembly scanning, reflected mapping, runtime expression compilation, or service locator is
used to discover codecs. The selector requires the injected collection; it has no parameterless
constructor, built-in codec factory, or global default. Existing direct serializer and dispatcher
constructors retain current-contract-only behavior. Multi-version hosting uses the injected selector.

For example, an expert host can replace the historical implementation through standard services:

```csharp
services.AddInfoCarrierServer<ShopContext>();
services.RemoveAll<IWireProtocolCodec>();
services.AddSingleton<IWireProtocolCodec, CurrentWireProtocolCodec>();
services.AddSingleton<IWireProtocolCodec, ApplicationProtocol3Codec>();
```

`ApplicationProtocol3Codec` implements `IWireProtocolCodec`; it may delegate unchanged behavior
to the public built-in codec. Import `InfoCarrier.Core.Wire` and
`Microsoft.Extensions.DependencyInjection.Extensions`. Custom adapters resolve a selected endpoint
and call its `CreateEnvelopeServer` method so injected codecs also govern envelope dispatch.

Version 3 nested query, row, and save-value documents use their historical contexts. Application
binary values remain binary values. Converted type and method descriptors still pass ordinary
server admission; copying or mapping a descriptor never grants permission to execute it.
The historical request marker inherits only the live infrastructure marker so request limits remain
correctly classified. No historical payload definition inherits a live payload definition.

The copied frame reader uses explicit `JsonElement` metadata with default decoding settings.
This avoids introducing another reflection-based reader warning while preserving the historical
frame layout and depth behavior. The structural check verifies that layout against the original
effective protocol 3 description. It does not prove arbitrary behavioral equivalence.

### Separate-process evidence

Permanent codec execution coverage was added after the coverage review on 2026-11-04.
`Protocol3ExpressionTest` checks expression meaning after historical conversion, including calls,
conditions, arithmetic, type tests, field/property initializers, collection construction, generic
method descriptors and invocation. `Protocol3ExecutionTest` sends protocol 3 HTTP envelopes to
the ordinary SQLite host and checks database results, generated-key correlation, writes,
transactions, savepoints, rollback, failure atomicity and backend refusal. Streamed results use
the historical header and frozen frame reader. Historical conversion must not grant server raw
query permission; its refusal must issue no database read.

The test client uses a current client shell with a test-only serializer and buffered transport
adapter. Every exchanged envelope is checked as protocol 3; only the shell's local envelope
admission is adjusted. This covers the shipped codecs, not the behavior of an unchanged published
client. The separate-process evidence below and the published-client obligation remain distinct.

The full transport coverage run on 2026-11-04 exercises 170 of 178 `Protocol3Mapping` lines
(95.5%), up from 92 of 178 in the migration report (51.7%). Historical-codec coverage is
84 of 86 lines (97.7%), up from 60 of 86 (69.8%). Frozen V3 coverage is 264 of 297 lines,
up from 161 of 297. These are dated line observations, not branch-completeness claims.

Before changing product sources, the probe was built against product revision
`a91c364915ca9c05c860e507bd77ea7caf448ca1` and preserved under
`artifacts/compatibility/protocol3`. That earlier executable was not rebuilt after the protocol change.
Its product assembly SHA-256 is
`D9338F2D99B412B95AAF6B32C481F359D3D9676B875864769EE72E5E9555E49E`;
its probe assembly SHA-256 is
`4D43BE03589AB04A2A11BD6892F50DD04BA5AEFC7B92B7D6096FE0DB341E5C91`.
These hashes identify local experimental artifacts, not published packages or committed binaries.

The changed server and each client ran in separate processes against an isolated SQLite database.
Both protocol 3 and protocol 4 clients passed buffered queries, streamed queries, generated-key
saves, update rollback followed by a fresh read, and deletion followed by an absence check.
Run the preserved-client experiment with:

```powershell
dotnet build tools/InfoCarrier.CompatibilityProbe --configuration Release
dotnet run --file eng/Compatibility.cs -- --previous-client artifacts/compatibility/protocol3/InfoCarrier.CompatibilityProbe.dll
```

The command creates its own server and database and stops its server on exit. It retains the
database under `artifacts/compatibility`. The preserved earlier executable must already exist;
the command deliberately cannot manufacture historical evidence from the current implementation.
To rebuild earlier evidence, use the recorded product revision and the probe's client workflow
with that revision's public APIs. Do not rebuild the current mapper and label it an old client.

With protocol 3 admission temporarily removed in an isolated build, the unchanged earlier client
failed with `InfoCarrierProtocolException` identifying client 3 and server 4. A separate database
check confirmed zero rows after that refused client. The protocol 4 client still passed its complete
workflow. Separately, directly decoding an old query as a current query fails its required typed
field check. Thus compatibility does not rely on silently treating old bytes as the current shape.

### Measurements

Measurements used SDK 10.0.401 and an actual Entity Framework `Where` expression. Each protocol's
encode/decode measurement ran in its own process. These are dated experiment readings, not
performance thresholds or general throughput claims.

| Measurement | Protocol 3 | Protocol 4 |
|---|---:|---:|
| Encoded operation request bytes | 5,033 | 3,752 |
| First encode/decode allocated bytes | 431,128 | 491,512 |
| First encode/decode milliseconds | 217.89 | 163.86 |
| 1,000 warm encode/decode cycles, allocated bytes | 39,517,040 | 23,731,520 |
| 1,000 warm encode/decode cycles, milliseconds | 310.39 | 218.59 |

The sample demonstrates fewer bytes and warm allocations. Protocol 4's first cycle allocated more.
Timing varies and does not establish a
speed improvement. Concurrent validation work also prevents treating these timings as controlled
benchmarks. Buffered envelopes retain their outer encoded payload. Server graph validation still
serializes a decoded graph once before translation; this experiment does not remove all encoding.

### Retirement and limits

Retire a historical contract only after no promised supported minor requires it. Remove admission,
its conversions, its frozen declarations and codec, serializer dispatch, and historical inspection
coverage together. Preserve the allocation ledger. Run current-client acceptance and a refused
retired-client process against a fresh database before accepting removal.

The experiment rehearsed admission removal and verified refusal before database effects. It did
not compile a production tree after physically deleting every protocol 3 source file. No retirement
is authorized now, and refusal is not a substitute for promised previous-minor support.

This change preserves query and result meaning. It does not show that conversion can repair
arbitrary older-client local behavior or semantic changes. Version 11.0 still supports only 11.0
clients; version 10 remains excluded. From 11.1, published previous-minor packages in separate
processes remain a release obligation. A preserved development executable does not fulfill it.

Large adversarial graph measurements, broad historical-client specification coverage, and controlled
end-to-end performance benchmarks remain outside this bounded experiment. The normal transport
suite covers payload limits, failures, cancellation, stream termination, custom prefix serialization,
and transaction behavior; those checks are separate from the small historical-client workflow.

### Validation record

Final local validation on 2026-11-03:

| Check | Result |
|---|---|
| Windows functional suite, `Measure.cs protocol4-container` | Passed: 29,698, Failed: 0, Total: 29,932; Skipped: 234 |
| Document-store suite, same measurement | Passed: 237, Failed: 0, Total: 237 |
| Transport suite, Release | Passed: 377, Failed: 0, Total: 377 |
| Engineering-command suite, Release | Passed: 63, Failed: 0, Total: 63 |
| Full Release rebuild with `CI=true` | Passed; five documented framework Razor warnings, zero errors |
| Release package validation | Passed |
| Clean trimmed Blazor publish | Passed at the existing 115-warning product baseline |
| Structural check against protected revision and historical compatibility projection | Passed; saved description matches inspected implementation |
| Strict documentation build, link and word checks | Passed |
| File hygiene and `git diff --check` | Passed |

The Windows functional run includes the spatial read, write, and aggregate tests. Its failure-name
and failure-reason snapshots are empty. The first sandboxed run could not create the embedded
Firebird database or locate MongoDB binaries; rerunning with database access resolved those
environment failures. Earlier dummy payload tests and future-version fixtures were updated for the
typed query boundary. The final complete runs above passed.

The engineering entry's initial package restore reported NU1900 connection warnings while retrieving
vulnerability metadata. The final compilation gate used restored dependencies; it does not establish
a fresh vulnerability audit.

The console sample completed its query, projection, save, and rollback workflow against an isolated
sample server. Browser checks loaded the customer grid, order, customer navigation, and lines, then
committed a transfer and displayed fresh stored values. The browser's forced-failure switch was not
verified; error and rollback evidence comes from transport tests and the separate-process workflow.
The isolated sample server and browser tab were closed after validation.

The final transport run also verifies container-created codec replacement for buffered, streamed,
and custom-adapter requests; rejection after removing a codec; duplicate-version configuration
errors; and metadata isolation between containers. The selector has only its enumerable constructor.
There is no standalone codec factory or global default selector. Built-in codec implementations use
ordinary `TryAddEnumerable` singleton registrations. The compatibility probe's measurements also
resolve their selector from a service provider.

## 15. Abandoned single-representation trials, 2026-11-03

**Status: both abandoned by the owner, 2026-11-03.** These trials attempted to remove one of the
two committed representations: keep only frozen C#, or keep only JSON descriptions. Retain both.
Frozen C# provides the historical reader/writer types; the JSON catalogue records covered effective
serialization facts for comparison against protected Git history. The catalogue is derived checking
evidence, not a second independently authored contract or verified generation input.

The common failure was treating these representations as interchangeable because both describe
the contract. Successful C# compilation does not establish wire compatibility, and successful
schema-to-C# generation does not establish equivalence with the historical implementation.
Neither representation alone proves semantic compatibility. The validated protocol 3-to-4 migration
and its handwritten mappings are not abandoned.

### Trial A: drop JSON descriptions and keep only frozen C#

**Proposal.** Delete committed schema descriptions, express any missing metadata through C#
attributes, and let compilation of handwritten mappings expose incompatible contract changes.

**Evidence level.** This was a design and source review, not a completed runtime experiment.
No schema descriptions were removed and no custom analyzer was implemented. The counterexamples
below invalidate compiler-only coverage; they are not reported as measured test results.

**Why it failed.** The ordinary compiler checks C# type/member usage, not the effective serialized
contract. These changes can leave mapping code compiling:

| Change | Why mapping compilation misses it |
|---|---|
| Change a property's `JsonPropertyName` | The C# member and assignment remain unchanged |
| Change a discriminator tag or enum numeric assignment | The referenced type and enum member still exist |
| Add an optional destination property without mapping it | Object initializers need not initialize optional members |
| Change `JsonRequired`, ignore rules, serializer options, or a codec | Those changes do not necessarily alter C# assignment compatibility |
| Exchange two integer frame positions | Both values remain valid integer expressions |
| Change defaults, units, or same-typed field meaning | Assignment type compatibility does not check meaning |

C# `required` can detect omitted required destination members, but cannot detect wrong values or
prove every source member was handled. Custom attributes are metadata; a checker or analyzer must
interpret them. Adding attributes alone does not extend the ordinary compiler's checks.

Frozen V3 classes also do not provide a protected reference for all later releases using live V4.
Several package releases may share wire version 4 without creating another complete frozen copy.
Rebuilding descriptions from candidate source alone can reproduce the same accidental change on
both sides of a comparison. Deleting the committed descriptions without another protected reference
would therefore remove existing historical comparison coverage.

**Decision and reconsideration.** Keep protected JSON descriptions and the bounded structural
checker alongside frozen C#. Do not repeat the compiler-only proposal as though attributes or
strong typing solve these counterexamples. A future alternative must identify its protected
historical reference and demonstrate equivalent covered serialization checks, including live
same-version releases. An analyzer or source/artifact comparison could form part of that alternative,
but would be a separate mechanism requiring evidence. Existing schema checks still do not prove
arbitrary converter behavior or semantic compatibility; unchanged-client acceptance remains separate.

### Trial B: drop frozen C# and keep only JSON descriptions

**Proposal.** Generate historical classes from committed JSON Schema during the build and omit
the generated classes from Git. This was tested with NJsonSchema 11.6.1 and failed to produce an
equivalent historical contract set. Do not repeat the same generation trial merely because a
package advertises C# or System.Text.Json support.

The owner authorized a bounded NJsonSchema evaluation after discussing whether historical C#
declarations could be generated during the build. This experiment does not replace the frozen V3
classes, change production registration, or supersede the physical-copy decision. Generated files
remain ignored local artifacts. After abandonment, the experiment entry, implementation, probe,
and package reference were removed. No generator dependency remains in repository configuration.

#### Package and reproducibility

The evaluated package is
[NJsonSchema.CodeGeneration.CSharp 11.6.1](https://www.nuget.org/packages/NJsonSchema.CodeGeneration.CSharp/11.6.1),
under the MIT license. It is the C# generation library used within the NSwag ecosystem, not a package
that automatically wires historical-class generation into this project's build. The inspected
[versioned settings](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/src/NJsonSchema.CodeGeneration.CSharp/CSharpGeneratorSettings.cs)
mark System.Text.Json and its native polymorphism support experimental/incomplete.

The now-removed engineering command read `docs/wire-contracts/protocol3.json`, selected a query
request and the expression graph, generated code under `artifacts/schema-generation-experiment`,
then compiled an isolated Release probe referencing the unchanged product. The probe disabled
serializer reflection fallback and used System.Text.Json source-generated metadata for both the
existing and generated classes. No generated source or generated schema was committed. This note
preserves the inputs, settings, counterexamples, and conclusions instead of retaining abandoned tooling.

Generation settings explicitly select System.Text.Json 10, its native polymorphism attributes,
nullable reference annotations, and nullable optional properties. Data annotations are disabled.
A second request variant enables `UseRequiredKeyword`. This is a configured evaluation, not a claim
about every possible generator setting.

#### Observations

| Case | Observed result |
|---|---|
| Existing request catalogue selected through `$defs` | Generation fails with an unresolved property reference |
| Existing expression catalogue selected through `$defs` | Generates an extension-data container without typed variants |
| Request after reference/dialect adaptation | Generates compilable classes; simple valid request JSON round-trips |
| Expression after the same adaptation | Generation fails resolving the recursive expression definition |
| Required request fields with ordinary generated properties | Missing fields are accepted, whereas V3 rejects them |
| Same fields with `UseRequiredKeyword` | Missing fields are rejected through source-generated serializer metadata |
| Encoded query bytes | Generates `string`, losing byte-array typing and invalid-base64 rejection |
| Numeric tracking enum | Generates `int`, losing the named enum vocabulary |
| Nullable split-query enum | Generates an object class; a valid numeric V3 value is rejected |
| Omitted optional protocol integer | Generated value is null; existing V3 value is zero |
| Unknown request field | Generated extension data retains it; existing V3 skips it |
| Unknown expression discriminator | Raw generated container accepts it; existing V3 rejects it |

The adaptation extracts the reachable definition closure, converts `$defs` references to
`definitions`, changes the declared dialect, and assigns safe definition names. It deliberately
does not translate InfoCarrier extensions or claim semantic equivalence between schema drafts.
These failures describe this input and adapter; they do not prove that all recursive schemas fail.

A separate, explicitly synthetic control uses conventional inheritance definitions and the library's
discriminator mapping representation. It produces a typed base class and derived class. However,
its native System.Text.Json output registers discriminator `"0"` as a string. The compiled probe
accepts `{"$kind":"0","value":42}` and rejects `{"$kind":0,"value":42}`. V3 uses numeric tags.
The [versioned class template](https://github.com/RicoSuter/NJsonSchema/blob/v11.6.1/src/NJsonSchema.CodeGeneration.CSharp/Templates/Class.liquid)
confirms that the native discriminator argument is emitted as a quoted string. Thus, conventional
inheritance input alone does not preserve the V3 discriminator representation.

The probe reports `Passed: 17, Failed: 0, Total: 17`. These are checks that reproduce the observations,
including incompatibilities; they are not seventeen successful compatibility cases. Reflection
fallback reports false. No unchanged earlier client was run against a server using generated
contracts, because no equivalent generated contract set exists yet. Earlier unchanged-client
acceptance in section 14 applies only to the committed physical classes.

#### Assessment and recommendation

**Why this failed.** Compilable classes were not equivalent V3 contracts. The generator did not
understand the current catalogue's definition/union representation and InfoCarrier-specific metadata
well enough to restore the byte, enum, default, and polymorphic behavior. Even the conventional
inheritance control changed numeric discriminator `0` into string discriminator `"0"`. Successful
ordinary-property generation and a successful simple JSON round-trip therefore gave insufficient
evidence. The result would require an additional adapter/template maintenance mechanism to preserve
historical behavior, rather than removing maintenance by replacing copied source.

NJsonSchema is viable as a code-emission component, but is not a direct replacement for the V3
snapshot with the current catalogue. A production experiment would first need a generation-input
adapter for nullable unions, byte formats, enum assignments, inheritance, and context-specific
settings. Numeric polymorphic tags require custom generation or another verified technique.
Defaults and unknown-property behavior also need explicit decisions. Settings such as
`UseRequiredKeyword` resolve some differences without custom templates.

The existing catalogue describes effective serializer metadata, not every C# declaration. It cannot
by itself restore methods, ignored implementation members, constructor choices, or interface
relationships. Equivalent wire behavior may allow different source structures, but then existing
handwritten mappings and codec assumptions must be checked against those structures.

**Decision.** Abandon schema-to-class generation for this implementation and retain committed V3
declarations. This is a project decision about the tested input and maintenance cost, not a claim
that NJsonSchema cannot generate useful classes or that every observed difference is unfixable.
`UseRequiredKeyword` already fixed missing-field presence checks; that setting did not fix the
other differences.

**Revisit only with new evidence.** Identify a specific generator, input adapter, or template change
that addresses the recorded mismatches before proposing another trial. First demonstrate numeric
discriminator preservation, byte validation, nullable enums, defaults, and unknown-field behavior.
Then require complete mapping compilation and unchanged-client acceptance. A new package version,
a successful build, or a source-generation label alone is not sufficient reason to repeat this trial.

Validation: the experiment passes with `CI=true`; the Release solution build succeeds with five
documented framework Razor warnings and zero errors. Documentation links, file hygiene, and
`git diff --check` pass. The functional and transport suites were not repeated because production
code was unchanged.
