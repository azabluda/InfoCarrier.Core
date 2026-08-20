# Plans

Working documents that belong to one generation of the provider: the roadmap, the rolling
implementation plan, superseded plans, and feature-level design notes.

`docs/` above this folder is the other kind of document. Architecture, the ADR log, the wire
format, the security review and the requirements describe the provider itself. They are edited as
it changes and they are never copied forward.

## Where the next generation goes

This provider's major version tracks Entity Framework Core's, so `v10` is the work that produced
InfoCarrier.Core 10. When EF Core 11 arrives, add `docs/plans/v11/` beside it and leave `v10/`
alone as the record of how 10 was built.

Only plans are scoped this way. Do not create `docs/plans/v11/architecture.md`: architecture does
not fork per EF major, it gets edited, and a copied file drifts from the one it was copied from.
An ADR written during M4 still binds in v11, which is why `docs/decisions.md` stays where it is.

## Inside a generation

| | |
|---|---|
| `roadmap.md` | Milestone scope, ordering and exit criteria. Changes when scope changes. |
| `implementation-plan.md` | Checkbox detail for the current milestone only. |
| `archive/` | Plans for finished milestones, kept as the record and never edited again. |
| `superpowers/` | Feature-level plans and specs produced while building something specific. |

The split between the roadmap and the implementation plan is deliberate and predates this folder:
milestone-level scope in one, per-task checkboxes in the other. Mixing them is what caused the
drift those two documents were written to replace.
