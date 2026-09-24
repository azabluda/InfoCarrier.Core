"""Compare, test method by test method, the READS the server ran through InfoCarrier with the reads
plain EF Core ran for the same method (INFOCARRIER_DIRECT_CLIENT=1). One-off experiment.

Usage: py direct-compare.py <ic.log> <direct.log> [--direct-failures names.txt] [--ic-failures names.txt]
                            [--limit N] [--dump out.tsv]
"""
import argparse
import collections
import importlib.util
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
spec = importlib.util.spec_from_file_location('efsqldiff', os.path.join(REPO, 'eng', 'ef-sql-diff.py'))
d = importlib.util.module_from_spec(spec)
spec.loader.exec_module(d)


def reads(cases):
    out = collections.Counter()
    for case in cases:
        for s in case:
            if not d.WRITE.match(s):
                out[d.exact(s)] += 1
    return out


LIMITS = ('LIMIT', 'DISTINCT', 'WHERE', 'COUNT', 'SUM', 'AVG', 'MIN', 'MAX', 'GROUP BY', 'EXISTS')


def tables(sql):
    return frozenset(d.FROM_TABLE.findall(d.canon(sql)))


def bounds(sql):
    s = ' ' + d.canon(sql).upper() + ' '
    return {k for k in LIMITS if f' {k} ' in s or f' {k}(' in s}


def classify(stmt, direct_all):
    """How an InfoCarrier-only read compares with what plain EF read in the same test method.

    WHOLE-TABLE  it reads a table with no bound at all, and plain EF bounded every read of it;
    LOOSER       it reads the same tables as a plain-EF read that carries a bound it lacks
                 (a LIMIT, a WHERE, a DISTINCT, an aggregate);
    SAME-ROWS    anything else: a constant or a CASE in the projection, an alias, a literal.
    """
    direct_unbounded = set()
    for other in direct_all:
        if d.unbounded(other):
            direct_unbounded |= tables(other)
    if d.unbounded(stmt):
        if tables(stmt) - direct_unbounded:
            return 'WHOLE-TABLE', ''
        return 'SAME-ROWS', ''
    mine = bounds(stmt)
    for other in direct_all:
        if tables(other) == tables(stmt):
            missing = bounds(other) - mine
            if missing:
                return 'LOOSER', '+'.join(sorted(missing))
    return 'SAME-ROWS', ''


def load_failures(path):
    """Failing test names from trx-failures.py, reduced to the (key, method) this report uses."""
    if not path:
        return set()
    keys = set()
    with open(path, encoding='utf-8') as f:
        for line in f:
            name = line.strip().split('(')[0]
            if not name:
                continue
            cls, _, method = name.rpartition('.')
            keys.add((d.key(cls.rsplit('.', 1)[-1]), method))
    return keys


def one_line(sql, width=240):
    s = ' '.join(d.canon(sql).split())
    return s if len(s) <= width else s[:width] + ' ...'


def main(argv):
    p = argparse.ArgumentParser()
    p.add_argument('ic')
    p.add_argument('direct')
    p.add_argument('--direct-failures')
    p.add_argument('--efcore', default=os.path.join(REPO, 'subrepos', 'efcore'))
    p.add_argument('--limit', type=int, default=40)
    p.add_argument('--dump')
    a = p.parse_args(argv)

    ic = d.server_sections(a.ic)
    direct = d.server_sections(a.direct)
    dfail = load_failures(a.direct_failures)
    expected, _ = d.ef_expected(a.efcore)

    both = sorted(set(ic) & set(direct))
    print(f'test methods: IC log {len(ic)}, direct log {len(direct)}, in both {len(both)}, '
          f'IC log only {len(set(ic) - set(direct))}, direct log only {len(set(direct) - set(ic))}')
    usable = [k for k in both if k not in dfail]
    print(f'in both and the direct run passed every case: {len(usable)} '
          f'(of which EF asserts SQL for {sum(1 for k in usable if k in expected)})')

    status = collections.Counter()
    per_test = {}
    for k in usable:
        r_ic, r_d = reads(ic[k]), reads(direct[k])
        if r_ic == r_d:
            status['identical reads' if r_ic else 'no reads either side'] += 1
            continue
        plus = r_ic - r_d
        kinds = collections.defaultdict(list)
        for stmt in plus:
            kind, note = classify(stmt, list(r_d))
            kinds[kind].append((stmt, note))
        n_ic, n_d = sum(r_ic.values()), sum(r_d.values())
        if not plus:
            kinds['FEWER-ONLY'].append((None, ''))
        worst = next(k2 for k2 in ('WHOLE-TABLE', 'LOOSER', 'SAME-ROWS', 'FEWER-ONLY') if k2 in kinds)
        status[f'differ: worst {worst}'] += 1
        per_test[k] = (worst, kinds, r_d - r_ic, n_ic, n_d)
    for s, n in sorted(status.items()):
        print(f'  {s}: {n}')

    # The handoff's question: methods EF never asserts, that read a table unbounded through InfoCarrier.
    never = [k for k in usable if k not in expected]
    with_unb = [k for k in never if any(d.unbounded(s) for s in reads(ic[k]))]
    explained = [k for k in with_unb if k not in per_test or per_test[k][0] not in ('WHOLE-TABLE',)]
    print(f'\nmethods EF does not assert, whose IC reads include an unbounded read: {len(with_unb)}; '
          f'plain EF reads those tables unbounded too in {len(explained)}; '
          f'IC reads a table whole where plain EF bounded it in {len(with_unb) - len(explained)}')

    for wanted in ('WHOLE-TABLE', 'LOOSER'):
        groups = collections.defaultdict(lambda: [0, set(), None, ''])
        for k, (worst, kinds, minus, n_ic, n_d) in per_test.items():
            for stmt, note in kinds.get(wanted, []):
                g = groups[(k[0], d.shape(stmt)[0])]
                g[0] += 1
                g[1].add(k)
                if g[2] is None:
                    g[2], g[3] = (stmt, list(minus)), note
        tests = set().union(*(g[1] for g in groups.values())) if groups else set()
        print(f'\n===== {wanted}: {len(groups)} shapes in {len(tests)} test methods '
              f'({sum(1 for k in tests if k in expected)} of them EF asserts)')
        for _, g in sorted(groups.items(), key=lambda i: (-len(i[1][1]), i[0]))[:a.limit]:
            (stmt, minus), note = g[2], g[3]
            names = ', '.join(sorted(f'{c}.{m}' for c, m in g[1])[:3])
            print(f'\n  x{g[0]} in {len(g[1])} tests{" (missing " + note + ")" if note else ""}: {names}'
                  + (', ...' if len(g[1]) > 3 else ''))
            print('    IC:     ', one_line(stmt))
            for other in minus[:2]:
                print('    direct: ', one_line(other))

    if a.dump:
        with open(a.dump, 'w', encoding='utf-8', newline='\n') as out:
            out.write('class\tmethod\tworst\tef_asserts\tic_reads\tdirect_reads\tside\tkind\tstatement\n')
            for (c, m), (worst, kinds, minus, n_ic, n_d) in sorted(per_test.items()):
                ef = (c, m) in expected
                for kind, items in kinds.items():
                    for stmt, note in items:
                        if stmt:
                            out.write(f'{c}\t{m}\t{worst}\t{ef}\t{n_ic}\t{n_d}\tIC\t{kind}{"(" + note + ")" if note else ""}\t{" ".join(d.canon(stmt).split())}\n')
                for stmt in minus:
                    out.write(f'{c}\t{m}\t{worst}\t{ef}\t{n_ic}\t{n_d}\tDIRECT\t\t{" ".join(d.canon(stmt).split())}\n')


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
