#!/usr/bin/env bash
#
# Trim-warning ratchet for the Blazor sample.
#
# WHY A RATCHET AND NOT A ZERO. Spec §7 asked for "no IL warning attributable to InfoCarrier.Core",
# and that criterion is NOT met: the publish reports 86 of them. They are not an oversight, they
# are the provider's premise showing through -- the wire carries a type's NAME and the far end
# resolves it, so `Assembly.GetType(string)`, `MakeGenericMethod` and a reflective object walk are
# what this provider IS. `[DynamicallyAccessedMembers]` cannot express "whatever type the caller's
# model happens to name". The honest annotation is `[RequiresUnreferencedCode]` on the public query
# API, which is a product decision and not a sample's to make.
#
# So this gates on DIRECTION, exactly as eng/ratchet.sh does for the spec suite: our count must not
# rise. Everyone else's is reported but not gated -- EF Core alone contributes an order of
# magnitude more, and this repo does not get to fix those.
#
# NOTE THAT THE APP RUNS TRIMMED. These warnings say the trimmer cannot PROVE the reflection is
# safe, not that it broke: `PublishTrimmed=true` produces a working client (M8-17 drove all three
# pages against the published output). Do not read a rising count as "it is broken now" -- read it
# as "something new became unprovable", which is worth a look either way.
#
# Usage: bash eng/trim-ratchet.sh [baseline-file]
set -u

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
baseline_file="${1:-$root/eng/trim-baseline.txt}"
out="$root/artifacts/trim"
mkdir -p "$out"
log="$out/publish.log"

# A CLEAN publish, and this is not tidiness. MSBuild is incremental: publishing an up-to-date
# project does not re-run ILLink, the log then contains no diagnostics at all, and this script
# happily reports OURS: 0. It did exactly that on its first run, which would have made the gate
# pass forever while regressions sailed through. The count must come from a trimmer that RAN.
rm -rf "$root/samples/Northwind.Client/obj/Release" "$root/samples/Northwind.Client/bin/Release"

# -p:TrimReport=true IS THE WHOLE SWITCH, and this is the only place in the repository that sets it.
#
# It turns on SuppressTrimAnalysisWarnings=false, TrimmerSingleWarn=false and
# TreatWarningsAsErrors=false together, all three declared in one PropertyGroup in
# Northwind.Client.csproj. Everywhere else the trim diagnostics stay off, so no other build needs an
# exemption to survive them and the repository carries no WarningsNotAsErrors at all.
#
# NoWarn would be worse than useless: this script COUNTS these warnings, so silencing them reports
# OURS: 0 and passes for ever -- exactly the failure the clean-publish rule above exists to prevent.

echo "trim-ratchet: publishing samples/Northwind.Client (Release, trimmed, clean)…"
dotnet publish "$root/samples/Northwind.Client/Northwind.Client.csproj" \
    -c Release --nologo -p:TrimReport=true > "$log" 2>&1
status=$?

if [ $status -ne 0 ]; then
    echo "trim-ratchet: the publish itself failed — see $log" >&2
    grep -E " error " "$log" | head -10 >&2
    exit 1
fi

# The second half of the same guard: prove the trimmer ran before believing any number it did not
# print. "Optimizing assemblies for size" is ILLink's own banner.
if ! grep -qE "Optimizing assemblies for size|ILLink" "$log"; then
    echo "trim-ratchet: the publish produced no ILLink output — the trimmer did not run." >&2
    echo "A count taken from this log would be meaningless. See $log" >&2
    exit 1
fi

# Classify by the DECLARING MEMBER, never by the file path in the message.
#
# ILLink appends "[C:\...\Northwind.Client.csproj]" to every line, and this repository's path
# contains the string "InfoCarrier" -- so a naive `grep InfoCarrier` attributes EVERY warning in
# the build to this product, including EF Core's 864. That mistake was made once while writing
# this script and it inverted the conclusion.
# Resolve an interpreter that actually RUNS. On Windows a bare `python3` often resolves to the
# Microsoft Store stub, which is on PATH, exits non-zero and prints an advert -- so `command -v`
# finding it proves nothing.
py=""
for candidate in python3 python py; do
    if command -v "$candidate" > /dev/null 2>&1 && "$candidate" -c "pass" > /dev/null 2>&1; then
        py="$candidate"
        break
    fi
done

if [ -z "$py" ]; then
    echo "trim-ratchet: no working python interpreter on PATH." >&2
    exit 1
fi

counts=$("$py" - "$log" <<'PY'
import re, sys, collections

text = open(sys.argv[1], encoding='utf-8', errors='replace').read()

# ILLink emits two shapes: "Trim analysis warning IL2070: <member>: <text>" and the plain
# MSBuild "warning IL2026: <member>: <text>". Both carry the declaring member first.
pattern = re.compile(r'(?:Trim analysis warning|warning|error)\s+(IL\d{4}):\s*(.*)$', re.M)

unique = {(m.group(1), m.group(2).strip()) for m in pattern.finditer(text)}

def owner(diagnostic):
    member = diagnostic.split(': ')[0].split('(')[0]
    for prefix, name in (
        ('InfoCarrier.Core.', 'InfoCarrier.Core'),
        ('Castle.', 'Castle.Core'),
        ('Microsoft.EntityFrameworkCore', 'EFCore'),
        ('Microsoft.AspNetCore', 'AspNetCore'),
        ('Microsoft.FluentUI', 'FluentUI'),
        ('Northwind.', 'Northwind'),
    ):
        if member.startswith(prefix):
            return name
    return 'other'

tally = collections.Counter(owner(d) for _, d in unique)
print(tally['InfoCarrier.Core'])
print(len(unique))
print(tally['Northwind'])
for name, n in tally.most_common():
    print(f"{n}\t{name}")
PY
)

ours=$(echo "$counts" | sed -n 1p)
total=$(echo "$counts" | sed -n 2p)
sample=$(echo "$counts" | sed -n 3p)

if [ -z "$ours" ]; then
    echo "trim-ratchet: could not parse the publish log — see $log" >&2
    exit 1
fi

echo
echo "OURS: $ours   SAMPLE: $sample   TOTAL (all assemblies): $total"
echo
echo "$counts" | tail -n +4 | sed 's/^/    /'
echo

# Two gated owners, and the second one is not padding.
#
# `ours` is the product and always was. `northwind` is the SAMPLE's own code, gated since N30 to
# replace a tripwire that N30 removed: the trim analyzer used to run on every Release compile, so a
# new trim diagnostic in the sample failed CI. That analyzer was measured to contribute 6 of this
# publish's 1119 IL warning lines and none of InfoCarrier.Core's, so it went -- and the signal it
# did carry moves here, onto the instrument that measures the whole publish rather than one compile.
#
# Everyone else is reported and not gated. EF Core alone contributes 585 and this repo does not get
# to fix those.
fail=0
for owner in ours northwind; do
    case "$owner" in
        ours)     actual=$ours   ;;
        northwind) actual=$sample ;;
    esac

    baseline=$(grep -E "^$owner=" "$baseline_file" | cut -d= -f2)
    if [ -z "$baseline" ]; then
        echo "trim-ratchet: no '$owner=' line in $baseline_file" >&2
        exit 1
    fi

    # BOTH SIDES MUST BE INTEGERS BEFORE THEY ARE COMPARED, and this is not defensive padding.
    # `[ "$a" -gt "$b" ]` on a non-integer prints "integer expression expected" to STDERR and then
    # takes the FALSE branch, so a mis-parsed count sails through as "OK" and the script exits 0.
    # That happened while N30 was being written -- an off-by-one in which line of the counter's
    # output was read fed a tab-joined "585 EFCore" into the comparison and the gate passed anyway.
    for n in "$actual" "$baseline"; do
        case "$n" in
            ''|*[!0-9]*)
                echo "trim-ratchet: $owner count is not a number: '$n'. See $log" >&2
                exit 1
                ;;
        esac
    done

    if [ "$actual" -gt "$baseline" ]; then
        echo "trim-ratchet: FAIL — $owner trim warnings rose from $baseline to $actual." >&2
        echo "Lower the baseline in the same commit as a fix; raise it only with a reason." >&2
        fail=1
    elif [ "$actual" -lt "$baseline" ]; then
        echo "trim-ratchet: $owner improved ($baseline -> $actual). Lower '$owner=' in $baseline_file."
    else
        echo "trim-ratchet: $owner OK ($actual <= $baseline)."
    fi
done

exit $fail
