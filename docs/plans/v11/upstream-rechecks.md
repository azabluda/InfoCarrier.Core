# Upstream behaviour to re-read at the EF Core 11 tag

EF defects and EF behaviour that the 10.x line pins, works around or cites. Each entry says what to
re-read, and what the answer changes here. **A fixed upstream defect is invisible to the audit**:
`OverrideAudit` checks that a `[StoreDefect]` names a section that exists, not that the defect still
does.

## dotnet/efcore#37370: a compiled query over an uncorrelated list

**Written 2026-09-21.** In EF Core 10, a compiled query over a list parameter that nothing compares
with a column throws `UnreachableException` on a server in `Constant` or `MultipleParameters` mode.
The throw is the `default:` branch of
`RelationalTypeMappingPostprocessor.ApplyTypeMappingsOnValuesExpression`, reached because EF has no
element type mapping to give the list. **It is a defect, not a refusal**: the issue calls it a
failure, and dotnet/efcore#37372 fixed it by falling back to the default type mapping. That fix is
milestone `11.0.0`, and `release/10.0` still had the old code on 2026-09-21.

At the EF 11 tag:

1. **EF 11 adds `PrimitiveCollectionsQueryTestBase.Compiled_query_with_uncorrelated_parameter_collection_expression`**,
   which Tier B inherits. If this provider's result differs from EF's own SQLite test, the override
   carries `[StoreIssue(IssueTracker.EfCore, 37370)]` (the owner, 2026-09-21).
2. **Re-decide the deviation that
   `ServerSqlTest.A_compiled_query_consuming_a_list_sends_its_result_as_one_parameter` pins.** The
   client does not mark a list that `Count` or `Any` consumes, so the server evaluates it and sends
   one scalar, where EF 10 runs `json_array_length(@ids)` in `Parameter` mode. The reason for
   accepting that was that marking the list would reach EF 10's crash in the other two modes. On EF
   11 that reason is gone. Matching EF is then possible, and whether to do it is the owner's call; the
   promise is what turns red if the client changes.
3. **Update the row in [`docs/upstream-defects.md`](../../upstream-defects.md) §2**, which says
   "fixed for EF Core 11 only", to the release that shipped the fix.

## `docs/upstream-defects.md` §1.12: a primitive-collection parameter without a type mapping

**Written 2026-09-21.** EF refuses three tests of `PrimitiveCollectionsQueryRelationalTestBase`, two
of them compiled queries, and EF's own comment on the first says *"We should apply the default type
mapping to the parameter, but need to figure out the exact rules when to do this"*. The fix for
#37370 applies the default mapping in one such place. **Read the three tests at the EF 11 tag**: if
EF answers one of them now, an override that cites §1.12 for it has lost its reason.

## dotnet/efcore#19749: evaluating a compiled query's parameters

**Written 2026-09-21, when it was open in Backlog.** EF does not evaluate an uncorrelated expression
in a compiled query. That is why the client marks a list that a compiled query transforms
(`CollectionParameterMark`, #122 and #139), and #37370 names it as the change that would remove that
class of failure. **If this
issue ships, EF's own statement for `ids.Skip(1).Contains(x)` in a compiled query changes**, and the
mark becomes the deviation rather than the match. Read its milestone when adopting.

## `StringTranslationsSqliteTest.IsNullOrEmpty`: EF's test bug

**Written 2026-09-21.** Since dotnet/efcore#35319 created the file, EF's SQLite override calls
`base.IsNullOrWhiteSpace()` and asserts that statement. `eng/ef-sql-diff.py` does not pair an EF
override that runs another EF test (`dca8acb`), and on EF 10 this is the only one. Read it at the EF
11 tag. If EF fixed it, the comparison pairs it again and it has to match. Whether to report it is
not decided.

## FirebirdSQL/NETProvider#1277: `LATERAL` over a function

**Written 2026-09-21.** `FirebirdLateralQuerySqlGenerator` corrects it on the server half of the
harness and is deleted when it is fixed. It was open at `EFCore-13.0.0.0`. Read the Firebird provider
that supports EF 11 before carrying the correction forward.

## The rest of `docs/upstream-defects.md` §1

Each entry names the EF test or the EF code it is about, and says what it blocks. Re-read each at the
new tag. A fixed one ends as a deleted override or a changed one, and the section is then corrected
in place with the date.
