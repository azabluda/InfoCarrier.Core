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


def extras(expected, sections):
    """shape -> (occurrences, tests, one example), for every statement EF's fragment did not take.

    Per test CASE, because a theory runs its arrange once per row and each run is a chance for an
    extra round trip; grouped by shape, because reading the same arrange read four hundred times is
    not reading it four hundred times. The variant chosen per case is the one that matches it best,
    exactly as the difference report chooses it, so the two halves account for the same statements.
    """
    groups = collections.defaultdict(lambda: [0, set(), None])
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
                group = groups[shape(statement)[0]]
                group[0] += 1
                group[1].add(k)
                if group[2] is None:
                    group[2] = statement
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
    groups = collections.defaultdict(lambda: [0, set(), None])
    for k, cases in sections.items():
        for case in cases:
            for statement in case:
                group = groups[shape(statement)[0]]
                group[0] += 1
                group[1].add(k)
                if group[2] is None:
                    group[2] = statement
    return groups


def report_groups(groups, limit, label):
    """Every group, unbounded reads first, then the other reads, then the writes."""
    def rank(item):
        example = item[1][2]
        return (0 if unbounded(example) else 1 if not WRITE.match(example) else 2, -item[1][0])

    order = sorted(groups.items(), key=rank)
    statements = sum(g[0] for g in groups.values())
    reads = sum(1 for g in groups.values() if not WRITE.match(g[2]))
    loose = sum(1 for g in groups.values() if unbounded(g[2]))
    print()
    print(f'{label}: {statements} statements in {len(groups)} shapes '
          f'({loose} unbounded reads, {reads - loose} other reads, {len(groups) - reads} writes)')

    for _, (count, tests, example) in order[:limit]:
        flag = 'UNBOUNDED' if unbounded(example) else 'WRITE' if WRITE.match(example) else 'READ'
        names = ', '.join(sorted(f'{c}.{m}' for c, m in tests)[:3])
        print()
        print(f'  {flag}  x{count} in {len(tests)} tests: {names}'
              + (', ...' if len(tests) > 3 else ''))
        print('    ', ' '.join(canon(example).split())[:280])
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
