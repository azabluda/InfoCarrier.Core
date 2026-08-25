<!--
  CONTRIBUTING.md has the build and test commands. Two gates decide whether a change can land, and
  which of them applies depends only on what the change touches:

    src/           both eng/measure.sh and eng/trim-ratchet.sh
    test/ only     eng/measure.sh
    docs or text   neither

  A change to src/ also has to survive `CI=true dotnet build InfoCarrier.Core.slnx --configuration
  Release`, which is what the server runs. The Release part is not optional.
-->

## What this changes

## Evidence

<!--
  For a change to src/ or test/, paste the numbers rather than describing them: the failure count,
  and what the run fixed and broke. A count that did not move cannot tell "fixed four, broke four"
  from "changed nothing", which is why eng/measure.sh prints all three levels.
-->

- [ ] `eng/measure.sh` run, and the fixed/broken lists read, not just the count
- [ ] `eng/trim-ratchet.sh` green, if `src/` changed
- [ ] The plan checkbox in `docs/plans/v10/implementation-plan.md` updated in this same commit
