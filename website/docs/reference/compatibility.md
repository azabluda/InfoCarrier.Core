# Client and server compatibility

For a new application, use matching InfoCarrier package versions on the client and server.

## Which versions work together?

| Server | Supported clients |
|---|---|
| Version 11.0 | Version 11.0 clients |
| Later version 11 minor releases | Clients from that minor release and the previous minor release |
| Version 11 server with a version 10 client | Not supported |

The support window follows minor releases, rather than a fixed number of months.
Earlier development builds are outside this published-package support promise.

## Plan an application upgrade

Check the [release notes](../release-notes/11.0.md) and [upgrade instructions](../upgrades/index.md)
before updating either application.
If client and server updates happen separately, keep the deployed combination inside the supported window.
Keep their shared entity model and deployment settings compatible too.
Package support does not check application-specific model changes.

Test the combinations your application will actually deploy.
Exercise representative queries, saves, transactions, authentication, and any custom components.
A read that succeeds does not establish that every application operation is compatible.

## Recognize an incompatible connection

The built-in transport identifies the message format automatically.
A refused format raises `InfoCarrierProtocolException`.
Its `ClientVersion` and `ServerVersion` properties describe message-format versions, not NuGet versions.
Retrying the same request does not fix the mismatch.

Use [error handling](../operations/errors.md#incompatible-client-and-server) to diagnose it.
An ordinary connection or malformed-response failure uses `InfoCarrierTransportException` instead.

## If you write a custom transport

The current message format is 4. The server also implements conversion for format 3.
Accepting a message format does not extend the package-version support window above.

Preserve `InfoCarrierEnvelope.ProtocolVersion`.
Missing identification is zero and is refused.
For streaming, preserve `QueryDataRequest.ProtocolVersion` and check the response's opening version message.
The built-in `TransportInfoCarrierClient` and HTTP transport handle this automatically.

Streamed HTTP queries use `InfoCarrier-Stream-Operation: Query` and the
`InfoCarrier-Protocol-Version` header. The request body version is checked too.
The older `InfoCarrier-Operation` selector is rejected.

The HTTP endpoint returns an incompatible-format refusal as status 400 with a plain-text explanation.
The `InfoCarrier-Protocol-Version` and `InfoCarrier-Client-Protocol-Version` response headers
identify the server and received client formats.
Older servers without those refusal headers retain their own error behavior.

If you replace serialization, keep the client and server formats compatible for your deployments.
See [serialization](../extending/serialization.md) and [custom transports](../extending/transports.md).


## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
