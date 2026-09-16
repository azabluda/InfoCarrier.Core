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
them were real; the statements the server ran that EF does not assert are therefore not reported.
A round trip of OURS is pinned in `Sqlite/ServerSqlTest.cs` instead, where the query is ours too.

WHAT IT REPORTS, per statement EF asserts and the server did not run, dangerous first:
  LITERAL     the server ran the same shape with a literal where EF has a parameter
  PARAMETER   the server ran the same shape with a parameter where EF has a literal
  VALUE       the same shape and kinds, a different value
  STRUCTURAL  the server ran no statement of that shape in that test
The owner's rule (docs/test-policy.md): the first two are equally bad, and a structural difference
that can change the store's plan is a red flag. A structural difference is also how a client-side
join shows: the server ran two whole-table reads and EF ran one join.

Usage: eng/ef-sql-diff.py <server-sql.log> [--efcore <path>] [--quiet] [--limit N]
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

KINDS = ('LITERAL', 'PARAMETER', 'VALUE', 'STRUCTURAL')


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
    """(class, method) -> [(statements, where)], from the raw strings of every AssertSql call."""
    root = os.path.join(efcore, 'test', 'EFCore.Sqlite.FunctionalTests')
    if not os.path.isdir(root):
        raise ValueError(f"no EF SQLite functional tests under '{root}'")

    expected = collections.defaultdict(list)
    for path in glob.glob(root + '/**/*.cs', recursive=True):
        text = open(path, encoding='utf-8-sig').read()
        classes = [(m.start(), m.group(1)) for m in CLASS.finditer(text)]
        methods = [(m.start(), m.group(1)) for m in METHOD.finditer(text)]
        for call in ASSERT_SQL.finditer(text):
            end = call_end(text, call.end() - 1)
            if end is None:
                continue
            method = next((n for p, n in reversed(methods) if p < call.start()), None)
            cls = next((n for p, n in reversed(classes) if p < call.start()), None)
            if method is None or cls is None or method.startswith('Assert'):
                continue
            statements = [s for s in raw_strings(text[call.end():end]) if DML.match(s)]
            if statements:
                line = text.count('\n', 0, call.start()) + 1
                expected[(key(cls), method)].append((statements, f'{os.path.relpath(path, root)}:{line}'))
    return expected


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


def compare_case(wanted, ran):
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
    unmatched, index = [], 0
    for statement in wanted:
        found = next((i for i in range(index, len(ran)) if exact(ran[i]) == exact(statement)), None)
        if found is None:
            unmatched.append(statement)
        else:
            index = found + 1
    return unmatched


def differences(wanted_variants, cases):
    """The worst case's unmatched statements, against the variant that fits it best."""
    worst = ([], None)
    for case in cases:
        best = None
        for wanted, where in wanted_variants:
            unmatched = compare_case(wanted, case)
            if best is None or len(unmatched) < len(best[0]):
                best = (unmatched, where, case)
        if len(best[0]) > len(worst[0]):
            worst = (best[0], (best[1], best[2]))
    return worst


def main(argv):
    sys.stdout.reconfigure(newline='\n')
    parser = argparse.ArgumentParser(description='Compare the server SQL log with EF\'s expected SQL.')
    parser.add_argument('log', help='a server-sql.log from a serial run with INFOCARRIER_SERVER_SQL set')
    parser.add_argument('--efcore', default='subrepos/efcore', help="EF Core checkout (default: subrepos/efcore)")
    parser.add_argument('--quiet', action='store_true', help='counts only, no per-test listing')
    parser.add_argument('--limit', type=int, default=20, help='how many tests to list per kind (default: 20)')
    args = parser.parse_args(argv[1:])

    try:
        expected = ef_expected(args.efcore)
        sections = server_sections(args.log)
    except (OSError, ValueError) as error:
        print(f'ef-sql-diff: {error}', file=sys.stderr)
        return 1

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

    print(f'EF tests with expected SQL {len(expected)}; tests in the log {len(sections)}; paired {len(paired)}')
    print(f'identical {counts["same"]}, differing tests {counts["differs"]}')
    print('by kind, in tests: ' + ', '.join(f'{kind} {counts[kind]}' for kind in KINDS))

    if args.quiet:
        return 0

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
