#!/usr/bin/env python3
"""Compare the SQL the server ran, test by test, with the SQL EF's own SQLite suite expects.

This is the measuring half of #111. It asserts nothing and gates nothing: it reads a server-sql.log
written by a run with INFOCARRIER_SERVER_SQL=1, and EF's `AssertSql` text at the pinned commit, and
says for each test whether the statements agree.

HOW TO PRODUCE THE LOG. Serially, or the statements of two tests interleave between two markers:

    INFOCARRIER_SERVER_SQL=1 dotnet test test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj \\
        --filter "FullyQualifiedName~InfoCarrier.Core.FunctionalTests.Sqlite" -- xUnit.ParallelizeTestCollections=false
    eng/ef-sql-diff.py test/InfoCarrier.Core.FunctionalTests/bin/Debug/net10.0/server-sql.log

It needs `subrepos/efcore` checked out at the commit `Upstream.CommitOf` pins, which a CI runner has
not, so this is a local tool. Tier B only: EF asserts SQL in its SQLite suite, and Tier A has no SQL.

WHAT IS IGNORED, AND WHY EACH IS A NAME AND NOT A PLAN. Whitespace; parameter names (`@Value` for
`@id`: a parameter crosses inside `ParameterBox<T>` and EF names it after the box's property);
aliases and the qualifiers that reference them (`"Item1"` for `"X"`, `"v"` for `"i"`: the projection
split and a boxed collection name them differently). Literals and structure are kept.

WHAT ELSE IT REPORTS, with --reasons: how many of the tests EF asserts SQL for this suite asserts
too, read from the *.override-reasons.tsv that OverrideAudit writes (INFOCARRIER_OVERRIDE_REASONS).
That count is #111's progress, and it is the ALARM FOR A NEW EF VERSION: a test EF has added since
the last bump is one this suite runs, inherits and does not assert, so the figure falls. A test EF
moved or renamed is caught earlier, by the audit, which checks that an [UpstreamOverride]'s line
range still declares that test at the pinned commit.

WHAT IT REPORTS, per statement EF expects and the server did not produce:
  LITERAL     the server ran the same shape with a literal where EF has a parameter
  PARAMETER   the server ran the same shape with a parameter where EF has a literal
  STRUCTURAL  the server ran no statement of that shape in that test
The owner's rule (docs/plans/v10/test-overhaul.md): the first two are equally bad, and a structural
difference that can change the store's plan is a red flag. A refused query is a false positive here,
because the log records only commands that ran.

Usage: eng/ef-sql-diff.py <server-sql.log> [--efcore <path>] [--reasons <tsv> ...] [--quiet]
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
LOG_HEADER = re.compile(r'^\w+: \d\d/\d\d/\d{4} ')
METHOD = re.compile(r'public\s+(?:override\s+)?(?:async\s+)?[\w<>\[\],?. ]+?\s+(\w+)\s*\(')
CLASS = re.compile(r'\bclass\s+(\w+)')
ASSERT_SQL = re.compile(r'\bAssert(?:ExecuteUpdate)?Sql\s*\(')


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


def upstream_overrides(paths):
    """(class, method) of every test carrying [UpstreamOverride], read off the audit's rows."""
    asserted = set()
    for path in paths:
        with open(path, encoding='utf-8') as tsv:
            for line in tsv:
                fields = line.rstrip('\n').split('\t')
                if len(fields) == 6 and fields[0] == 'reason' and fields[4] == 'upstream':
                    asserted.add((key(fields[1].rsplit('.', 1)[-1]), fields[2]))
    return asserted


def classify(statement, ran_shapes):
    sh, kinds = shape(statement)
    if sh not in ran_shapes:
        return 'STRUCTURAL'

    literal = any(any(e == 'P' and r == 'L' for e, r in zip(kinds, k)) for k in ran_shapes[sh])
    clean = any(all(not (e == 'P' and r == 'L') for e, r in zip(kinds, k)) for k in ran_shapes[sh])
    return 'LITERAL' if literal and not clean else 'PARAMETER'


def main(argv):
    sys.stdout.reconfigure(newline='\n')
    parser = argparse.ArgumentParser(description='Compare the server SQL log with EF\'s expected SQL.')
    parser.add_argument('log', help='a server-sql.log from a serial run with INFOCARRIER_SERVER_SQL=1')
    parser.add_argument('--efcore', default='subrepos/efcore', help="EF Core checkout (default: subrepos/efcore)")
    parser.add_argument('--quiet', action='store_true', help='counts only, no per-statement listing')
    parser.add_argument(
        '--reasons', nargs='*', default=[],
        help='*.override-reasons.tsv from OverrideAudit, to report how many of these tests assert SQL here')
    args = parser.parse_args(argv[1:])

    try:
        expected = ef_expected(args.efcore)
        sections = server_sections(args.log)
    except (OSError, ValueError) as error:
        print(f'ef-sql-diff: {error}', file=sys.stderr)
        return 1

    paired = [k for k in expected if k in sections]
    print(f'EF tests with expected SQL {len(expected)}; tests in the log {len(sections)}; paired {len(paired)}')

    if args.reasons:
        try:
            asserted = upstream_overrides(args.reasons)
        except OSError as error:
            print(f'ef-sql-diff: {error}', file=sys.stderr)
            return 1

        here = {k for k in paired if k in asserted}
        print(f'of those paired tests, this suite asserts the SQL of {len(here)}; {len(paired) - len(here)} do not assert it')
        if not args.quiet:
            for cls, method in sorted(k for k in paired if k not in asserted)[:40]:
                print(f'    not asserted here: {cls}.{method}')

    counts = collections.Counter()
    findings = []
    for k in paired:
        ran = [s for case in sections[k] for s in case]
        ran_exact = {exact(s) for s in ran}
        ran_shapes = collections.defaultdict(set)
        for statement in ran:
            sh, kinds = shape(statement)
            ran_shapes[sh].add(kinds)

        # EF may assert several variants of one test, one per class in the file; any that matches counts.
        missing, where = min(
            (([s for s in statements if exact(s) not in ran_exact], source) for statements, source in expected[k]),
            key=lambda pair: len(pair[0]))

        if not missing:
            counts['same'] += 1
            continue

        counts['differs'] += 1
        for statement in missing:
            kind = classify(statement, ran_shapes)
            counts[kind] += 1
            findings.append((kind, k, where, statement, ran))

    print(f'identical {counts["same"]}, differing tests {counts["differs"]} '
          f'(LITERAL {counts["LITERAL"]}, PARAMETER {counts["PARAMETER"]}, STRUCTURAL {counts["STRUCTURAL"]})')

    if args.quiet:
        return 0

    for kind in ('LITERAL', 'PARAMETER', 'STRUCTURAL'):
        for found_kind, (cls, method), where, statement, ran in findings:
            if found_kind != kind:
                continue
            print(f'\n{kind}  {cls}.{method}  (EF: {where})')
            print('    EF     :', ' '.join(canon(statement).split())[:400])
            same_shape = [r for r in ran if shape(r)[0] == shape(statement)[0]] or ran[:2]
            for statement_ran in same_shape[:2]:
                print('    server :', ' '.join(canon(statement_ran).split())[:400])

    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
