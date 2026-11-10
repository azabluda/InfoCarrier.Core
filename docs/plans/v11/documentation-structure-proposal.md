# A clearer documentation site

Reviewed November 4, 2026. Updated November 10, 2026.

## Implementation status

The first implementation pass is committed as `c76062b7`. It reorganized the site into 38 pages
and added a working SQLite tutorial. It is not the completed editorial overhaul.
The owner review found that the ordering still gives advanced details too much attention.
The reader-order pass now addresses that across the site, not only on the Blazor page.
Its changes and checks are recorded below. Browser interaction still requires a connected browser.

The original review and decisions below remain as the record of why this work was done.
The verification results at the end replace the earlier statement that examples had not been run.
Publishing, pushing, and merging have not been performed.

## Revised approach after owner review

The first pass treated the existing material as content to sort into better folders.
It checked whether examples worked, but did not sufficiently check whether their explanations
helped a reader make progress. The Blazor page made the problem clear: setup appeared after
compiled-model and trimming discussion, even though the page was in Getting started.

The organizing question for the next pass is: what does the reader need to accomplish now?
Begin with a complete normal case. Introduce a choice when the reader reaches that choice.
Explain a restriction at the point where it changes the reader's action. Put detailed contracts,
rare failures, and optional tuning in later sections or linked reference pages.
Necessary security and data-loss instructions still belong before the affected action.

### The reading path

1. Understand what the product does and whether its architecture fits the application.
2. Run one complete client/server example, read data, save a change, and see the result.
3. Learn ordinary application work: queries, paging, saves, relationships, and transactions.
4. Adapt that working application to a platform, another database, or more than one endpoint.
5. Prepare deployment: authentication, authorization, configuration, observation, and recovery.
6. Find exact settings or customize components when an actual requirement calls for it.

This is a path through the documentation, not a requirement that everyone read the entire site.
A returning reader must still be able to go directly to a task or reference entry.
Existing menu approval does not establish that every page has the right priority or content.
Change grouping where the reading path requires it. The current page count is not a target.

### Problems to correct across the site

| Area | First-pass problem | Direction for the next pass |
|---|---|---|
| Overview | The menu promises capabilities and limitations, but its second page begins with a rare concurrency-interceptor case. | Explain product fit, useful capabilities, and major architectural tradeoffs first. Link to exact unsupported cases. |
| Getting started | Installation mixes the basic route with source packaging and authenticated development feeds. Blazor leads with advanced restrictions. | Keep one clear route to a first result. Move optional installation alternatives and platform details out of that route. |
| Application guides | Querying discusses splitting, helper registration, and provider refusals before ordinary paging. Streaming precedes several more common tasks. | Teach everyday query and write tasks first. Explain execution details where they affect a shown query. |
| Configuration | Shared settings and anonymous structures can look like prerequisites for the simplest application. | Start with minimal client/server setup. Introduce extra registrations only for examples that need them. |
| Platform guides | Blazor is largely a list of unusual runtime behaviors, rather than a guide to using a browser client. | Start with working setup and a visible result. Follow with normal data loading and UI lifetime. Put publishing and unusual model options later. |
| Deployment | Security opens with serializer protections before giving the application's deployment steps. | Lead with the actions the application owner must take. Explain protections and their limits in support of those actions. |
| Customization | Some pages introduce implementation details before helping readers choose whether customization is needed. | Start with the need and the simplest supported solution, then a complete example and its contract. |
| Reference | Precise details are useful, but introductory pages still repeat much of them. | Keep exact limits, defaults, protocol rules, and full signatures available for lookup. Link from guides at the relevant choice. |

### How to judge each rewritten page

Before editing, state its reader, task, prerequisites, and expected result in the review notes.
Then read it from the previous page in that journey. Check whether the reader has already learned
every required concept. For task pages, put a usable example before optional internals.
Do not force the same template onto product explanations, reference tables, or release notes.

After editing, ask whether the reader can complete the task without visiting unrelated pages.
Check that warnings change a concrete action, that cross-links answer a current question, and that
advanced detail does not interrupt the basic path. Compilation, links, and the strict build remain
technical checks; none proves that this reading order is useful.

First rework the complete Overview-to-first-save journey, including its onward links.
Then apply the same review to ordinary application work, platform choices, deployment, customization,
and reference. Treat the work as in progress until these journeys have been reviewed as a whole.

## What I recommend

Arrange the site around what readers want to do: understand the product, start an application,
use it, deploy it, customize it, find an exact setting, or upgrade.

The main problem is that some pages now do too many jobs. For example, server configuration
also explains logging, transaction expiry, document databases, and several endpoints.
Meanwhile, shorter pages still contain old instructions.

Changing the menu names will help, but we also need to fix examples and remove conflicting advice.

You have already approved these principles:

- Ordinary pages should explain the current features. They should not become a history of changes.
- We should draft the [11.0 release notes](../../../website/docs/release-notes/11.0.md) now,
  with a place for Entity Framework Core 11 support.
- Version 11 can change page addresses and heading names. A clear structure takes priority over
  keeping old links working. This updates the earlier address-preservation proposal.
- The menu below is approved as of November 10, 2026, including its subject grouping and proposed
  page destinations. Use it for the documentation overhaul.

The menu and first-application database decisions are complete.
This work does not include publishing the site, creating a release, or merging changes.
Coverage reporting remains separate work.

## Approved first application

The owner approved SQLite on November 10, 2026. Use it for the first complete application,
with `ShopContext`, `Customer`, and `Order`. Include database creation and seed data so readers
can run the example without setting up a separate database server. This does not change which
databases the product supports.

Version 11 documentation stays unpublished during the rewrite under the existing publication restriction.
The public site continues to describe the published release. Hosting several documentation versions
can be decided separately. Keep the current visual design unless browser checks show a specific problem.

## What readers cannot easily do today

| Reader's question | Problem found | Proposed fix |
|---|---|---|
| Is this product suitable for my application? | Home says a filter can run locally when the server cannot run it. Version 11 now refuses that automatic fallback. | Put a clear explanation of where queries run on Home, with a link to limitations. |
| Can I run the first example? | The tutorial assumes a database, connection settings, tables, and existing rows. It never creates them. | Give every step, from creating projects to reading and updating seeded data. |
| Which settings must match on both sides? | The answer is spread across client, server, query, and mapper pages. | Explain shared settings once, then link to that explanation. |
| How do I connect to several servers? | Client names and server selection are explained in different long pages. | Show both sides in one example. |
| How do I query, save, and load related data? | The query page also covers bulk writes and streaming. Examples introduce model properties that earlier pages never defined. | Give each operation a clear home and use one consistent example model. |
| How do I deploy safely? | Security and tenant isolation sit under Reference. Related transaction and identity settings appear elsewhere. | Group deployment instructions, security, transaction management, and recovery. |
| Why did my request fail? | Errors are explained on one page, but logs and measurements are split between configuration pages. | Start with the symptom, then show what to inspect on both sides. |
| What is the default for this setting? | Readers must search long explanations to find defaults and limits. | Add a concise settings table with links to examples. |
| How do I upgrade from 10.3? | There is no complete guide. The older 3.1 guide mixes new setup instructions with old interface descriptions. | Give 10.3 users their own upgrade guide. Move upgrades out of first-time setup. |
| How do I write a custom transport or serializer? | One transport page mixes hosting, serialization, service replacement, compatibility, and testing. | Separate these tasks and give each a complete example. |

Keep the useful existing material: the client/server diagram, transaction examples, error table,
write-recovery advice, and tenant-security examples. We do not need to discard everything.

## Approved menu

Paths are relative to `website/docs`. Choose folders and names for the new structure.
Configuration and data operations share the `applications/` folder. Deployment instructions use
`operations/`, and customization uses `extending/`. The home page stays at the site root.
What's new contains two clear subjects: `upgrades/` and `release-notes/`.

An existing address can stay when it already fits. Keeping it is not a requirement.

```text
Overview
  What InfoCarrier does                         index.md
  Choosing InfoCarrier                          overview/choosing-infocarrier.md
Get started
  Install the packages                          getting-started/installation.md
  Build your first client and server            getting-started/first-app.md
Build applications
  Query data                                    applications/querying.md
  Save changes                                  applications/saving.md
  Load related data                             applications/related-data.md
  Use transactions and savepoints               applications/transactions.md
  Stream query results                          applications/streaming.md
  Build a Blazor WebAssembly client              applications/blazor-webassembly.md
  Explore the Northwind example                  applications/northwind-sample.md
  Configure your application
    Configure a client                          applications/configuration/client.md
    Configure a server                          applications/configuration/server.md
    Share additional settings                   applications/configuration/shared.md
    Use multiple clients and endpoints          applications/configuration/multiple-endpoints.md
    Use a document database                     applications/configuration/document-stores.md
Deploy and operate
  Secure the deployment                         operations/security.md
  Isolate tenants                               operations/tenants.md
  Operate transactions across requests          operations/transactions.md
  Diagnose requests and execution               operations/diagnostics.md
  Handle errors                                 operations/errors.md
  Recover an uncertain write                    operations/write-recovery.md
Extend
  Map custom values                             extending/value-mappers.md
  Write transports and custom hosts             extending/transports.md
  Replace serialization                         extending/serialization.md
  Replace services and pipelines                extending/services.md
  Test without a network                        extending/testing.md
Reference
  Public API index                              reference/api.md
  Configuration options and limits              reference/options.md
  Client/server compatibility                   reference/compatibility.md
  Detailed limitations                          reference/limitations.md
What's new
  Choose an upgrade path                        upgrades/index.md
  Upgrade from 10.3 to 11                        upgrades/from-10-to-11.md
  Upgrade from 3.1 to 10                         upgrades/from-3-to-10.md
  Release notes                                 release-notes/11.0.md and 10.x pages
```

The reader-order pass gives the development site 39 pages. There were 23 in the original review; we then added
the 11.0 draft. Most proposed additions take material out of long existing pages.
Do not add empty introduction pages just to fill the menu.

| Section | Who it helps | What belongs there |
|---|---|---|
| Overview | Someone evaluating the product | What it does, its limits, and links to security and version support |
| Get started | Someone using it for the first time | One complete working application and samples |
| Build applications | Someone writing application code | Client/server setup, queries, saves, relationships, and transactions |
| Deploy and operate | Someone running an application | Security, tenant isolation, logs, transaction expiry, and failure recovery |
| Extend | Someone replacing a built-in component | Custom transports, serializers, services, value conversion, and testing |
| Reference | Someone looking for an exact answer | Method names, defaults, limits, and supported client/server versions |
| What's new | Someone returning after an update | Release changes and instructions for upgrading an existing application |

For example, a reader configuring transactions would first learn how to begin and commit one.
The deployment page would then explain expiry, caller identity, server restarts, and request routing.
The reference would give the exact timeout setting and its default.

## What happens to each existing page

The table maps old content to its proposed new home. It does not require a page or redirect
at the old address. Split material should live with the task it explains.

| Existing page | Proposed destination | What changes |
|---|---|---|
| `index.md` | `index.md` | Keep the diagram. Correct the version and query-execution claims. Put it under Overview. |
| `limitations.md` | `reference/limitations.md` | Keep detailed unsupported cases in Reference. The new product-fit page covers major adoption considerations first. |
| `getting-started/installation.md` | `getting-started/installation.md` | Explain all three packages and distinguish published packages from development builds. |
| `getting-started/first-app.md` | `getting-started/first-app.md` | Make the application runnable from an empty directory. Move testing without a network to Extend. |
| `getting-started/samples.md` | `applications/northwind-sample.md` | Optional source exploration after the package tutorial. Identify the sample version and explain the request inspector. |
| `getting-started/upgrading-from-3-1.md` | `upgrades/from-3-to-10.md` | Move it to What's new. Explain the path from 3.1 to 10, then link to the 10.3-to-11 guide. |
| `guide/querying.md` | `applications/querying.md` | Explain query behavior and explicit local execution. Move streaming to its own page and bulk writes to saving. |
| `guide/saving-changes.md` | `applications/saving.md` | Explain ordinary saves and bulk updates together. Link to document-database setup and recovery. |
| `guide/transactions.md` | `applications/transactions.md` | Keep begin, commit, savepoint, and transaction-sharing examples. Move deployment concerns to the operations page. |
| `guide/loading-related-data.md` | `applications/related-data.md` | Keep the loading choices. Define all model properties and proxy requirements used by examples. |
| `guide/errors.md` | `operations/errors.md` | Keep error types and cancellation advice. Move the full uncertain-write procedure to its own page. |
| `configuration/client.md` | `applications/configuration/client.md` | Keep basic and manual client setup. Move shared settings, multiple endpoints, logs, and defaults to their dedicated pages. |
| `configuration/server.md` | `applications/configuration/server.md` | Keep context and endpoint setup. Move shared settings, document databases, transaction operation, and logs to their dedicated pages. |
| `configuration/transports.md` | `extending/transports.md` | Keep custom transports and hosting. Move serialization, service replacement, compatibility, and network-free testing out. |
| `configuration/value-mappers.md` | `extending/value-mappers.md` | Keep the worked example. Explain mapper order and declared types. Recheck the spatial-query advice. |
| `platforms/blazor-webassembly.md` | `applications/blazor-webassembly.md` | Build a customer page against the working tutorial server first. Put publishing and compiled-model options after normal use. |
| `security.md` | `operations/security.md` | Put it under Deploy and operate. Explain authorization and link to the relevant settings. |
| `multi-tenancy.md` | `operations/tenants.md` | Put it beside security. Check a complete example for reads, ordinary saves, and bulk writes. |
| `api-surface.md` | `reference/api.md` | Keep it as an index of the public application programming interface (API). Correct the event list and link to detailed reference pages. |
| `release-notes/10.0.md` | `release-notes/10.0.md` | Keep the historical examples. Remove any undated claim that 10.0 is the current release. |
| `release-notes/10.1.md` | `release-notes/10.1.md` | Keep the original syntax and dated results. Make the version of linked instructions clear. |
| `release-notes/10.2.md` | `release-notes/10.2.md` | Keep historical method names. Link to upgrade instructions where needed. |
| `release-notes/10.3.md` | `release-notes/10.3.md` | Keep the shipped shared-configuration example and Abstractions package introduction. |
| `release-notes/11.0.md` | `release-notes/11.0.md` | Keep it as an unpublished draft. Use it to find missing explanations in ordinary guides. |

Remove duplicate testing examples and incorrect version 11 descriptions.
Do not remove old release examples just because those methods no longer exist on `main`.

### README, package pages, and other links

These are different entrances to the same documentation. They should not all repeat a full tutorial.

| Entry point | What it should contain |
|---|---|
| Repository README | A short product explanation and example, development status, and links to published instructions and matching samples |
| Core package readme | How to use that package, prerequisites, the server package, and setup links |
| AspNetCore package readme | Endpoint setup, authorization responsibility, the compatible Core dependency, and a server-guide link |
| Abstractions package readme | When a shared project needs it, a shared-configuration example, dependencies, and working links |
| Package metadata | Correct descriptions, documentation links, and the right readme in every package |
| Sample README and Codespaces links | Whether the sample uses published packages or development code |
| GitHub release text | A short description of that release; keep its maintained source in `docs/release-bodies` |
| `SECURITY.md` | Private reporting instructions and a support statement consistent with the site |
| Internal `docs/` | Design decisions and investigation records. Application developers should not need these to set up the product. |
| Site images and other assets | Keep their addresses and appearance unless the new navigation requires a specific change. |

The Abstractions readme used a different documentation address that failed the November 4 check.
On November 10, its link was corrected to `https://azabluda.github.io/InfoCarrier.Core/`,
matching the site configuration and other package readmes. This local correction does not change
readmes inside already published packages.

## Fixes in priority order

“First” means a reader could install the wrong package, fail to run an example, or misunderstand
query execution or security. “Next” means the feature needs a complete explanation.
“Later” means a smaller consistency or maintenance improvement.

The evidence column names the code or tests inspected. It does not mean those tests were run during this review.

| Priority and topic | Problem and required change | Evidence and version |
|---|---|---|
| First: packages | Home and installation say two packages. Explain the optional direct Abstractions reference and dependencies between the three packages. | All three were published in 10.3.0. |
| First: installation | A stable install command is followed by development-only code. Separate the instructions. A `--prerelease` flag alone does not select the GitHub package feed. | Current role-configuration methods and package workflows |
| First: custom transports | The first-app transport implements only `SendAsync`. Version 11 requires `Send` too. Correct claims that synchronous calls simply wait for asynchronous calls. | `IInfoCarrierTransport` and `HttpInfoCarrierTransport.Send` |
| First: query execution | Home and the Core readme still suggest automatic local filtering. Explain refusals, permitted final result construction, server-side rewrites, and explicit `AsEnumerable`. | Version 11 `QuerySplitter.RejectClientEvaluation` and query tests |
| First: serializers | The transport page says custom serializers handle query content, then says that content always uses built-in JSON. Explain query content separately from streamed messages. | Protocol 4 code and protocol 3 conversion tests |
| First: first application | Add project creation, provider installation, connection settings, database creation, seed data, and expected output. Avoid assuming `recent[0]` exists or customer `AROUT` was created elsewhere. | First-app page and Northwind startup |
| First: authorization | Explain that query filters and caller identity are not a complete access-control system. Check reads, saves, and bulk updates separately. | Security review and server permission checks |
| Next: shared settings | Show one shared configuration method called by both client and server. Explain model, named types, anonymous types, and value mappers. Registering a type allows transport; it does not make a database provider understand every operation. | Shared builders shipped in 10.3; role callbacks change in 11. `ConfigureShared` runs immediately. |
| Next: multiple endpoints | Show both sides together. Explain names, duplicate names, missing names, case rules, selection by builder or context type, and which services belong to each registration. | Named-role methods and `NamedEndpointPolicyIsolationTest`; development feature |
| Next: component replacement | Explain which component owns and disposes a replacement. Separate host services from Entity Framework's internal services. | `ReplaceService`; development feature |
| Next: streaming | Explain when rows arrive early, when results stay buffered, and what happens after cancellation, early disposal, or a late error. Include limits and custom adapters. | `StreamingQueryRegressionTest`; 10.3 buffers results |
| Next: defaults and limits | Put defaults in one table: request limit 64 MiB, no response limit, no transaction idle timeout, and log forwarding off. Explain which side enforces each limit. | Current role options; streamed enforcement is new in 11 |
| Next: diagnosis | The API page lists only QuerySplit. Add ClientQueryFailure and ClientSaveFailure. Explain logs and measurements for both sides, cancellation, unfinished streams, and sensitive data. | `InfoCarrierEventId` and `InfoCarrierMetrics`; some measurements predate 11 |
| Next: transaction deployment | Explain expiry, the server process holding the transaction, caller binding, joining, restarts, and lost responses together. | Owner/timeout tests and `CommitAsync`; timeout and caller binding shipped in 10.2, uncertain-completion changes in 10.3 |
| Next: uncertain writes | Give a complete procedure for checking whether a write succeeded after its response was lost. Explain the durable marker, fresh verification context, generated-key and Cosmos limits, and retry limits. Do not promise automatic duplicate-write prevention. | `ServerSaveChangesExecutor` and existing marker-row guidance |
| Next: document databases | Show both role settings together. Explain a partially attached entity and how missing owned data is loaded before saving. Limit claims to verified providers. | `CompleteDocumentsAsync`, `IncompleteDocumentTest`, and MongoDB evidence; behavior shipped in 10.2, names change in 11 |
| Next: upgrades | Explain changed setup methods, synchronous methods, query behavior, and compatibility in one 10.3-to-11 guide. | Current interfaces, history, and the method table below |
| Next: compatibility | Separate supported package versions from accepted message-format versions. A conversion test with development code is not a test with a published older client. | Protocols 3/4 and support policy |
| Next: database setup | Tell readers to create tables and check database connectivity through the server context. Client calls no longer return success without doing the work. | Database-creator removal and current refusal |
| Later: samples | Select a release tag for stable examples. Update the inspector explanation, which assumes older encoded query content. | Northwind startup and protocol 4 |
| Later: example model | Use `PlacedOn` consistently, define `Order.Lines` before using it, and supply or replace the missing `Money.Parse`. | Existing example pages |
| Completed: word checks | Removed the checker and page budgets at the owner's request on November 10. | Separate maintenance commit |

Some claims still need checking: spatial operations described as local, the exact browser failure
for synchronous calls, document stores beyond the tested MongoDB setup, and custom-model trimming
requirements. These are questions, not confirmed product defects.

The reference should also explain unsupported local database-connection and schema operations,
automatic browser lazy loading, Native AOT, ambient transactions, and the lack of built-in retries.
Native AOT means compiling the application to native code before it runs.
Use implemented behavior, not an old design proposal, when writing these explanations.

## Keep published and development instructions separate

The November 4 checks found:

| Item | Result |
|---|---|
| Published packages | 10.3.0 was the latest version of Core, AspNetCore, and Abstractions. No 11.x packages appeared on nuget.org. |
| GitHub release | [10.3.0](https://github.com/azabluda/InfoCarrier.Core/releases/tag/v10.3.0), published October 13, included all three packages. |
| Public site | [Deployment 40414995947](https://github.com/azabluda/InfoCarrier.Core/actions/runs/40414995947) used `release/10.3`, commit `1fd8cdcd4bde50960b388e76d0c0fc4189485e59`. |
| Publishing | The documentation workflow needs a manual run from a release branch. A commit does not publish the site. |
| Development code | `main` was version 11 but still depended on Entity Framework Core 10: `[10.0.1,11.0.0)`. |
| Development packages | These use GitHub Packages, which needs separate feed instructions. |

Package-version sources: [Core](https://api.nuget.org/v3-flatcontainer/infocarrier.core/index.json),
[AspNetCore](https://api.nuget.org/v3-flatcontainer/infocarrier.core.aspnetcore/index.json),
[Abstractions](https://api.nuget.org/v3-flatcontainer/infocarrier.core.abstractions/index.json).
These are dated findings. Recheck them before publication.

Keep the public site on the published release. Clearly label development instructions on `main`,
link to the published documentation, and explain how to build development code from source.
Give GitHub feed setup as an advanced alternative.

Correct errors affecting 10.3 on `release/10.3`, then bring those corrections into `main`.
Do not copy version 11 methods or streaming behavior into 10.3 instructions.
Use the same organization on both branches where the features exist.

Old release notes should keep their old examples. The 3.1 upgrade page should explain the route
to version 10; the new guide should continue from 10.3 to 11.

A public version selector could keep older documentation available after version 11 ships.
It would also add hosted copies, separate search results, and more publishing and link maintenance.
Decide that separately. It is not necessary for this rewrite.

## Entity Framework Core 11 and the release notes

Do not give Entity Framework Core 11 a permanent section in ordinary guides.
After adoption, put each verified capability where a reader would use it: queries, saving,
mapping, or configuration.

Keep its reserved section in the 11.0 release notes. For now, it says adoption is pending.
An upstream feature is not automatically a verified InfoCarrier feature.
Check that it works through the client/server connection before claiming support.
Use the existing [adoption checklist](adoption-checklist.md).

The release notes should help us find missing instructions:

| Release-note topic | What the ordinary documentation must explain |
|---|---|
| Entity Framework Core 11 | Which inherited features actually work, their provider limits, and the required framework version |
| New configuration | A complete client/server example, shared settings, and replacements for old calls |
| Explicit local execution | What now throws, when registration helps, and how to choose local execution |
| Streaming | Which queries stream, partial results, limits, cancellation, and late errors |
| Synchronous calls | How to update a custom transport and what browser applications can use |
| Custom components and diagnostics | Replacement methods, disposal, logs, events, and measurements |
| Compatibility | Supported package combinations, accepted message formats, and protocol errors |
| Removed methods | Required application changes, with working before/after examples |
| Database setup | Where to create the database and check connectivity |

If a release note has nowhere useful to link, that is a missing guide or explanation.
Keep detailed engineering evidence here, rather than adding it all to the release notes.
Before publication, remove drafting comments and check requirements, upgrade instructions, and links.
Do not maintain another changing test count in the notes.

## Upgrade mapping review, 2026-11-10

This section answers: “Which calls in an existing application need attention?”

I compared the three published 10.3.0 libraries with development commit `6d2d7964`.
I checked current source and change history for replacements.
I have not yet compiled or run before/after migration examples.

The comparison tool also reports differences caused by generated code and by how it reads libraries.
Its raw count is not a reliable count of removed features.
Some changes, such as turning a static method into an instance method, need manual checking.

### Application setup

| Old call or setting | What to use in version 11 |
|---|---|
| `AddInfoCarrier(...)` | Configure each side with `AddInfoCarrierClient` and `AddInfoCarrierServer<TContext>`. |
| `InfoCarrierDbContextOptionsBuilder` | Use `InfoCarrierClientOptions`. A registered client attaches with `UseInfoCarrier()` or a client name. Overloads accepting a client object still exist. |
| `AllowTypes` or `AddInfoCarrierAllowedTypes` | Call `AddAllowedTypes` from shared configuration on both sides. The database provider must still support the query. |
| `AddInfoCarrierArbitrarySqlExecution` | Call `AllowArbitrarySqlExecution(true)` in shared configuration on both sides. The server permission remains necessary. |
| `AddInfoCarrierStandardValueMappers` | Pass the required mapper instances to `AddValueMappers` in shared configuration. |
| `UseNonRelationalServerStore()` | Set the client's `ServerUsesRelationalQueryRules` to `false`. |
| `AddInfoCarrierServerDocumentStore` | Set the server's `StoresOwnedDataInsideOwner` to `true`. This controls document writes; it does not replace the client setting above. |
| `AddInfoCarrierServerTransactionTimeout` | Set `TransactionIdleTimeout` or call `WithTransactionIdleTimeout` on server options. |
| Old server log-forwarding registrations | Set server `LogForwarding` options or call `WithLogForwarding`. Choose sensitive-data forwarding explicitly. Each fluent call sets both values. |
| `AddInfoCarrierHttpCallerIdentity` on the services collection | Call `AddHttpCallerIdentity` on the server builder whose endpoint needs that identity. |
| Client database creation or connection checks | Run these through the server database context. The old client implementation returned success without checking the database. |

The old configuration-only marker interfaces were also removed.
Code that implemented those interfaces should use the corresponding settings above.

### Custom components

Most application authors will not need every item in this table.

| Custom code | What needs checking |
|---|---|
| Calls on `InfoCarrierDeploymentBuilder` | Several instance methods became extension methods. Ordinary chained calls can still compile. Rebuild against version 11; do not assume an unchanged call means old compiled code remains compatible. |
| `InfoCarrierOptionsExtension.WithAllowedTypes` or `WithArbitrarySqlExecution` | Use the supported client/server configuration callbacks. |
| A transport, client, or server implementing only asynchronous methods | Implement the required synchronous methods too. Check streaming support separately. A registration rename alone is insufficient. |
| Static `ServerLogCapture.Begin` | Call `Begin` on the capture instance used by that host. Custom hosting must use the same instance for logging and server capture. |
| Query-split logging methods or `QuerySplitEventData.UnregisteredKeyTypes` | Check the remaining `ServerQueryCount` and `ClientOperators` properties. Update code that expected automatic local filtering. |
| Old query-translation or shaped-query factory classes | These obsolete classes were removed. Ordinary applications need no replacement registration. |

The [database-creator removal](https://github.com/azabluda/InfoCarrier.Core/pull/248) includes
infrastructure tests. Current `InfoCarrierRelationalFacadeDependencies.DatabaseCreator` refuses
these operations. The [logging cleanup](https://github.com/azabluda/InfoCarrier.Core/pull/252)
explains the instance change. Other evidence is in the existing
[configuration design](configuration-api-design.md) and
[registration audit](multiple-registrations.md#post-implementation-obsolete-code-audit-2026-10-25).
The later [migration tests](https://github.com/azabluda/InfoCarrier.Core/pull/277) check development
clients in separate processes. They do not replace future tests with a published older client.

### What “finish checking the upgrade” means

1. Take a small application using published 10.3 packages and confirm that it builds.
2. Replace its old setup calls with the calls above. Build it against development version 11.
3. Do the same for a custom transport, including `Send` and `SendAsync`.
4. Check less common extension code: constructors called directly, serializer overloads,
   value-mapper method parameters, moved types, and changed query-message contents.
5. Run examples or existing tests for matching client/server settings, caller identity,
   document-store settings, and refused client database operations.

For example, adding an optional parameter can let old source code compile again, while breaking
an application that still loads an old compiled extension. We must check both situations before
calling the upgrade instructions complete.

This is proposed verification work. Executable migration examples have not been created.
The final upgrade guide should contain the instructions; release notes should link to it.

## Handle changed addresses

Choose useful page names and headings first. Do not keep awkward folders, duplicate explanations,
or old headings only to preserve links.

Update every link we control to point to the new pages and headings. This includes navigation,
README, package sources, samples, release-text sources, and the security policy.
Site edit links should select the branch shown to the reader.

Old bookmarks and links inside already published packages may stop working. That is an accepted
cost of this overhaul. Use the old-to-new table above to review the effect before publication.

A small set of redirects may help frequently used entry links, if they are simple to maintain.
Redirects are optional. They must not determine the new page structure or require preserving every
old heading. Decide whether they are worth adding after the new structure is settled.

Historical release notes still describe their own releases. Moving a page must not change that
meaning. Their current `release-notes/` folder already fits the proposal.

Check all new internal links and anchors with the existing checker. Check public destinations
separately before publication. Report deliberately retired addresses separately from broken links
inside the new site.

## Other structures considered

| Choice | Why it helps | Why I do not recommend it |
|---|---|---|
| Only rename menu tabs | Small change | Leaves conflicting examples and long mixed-purpose pages |
| Divide everything into Client and Server | Matches the two processes | Shared settings, transactions, security, and failures still require both sides |
| Divide everything into Tutorials, How-to, Explanation, and Reference | Separates kinds of writing | Readers must first classify their question instead of choosing their task |
| Keep one large configuration manual | Fewer pages | Makes readers pass unrelated hosting and operational details to find setup instructions |

The proposed sections keep exact reference separate from examples, while using task names in the menu.
Seven sections may be awkward on a narrow screen. Check this in the existing theme before changing its appearance.

## Implementation batches

Use the approved menu and SQLite tutorial for these batches.
These decisions do not authorize publication or merging.

On November 10, 2026, the owner approved implementation of all three batches and requested plain
language throughout. Complete the batches locally without stopping for another approval between
them. Publishing and merging remain outside this authorization.

### 1. Fix installation and the first application

Status: implemented and checked locally.

Update README, the three package pages, Home, installation, samples, and upgrade entry pages.
Correct incompatible method names wherever they occur.

Build a complete SQLite example using `ShopContext`, `Customer`, and `Order`.
Create and seed its database. Keep Northwind as the larger sample with its own model.

The check: a reader can create both projects, install the correct packages, run both processes,
read seeded rows, save a change, and read it through a fresh context.
Check the readmes actually packed into the packages.
Keep published-version fixes separate from development-only instructions using the existing branches.

### 2. Split and reorganize the guides

Status: implemented and checked locally. Interactive browser checks remain unavailable.

Apply the proposed menu. Separate shared settings, multiple endpoints, document databases,
streaming, transaction deployment, logs, and write recovery.
Use the new paths and useful heading names. Update all links within the documentation.
Review retired addresses using the old-to-new table.

The check: follow every reader question listed above from an entry page.
Verify examples for several endpoints, partial streamed results, transaction expiry, and lost responses.
Security examples need two tenants and separate checks for reads, ordinary saves, bulk writes,
and `IgnoreQueryFilters`. A sample must not claim to solve every application's authorization needs.

### 3. Finish customization and reference

Status: implemented and checked locally.

Separate serialization and service replacement from custom transports.
Keep one example of testing without a network.
Finish settings, errors, events, measurements, compatibility, and upgrade instructions.
Correct package links and review all three package readmes.

The check: compile complete custom-component examples and exercise synchronous and asynchronous calls,
service disposal, server selection, and custom serialization.
Check supported package combinations separately from message-format conversion.

Complete all three batches before submitting the overhaul for continuous integration checks.
Publishing and merging require separate authorization.

### Checks for every batch

- Apply the humanizer skill to user-facing prose.
- Build the site strictly: `dotnet run --file eng/DocsServe.cs -- --build`.
- Check links and headings: `dotnet run --file eng/DocLinks.cs --`.
- Check public links separately.
- Compile complete examples with their models and setup, using the version they describe.
  Temporary example projects and existing tests are enough; do not build a new testing framework.
- Check what runs on the server, not just whether the client gets an answer.
- Check desktop and mobile navigation, keyboard use, and searches for common tasks.
- Run `.claude/hooks/file-hygiene.py` on changed files before committing.

Review clarity and completeness without word limits. Split pages by reader task.

## Review evidence and limits

The original review read all 23 site Markdown pages, README, three package readmes, sample entry
instructions, package metadata, publishing configuration, and maintained release text.
The starting checkout was clean `main` at `7dac7cc6ef6578452aa4bb831c3347663f34298e`.
Later release-note and method review reached `6d2d7964`.

Relevant internal sources are [style](../../doc-style.md), [versioning](../../versioning.md),
[requirements](../../infocarrier-core-requirements.md), [architecture](../../architecture.md),
[decisions](../../decisions.md), [security](../../security-review.md),
[message formats](../../wire-protocol.md), [the earlier rewrite](../v10/docs-rewrite-plan.md),
and [earlier reader findings](../v10/cold-read-findings.md).
Old design notes help explain decisions; current code must confirm behavior.

The August 23 rewrite and August 24 reader checks deliberately kept the menu unchanged.
The latest comprehensive content review found in history was `659622fb`, September 9:
it read 25 consumer documents and corrected five pages.
[The October styling change](https://github.com/azabluda/InfoCarrier.Core/pull/259) did not review content.

The actual menu has five top-level entries: Home, Getting started, Using it, Configuration,
and Reference. Release notes are inside Reference, contrary to the handoff description.

The November 4 word check measured server configuration at 1,945 prose words, transports at 1,392,
client configuration at 1,290, and querying at 1,150. Code was excluded.
Their mixed purposes, not length alone, justify splitting them.
The old checker omitted Abstractions and disagreed with the written Core limit.
The owner requested removal of all word-count checks and budgets on November 10.

The public site responded successfully and its search data included transaction, security,
and shared-configuration headings. Its modification date matched the deployment.
The GitHub command-line tool supplied deployment history after the connector rejected that request.
The browser failed before loading the site. Visual layout, mobile navigation, and search ranking
therefore remain unchecked.

No application example was compiled or run during this analysis.
Code and test inspection supported the findings, but did not replace execution.
At the original post-merge check, Packages had passed; Build & Test and Coverage were still running.
This documentation review neither waited for them nor changed those workflows.

## Implementation verification, November 10, 2026

- The exact SQLite tutorial code built in temporary projects outside the repository.
  Its HTTP client printed `Before: Example Shop` and `After: Updated Shop`.
  The second value was read through a new client context.
- The loopback transport, logging transport, logging serializer, and Money mapper were copied
  from their code blocks into a temporary project and compiled. The loopback example ran against SQLite.
- A published 10.3.0 setup and an asynchronous-only transport compiled against the published package.
  The new tutorial and custom transports compiled against the current development projects.
- The tenant database-selection code compiled and ran with temporary test authentication.
  Checks covered `IgnoreQueryFilters`, a guessed foreign tenant key in `SaveChanges`, bulk updates,
  bulk deletes, concurrent database selection, and refusing another tenant's transaction.
  The temporary authentication handler was not added to the documentation or repository.
- Transport tests: Passed: 394, Failed: 0, Total: 394. This includes existing tests for streaming,
  registrations, replacement, protocol conversion, transaction behavior, and diagnostics.
- Incomplete-document tests: Passed: 5, Failed: 0, Total: 5. The sandboxed attempt could not locate
  MongoDB binaries. The same tests passed with access to the package binaries outside the sandbox.
- Query smoke tests: Passed: 12, Failed: 0, Total: 12. These include the refusal of implicit local
  row evaluation described by the updated querying guidance.
- All three packages packed locally. Each packed `README.md` matched its source `PACKAGE.md`.
  No package was published.
- The strict documentation build passed. The link checker included new, untracked pages explicitly
  and reported no broken links in 116 Markdown files.
- Generated HTML contained all seven navigation sections. Search data included transaction,
  shared configuration, streaming, upgrade, and SQL sections. These checks do not establish search ranking.
- No browser was connected. Desktop/mobile appearance, keyboard interaction, and interactive search
  remain unchecked. The existing theme and stylesheet were kept; a development-version notice was added.

Temporary example restores reported NU1900 because the sandbox could not retrieve package
vulnerability data over TLS. The examples still compiled and ran. No product code, new testing
framework, or word-count check was introduced.

Historical release examples retain their version-specific syntax. Their detail links now point
to source pages at the corresponding release tags. The 11.0 draft links to the new guides.
Retired public addresses and anchors are intentionally not preserved with redirects.
Previously published package readmes and release bodies are unchanged.

## Reader-order pass, November 10, 2026

The owner approved the revised approach. This pass changes teaching order and examples, as well
as menu placement.

| Reader journey or page group | Result of the review |
|---|---|
| Home and product choice | Home explains the application relationship and leads to a working example. A new Choosing InfoCarrier page covers capabilities, model sharing, network requests, and server responsibilities. Rare limitations moved to Reference. |
| Installation and first application | This pass mistakenly made a source checkout the main route. The consumer-audience correction below replaces it with NuGet packages and the reader's own projects. |
| Queries | Ordinary filtering, paging, selection, and tracking come first. Helper registration, query splitting, raw SQL, and custom keys follow only after those tasks. |
| Saves and relationships | Examples use the tutorial's actual seeded customer. Bulk changes sit with normal writes. Relationship-loading choices precede the more specialized proxy case. Missing variables were supplied. |
| Transactions | The basic transaction explains its purpose before implementation details. Savepoint and shared-transaction examples now contain actual Shop operations instead of undefined helper methods. |
| Client and server setup | Minimal examples no longer require an unexplained shared helper. Extra shared registrations have their own later task. |
| Multiple endpoints and document stores | These remain optional configuration tasks, rather than steps on the first-application path. Endpoint examples no longer require an unused shared method. Required document-write protection stays before the affected writes. |
| Browser client | Moved to Build applications. The page creates a project, configures the endpoint and local cross-origin access, adds a customer component, and gives its address. Publishing, trimming, and compiled models follow the working example. |
| Security and tenants | Security now starts with deployment actions: authenticate, enforce data permissions, bind transactions, and set transport/resource controls. Serializer protections follow those steps. The tenant page retains its checked database-selection example. |
| Errors, diagnostics, transaction operation, and recovery | Error and diagnostic pages start with the reader's symptom and response. Transaction operation and write recovery already follow their specific deployment tasks, with required safeguards before retrying writes. |
| Customization | The pages first help readers choose the appropriate extension point. The mapper guide presents the usual model converter before a custom mapper and special value hazards. |
| Reference and release history | Exact defaults, limits, signatures, and compatibility stay available for direct lookup. Historical release examples retain their historical meaning. Release notes and upgrades do not become prerequisites for a new application. |

Verification for changed examples:

- A standalone Blazor project created from the installed template compiled with the exact Program
  and Customers component copied from the guide. Restore needed network access outside the sandbox.
- The exact server cross-origin snippets compiled. A preflight request from
  `http://localhost:5182` received HTTP 204 with that allowed origin, POST, and the requested headers.
- Eleven common-guide code blocks compiled and ran through HTTP against the Shop server:
  queries, paging, projections, saves, eager/explicit loading, transactions, savepoints, and two
  contexts sharing a transaction.
- The first tutorial's model and application code were retained from the prior successful run.
  This pass changes its introduction and onward route.
- Full transport and document-store suites were not repeated: this pass changes documentation,
  and the relevant existing behavior was already checked in the first pass.
- A compiled browser project is not evidence of rendered browser behavior. A browser later connected,
  but its navigation and keyboard APIs failed before loading the preview. The documented fallback
  failed too. Interactive browser execution, desktop/mobile appearance, and keyboard/search
  interaction remain explicitly unverified.

The remaining review is whether the revised reading journeys work for the owner and other readers.
Do not describe technical build checks as proof of that editorial result.

## Consumer-audience correction, November 10, 2026

The previous pass confused documenting unreleased APIs with asking readers to develop InfoCarrier.
Its source-checkout tutorial was the wrong starting point for a NuGet library. The owner identified
that error after the reader-order pass. This correction supersedes that pass's onboarding decision.

The primary reader creates an application, installs packages, and tries queries and saves.
Home now shows those operations in C# before the architecture diagram. Getting started contains
package installation and a complete application made from an empty directory.

The audience review covered all current site pages, README, the three package readmes, sample
instructions, and their entry links. It checked who performs each task, prerequisites, repository
dependencies, and whether a page asks the reader to maintain the library. Release history retains
its historical scope. The confirmed problems were broader than the first tutorial:

| Location | Problem and correction |
|---|---|
| Installation and first application | Removed source checkouts, library project references, local package building, and development-feed setup. The reader installs packages into three application-owned projects. |
| README and package readmes | Removed links labelled as source/development installation. README shows a query and save; package entry links lead to installation and the version 11 tutorial. |
| Sample placement | Moved Northwind from Get started to an optional application example. Its page explicitly describes source exploration, with a separate link to the package tutorial. |
| Upgrade entry and 10.3-to-11 guide | Replaced checkout/source-installation framing with package availability. Verification instructions now concern the reader's applications and custom components. |
| Compatibility reference | Removed release-maintainer acceptance procedures and fixture requirements. The page now explains supported package combinations, application model agreement, and deployment checks. Custom transport requirements remain an optional section. |
| Detailed limitations | Removed the opening test-suite measurements and test-porting assumptions. The page describes application operations and behavior. |
| Blazor and application testing | Added explicit package prerequisites. These examples cannot assume the shared model project references InfoCarrier. |
| Spatial mapping | Clarified that readers adapt the linked mapper into their own shared project. The test utilities are an example source, rather than an application dependency. |

Configuration, security, diagnostics, transaction operation, and write recovery concern application
developers and operators. They do not require building InfoCarrier. The extension pages serve readers
who choose to replace a documented component. Their advanced contracts remain there, outside onboarding.
IntelliSense and optional source debugging are ordinary package features; neither requires a checkout.

Version 11 packages are not yet published. A draft availability notice states that fact and links
to the published 10.3 guide. It does not turn source development into the installation procedure.
The public NuGet index still listed 10.3.0 as its latest version when checked on November 10.

For verification, temporary consumer projects used locally packed version 11 packages. Their only
project references were to the application's own shared model. This private verification feed is
not part of the reader instructions and does not establish public package availability.
With the latest Entity Framework Core 10 dependencies, the client and server built without warnings.
The tutorial read and saved through HTTP, then printed `Before: Example Shop` and `After: Updated Shop`.
The exact Home teaser also queried and saved through those packages.

This is an audience and dependency review, not a second verification of every documented behavior.
Earlier runtime checks remain recorded above. Interactive browser checks remain unavailable.
