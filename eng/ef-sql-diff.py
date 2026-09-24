#!/usr/bin/env python3
"""Compare the SQL the server ran, test by test, with the SQL EF's own SQLite suite expects.

WHAT THIS IS FOR. It is an INVESTIGATION, not a gate and not a quality claim. EF's specification
suite is thousands of little users of this provider: they report a wrong answer loudly, and say
nothing at all about a full table crossing the wire. This is the instrument that makes the silent
half visible, by asking what the server ran and comparing it with what EF's own provider runs for
the same test. Each difference is then read and triaged:

  * our defect  -> fix it, and pin the promise in `Sqlite/ServerSqlTest.cs`, in our own words;
  * a deviation we accept -> pin THAT in the same file, with the reason;
  * an artifact of EF's own test harness -> nothing to write.

Nothing here is copied into the suite. Four defects came out of the run on 2026-09-15/16: a
parameter replaced by its literal values, a concurrency token dropped from a write, a whole complex
column written for a one-member change, and a `First()` that ran with no `LIMIT`.

HOW TO RUN IT: `bash eng/ef-sql-compare.sh`, which needs nothing checked out and leaves nothing
behind. This script is the comparing half of it, and can be run alone on a log produced by a
SERIAL run with INFOCARRIER_SERVER_SQL naming a file (parallel tests interleave between two
markers, which corrupts the comparison quietly).

WHAT IS IGNORED, AND WHY EACH IS A NAME AND NOT A PLAN. Whitespace; parameter names (`@Value` for
`@id`: a parameter crosses inside `ParameterBox<T>` and EF names it after the box's property);
aliases and the qualifiers that reference them (`"Item1"` for `"X"`, `"v"` for `"i"`: the projection
split and a boxed collection name them differently). Literals and structure are kept.

WHAT IT COMPARES. EF's expected statements, matched IN ORDER inside the test's own statements.
EF clears its baseline inside the test, so what it asserts is a FRAGMENT: the reads a test does to
arrange and to verify are not in it, and neither is the seeding of a fixture that builds its store
per test. Comparing whole against whole reported 182 tests as differing on 2026-09-16, and 7 of
them were real; the statements the server ran that EF does not assert are therefore not part of the
report below. A round trip of OURS is pinned in `Sqlite/ServerSqlTest.cs` instead, where the query
is ours too.

A TEST WHOSE EF OVERRIDE RUNS ANOTHER EF TEST IS NOT PAIRED, and is listed as such. EF's SQLite
`IsNullOrEmpty` runs `base.IsNullOrWhiteSpace()` and asserts that statement, a copy-and-paste
slip in EF's own suite since the refactor that created the file. Pairing it compared two
different queries and reported the same difference on every run (2026-09-21).

`--extras` READS THAT REMAINDER, and it is a second question rather than a louder version of the
first. Most of it is the test's own arrange and verify, which EF runs too and never asserts. What
hides in it is the silent half of the silent half: a round trip this provider makes that EF's
provider does not, which no assertion of EF's can report because EF's window is not open when it
happens. The statements are grouped by SHAPE, so one shape is read once however many tests run it,
and every extra belongs to a group. Unbounded reads come first, because a table crossing the wire
is what this instrument was built to find.

UNDER TPC THE TABLE IS THE PREDICATE -- BUT ONLY THE *TYPE* PREDICATE, AND THAT DISTINCTION IS THE
WHOLE OF THIS CLAUSE (the owner, 2026-09-23). Each concrete type has its own table, so `FROM
"Kiwi"` returns exactly the rows TPH reads as `FROM "Animals" WHERE "Discriminator" = 'Kiwi'` and
TPT as `FROM "Animals" INNER JOIN "Birds"`: that missing `WHERE` is the store's schema. **It says
nothing about any other filter.** If the query also said `Where(k => k.Name == "x")`, then `FROM
"Kiwi"` bare is a dropped filter and a whole-table transfer -- exactly the sign this instrument
exists to find -- so "no predicate under TPC" must never be read as "fine".

The report carries the control itself rather than leaving the next reader to rebuild it by hand.
A flagged TPC read is looked up in the SAME TEST METHOD of its TPH and TPT siblings, and the
shape is reported MAPPING-BOUND, with the sibling's statement printed beside it, only when a
sibling NARROWS the same columns AND its predicate is the type test and nothing else. A shape
keeps UNBOUNDED unless EVERY one of its occurrences found such a sibling.

"THE TYPE TEST AND NOTHING ELSE" HAS TO RULE OUT A BUSINESS FILTER STANDING BOTH BESIDE THE TYPE
TEST AND IN PLACE OF IT, and the two need different answers. Beside it is a count: `WHERE
"Discriminator" = 'Kiwi' AND "Name" = 'x'` has a conjunct too many. In place of it is not, since
`WHERE "Name" = 'x'` alone is the same SHAPE as a discriminator test -- so the column is asked
whose it is. A discriminator names a column the TPC leaf table does NOT have, which is what TPC
means; a dropped filter names one it does. TPT's `IS NOT NULL` terms name the leaf's own key
columns and so cannot use that rule: each must appear in a JOIN's `ON`, which is what makes it
the join's key test rather than a filter on it. See SINGLE_TABLE below, including the simpler
rule that was tried first and is wrong.

It remains a claim about one statement. A filter dropped on BOTH sides looks clean to both, and
that is not this report's question: the comparison against EF's own `AssertSql` above is what
reports a filter that went missing.

DO NOT COMPARE THE EXTRAS TOTAL BETWEEN TWO RUNS; COMPARE THE `reads:` LINE, which is why the
header prints reads and writes apart, in statements as well as shapes. The writes include each
run's fixture SEEDING, and which fixtures seed is a property of the machine rather than of the
product: the Tier B store is file-backed and is not deleted on disposal, so a fixture whose `.db`
survives the startup sweep reads it instead of seeding it. Measured on two serial runs of the same
tier, 2026-09-22 and 2026-09-23, over the same 19 419 and 19 420 tests: the total fell from 4157
statements to 3002, a 28% fall that says nothing at all, because the reads were 397 statements in
59 shapes BOTH times and every statement of the difference was a write (3760 against 2605) --
`INSERT`s into the many-to-many join tables in one run, into the conference planner's in the other.

Each of those four conditions was measured, and `mapping_control` says what dropping it costs.
On 2026-09-22 the two false positives were the whole of the run's unbounded extras: `FROM "Kiwi"`
and `FROM "Coke"` in `TPCInheritanceBulkUpdates`. Over the whole log `--survey` reclassifies six
shapes and leaves 484 flagged, which is the ratio to expect: this is a narrow fact about one
mapping, not a way to make the report quieter.

WHAT IT REPORTS, per statement EF asserts and the server did not run, dangerous first:
  LITERAL     the server ran the same shape with a literal where EF has a parameter
  PARAMETER   the server ran the same shape with a parameter where EF has a literal
  VALUE       the same shape and kinds, a different value
  STRUCTURAL  the server ran no statement of that shape in that test
The owner's rule (docs/test-policy.md): the first two are equally bad, and a structural difference
that can change the store's plan is a red flag. A structural difference is also how a client-side
join shows: the server ran two whole-table reads and EF ran one join.

Usage: eng/ef-sql-diff.py <server-sql.log> [--efcore <path>] [--quiet] [--limit N] [--extras]
"""

import argparse
import collections
import glob
import os
import re
import sys

TEST_MARKER = '=== TEST '

TOKEN = re.compile(
    r"(?P<param>@[A-Za-z_][A-Za-z0-9_]*)"
    r"|(?P<blob>X'[0-9A-Fa-f]*')"
    r"|(?P<string>'(?:[^']|'')*')"
    r"|(?P<number>(?<![\w\"])\d+(?:\.\d+)?(?:[eE][+-]?\d+)?(?![\w\"]))")
PARAM = re.compile(r'@[A-Za-z_][A-Za-z0-9_]*')
ALIAS = re.compile(r'\s+AS "[^"]*"')
QUALIFIER = re.compile(r'"[^"]*"\.')
DML = re.compile(r'^\s*(SELECT|UPDATE|DELETE|WITH|INSERT)\b', re.I)
WRITE = re.compile(r'^\s*(UPDATE|DELETE|INSERT)\b', re.I)
LOG_HEADER = re.compile(r'^\w+: \d\d/\d\d/\d{4} ')
METHOD = re.compile(r'public\s+(?:override\s+)?(?:async\s+)?[\w<>\[\],?. ]+?\s+(\w+)\s*\(')
CLASS = re.compile(r'\bclass\s+(\w+)')
ASSERT_SQL = re.compile(r'\bAssert(?:ExecuteUpdate)?Sql\s*\(')
BASE_CALL = re.compile(r'\bbase\.(\w+)\s*[(<]')

KINDS = ('LITERAL', 'PARAMETER', 'VALUE', 'STRUCTURAL')

# A read with nothing to bound it: no predicate, no row limit, no join and no aggregate. It is the
# shape a whole table crosses the wire in, and the shape a client-side operator leaves behind.
BOUND = re.compile(r'\b(WHERE|LIMIT|JOIN|EXISTS|COUNT|SUM|AVG|MIN|MAX|GROUP BY|UNION|INTERSECT|EXCEPT)\b', re.I)

# The rule above cannot read a schema, and under TPC the schema IS the predicate: one table per
# concrete type means `FROM "Kiwi"` returns exactly the rows TPH fetches with `WHERE
# "Discriminator" = 'Kiwi'` and TPT with `INNER JOIN "Birds"`. Only TPC gets this benefit. A TPH
# or TPT read with no predicate is a read of the hierarchy's own table and stays flagged.
SIBLINGS = ('TPH', 'TPT')
TPC = re.compile('^TPC')
SELECT_LIST = re.compile(r'^SELECT (?:DISTINCT )?("_"\."[^"]+"(?:, "_"\."[^"]+")*) FROM ')
COLUMN = re.compile(r'"_"\."([^"]+)"')

# What makes a sibling's read a CONTROL rather than merely another statement that parsed. BOUND
# above is deliberately generous -- it answers "is anything holding this back" -- and `LEFT JOIN`
# and `UNION` both satisfy it while narrowing nothing: TPC reads a whole hierarchy as a UNION of
# its tables, and a LEFT JOIN keeps every row of the left side. Measured on 2026-09-23: accepting
# those excused `FROM "LocustHordes"` against a `Factions LEFT JOIN LocustHordes`, which is not
# the same rows in either direction.
NARROWING = re.compile(r'\b(WHERE|INNER JOIN)\b', re.I)

# AND THE SIBLING'S PREDICATE MUST BE THE TYPE TEST AND NOTHING ELSE (the owner, 2026-09-23).
# "The table is the predicate" is true only of the TYPE. If the query also said
# `Where(k => k.Name == "x")`, then `FROM "Kiwi"` bare is a DROPPED FILTER and a whole-table
# transfer -- and without this clause the TPH sibling's
# `WHERE "Discriminator" = 'Kiwi' AND "Name" = 'x'` would still have been accepted as a control
# and excused it. So the sibling's WHERE must be absent, or one term of `col = literal` or
# `col IN (...)`, or any number of `col IS NOT NULL` -- which are TPH's discriminator test and
# TPT's outer-join key test, one per key column, and no business filter has that shape without
# also adding a conjunct.
#
# Conservative where it cannot parse: an `OR` or a nested SELECT answers no. And it remains a
# claim about THIS statement only. A filter dropped on both sides would look clean to both, which
# is not this report's question: the comparison half against EF's own `AssertSql` is what reports
# a filter that went missing.
WHERE_CLAUSE = re.compile(r'\bWHERE\b(.*?)(?:\bGROUP BY\b|\bORDER BY\b|\bHAVING\b|\bLIMIT\b|$)', re.I)
TYPE_EQUALITY = re.compile(r'^"_"\."([^"]+)" (?:=|IN) (?:\'(?:[^\']|\'\')*\'|\d+|\([^()]*\))$')
TYPE_PRESENCE = re.compile(r'^"_"\."([^"]+)" IS NOT NULL$')
JOIN_ON = re.compile(r'\bON\b(.*?)(?:\bWHERE\b|\bGROUP BY\b|\bORDER BY\b|\bLIMIT\b|$)', re.I)

# AND ONE CONJUNCT IS NOT ENOUGH ON ITS OWN: it has to be the TYPE. `WHERE "Discriminator" =
# 'Kiwi'` and `WHERE "Name" = 'x'` are the same shape, and the second one, standing where the
# first should, would mean the TPC read had dropped a filter rather than never had one. The two
# are told apart by asking whose column it is:
#
#   * a DISCRIMINATOR names a column the TPC leaf table does not have -- that is what TPC means;
#   * a DROPPED FILTER names one it does.
#
# A table's columns are taken from the log itself, as every column ever projected by a
# single-table read of it. Measured 2026-09-23: `Kiwi`, `Coke`, `Officers`, `LocustHordes` and
# `Leaves` carry no `Discriminator`, while `Animals` and `Drinks` do.
#
# An earlier and simpler rule was tried first and is recorded because it LOOKS right: "a column
# the TPC test class never mentions". It fails outright -- `Discriminator` appears 63 times in
# `TPCInheritance` statements and 532 in `TPCGearsOfWar`, because one context holds several
# hierarchies and only some are TPC.
#
# TPT's `IS NOT NULL` terms are a different test and cannot use this rule, since they name the
# leaf's own key columns, which the TPC table certainly has. They must appear in a JOIN's `ON`
# instead, which is what makes them the join's key test rather than a filter on it.
SINGLE_TABLE = re.compile(r'^SELECT (?:DISTINCT )?(.*?) FROM "([^"]+)"(?: WHERE| ORDER BY| LIMIT|$)')
FROM_TABLE = re.compile(r'\bFROM "([^"]+)"')
ANY_COLUMN = re.compile(r'"_"\."([^"]+)"')

_TABLE_COLUMNS = {}


def table_columns(sections):
    """table -> the columns any single-table read of it projects, over the whole log.

    Memoized per `sections`, because it is one pass over every statement and the answer is a
    property of the log rather than of the statement being classified.
    """
    cached = _TABLE_COLUMNS.get(id(sections))
    if cached is not None:
        return cached

    columns = collections.defaultdict(set)
    for cases in sections.values():
        for case in cases:
            for statement in case:
                read = SINGLE_TABLE.match(canon(statement))
                if read:
                    columns[read.group(2)].update(ANY_COLUMN.findall(read.group(1)))

    _TABLE_COLUMNS[id(sections)] = columns
    return columns


def canon(sql):
    return QUALIFIER.sub('"_".', ALIAS.sub('', ' '.join(sql.split())))


def exact(sql):
    return PARAM.sub('@p', canon(sql))


def shape(sql):
    kinds = []

    def replace(match):
        kinds.append('P' if match.group('param') else 'L')
        return '?'

    return TOKEN.sub(replace, canon(sql)), tuple(kinds)


def key(class_name):
    """The name both sides share: NorthwindWhereQuerySqliteTest and NorthwindWhereQuerySqliteInfoCarrierTest."""
    name = class_name.replace('+', '.')
    for word in ('SqliteInfoCarrier', 'InfoCarrier', 'Sqlite', 'Test', 'Query', 'Base'):
        name = name.replace(word, '')
    return name


def ef_expected(efcore):
    """(class, method) -> [(statements, where)], from the raw strings of every AssertSql call.

    Also returns the tests it did NOT keep, (class, method) -> (the other test, where): an override
    whose body runs a DIFFERENT EF test that has its own AssertSql. Its assertion is that other
    test's SQL, so pairing it with the test of the same name compares two different queries. The
    rule names no test and reads EF's own source, so it stops matching the day EF corrects one.
    `DeviationKind.UpstreamCallsAnotherTest` is the same finding for an override of ours.
    """
    root = os.path.join(efcore, 'test', 'EFCore.Sqlite.FunctionalTests')
    if not os.path.isdir(root):
        raise ValueError(f"no EF SQLite functional tests under '{root}'")

    expected = collections.defaultdict(list)
    calls = {}
    for path in glob.glob(root + '/**/*.cs', recursive=True):
        text = open(path, encoding='utf-8-sig').read()
        classes = [(m.start(), m.group(1)) for m in CLASS.finditer(text)]
        methods = [(m.start(), m.group(1)) for m in METHOD.finditer(text)]
        for call in ASSERT_SQL.finditer(text):
            end = call_end(text, call.end() - 1)
            if end is None:
                continue
            start, method = next(((p, n) for p, n in reversed(methods) if p < call.start()), (None, None))
            cls = next((n for p, n in reversed(classes) if p < call.start()), None)
            if method is None or cls is None or method.startswith('Assert'):
                continue
            statements = [s for s in raw_strings(text[call.end():end]) if DML.match(s)]
            if statements:
                line = text.count('\n', 0, call.start()) + 1
                where = f'{os.path.relpath(path, root)}:{line}'
                expected[(key(cls), method)].append((statements, where))
                called = set(BASE_CALL.findall(text[start:call.start()]))
                if called and method not in called:
                    calls[(key(cls), method)] = (called, where)

    # A base call to a helper, or to `OnModelCreating`, is not another test: only a name that has
    # an AssertSql of its own is. Anything looser matched three such calls when it was tried.
    another = {}
    for (cls, method), (called, where) in calls.items():
        others = sorted(c for c in called if (cls, c) in expected)
        if others:
            another[(cls, method)] = (others[0], where)
    for k in another:
        del expected[k]
    return expected, another


def call_end(text, start):
    """The index of the ')' closing the call whose '(' is at start, skipping raw strings."""
    depth, i = 0, start
    while i < len(text):
        if text.startswith('"""', i):
            close = text.find('"""', i + 3)
            if close < 0:
                return None
            i = close + 3
            continue
        if text[i] == '(':
            depth += 1
        elif text[i] == ')':
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return None


def raw_strings(body):
    """The SQL of each raw string in an AssertSql call, without EF's parameter preamble."""
    for block in re.findall(r'"""\r?\n(.*?)\r?\n\s*"""', body, re.S):
        lines = block.replace('\r\n', '\n').split('\n')
        if lines and lines[0].startswith('@'):
            if '' not in lines:
                continue
            lines = lines[lines.index('') + 1:]
        yield '\n'.join(lines)


def server_sections(path):
    """(class, method) -> [statements of one test case], read off the markers."""
    sections = collections.defaultdict(list)
    current, statements, entry = None, [], []

    def flush():
        for index, line in enumerate(entry):
            # A statement that FAILED counts. It is exactly what a store-limit test asserts, and
            # EF's own baseline records it, so skipping it reported the statement as never run.
            if 'Executed DbCommand' in line or 'Failed executing DbCommand' in line:
                sql = '\n'.join(l[6:] if l.startswith('      ') else l for l in entry[index + 1:]).strip()
                if DML.match(sql):
                    statements.append(sql)
                return

    with open(path, encoding='utf-8', errors='replace') as log:
        for line in log:
            line = line.rstrip('\n')
            if line.startswith(TEST_MARKER):
                flush()
                entry = []
                if current:
                    sections[current].append(statements)
                cls, _, method = line[len(TEST_MARKER):].rpartition('.')
                sections_key = (key(cls.rsplit('.', 1)[-1]), method)
                current, statements = sections_key, []
            elif LOG_HEADER.match(line):
                flush()
                entry = []
            else:
                entry.append(line)

    flush()
    if current:
        sections[current].append(statements)

    if not sections:
        raise ValueError(f"'{path}' has no '{TEST_MARKER}' lines. Was it written by a run of this repository's tests?")
    return sections


def classify(statement, ran):
    """What kind of difference an unmatched statement of EF's is, against everything we ran."""
    expected_shape, expected_kinds = shape(statement)
    same_shape = [shape(s)[1] for s in ran if shape(s)[0] == expected_shape]
    if not same_shape:
        return 'STRUCTURAL'
    if any(any(e == 'P' and r == 'L' for e, r in zip(expected_kinds, k)) for k in same_shape):
        return 'LITERAL'
    if any(any(e == 'L' and r == 'P' for e, r in zip(expected_kinds, k)) for k in same_shape):
        return 'PARAMETER'
    return 'VALUE'


def match_case(wanted, ran):
    """EF's expected statements, matched IN ORDER inside one test case's run.

    WHY IN ORDER AND NOT ONE BLOCK. EF's baseline is cleared inside the test and asserted inside it,
    so what EF expects is a fragment of the test, and our marker covers the whole test. The reads a
    test does to arrange and to verify are ours alone, and EF clears its log between two writes, so
    its fragment is not even contiguous in ours. Requiring the fragment IN ORDER keeps what can be
    compared and drops what cannot: on 2026-09-16 comparing whole against whole reported 182 tests
    as differing and eight of them were real.

    The statements the server ran that EF does not assert are counted and never reported. They are
    the test's own arrange, verify and seeding, outside EF's window, and a promise of ours in
    `Sqlite/ServerSqlTest.cs` is where a round trip of ours is pinned instead.
    """
    unmatched, consumed, index = [], set(), 0
    for statement in wanted:
        found = next((i for i in range(index, len(ran)) if exact(ran[i]) == exact(statement)), None)
        if found is None:
            unmatched.append(statement)
        else:
            consumed.add(found)
            index = found + 1
    return unmatched, consumed


def differences(wanted_variants, cases):
    """The worst case's unmatched statements, against the variant that fits it best."""
    worst = ([], None)
    for case in cases:
        best = None
        for wanted, where in wanted_variants:
            unmatched, _ = match_case(wanted, case)
            if best is None or len(unmatched) < len(best[0]):
                best = (unmatched, where, case)
        if len(best[0]) > len(worst[0]):
            worst = (best[0], (best[1], best[2]))
    return worst


def unbounded(sql):
    """Whether a statement reads without a predicate, a row limit, a join or an aggregate."""
    return not WRITE.match(sql) and not BOUND.search(canon(sql))


def projection(sql):
    """The columns a bare `SELECT "t"."A", "t"."B" FROM ...` reads, as a set, or None.

    `Discriminator` is dropped, because it is the column TPH adds to say what TPC says with the
    table name and a comparison across mappings has to look past it. None for anything that is not
    a bare column list -- an aggregate, a literal, a subquery -- because a projection we could not
    parse must never be reported as one that matched.
    """
    head = SELECT_LIST.match(canon(sql))
    if not head:
        return None
    return frozenset(COLUMN.findall(head.group(1))) - {'Discriminator'}


def type_test_only(sql, leaf, sections):
    """Whether a statement's predicate is the type test of `leaf`'s mapping and nothing else.

    See WHERE_CLAUSE and SINGLE_TABLE above for why this exists and how the two halves are told
    apart: without the first a sibling carrying a business filter ALONGSIDE its discriminator test
    would excuse a TPC read that had dropped that filter, and without the second a sibling
    carrying one INSTEAD of a discriminator test would.
    """
    text = canon(sql)
    if len(re.findall(r'\bSELECT\b', text, re.I)) > 1 or re.search(r'\bOR\b', text, re.I):
        return False

    where = WHERE_CLAUSE.search(text)
    if not where:
        return True

    terms = [t.strip() for t in re.split(r'\bAND\b', where.group(1), flags=re.I)]

    # TPT: one `IS NOT NULL` per key column of the joined leaf, and each must be a column the
    # join keys on. Anything else with that shape is a filter on the join, not the join's own test.
    presence = [TYPE_PRESENCE.match(t) for t in terms]
    if all(presence):
        keys = set(ANY_COLUMN.findall(' '.join(m.group(1) for m in JOIN_ON.finditer(text))))
        return bool(keys) and all(m.group(1) in keys for m in presence)

    # TPH: one equality, on a column the leaf table does not have. See SINGLE_TABLE above.
    if len(terms) != 1:
        return False
    equality = TYPE_EQUALITY.match(terms[0])
    return equality is not None and equality.group(1) not in table_columns(sections)[leaf]


def mapping_control(statement, k, sections):
    """(where, statement) for a TPC read whose siblings NARROW the same columns, or None.

    The control for "this read has no predicate because the mapping is the predicate" -- where
    "the predicate" means THE TYPE and nothing else. Four things have to hold, and each was
    measured to matter on 2026-09-23 against the same log:

    THE CLASS IS A TPC ONE, because only TPC gives a concrete type its own table. `FROM "Animals"`
    under TPH is the whole hierarchy, and excusing it against a TPT sibling's join reclassified
    eight reads that were loose in exactly the way this instrument exists to find.

    THE SIBLING IS THE SAME TEST METHOD, because the claim is about one scenario run three ways.
    Searching the whole sibling class instead excused 162 statements in 12 shapes across 75 tests
    -- `SELECT "Id" FROM "Weapons"`, whole reads of `EntityOnes` and `EntityThrees` -- since a
    short column list is shared by many tests and one of them somewhere has a predicate. That is a
    coincidence of the column list, not a control.

    THE SIBLING'S READ NARROWS, per NARROWING above, rather than merely satisfying BOUND.

    AND ITS PREDICATE IS THE TYPE TEST AND NOTHING ELSE, per `type_test_only`. Without this the
    claim would be "a TPC read with no predicate is fine", which is false the moment the query
    carries an ordinary filter: `FROM "Kiwi"` bare is then a DROPPED FILTER and a whole-table
    transfer, and a sibling's `WHERE "Discriminator" = 'Kiwi' AND "Name" = 'x'` would have
    excused it as readily as a bare discriminator test -- as would `WHERE "Name" = 'x'` on its
    own, which is why the column is asked whose it is and not merely counted.
    """
    class_name, method = k
    wanted = projection(statement)
    leaf = FROM_TABLE.search(canon(statement))
    if not TPC.match(class_name) or wanted is None or leaf is None:
        return None

    for other in SIBLINGS:
        sibling = (other + class_name[3:], method)
        for case in sections.get(sibling, ()):
            for candidate in case:
                if WRITE.match(candidate) or not NARROWING.search(canon(candidate)):
                    continue
                if projection(candidate) == wanted and type_test_only(candidate, leaf.group(1), sections):
                    return f'{other}.{method}', candidate
    return None


def collect(groups, statement, k, sections):
    """Add one statement to its shape's group, with the sibling-mapping control if it has one.

    The control is asked for only where it could matter, which is a read the BOUND rule flagged,
    and the number of occurrences that found one is kept beside it. A shape is reported
    MAPPING-BOUND only when that number is all of them, so one genuinely loose read cannot hide
    behind the neighbours that share its shape.
    """
    group = groups[shape(statement)[0]]
    group[0] += 1
    group[1].add(k)
    if group[2] is None:
        group[2] = statement
    if unbounded(statement):
        control = mapping_control(statement, k, sections)
        if control:
            group[3] = group[3] or control
            group[4] += 1


def extras(expected, sections):
    """shape -> (occurrences, tests, one example), for every statement EF's fragment did not take.

    Per test CASE, because a theory runs its arrange once per row and each run is a chance for an
    extra round trip; grouped by shape, because reading the same arrange read four hundred times is
    not reading it four hundred times. The variant chosen per case is the one that matches it best,
    exactly as the difference report chooses it, so the two halves account for the same statements.
    """
    groups = collections.defaultdict(lambda: [0, set(), None, None, 0])
    for k in expected:
        if k not in sections:
            continue
        for case in sections[k]:
            best = None
            for wanted, _ in expected[k]:
                unmatched, consumed = match_case(wanted, case)
                if best is None or len(unmatched) < best[0]:
                    best = (len(unmatched), consumed)
            for index, statement in enumerate(case):
                if index in best[1]:
                    continue
                collect(groups, statement, k, sections)
    return groups


def survey(sections):
    """shape -> (occurrences, tests, one example), for EVERY statement in the log.

    The reading for a tier with no upstream baseline to compare against. ADR-009 Tier C is the
    case: the Firebird provider ships 158 functional test files at EFCore-13.0.0.0 and not one
    AssertSql call, so there is nothing to subtract and nothing to pair. What is left is still
    worth reading, because the red flags this instrument was built to find are visible without a
    baseline: a table crossing the wire has no predicate and no row limit whatever EF would have
    written.
    """
    groups = collections.defaultdict(lambda: [0, set(), None, None, 0])
    for k, cases in sections.items():
        for case in cases:
            for statement in case:
                collect(groups, statement, k, sections)
    return groups


def mapping_bound(group):
    """Whether EVERY occurrence of this shape found a sibling mapping that bounds it."""
    return group[3] is not None and group[4] == group[0]


def flag_of(group):
    if not unbounded(group[2]):
        return 'WRITE' if WRITE.match(group[2]) else 'READ'
    return 'MAPPING-BOUND' if mapping_bound(group) else 'UNBOUNDED'


ORDER = ('UNBOUNDED', 'MAPPING-BOUND', 'READ', 'WRITE')


def report_groups(groups, limit, label):
    """Every group, unbounded reads first, then the ones the mapping bounds, reads, then writes."""
    def rank(item):
        return (ORDER.index(flag_of(item[1])), -item[1][0])

    order = sorted(groups.items(), key=rank)

    # Reads and writes are counted apart, in statements as well as shapes, because only the reads
    # are comparable between two runs. See "DO NOT COMPARE THE EXTRAS TOTAL" in the docstring.
    statements = collections.Counter()
    shapes = collections.Counter()
    for group in groups.values():
        statements[flag_of(group)] += group[0]
        shapes[flag_of(group)] += 1
    reads = ('UNBOUNDED', 'MAPPING-BOUND', 'READ')

    print()
    print(f'{label}: {sum(statements.values())} statements in {len(groups)} shapes')
    print(f'  reads:  {sum(statements[f] for f in reads)} statements in {sum(shapes[f] for f in reads)} shapes '
          f'({shapes["UNBOUNDED"]} unbounded, {shapes["MAPPING-BOUND"]} bounded by the mapping, '
          f'{shapes["READ"]} other)')
    print(f'  writes: {statements["WRITE"]} statements in {shapes["WRITE"]} shapes '
          f"(includes this machine's fixture seeding; compare the reads between runs, not this)")

    for _, group in order[:limit]:
        count, tests, example = group[0], group[1], group[2]
        names = ', '.join(sorted(f'{c}.{m}' for c, m in tests)[:3])
        print()
        print(f'  {flag_of(group)}  x{count} in {len(tests)} tests: {names}'
              + (', ...' if len(tests) > 3 else ''))
        print('    ', ' '.join(canon(example).split())[:280])
        if mapping_bound(group):
            where, bounded = group[3]
            print(f'      the same columns, bounded, under {where}:')
            print('    ', ' '.join(canon(bounded).split())[:280])
    if len(order) > limit:
        print(f'    ... and {len(order) - limit} more shapes; raise --limit to see them')


def main(argv):
    sys.stdout.reconfigure(newline='\n')
    parser = argparse.ArgumentParser(description='Compare the server SQL log with EF\'s expected SQL.')
    parser.add_argument('log', help='a server-sql.log from a serial run with INFOCARRIER_SERVER_SQL set')
    parser.add_argument('--efcore', default='subrepos/efcore', help="EF Core checkout (default: subrepos/efcore)")
    parser.add_argument('--quiet', action='store_true', help='counts only, no per-test listing')
    parser.add_argument('--limit', type=int, default=20, help='how many tests to list per kind (default: 20)')
    parser.add_argument('--extras', action='store_true',
                        help='read the statements EF does not assert, grouped by shape')
    parser.add_argument('--survey', action='store_true',
                        help='read EVERY statement in the log, grouped by shape, for a tier with no baseline')
    args = parser.parse_args(argv[1:])

    try:
        sections = server_sections(args.log)
        # A survey needs no reference at all, which is the point of it: the tier it is for has none.
        expected, another = ({}, {}) if args.survey else ef_expected(args.efcore)
    except (OSError, ValueError) as error:
        print(f'ef-sql-diff: {error}', file=sys.stderr)
        return 1

    if args.survey:
        report_groups(survey(sections), args.limit, 'the server ran')
        return 0

    paired = [k for k in expected if k in sections]
    counts = collections.Counter()
    findings = collections.defaultdict(list)

    # Counted per TEST and not per statement, so that one test cannot dominate the report.
    for k in paired:
        unmatched, source = differences(expected[k], sections[k])
        if not unmatched:
            counts['same'] += 1
            continue

        counts['differs'] += 1
        where, ran = source
        seen = collections.defaultdict(list)
        for statement in unmatched:
            seen[classify(statement, ran)].append(statement)

        for kind, examples in seen.items():
            counts[kind] += 1
            findings[kind].append((k, where, examples, ran))

    not_paired = sorted(k for k in another if k in sections)

    print(f'EF tests with expected SQL {len(expected)}; tests in the log {len(sections)}; paired {len(paired)}')
    print(f'identical {counts["same"]}, differing tests {counts["differs"]}; '
          f"not paired, EF asserts another test's SQL {len(not_paired)}")
    print('by kind, in tests: ' + ', '.join(f'{kind} {counts[kind]}' for kind in KINDS))

    if args.extras:
        report_groups(extras(expected, sections), args.limit, 'extras, which EF does not assert')
        return 0

    if args.quiet:
        return 0

    if not_paired:
        print(f"\n===== NOT PAIRED: EF asserts another test's SQL  ({len(not_paired)} tests)")
        for cls, method in not_paired:
            other, where = another[(cls, method)]
            print(f'\n{cls}.{method}  (EF: {where}) runs base.{other}(), so its AssertSql is {other}\'s')

    for kind in KINDS:
        found = findings[kind]
        if not found:
            continue
        print(f'\n===== {kind}  ({len(found)} tests)')
        for (cls, method), where, examples, ran in found[:args.limit]:
            print(f'\n{cls}.{method}  (EF: {where}; {len(ran)} statements ran in the test)')
            for statement in examples[:2]:
                print('    EF    :', ' '.join(canon(statement).split())[:300])
                same_shape = [s for s in ran if shape(s)[0] == shape(statement)[0]] or ran[:1]
                for ran_statement in same_shape[:1]:
                    print('    ours  :', ' '.join(canon(ran_statement).split())[:300])
            if len(examples) > 2:
                print(f'              ... and {len(examples) - 2} more in this test')
        if len(found) > args.limit:
            print(f'\n    ... and {len(found) - args.limit} more tests; raise --limit to see them')

    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
