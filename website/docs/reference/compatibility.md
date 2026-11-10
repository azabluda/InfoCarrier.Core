# Client and server compatibility

Compatible optional fields can share a wire version. Meaning changes can require a new version
despite unchanged types. Structural checks cover default serialization contracts. They exclude
application models and custom serializers. Keep those contracts compatible across supported deployments.
Advanced hosts can replace protocol codecs through dependency injection.

Version 11 supports clients from its current and previous minor releases. The first release,
11.0, supports only 11.0 clients. Version 10 and earlier version 11 development clients are outside the support promise. The support window is measured in releases, not elapsed time.

The wire contract uses an independent integer, currently 4. Several package releases can share
it. Packages outside the support window may still work with the same contract, without a compatibility
promise. The server checks the wire contract, not the caller's package version.
It also accepts contract 3 through explicit request and response conversion.
This does not extend the published package support window.

Custom envelope transports must preserve `ProtocolVersion`. Missing identification is zero and is
rejected. For `IInfoCarrierStreamTransport`, the query body carries `QueryDataRequest.ProtocolVersion`.
`TransportInfoCarrierClient` supplies it automatically. The response starts with a frame carrying
the same contract version, which the client checks before reading rows or faults.

Streamed HTTP requests use `InfoCarrier-Stream-Operation: Query`. The earlier
`InfoCarrier-Operation` selector is rejected. The new selector also prevents earlier servers from
silently executing a query while ignoring its added version field.
The `InfoCarrier-Protocol-Version` request header is checked before the query body is parsed;
the body version is checked too. The HTTP transport supplies both automatically.

The HTTP endpoint answers a version refusal with status 400 and a plain text explanation. Its
`InfoCarrier-Protocol-Version` and `InfoCarrier-Client-Protocol-Version` response headers identify
the server and received client contracts. The HTTP transport raises `InfoCarrierProtocolException`,
separately from connection failures. An older server without these refusal headers retains its
existing error behavior. See [handling errors](../operations/errors.md#incompatible-client-and-server).

## Before upgrading

Use matching client/server packages for a new application.
For a rolling upgrade, test every package combination promised by the support policy.
For version 11.1 and later, this includes a real published previous-minor client against the new server.

Successful decoding of protocol 3 development fixtures does not establish that package support.
Message format and package version are different numbers.
See [upgrade instructions](../upgrades/from-10-to-11.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
