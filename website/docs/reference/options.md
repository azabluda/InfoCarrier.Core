# Configuration options and limits

Configure a complete client or server registration at startup.
The library copies these settings when creating the registration.
Changing the original options object later does not reconfigure an active client or endpoint.

## Client options

| Property | Default | Meaning |
|---|---|---|
| `Endpoint` | Not set | Absolute HTTP address. Required when the default HTTP transport activates. A custom transport can omit it. Relative addresses are refused. |
| `ServerUsesRelationalQueryRules` | `true` | Apply relational query checks. Set `false` for a non-relational server store. |
| `MaxResponseBytes` | `null` | No response-size limit. A positive integer sets the limit. |
| `MediaType` | `application/json` | HTTP envelope and request payload type; stream messages keep their fixed format. |

## Server options

| Property | Default | Meaning |
|---|---|---|
| `StoresOwnedDataInsideOwner` | `false` | Complete missing owned data before document updates. See [document databases](../applications/configuration/document-stores.md). |
| `MaxRequestBytes` | 64 MiB | Maximum accepted request size, including streamed query requests. |
| `TransactionIdleTimeout` | `null` | No idle eviction. A positive duration enables rollback of abandoned transactions. |
| `LogForwarding.MinimumLevel` | `null` | Forwarding off. A logging level enables it. |
| `LogForwarding.IncludeSensitiveData` | `false` | Separate permission for a context using sensitive-data logging. Requires a minimum level. |
| `MediaType` | `application/json` | HTTP response envelope type; stream messages keep their fixed format. |

## Shared configuration

| Method | Default | Rule |
|---|---|---|
| `AddAllowedTypes` | Model-derived type permissions only | Add trusted named types on both sides. |
| `AddAnonymousShapes` | No extra registered shapes | Use matching prototype structures on both sides. |
| `AddValueMappers` | Standard and model-derived handling | Application mappers are tried first, in addition order. First match wins. |
| `AllowArbitrarySqlExecution` | `false` | Both roles opt in; the server grant controls execution. |
| `ConfigureShared` | No callback | Runs immediately once at its position in the chain. |

See [shared settings](../applications/configuration/shared.md).
Named registrations hold independent settings. Their names are case-sensitive.
Empty or duplicate names fail during registration. A missing client name fails when options resolve;
a missing server name fails during endpoint selection. No named registration silently becomes the default.

## Byte limits

A positive integer enables a limit. `null` disables it. Zero and negative values are refused.
The request and response directions are configured independently.
A streamed response limit counts all messages and metadata together.
Rows may already have arrived when the limit is exceeded.

Direct serializers use `InfoCarrierPayloadLimits`.
Its default request limit is `DefaultMaxRequestBytes`, 64 MiB.
A custom host must also limit input before buffering it.
Gateway and web-server limits can refuse a request before InfoCarrier sees it.

## Anonymous structures

| Limit | Value |
|---|---|
| Properties per anonymous type | 32 |
| Nodes in a complete type description | 1,024 |
| Nesting depth | 16 |
| Anonymous identities per exchange | 64 |
| Process-wide generated-type attempt limit | 4,096; applies to trusted configuration |

Nested anonymous values in arrays and generic collections are supported.
Rank-one non-vector arrays containing anonymous elements are unsupported.
Unknown structures and conflicting original identities are refused.
Registering a structure does not permit otherwise forbidden component types or expensive methods.

## Ownership and disposal

Registered transports, serializers, clients, servers, and pipeline factories are host-owned singletons.
They must support concurrent calls. Do not dispose injected services you did not create.
A server operation owns its fresh pipeline; the host owns its supplied context.
A transaction holds its context until it ends.
See [service replacement](../extending/services.md) for constructor selection and disposal.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
