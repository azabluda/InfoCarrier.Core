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
2. **`ServerParameterizationTest.A_compiled_query_consuming_a_list_fails_where_EF_Core_10_fails`
   turns red**, because the direct query answers on EF 11. The failure is parked there on purpose
   (the owner, 2026-09-22): this client marks a list that `Count` or `Any` consumes, as plain EF
   keeps it, so it fails where plain EF Core 10 fails. Move the test's two modes into
   `A_compiled_query_consuming_a_list_matches_the_direct_query`, and delete the failing test.
3. **Update the row in [`docs/upstream-defects.md`](../../upstream-defects.md) §2**, which says
   "fixed for EF Core 11 only", to the release that shipped the fix.
4. **Read the `Parameter` mode of `ids.Skip(1).Count()` too.** On EF 10 it fails with a different
   message, *"Expression '@ids' in the SQL tree does not have a type mapping assigned"*, directly
   and over the wire alike (measured 2026-09-22). That is §1.12's message, and #37372 did not claim
   it. No test of ours pins it.

## `docs/upstream-defects.md` §1.12: a primitive-collection parameter without a type mapping

**Written 2026-09-21.** EF refuses three tests of `PrimitiveCollectionsQueryRelationalTestBase`, two
of them compiled queries, and EF's own comment on the first says *"We should apply the default type
mapping to the parameter, but need to figure out the exact rules when to do this"*. The fix for
#37370 applies the default mapping in one such place. **Read the three tests at the EF 11 tag.**
Since 2026-09-22 all three inherit EF's refusal here and no override cites §1.12, so if EF 11 answers
one of them, EF's own base changes with it and this suite follows; §1.12 is then corrected in
place.

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
