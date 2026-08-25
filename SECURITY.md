# Security policy

## Reporting a vulnerability

Report it privately. [Open a draft security advisory](https://github.com/azabluda/InfoCarrier.Core/security/advisories/new)
and GitHub notifies the maintainer without making the report public. Please do not open an issue for
a vulnerability.

## What is in scope

Your server executes an expression tree that arrived over the network, and that path is the one this
policy is about.

- A payload that makes the server construct a type, call a method, or load an assembly that the
  allowlist does not admit. `docs/security-review.md` states the bound the allowlist is meant to
  hold, so a payload that crosses it is a defect.
- A payload that gets past the size limits before parsing.
- Anything that lets one caller read or change another caller's data through the endpoint.

## What is not in scope

These are known and documented, and a report about them is a support question rather than a
vulnerability.

- An endpoint mapped without authentication. It executes `SaveChanges`, `ExecuteUpdate` and
  `ExecuteDelete`, and authenticating it is the application's job.
- A global query filter used as an authorization boundary. `IgnoreQueryFilters` crosses the wire and
  the server honours it. The documented control is a query interceptor on the server.
- A transaction token used by a caller who did not open that transaction. The server does not bind a
  token to its creator.
- Expensive queries from an authenticated caller. Cap them where the caller cannot reach: a rate
  limit, a statement timeout, or a query interceptor.

[Security](https://azabluda.github.io/InfoCarrier.Core/security/) covers all four for the reader who
has to build against them.

## Versions

Fixes go to the `10.0` line. The `1.0` to `3.1` line last shipped in 2020 and gets none.
