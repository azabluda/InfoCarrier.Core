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

WHAT IT REPORTS, in the order it prints, dangerous first:
  LITERAL     the server ran the same shape with a literal where EF has a parameter
  PARAMETER   the server ran the same shape with a parameter where EF has a literal
  STRUCTURAL  the server ran no statement of that shape in that test
  EXTRA       the server ran a statement EF does not expect at all
  ORDER       the same statements, in another order
The owner's rule (docs/test-policy.md): the first two are equally bad, a structural
difference that can change the store's plan is a red flag, and an EXTRA read is how a whole table
crosses the wire. A refused query is a false positive here, because the log records only commands
that ran; so is a test whose own seeding runs between the markers.

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

KINDS = ('LITERAL', 'PARAMETER', 'STRUCTURAL', 'EXTRA READ', 'EXTRA WRITE', 'ORDER')


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
            if 'Executed DbCommand' in line:
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


def classify(statement, ran_shapes):
    sh, kinds = shape(statement)
    if sh not in ran_shapes:
        return 'STRUCTURAL'

    literal = any(any(e == 'P' and r == 'L' for e, r in zip(kinds, k)) for k in ran_shapes[sh])
    clean = any(all(not (e == 'P' and r == 'L') for e, r in zip(kinds, k)) for k in ran_shapes[sh])
    return 'LITERAL' if literal and not clean else 'PARAMETER'


def compare_case(wanted, ran):
    """The differences between what EF expects and what one test case ran, both directions."""
    missing = list(wanted)
    extra = list(ran)
    for statement in list(missing):
        match = next((s for s in extra if exact(s) == exact(statement)), None)
        if match is not None:
            missing.remove(statement)
            extra.remove(match)

    if not missing and not extra and [exact(s) for s in wanted] != [exact(s) for s in ran]:
        return [], [], True
    return missing, extra, False


def differences(wanted_variants, cases):
    """The worst case's differences, against the variant that fits it best."""
    worst = ([], [], False, None)
    for case in cases:
        best = None
        for wanted, where in wanted_variants:
            missing, extra, reordered = compare_case(wanted, case)
            cost = len(missing) + len(extra) + (1 if reordered else 0)
            if best is None or cost < best[0]:
                best = (cost, missing, extra, reordered, where, case)
        if best[0] > (len(worst[0]) + len(worst[1]) + (1 if worst[2] else 0)):
            worst = (best[1], best[2], best[3], (best[4], best[5]))
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

    # Counted per TEST and not per statement: one test that re-seeds its data runs dozens of
    # statements EF never runs, and counting those would bury the two that matter.
    for k in paired:
        missing, extra, reordered, source = differences(expected[k], sections[k])
        if not missing and not extra and not reordered:
            counts['same'] += 1
            continue

        counts['differs'] += 1
        where, ran = source
        ran_shapes = collections.defaultdict(set)
        for statement in ran:
            sh, kinds = shape(statement)
            ran_shapes[sh].add(kinds)

        seen = collections.defaultdict(list)
        for statement in missing:
            seen[classify(statement, ran_shapes)].append(('EF expects', statement))
        for statement in extra:
            seen['EXTRA WRITE' if WRITE.match(statement) else 'EXTRA READ'].append(('the server ran', statement))
        if reordered:
            seen['ORDER'].append(('in another order', ran[0] if ran else ''))

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
        if kind == 'EXTRA WRITE':
            print('      a write EF does not expect is usually the test seeding or restoring its own data')
        for (cls, method), where, examples, ran in found[:args.limit]:
            print(f'\n{cls}.{method}  (EF: {where})')
            for direction, statement in examples[:2]:
                print(f'    {direction:>14} :', ' '.join(canon(statement).split())[:300])
                if kind in ('LITERAL', 'PARAMETER', 'STRUCTURAL'):
                    same_shape = [r for r in ran if shape(r)[0] == shape(statement)[0]] or ran[:1]
                    for statement_ran in same_shape[:1]:
                        print('    the server ran :', ' '.join(canon(statement_ran).split())[:300])
            if len(examples) > 2:
                print(f'                     ... and {len(examples) - 2} more in this test')
        if len(found) > args.limit:
            print(f'\n    ... and {len(found) - args.limit} more tests; raise --limit to see them')

    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
